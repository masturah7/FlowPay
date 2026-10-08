using FlowPay.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Ledger.Data;

public class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LedgerEntry>(entry =>
        {
            entry.ToTable("ledger_entries");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Direction).HasConversion<string>().HasMaxLength(16);
            entry.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entry.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(200);

            // Same wallet can't record the same idempotency key twice, but
            // the same key legitimately appears once for the debit wallet
            // and once for the credit wallet of one transfer.
            entry.HasIndex(e => new { e.WalletId, e.IdempotencyKey }).IsUnique();

            // Fast "what's this wallet's balance" (sum credits - debits).
            entry.HasIndex(e => e.WalletId);

            entry.HasIndex(e => e.TransferReference);
        });
    }
}
