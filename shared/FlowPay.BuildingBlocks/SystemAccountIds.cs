namespace FlowPay.BuildingBlocks;

/// <summary>
/// Reserved account ids for platform-owned wallets — not real customers.
/// FlowPay.Wallet auto-provisions a wallet per (system account id, currency)
/// on first use (see the internal "get or create system wallet" endpoint).
/// Used by FlowPay.Transfers to route fee revenue and simulated
/// external-transfer settlement through the same real, balance-tracked
/// debit/credit/ledger mechanism as any other transfer — see
/// docs/epics/07-external-transfers-beneficiaries-fees-limits.md.
/// </summary>
public static class SystemAccountIds
{
    public static readonly Guid FeeRevenue = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static readonly Guid ExternalSettlement = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
