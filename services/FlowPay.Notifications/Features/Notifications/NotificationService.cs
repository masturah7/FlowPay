using FlowPay.Notifications.Data;
using FlowPay.Notifications.Domain;

namespace FlowPay.Notifications.Features.Notifications;

public enum MarkReadOutcome
{
    Success,
    NotFound,
}

public interface INotificationService
{
    Task<Notification> CreateAsync(
        Guid accountId,
        NotificationType type,
        string message,
        Guid? relatedTransferId,
        CancellationToken cancellationToken);

    Task<List<Notification>> GetMineAsync(Guid accountId, CancellationToken cancellationToken);

    Task<MarkReadOutcome> MarkReadAsync(Guid accountId, Guid notificationId, CancellationToken cancellationToken);
}

public class NotificationService(INotificationRepository notificationRepository) : INotificationService
{
    public async Task<Notification> CreateAsync(
        Guid accountId,
        NotificationType type,
        string message,
        Guid? relatedTransferId,
        CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Message = message,
            RelatedTransferId = relatedTransferId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        notificationRepository.Add(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public Task<List<Notification>> GetMineAsync(Guid accountId, CancellationToken cancellationToken) =>
        notificationRepository.GetByAccountIdAsync(accountId, cancellationToken);

    public async Task<MarkReadOutcome> MarkReadAsync(
        Guid accountId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.GetByIdAsync(notificationId, cancellationToken);

        if (notification is null || notification.AccountId != accountId)
        {
            return MarkReadOutcome.NotFound;
        }

        // Idempotent: marking an already-read notification read again just
        // confirms the same state rather than needing special-casing.
        notification.ReadAtUtc ??= DateTimeOffset.UtcNow;

        await notificationRepository.SaveChangesAsync(cancellationToken);

        return MarkReadOutcome.Success;
    }
}
