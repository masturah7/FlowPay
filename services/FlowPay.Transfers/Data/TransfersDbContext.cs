using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Data;

public class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transfer>(transfer =>
        {
            transfer.ToTable("transfers");
            transfer.HasKey(t => t.Id);
            transfer.Property(t => t.Currency).IsRequired().HasMaxLength(3);
            transfer.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
            transfer.Property(t => t.DestinationType).HasConversion<string>().HasMaxLength(32);
            transfer.Property(t => t.IdempotencyKey).IsRequired().HasMaxLength(200);
            transfer.Property(t => t.ExternalBankName).HasMaxLength(200);
            transfer.Property(t => t.ExternalBankAccountNumber).HasMaxLength(64);
            transfer.HasIndex(t => t.IdempotencyKey).IsUnique();
            transfer.HasIndex(t => t.AccountId);
        });

        modelBuilder.Entity<Beneficiary>(beneficiary =>
        {
            beneficiary.ToTable("beneficiaries");
            beneficiary.HasKey(b => b.Id);
            beneficiary.Property(b => b.Label).IsRequired().HasMaxLength(100);
            beneficiary.Property(b => b.Type).HasConversion<string>().HasMaxLength(32);
            beneficiary.Property(b => b.BankName).HasMaxLength(200);
            beneficiary.Property(b => b.BankAccountNumber).HasMaxLength(64);
            beneficiary.HasIndex(b => b.AccountId);
        });
    }
}
