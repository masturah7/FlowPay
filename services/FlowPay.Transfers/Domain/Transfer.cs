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
    /// recipient (or, for an external transfer, the fee leg) failed
    /// afterward — money has left the sender and isn't fully reflected
    /// elsewhere yet. Needs reconciliation; see docs/epics/05-transfers.md.
    /// </summary>
    PendingReconciliation,

    /// <summary>
    /// Terminal state for a successful external-bank transfer — distinct
    /// from Completed because no real settlement rail is integrated. Money
    /// left the sender and was recorded against FlowPay's own
    /// ExternalSettlement system wallet, not an actual bank. See
    /// docs/epics/07-external-transfers-beneficiaries-fees-limits.md.
    /// </summary>
    SubmittedExternally,
}

public enum TransferDestinationType
{
    InternalWallet,
    ExternalBank,
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

    public TransferDestinationType DestinationType { get; init; }

    /// <summary>
    /// Raw caller input, echoed back for idempotency-replay comparison —
    /// null when the caller gave ToWalletId directly instead. Not
    /// re-resolved on retry; ToWalletId/ExternalBank* below are the
    /// resolved snapshot taken at creation time.
    /// </summary>
    public Guid? ToBeneficiaryId { get; init; }

    /// <summary>Resolved destination wallet — set when DestinationType is InternalWallet.</summary>
    public Guid? ToWalletId { get; init; }

    /// <summary>Resolved destination — set when DestinationType is ExternalBank.</summary>
    public string? ExternalBankName { get; init; }

    /// <summary>Resolved destination — set when DestinationType is ExternalBank.</summary>
    public string? ExternalBankAccountNumber { get; init; }

    public long AmountMinorUnits { get; init; }

    /// <summary>
    /// Charged in addition to AmountMinorUnits, debited from the same
    /// sender wallet. Zero for internal transfers (see
    /// docs/epics/07-external-transfers-beneficiaries-fees-limits.md).
    /// </summary>
    public long FeeMinorUnits { get; set; }

    /// <summary>
    /// Ledger grouping key for the fee's own debit/credit pair — distinct
    /// from Id, which groups the main transfer's pair. Null when
    /// FeeMinorUnits is 0.
    /// </summary>
    public Guid? FeeLedgerReference { get; set; }

    public required string Currency { get; init; }

    public TransferStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public required string IdempotencyKey { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    [NotMapped]
    public Money Amount => new(AmountMinorUnits, Currency);
}
