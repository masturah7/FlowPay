namespace FlowPay.Notifications.Domain;

public enum NotificationType
{
    TransferSent,
    TransferReceived,
    TransferFailed,
    TransferPendingReconciliation,
}

/// <summary>
/// An in-app notification for one account. See
/// docs/epics/06-transaction-visibility.md — no email/SMS/push yet, this is
/// the mechanism those would eventually hang off of.
/// </summary>
public class Notification
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public NotificationType Type { get; init; }

    public required string Message { get; init; }

    public Guid? RelatedTransferId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? ReadAtUtc { get; set; }
}
