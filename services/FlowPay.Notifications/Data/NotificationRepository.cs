using FlowPay.BuildingBlocks;
using FlowPay.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Notifications.Data;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    Task<List<Notification>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
}

public class NotificationRepository(NotificationsDbContext dbContext)
    : EfRepository<Notification, Guid>(dbContext), INotificationRepository
{
    public Task<List<Notification>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        Set.Where(n => n.AccountId == accountId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(cancellationToken);
}
