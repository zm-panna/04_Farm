namespace EnterpriseERP.Domain;

/// <summary>Immutable, one-recipient, branch-scoped internal correspondence.</summary>
public sealed class InternalMessage
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string SenderUserId { get; set; } = "";
    public string RecipientUserId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>Read state belongs to exactly one recipient; transport hints are not delivery receipts.</summary>
public sealed class UserInboxNotification
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string UserId { get; set; } = "";
    public long MessageId { get; set; }
    public InternalMessage Message { get; set; } = null!;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ReadUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Durable hint work. Completion means attempted hint or lost eligibility, never delivery/read.</summary>
public sealed class MessagingOutbox
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string UserId { get; set; } = "";
    public long NotificationId { get; set; }
    public UserInboxNotification Notification { get; set; } = null!;
    public long CreatedUtcTicks { get; set; }
    public long? HintProcessedUtcTicks { get; set; }
    public long LeaseUntilUtcTicks { get; set; }
    public string? LeaseOwner { get; set; }
    public int Attempts { get; set; }
}

/// <summary>Shared relational flood budget, updated in the same transaction as the message.</summary>
public sealed class MessagingRateBucket
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string UserId { get; set; } = "";
    public string Period { get; set; } = "";
    public long WindowUtcTicks { get; set; }
    public int Count { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Automation alerts stay branch-scoped; this stores only each reviewer's acknowledgement.</summary>
public sealed class MessagingAutomationRead
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string UserId { get; set; } = "";
    public long NotificationId { get; set; }
    public DateTimeOffset ReadUtc { get; set; }
}
