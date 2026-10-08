using FlowPay.BuildingBlocks;
using FlowPay.Wallet.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Wallet.Data;

public interface IWalletRepository : IRepository<Domain.Wallet, Guid>
{
    Task<List<Domain.Wallet>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    void AddLedgerEntry(WalletLedgerEntry entry);

    Task<WalletLedgerEntry?> GetLedgerEntryByIdempotencyKeyAsync(
        Guid walletId, string idempotencyKey, CancellationToken cancellationToken);
}

public class WalletRepository(WalletDbContext dbContext)
    : EfRepository<Domain.Wallet, Guid>(dbContext), IWalletRepository
{
    public Task<List<Domain.Wallet>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        Set.Where(w => w.AccountId == accountId).ToListAsync(cancellationToken);

    public void AddLedgerEntry(WalletLedgerEntry entry) => dbContext.WalletLedgerEntries.Add(entry);

    public Task<WalletLedgerEntry?> GetLedgerEntryByIdempotencyKeyAsync(
        Guid walletId, string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.WalletLedgerEntries.SingleOrDefaultAsync(
            e => e.WalletId == walletId && e.IdempotencyKey == idempotencyKey,
            cancellationToken);
}
