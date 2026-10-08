using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Data;

public class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transfer>(transfer =>
        {
            transfer.ToTable("transfers");
            transfer.HasKey(t => t.Id);
            transfer.Property(t => t.Currency).IsRequired().HasMaxLength(3);
            transfer.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
            transfer.Property(t => t.IdempotencyKey).IsRequired().HasMaxLength(200);
            transfer.HasIndex(t => t.IdempotencyKey).IsUnique();
            transfer.HasIndex(t => t.AccountId);
        });
    }
}
