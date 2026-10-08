using System.ComponentModel.DataAnnotations.Schema;
using FlowPay.BuildingBlocks;

namespace FlowPay.Wallet.Domain;

public enum LedgerEntryDirection
{
    Credit,
    Debit,
}

/// <summary>
/// An append-only record of a single balance change on a wallet. Never
/// updated or deleted — see docs/epics/04-wallets.md for why this exists
/// instead of a call to FlowPay.Ledger (not yet built out).
/// </summary>
public class WalletLedgerEntry
{
    public Guid Id { get; init; }

    public Guid WalletId { get; init; }

    public LedgerEntryDirection Direction { get; init; }

    public long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>Balance snapshot immediately after this entry, for audit/history reads.</summary>
    public long BalanceAfterMinorUnits { get; init; }

    public required string IdempotencyKey { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    [NotMapped]
    public Money Amount => new(AmountMinorUnits, Currency);
}
