namespace FlowPay.BuildingBlocks;

/// <summary>
/// Bound from the "InternalApi" configuration section. The same key must be
/// configured identically on every service that calls, or is called by,
/// another FlowPay service directly (not through the gateway) — e.g.
/// FlowPay.Transfers calling FlowPay.Wallet's internal credit/debit
/// endpoints. This is a shared-secret stand-in for real service identity
/// (mTLS, per-service credentials); see docs/epics/05-transfers.md for why
/// that's deferred rather than silently skipped.
/// </summary>
public class InternalApiKeyOptions
{
    public const string SectionName = "InternalApi";

    public required string ApiKey { get; init; }
}
