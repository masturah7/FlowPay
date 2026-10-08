using System.ComponentModel.DataAnnotations.Schema;
using FlowPay.BuildingBlocks;

namespace FlowPay.Ledger.Domain;

public enum LedgerEntryDirection
{
    Credit,
    Debit,
}

/// <summary>
/// One leg of a double-entry transfer. Every transfer writes exactly two of
/// these (one Debit, one Credit) in the same transaction — see
/// Features/Ledger/LedgerService.cs. Immutable: never updated, never
/// deleted. Not the authority on whether a transfer was *allowed* (that's
/// FlowPay.Transfers' job) — only on what was recorded as having happened.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; init; }

    /// <summary>Groups the debit+credit pair that make up one transfer.</summary>
    public Guid TransferReference { get; init; }

    public Guid WalletId { get; init; }

    public LedgerEntryDirection Direction { get; init; }

    public long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required string IdempotencyKey { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    [NotMapped]
    public Money Amount => new(AmountMinorUnits, Currency);
}
