using FlowPay.Transfers.Domain;

namespace FlowPay.Transfers.Features.Transfers;

public record TransferResponse(
    Guid Id,
    Guid FromWalletId,
    Guid ToWalletId,
    long AmountMinorUnits,
    string Currency,
    TransferStatus Status,
    string? FailureReason)
{
    public static TransferResponse From(Transfer transfer) => new(
        transfer.Id,
        transfer.FromWalletId,
        transfer.ToWalletId,
        transfer.AmountMinorUnits,
        transfer.Currency,
        transfer.Status,
        transfer.FailureReason);
}
