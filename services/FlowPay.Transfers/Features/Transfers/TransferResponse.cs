using FlowPay.Transfers.Domain;
using FlowPay.Transfers.Features;

namespace FlowPay.Transfers.Features.Transfers;

public record TransferResponse(
    Guid Id,
    Guid FromWalletId,
    TransferDestinationType DestinationType,
    Guid? ToWalletId,
    string? ExternalBankName,
    string? ExternalBankAccountNumber,
    long AmountMinorUnits,
    long FeeMinorUnits,
    string Currency,
    TransferStatus Status,
    string? FailureReason)
{
    public static TransferResponse From(Transfer transfer) => new(
        transfer.Id,
        transfer.FromWalletId,
        transfer.DestinationType,
        transfer.ToWalletId,
        transfer.ExternalBankName,
        BankAccountNumberMasking.Mask(transfer.ExternalBankAccountNumber),
        transfer.AmountMinorUnits,
        transfer.FeeMinorUnits,
        transfer.Currency,
        transfer.Status,
        transfer.FailureReason);
}
