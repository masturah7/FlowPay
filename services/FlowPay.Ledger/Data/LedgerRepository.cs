using FlowPay.BuildingBlocks;
using FlowPay.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Ledger.Data;

public interface ILedgerRepository : IRepository<LedgerEntry, Guid>
{
    Task<LedgerEntry?> GetByWalletAndIdempotencyKeyAsync(
        Guid walletId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Derived balance for a wallet: sum of credits minus sum of debits.</summary>
    Task<long> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken);
}

public class LedgerRepository(LedgerDbContext dbContext)
    : EfRepository<LedgerEntry, Guid>(dbContext), ILedgerRepository
{
    public Task<LedgerEntry?> GetByWalletAndIdempotencyKeyAsync(
        Guid walletId, string idempotencyKey, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(
            e => e.WalletId == walletId && e.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<long> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken)
    {
        var credits = await Set
            .Where(e => e.WalletId == walletId && e.Direction == LedgerEntryDirection.Credit)
            .SumAsync(e => e.AmountMinorUnits, cancellationToken);

        var debits = await Set
            .Where(e => e.WalletId == walletId && e.Direction == LedgerEntryDirection.Debit)
            .SumAsync(e => e.AmountMinorUnits, cancellationToken);

        return credits - debits;
    }
}
