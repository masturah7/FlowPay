namespace FlowPay.Wallet.Features.Wallets;

public record AdminSystemWalletsResponse(string Currency, WalletResponse FeeRevenue, WalletResponse ExternalSettlement);
