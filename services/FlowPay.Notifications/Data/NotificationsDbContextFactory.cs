using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowPay.Notifications.Data;

/// <summary>
/// Used only by `dotnet ef` design-time tooling run from a developer
/// machine, where the "flowpay-db" Docker Compose DNS name in
/// appsettings.json doesn't resolve. Points at localhost:5432 instead,
/// which works because docker-compose.yml publishes Postgres there. Not
/// used by the running application.
/// </summary>
public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationsDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=flowpay_notifications;Username=flowpay;Password=flowpay");

        return new NotificationsDbContext(optionsBuilder.Options);
    }
}
