using FlowPay.Transfers.Domain;
using FlowPay.Transfers.Features;

namespace FlowPay.Transfers.Features.Transfers;

/// <summary>
/// Superset of TransferResponse for admin search — adds AccountId (the
/// owning account), never exposed on the customer-facing response. See
/// docs/epics/09-admin-ops-tooling.md.
/// </summary>
public record AdminTransferResponse(
    Guid Id,
    Guid AccountId,
    Guid FromWalletId,
    TransferDestinationType DestinationType,
    Guid? ToWalletId,
    string? ExternalBankName,
    string? ExternalBankAccountNumber,
    long AmountMinorUnits,
    long FeeMinorUnits,
    string Currency,
    TransferStatus Status,
    string? FailureReason,
    int ReconciliationAttempts)
{
    public static AdminTransferResponse From(Transfer transfer) => new(
        transfer.Id,
        transfer.AccountId,
        transfer.FromWalletId,
        transfer.DestinationType,
        transfer.ToWalletId,
        transfer.ExternalBankName,
        BankAccountNumberMasking.Mask(transfer.ExternalBankAccountNumber),
        transfer.AmountMinorUnits,
        transfer.FeeMinorUnits,
        transfer.Currency,
        transfer.Status,
        transfer.FailureReason,
        transfer.ReconciliationAttempts);
}
