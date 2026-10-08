using FlowPay.Wallet.Domain;

namespace FlowPay.Wallet.Features.Wallets;

public record WalletTransactionResponse(
    Guid Id,
    LedgerEntryDirection Direction,
    long AmountMinorUnits,
    string Currency,
    long BalanceAfterMinorUnits,
    DateTimeOffset CreatedAtUtc)
{
    public static WalletTransactionResponse From(WalletLedgerEntry entry) => new(
        entry.Id,
        entry.Direction,
        entry.AmountMinorUnits,
        entry.Currency,
        entry.BalanceAfterMinorUnits,
        entry.CreatedAtUtc);
}
