using Microsoft.AspNetCore.Authorization;
using TrustedInfrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

[Authorize]
public sealed class MessagingHub(MessagingAccess access, ITrustedRuntimeGate gate) : Hub
{
    private const string GroupKey = "Messaging.ServerGroup";
    public override async Task OnConnectedAsync()
    {
        try
        {
            gate.ThrowIfUnavailable();
            var request = Context.GetHttpContext()?.Request;
            var origin = request?.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var supplied)
                || !Uri.TryCreate($"{request!.Scheme}://{request.Host}", UriKind.Absolute, out var expected)
                || !string.Equals(supplied.GetLeftPart(UriPartial.Authority), expected.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException();
            gate.ThrowIfUnavailable();
            var actor = await access.DemandAsync(Context.User, "Messaging.View", Context.ConnectionAborted);
            var group = MessagingAccess.Group(actor);
            Context.Items[GroupKey] = group;
            await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
            await base.OnConnectedAsync();
        }
        catch (UnauthorizedAccessException) { Context.Abort(); throw new HubException("Messaging session is unavailable."); }
    }

    /// <summary>Only a content-free catch-up hint; messages/read changes use freshly authorized CSRF-protected REST.</summary>
    public async Task Sync()
    {
        try
        {
            gate.ThrowIfUnavailable();
            var actor = await access.DemandAsync(Context.User, "Messaging.View", Context.ConnectionAborted);
            if (!Context.Items.TryGetValue(GroupKey, out var group) || !string.Equals(group as string, MessagingAccess.Group(actor), StringComparison.Ordinal))
                throw new UnauthorizedAccessException();
            gate.ThrowIfUnavailable();
            await Clients.Caller.SendCoreAsync("inboxChanged", [], Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException)
        {
            if (Context.Items.TryGetValue(GroupKey, out var group) && group is string name)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, name, CancellationToken.None);
            Context.Abort();
            throw new HubException("Messaging session is unavailable.");
        }
    }
}

/// <summary>
/// Relational leases coordinate nodes. A crash between sending and completion may repeat a harmless hint.
/// A missing/disconnected client never destroys its durable inbox, and REST polling is the recovery path.
/// </summary>
public sealed class MessagingOutboxDispatcher(AppDbContext db, MessagingAccess access, IHubContext<MessagingHub> hub, ITrustedRuntimeGate gate)
{
    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); gate.ThrowIfUnavailable();
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var candidates = await db.MessagingOutbox.AsNoTracking()
            .Where(x => x.HintProcessedUtcTicks == null && x.LeaseUntilUtcTicks < now)
            .OrderBy(x => x.Id).Select(x => x.Id).Take(100).ToListAsync(ct);
        var processed = 0;
        foreach (var id in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var owner = Guid.NewGuid().ToString("N");
            var claimed = await db.MessagingOutbox.Where(x => x.Id == id && x.HintProcessedUtcTicks == null && x.LeaseUntilUtcTicks < now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseOwner, owner).SetProperty(x => x.LeaseUntilUtcTicks, now + TimeSpan.TicksPerMinute)
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
            if (claimed != 1) continue;
            try
            {
                var row = await db.MessagingOutbox.AsNoTracking().SingleAsync(x => x.Id == id && x.LeaseOwner == owner, ct);
                var actor = await access.EligibleAsync(row.CompanyId, row.BranchId, row.UserId, "Messaging.View", ct);
                if (actor is not null)
                {
                    gate.ThrowIfUnavailable();
                    await hub.Clients.Group(MessagingAccess.Group(actor)).SendCoreAsync("inboxChanged", [], ct);
                }
                await db.MessagingOutbox.Where(x => x.Id == id && x.LeaseOwner == owner)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.HintProcessedUtcTicks, (long?)DateTimeOffset.UtcNow.UtcTicks)
                        .SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseUntilUtcTicks, 0L), ct);
                processed++;
            }
            catch
            {
                // No content/error text is stored or published. An expired lease also recovers an unclean shutdown.
                await db.MessagingOutbox.Where(x => x.Id == id && x.LeaseOwner == owner)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseOwner, (string?)null)
                        .SetProperty(x => x.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddSeconds(10).UtcTicks), CancellationToken.None);
                throw;
            }
        }
        return processed;
    }
}

public sealed class MessagingOutboxWorker(IServiceScopeFactory scopes, ILogger<MessagingOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MessagingOutboxDispatcher>().DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Internal messaging hint dispatch will retry after {FailureType}. Durable inbox remains available.", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static class RealtimeMessagingRegistration
{
    public static ISignalRServerBuilder AddFarmMessaging(this IServiceCollection services)
    {
        services.AddScoped<MessagingAccess>();
        services.AddScoped<MessagingService>();
        services.AddScoped<MessagingOutboxDispatcher>();
        services.AddHostedService<MessagingOutboxWorker>();
        return services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.MaximumReceiveMessageSize = 4096;
            options.MaximumParallelInvocationsPerClient = 1;
        });
    }

    public static IEndpointConventionBuilder MapFarmMessaging(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<MessagingHub>("/hubs/messaging", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
}
