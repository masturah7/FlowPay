using FlowPay.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Notifications.Data;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(notification =>
        {
            notification.ToTable("notifications");
            notification.HasKey(n => n.Id);
            notification.Property(n => n.Type).HasConversion<string>().HasMaxLength(32);
            notification.Property(n => n.Message).IsRequired().HasMaxLength(1000);
            notification.HasIndex(n => n.AccountId);
        });
    }
}
