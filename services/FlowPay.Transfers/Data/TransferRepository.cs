using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Data;

public interface ITransferRepository : IRepository<Transfer, Guid>
{
    Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<List<Transfer>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Sum of AmountMinorUnits (excluding fees) for accountId's transfers
    /// that actually moved money — Completed, SubmittedExternally, or
    /// PendingReconciliation, never Failed — created at or after sinceUtc.
    /// excludeTransferId is the transfer currently being evaluated: on a
    /// retry of a PendingReconciliation transfer, its own prior contribution
    /// must not be double-counted against itself. Used for the daily
    /// transfer-limit check; see
    /// docs/epics/07-external-transfers-beneficiaries-fees-limits.md.
    /// </summary>
    Task<long> GetSentAmountSinceAsync(
        Guid accountId, DateTimeOffset sinceUtc, Guid excludeTransferId, CancellationToken cancellationToken);
}

public class TransferRepository(TransfersDbContext dbContext)
    : EfRepository<Transfer, Guid>(dbContext), ITransferRepository
{
    private static readonly TransferStatus[] MoneyMovedStatuses =
    [
        TransferStatus.Completed, TransferStatus.SubmittedExternally, TransferStatus.PendingReconciliation,
    ];

    public Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<List<Transfer>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        Set.Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<long> GetSentAmountSinceAsync(
        Guid accountId, DateTimeOffset sinceUtc, Guid excludeTransferId, CancellationToken cancellationToken)
    {
        var sum = await Set
            .Where(t =>
                t.AccountId == accountId &&
                t.CreatedAtUtc >= sinceUtc &&
                t.Id != excludeTransferId &&
                MoneyMovedStatuses.Contains(t.Status))
            .SumAsync(t => (long?)t.AmountMinorUnits, cancellationToken);

        return sum ?? 0L;
    }
}
