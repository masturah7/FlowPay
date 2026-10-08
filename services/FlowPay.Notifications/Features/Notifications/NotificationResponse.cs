using FlowPay.Notifications.Domain;

namespace FlowPay.Notifications.Features.Notifications;

public record NotificationResponse(
    Guid Id,
    NotificationType Type,
    string Message,
    Guid? RelatedTransferId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc)
{
    public static NotificationResponse From(Notification notification) => new(
        notification.Id,
        notification.Type,
        notification.Message,
        notification.RelatedTransferId,
        notification.CreatedAtUtc,
        notification.ReadAtUtc);
}
