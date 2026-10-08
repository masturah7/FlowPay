using System.ComponentModel.DataAnnotations.Schema;
using FlowPay.BuildingBlocks;

namespace FlowPay.Transfers.Domain;

public enum TransferStatus
{
    Pending,
    Completed,
    Failed,

    /// <summary>
    /// The sender was debited and the ledger recorded it, but crediting the
    /// recipient failed afterward — money has left the sender and isn't
    /// reflected anywhere else yet. Needs reconciliation; see
    /// docs/epics/05-transfers.md.
    /// </summary>
    PendingReconciliation,
}

/// <summary>
/// One attempted transfer and its outcome — including failed attempts.
/// Nothing about an attempt is thrown away: this is the audit trail
/// answering "can operations reconstruct what happened."
/// </summary>
public class Transfer
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public Guid FromWalletId { get; init; }

    public Guid ToWalletId { get; init; }

    public long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public TransferStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public required string IdempotencyKey { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    [NotMapped]
    public Money Amount => new(AmountMinorUnits, Currency);
}
