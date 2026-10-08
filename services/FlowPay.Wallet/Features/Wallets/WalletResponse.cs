namespace FlowPay.Wallet.Features.Wallets;

public record WalletResponse(Guid Id, Guid AccountId, string Currency, long BalanceMinorUnits)
{
    public static WalletResponse From(Domain.Wallet wallet) =>
        new(wallet.Id, wallet.AccountId, wallet.Currency, wallet.BalanceMinorUnits);
}
