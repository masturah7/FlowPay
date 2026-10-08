using FlowPay.Ledger.Data;
using FlowPay.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Ledger.Features.Ledger;

public enum RecordTransferOutcome
{
    Recorded,
    IdempotencyKeyConflict,
}

public record RecordTransferResult(RecordTransferOutcome Outcome, LedgerEntry? DebitEntry, LedgerEntry? CreditEntry);

public interface ILedgerService
{
    Task<RecordTransferResult> RecordTransferAsync(
        Guid transferReference,
        Guid fromWalletId,
        Guid toWalletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<long> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken);
}

/// <summary>
/// Writes the immutable, double-entry record of what FlowPay.Transfers
/// tells it happened. Does not decide whether a transfer is *allowed* —
/// that's Transfers' job; this just records it atomically and idempotently.
/// </summary>
public class LedgerService(ILedgerRepository ledgerRepository) : ILedgerService
{
    public async Task<RecordTransferResult> RecordTransferAsync(
        Guid transferReference,
        Guid fromWalletId,
        Guid toWalletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        var debitEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransferReference = transferReference,
            WalletId = fromWalletId,
            Direction = LedgerEntryDirection.Debit,
            AmountMinorUnits = amountMinorUnits,
            Currency = normalizedCurrency,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = now,
        };
        var creditEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransferReference = transferReference,
            WalletId = toWalletId,
            Direction = LedgerEntryDirection.Credit,
            AmountMinorUnits = amountMinorUnits,
            Currency = normalizedCurrency,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = now,
        };

        ledgerRepository.Add(debitEntry);
        ledgerRepository.Add(creditEntry);

        try
        {
            // Both entries go in a single SaveChanges — one transaction,
            // genuinely all-or-nothing. A retry of this exact call never
            // finds "only one of the two" from a prior partial attempt.
            await ledgerRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var existingDebit = await ledgerRepository.GetByWalletAndIdempotencyKeyAsync(
                fromWalletId, idempotencyKey, cancellationToken);
            var existingCredit = await ledgerRepository.GetByWalletAndIdempotencyKeyAsync(
                toWalletId, idempotencyKey, cancellationToken);

            if (existingDebit is null || existingCredit is null)
            {
                // This idempotency key was used before for only one side of
                // a transfer, not both — not a clean replay of this request.
                // Don't guess; fail loudly.
                throw;
            }

            var isSameRequest =
                existingDebit.TransferReference == transferReference &&
                existingDebit.AmountMinorUnits == amountMinorUnits &&
                existingDebit.Currency == normalizedCurrency &&
                existingCredit.TransferReference == transferReference &&
                existingCredit.AmountMinorUnits == amountMinorUnits &&
                existingCredit.Currency == normalizedCurrency;

            if (!isSameRequest)
            {
                return new RecordTransferResult(RecordTransferOutcome.IdempotencyKeyConflict, null, null);
            }

            return new RecordTransferResult(RecordTransferOutcome.Recorded, existingDebit, existingCredit);
        }

        return new RecordTransferResult(RecordTransferOutcome.Recorded, debitEntry, creditEntry);
    }

    public Task<long> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken) =>
        ledgerRepository.GetBalanceAsync(walletId, cancellationToken);
}
