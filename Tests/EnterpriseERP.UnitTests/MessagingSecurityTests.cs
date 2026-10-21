using System.Reflection;
using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class MessagingSecurityTests
{
    private static MessagingAccess Access(SecurityTestScope f) => new(f.Db, Options.Create(new IdentityOptions()));
    private static MessagingService Service(SecurityTestScope f) => new(f.Db, Access(f));
    private static async Task Auth(SecurityTestScope f, string user = "operator-a", int company = 1, int branch = 11)
    {
        f.ActAs(user, company, branch);
        ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim(new IdentityOptions().ClaimsIdentity.SecurityStampClaimType,
            await f.Db.Users.AsNoTracking().Where(x => x.Id == user).Select(x => x.SecurityStamp!).SingleAsync()));
    }
    private static async Task Ready(SecurityTestScope f)
    {
        await f.GrantAsync("Messaging.View"); await f.GrantAsync("Messaging.Send");
        await f.GrantAsync("Messaging.View", "approver-a"); await Auth(f);
    }
    private static MessagingSendRequest Request(string? key = null, string recipient = "approver-a", string subject = "Feed handover", string body = "Please review the morning feed record.") =>
        new(recipient, subject, body, key ?? Guid.NewGuid().ToString());
    private static InternalMessage SeedMessage(int company = 1, int branch = 11, string recipient = "approver-a") => new()
    {
        CompanyId = company, BranchId = branch, SenderUserId = "operator-a", RecipientUserId = recipient,
        Subject = "Synthetic handover", Body = "Synthetic local fixture", IdempotencyKey = Guid.NewGuid().ToString("N"), PayloadHash = new string('A', 64), CreatedUtc = DateTimeOffset.UtcNow
    };

    [Fact] public async Task SendAtomicallyCreatesMessageInboxAuditOutboxAndSharedBudgets()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        var result = await Service(f).SendAsync(f.Http.User, Request());
        Assert.False(result.Replayed);
        var message = await f.Db.InternalMessages.SingleAsync();
        Assert.Equal("operator-a", message.SenderUserId); Assert.Equal("approver-a", message.RecipientUserId);
        var notification = await f.Db.UserInboxNotifications.SingleAsync();
        Assert.Equal(message.Id, notification.MessageId); Assert.Null(notification.ReadUtc);
        Assert.Equal(notification.Id, (await f.Db.MessagingOutbox.SingleAsync()).NotificationId);
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "Messaging.Sent").ToListAsync());
        Assert.All(await f.Db.MessagingRateBuckets.ToListAsync(), x => Assert.Equal(1, x.Count));
    }

    [Fact] public async Task IdempotencyReplaysUnchangedPayloadAndRejectsChangedPayloadWithoutExtraAudit()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        var request = Request(); var service = Service(f);
        var first = await service.SendAsync(f.Http.User, request);
        var second = await service.SendAsync(f.Http.User, request);
        Assert.True(second.Replayed); Assert.Equal(first.MessageId, second.MessageId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(f.Http.User, request with { Body = "Different" }));
        Assert.Equal(1, await f.Db.InternalMessages.CountAsync());
        Assert.Equal(1, await f.Db.MessagingOutbox.CountAsync());
        Assert.Equal(1, await f.Db.Audits.CountAsync(x => x.Action == "Messaging.Sent"));
        Assert.All(await f.Db.MessagingRateBuckets.ToListAsync(), x => Assert.Equal(1, x.Count));
    }

    [Theory] [InlineData("operator-b")] [InlineData("unknown-user")]
    public async Task RecipientMustExistInSameCompanyAndBranchWithViewPermission(string recipient)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await f.GrantAsync("Messaging.View", "operator-b", 2);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SendAsync(f.Http.User, Request(recipient: recipient)));
        Assert.Empty(await f.Db.InternalMessages.ToListAsync()); Assert.Empty(await f.Db.MessagingRateBuckets.ToListAsync());
    }

    [Fact] public async Task MissingOrRevokedSenderAndRecipientPermissionsFailClosed()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        var grant = await f.Db.UserPermissions.SingleAsync(x => x.UserId == "approver-a");
        f.Db.UserPermissions.Remove(grant); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SendAsync(f.Http.User, Request()));
        Assert.Empty(await Service(f).RecipientsAsync(f.Http.User, "approver"));
        f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync(x => x.Permission == "Messaging.Send"));
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).SendAsync(f.Http.User, Request()));
    }

    [Fact] public async Task RecipientSearchNeverDiscoversOtherCompaniesBranchesOrUnprivilegedPeople()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await f.GrantAsync("Messaging.View", "operator-b", 2);
        Assert.Empty(await Service(f).RecipientsAsync(f.Http.User, "operator-b"));
        Assert.Single(await Service(f).RecipientsAsync(f.Http.User, "approver"));
        f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "approver-a"));
        f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = "approver-a", CompanyId = 1, BranchId = 12 });
        await f.Db.SaveChangesAsync();
        Assert.Empty(await Service(f).RecipientsAsync(f.Http.User, "approver"));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SendAsync(f.Http.User, Request()));
    }

    [Fact] public async Task SecurityStampAndFreshMembershipAreRequiredEvenForReadOperations()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        f.ActAs(); // Missing cookie security-stamp claim must not be treated as a fresh session.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).InboxAsync(f.Http.User, null, false));
        await Auth(f);
        (await f.Db.Users.SingleAsync(x => x.Id == "operator-a")).SecurityStamp = Guid.NewGuid().ToString();
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).InboxAsync(f.Http.User, null, false));
        await Auth(f);
        f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "operator-a")); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).InboxAsync(f.Http.User, null, false));
    }

    [Fact] public async Task ReadOwnershipAndCrossScopeDetailsAreEnforcedAndAcknowledgementIsIdempotent()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        var service = Service(f); var sent = await service.SendAsync(f.Http.User, Request());
        var notificationId = await f.Db.UserInboxNotifications.Select(x => x.Id).SingleAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReadAsync(f.Http.User, notificationId));
        Assert.Single((await service.SentAsync(f.Http.User, null)).Items);
        Assert.Empty((await service.InboxAsync(f.Http.User, null, false)).Items);
        await Auth(f, "approver-a");
        Assert.Equal(1, (await service.InboxAsync(f.Http.User, null, true)).UnreadCount);
        Assert.Equal(sent.MessageId, (await service.DetailAsync(f.Http.User, sent.MessageId)).MessageId);
        await service.ReadAsync(f.Http.User, notificationId); await service.ReadAsync(f.Http.User, notificationId);
        Assert.Equal(0, (await service.InboxAsync(f.Http.User, null, true)).UnreadCount);
        Assert.Equal(1, await f.Db.Audits.CountAsync(x => x.Action == "Messaging.Read"));
        await f.GrantAsync("Messaging.View", "operator-b", 2); await Auth(f, "operator-b", 2, 21);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DetailAsync(f.Http.User, sent.MessageId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReadAsync(f.Http.User, notificationId));
    }

    [Fact] public async Task ContentAndOwnershipAreImmutableAtSaveBoundary()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await Service(f).SendAsync(f.Http.User, Request());
        var message = await f.Db.InternalMessages.SingleAsync(); message.Body = "Rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        var notification = await f.Db.UserInboxNotifications.SingleAsync(); notification.UserId = "operator-a";
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        f.Db.InternalMessages.Remove(await f.Db.InternalMessages.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Fact] public async Task CompositeForeignKeyRejectsForgedRecipientReceipt()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await Service(f).SendAsync(f.Http.User, Request());
        var id = await f.Db.InternalMessages.Select(x => x.Id).SingleAsync();
        f.Db.UserInboxNotifications.Add(new UserInboxNotification { CompanyId = 1, BranchId = 11, UserId = "operator-a", MessageId = id, CreatedUtc = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Theory] [InlineData("", "ok", "valid")] [InlineData("ok", "", "valid")] [InlineData("ok", "ok", "invalid")]
    public async Task BoundedMandatoryContentAndUuidAreValidated(string subject, string body, string key)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SendAsync(f.Http.User, Request(key == "valid" ? null : key, subject: subject, body: body)));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SendAsync(f.Http.User, Request(subject: new string('s', 161))));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SendAsync(f.Http.User, Request(body: new string('b', 8001))));
        Assert.Empty(await f.Db.InternalMessages.ToListAsync());
    }

    [Fact] public async Task SharedFloodBudgetRejectsNewMessageButAllowsIdempotentRetry()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        var service = Service(f); var request = Request(); await service.SendAsync(f.Http.User, request);
        var bucket = await f.Db.MessagingRateBuckets.SingleAsync(x => x.Period == "Day"); bucket.Count = 200; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MessagingFloodException>(() => service.SendAsync(f.Http.User, Request()));
        Assert.True((await service.SendAsync(f.Http.User, request)).Replayed);
        Assert.Equal(1, await f.Db.InternalMessages.CountAsync());
        Assert.Equal(1, await f.Db.MessagingRateBuckets.Where(x => x.Period == "Minute").Select(x => x.Count).SingleAsync());
    }

    [Fact] public async Task FailureAfterDatabaseWritesRollsBackContentAuditOutboxAndBudgetThenRetrySucceeds()
    {
        var interceptor = new FailAfterSave();
        await using var f = await SecurityTestScope.CreateAsync(interceptor); await Ready(f);
        interceptor.Armed = true; var request = Request();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).SendAsync(f.Http.User, request));
        Assert.Empty(await f.Db.InternalMessages.ToListAsync()); Assert.Empty(await f.Db.UserInboxNotifications.ToListAsync());
        Assert.Empty(await f.Db.MessagingOutbox.ToListAsync()); Assert.Empty(await f.Db.MessagingRateBuckets.ToListAsync());
        Assert.Empty(await f.Db.Audits.Where(x => x.Action == "Messaging.Sent").ToListAsync());
        interceptor.Armed = false;
        Assert.False((await Service(f).SendAsync(f.Http.User, request)).Replayed);
    }

    [Fact] public async Task InboxUsesBoundedStableKeysetPagesAndUnreadCount()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        for (var i = 0; i < 35; i++)
            f.Db.UserInboxNotifications.Add(new UserInboxNotification { CompanyId = 1, BranchId = 11, UserId = "approver-a", Message = SeedMessage(), CreatedUtc = DateTimeOffset.UtcNow });
        await f.Db.SaveChangesAsync(); await Auth(f, "approver-a");
        var first = await Service(f).InboxAsync(f.Http.User, null, false);
        var second = await Service(f).InboxAsync(f.Http.User, first.NextBeforeId, false);
        Assert.Equal(30, first.Items.Count); Assert.Equal(5, second.Items.Count);
        Assert.Equal(35, first.UnreadCount); Assert.Null(second.NextBeforeId);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
    }

    [Fact] public async Task AutomationAlertsNeedFreshReviewPermissionAndHavePerUserScopedReadReceipts()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        f.Db.Notifications.AddRange(new InAppNotification { CompanyId = 1, BranchId = 11, ExecutionKey = "AUTO-A", Message = "Local" },
            new InAppNotification { CompanyId = 1, BranchId = 12, ExecutionKey = "AUTO-B", Message = "Other branch" },
            new InAppNotification { CompanyId = 2, BranchId = 21, ExecutionKey = "AUTO-C", Message = "Other company" });
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).AutomationAsync(f.Http.User, null, false));
        await f.GrantAsync("Automation.Review");
        var page = await Service(f).AutomationAsync(f.Http.User, null, false);
        var local = Assert.Single(page.Items); Assert.Equal("Local", local.Message);
        await Service(f).ReadAutomationAsync(f.Http.User, local.Id); await Service(f).ReadAutomationAsync(f.Http.User, local.Id);
        Assert.Empty((await Service(f).AutomationAsync(f.Http.User, null, true)).Items);
        await f.GrantAsync("Automation.Review", "approver-a"); await Auth(f, "approver-a");
        Assert.Equal(1, (await Service(f).AutomationAsync(f.Http.User, null, false)).UnreadCount);
        var foreign = await f.Db.Notifications.Where(x => x.BranchId == 12).Select(x => x.Id).SingleAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).ReadAutomationAsync(f.Http.User, foreign));
    }

    [Fact] public async Task OutboxFailureRetainsDurableMessageAndRetrySendsContentFreeHintOnly()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await Service(f).SendAsync(f.Http.User, Request(body: "SECRET-CONTENT"));
        var transport = new RecordingClients { Throw = true };
        var dispatcher = new MessagingOutboxDispatcher(f.Db, Access(f), new RecordingHubContext(transport), TestRuntimeGate.Available);
        await Assert.ThrowsAsync<IOException>(() => dispatcher.DrainAsync());
        Assert.Null(await f.Db.MessagingOutbox.AsNoTracking().Select(x => x.HintProcessedUtcTicks).SingleAsync());
        Assert.Equal(1, await f.Db.InternalMessages.CountAsync());
        transport.Throw = false;
        await f.Db.MessagingOutbox.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntilUtcTicks, 0L));
        Assert.Equal(1, await dispatcher.DrainAsync()); Assert.Equal(0, await dispatcher.DrainAsync());
        Assert.Equal("inboxChanged", Assert.Single(transport.Calls).Method); Assert.Empty(transport.Calls[0].Args);
        Assert.DoesNotContain("approver-a", Assert.Single(transport.SuccessfulGroups)); Assert.DoesNotContain("SECRET", transport.SuccessfulGroups[0]);
        await Auth(f, "approver-a"); Assert.Equal(1, (await Service(f).InboxAsync(f.Http.User, null, true)).UnreadCount);
    }

    [Fact] public async Task OutboxHonorsLeaseAndCurrentPermissionAndSecurityStampBeforeHint()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        await Service(f).SendAsync(f.Http.User, Request());
        var actor = await Access(f).EligibleAsync(1, 11, "approver-a", "Messaging.View");
        var oldGroup = MessagingAccess.Group(actor!);
        (await f.Db.Users.SingleAsync(x => x.Id == "approver-a")).SecurityStamp = Guid.NewGuid().ToString(); await f.Db.SaveChangesAsync();
        var transport = new RecordingClients(); var dispatcher = new MessagingOutboxDispatcher(f.Db, Access(f), new RecordingHubContext(transport), TestRuntimeGate.Available);
        await f.Db.MessagingOutbox.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks));
        Assert.Equal(0, await dispatcher.DrainAsync());
        await f.Db.MessagingOutbox.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntilUtcTicks, 0L));
        Assert.Equal(1, await dispatcher.DrainAsync()); Assert.NotEqual(oldGroup, Assert.Single(transport.SuccessfulGroups));
        await Service(f).SendAsync(f.Http.User, Request());
        f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync(x => x.UserId == "approver-a")); await f.Db.SaveChangesAsync();
        Assert.Equal(1, await dispatcher.DrainAsync()); Assert.Single(transport.Calls);
    }

    [Fact] public async Task HubGroupsAreServerDerivedAndEveryMethodRejectsRevokedPermission()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f);
        var transport = new RecordingClients(); var groups = new RecordingGroups(); var context = new RecordingCaller(f.Http.User);
        var hub = new MessagingHub(Access(f), TestRuntimeGate.Available) { Context = context, Clients = transport, Groups = groups };
        await hub.OnConnectedAsync(); Assert.Single(groups.Added);
        Assert.Equal(MessagingAccess.Group(await Access(f).DemandAsync(f.Http.User, "Messaging.View")), groups.Added[0]);
        await hub.Sync(); Assert.Empty(Assert.Single(transport.Calls).Args);
        f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync(x => x.UserId == "operator-a" && x.Permission == "Messaging.View")); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<HubException>(() => hub.Sync()); Assert.True(context.Aborted); Assert.Single(groups.Removed);
        Assert.Equal(new[] { "Sync" }, typeof(MessagingHub).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(x => x.Name != "OnConnectedAsync").Select(x => x.Name).ToArray());
    }

    [Fact] public async Task HubRejectsForgedIdentityStampAndRevokedConnectionMembership()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f); f.ActAs();
        var context = new RecordingCaller(f.Http.User); var groups = new RecordingGroups();
        var hub = new MessagingHub(Access(f), TestRuntimeGate.Available) { Context = context, Clients = new RecordingClients(), Groups = groups };
        await Assert.ThrowsAsync<HubException>(() => hub.OnConnectedAsync()); Assert.True(context.Aborted); Assert.Empty(groups.Added);
        await Auth(f); context = new RecordingCaller(f.Http.User); hub.Context = context; await hub.OnConnectedAsync();
        f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "operator-a")); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<HubException>(() => hub.Sync()); Assert.True(context.Aborted);
    }

    [Fact] public async Task UnavailableTrustedRuntimeDoesNotLeaseOrPublishOutbox()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Ready(f); await Service(f).SendAsync(f.Http.User, Request());
        var transport = new RecordingClients(); var dispatcher = new MessagingOutboxDispatcher(f.Db, Access(f), new RecordingHubContext(transport), new TestRuntimeGate { IsAvailable = false });
        await Assert.ThrowsAsync<TrustedInfrastructure.TrustedVaultUnavailableException>(() => dispatcher.DrainAsync());
        Assert.Empty(transport.Calls); Assert.Null(await f.Db.MessagingOutbox.Select(x => x.LeaseOwner).SingleAsync());
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        { if (Armed) throw new InvalidOperationException("Synthetic post-write failure"); return ValueTask.FromResult(result); }
    }
}

internal sealed class RecordingCaller(ClaimsPrincipal principal) : HubCallerContext
{
    public override string ConnectionId => "synthetic-connection";
    public override string? UserIdentifier => principal.FindFirstValue(ClaimTypes.NameIdentifier);
    public override ClaimsPrincipal? User => principal;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    public bool Aborted { get; private set; }
    public override void Abort() => Aborted = true;
}
internal sealed class RecordingGroups : IGroupManager
{
    public List<string> Added { get; } = []; public List<string> Removed { get; } = [];
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) { Added.Add(groupName); return Task.CompletedTask; }
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) { Removed.Add(groupName); return Task.CompletedTask; }
}
internal sealed class RecordingClients : IHubClients, IHubCallerClients, IClientProxy
{
    public bool Throw { get; set; }
    public List<(string Method, object?[] Args)> Calls { get; } = [];
    public List<string> SuccessfulGroups { get; } = [];
    private string? group;
    public IClientProxy All => this; public IClientProxy Caller => this; public IClientProxy Others => this;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => this;
    public IClientProxy Client(string connectionId) => this;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => this;
    public IClientProxy Group(string groupName) { group = groupName; return this; }
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => this;
    public IClientProxy OthersInGroup(string groupName) => Group(groupName);
    public IClientProxy User(string userId) => this;
    public IClientProxy Users(IReadOnlyList<string> userIds) => this;
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        if (Throw) throw new IOException("Synthetic transport offline");
        Calls.Add((method, args)); if (group is not null) SuccessfulGroups.Add(group);
        return Task.CompletedTask;
    }
}
internal sealed class RecordingHubContext(RecordingClients clients) : IHubContext<MessagingHub>
{
    public IHubClients Clients => clients;
    public IGroupManager Groups { get; } = new RecordingGroups();
}
