namespace FlowPay.Ledger.Features.Ledger;

public record RecordTransferResponse(
    Guid TransferReference, Guid FromWalletId, Guid ToWalletId, long AmountMinorUnits, string Currency);
