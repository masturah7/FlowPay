using FlowPay.Wallet.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Wallet.Data;

public class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Wallet> Wallets => Set<Domain.Wallet>();

    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Wallet>(wallet =>
        {
            wallet.ToTable("wallets");
            wallet.HasKey(w => w.Id);
            wallet.Property(w => w.Currency).IsRequired().HasMaxLength(3);
            wallet.HasIndex(w => new { w.AccountId, w.Currency }).IsUnique();

            // Without a concurrency token, two concurrent Fund calls on the
            // same wallet both read the same starting balance and the
            // second SaveChanges silently overwrites the first's credit —
            // no exception, no conflict detected, money just disappears.
            // Postgres's xmin system column (the row's last-updating
            // transaction id) gives us that check for free: EF includes it
            // in the UPDATE's WHERE clause and throws
            // DbUpdateConcurrencyException if it's changed since we read it.
            wallet.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<WalletLedgerEntry>(entry =>
        {
            entry.ToTable("wallet_ledger_entries");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entry.Property(e => e.Direction).HasConversion<string>().HasMaxLength(16);
            entry.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(200);
            entry.HasIndex(e => new { e.WalletId, e.IdempotencyKey }).IsUnique();
        });
    }
}
