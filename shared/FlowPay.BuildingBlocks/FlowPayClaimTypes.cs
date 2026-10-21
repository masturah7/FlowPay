namespace FlowPay.BuildingBlocks;

/// <summary>
/// Custom claim types FlowPay-issued JWTs carry beyond the standard
/// registered ones (sub, email). See docs/epics/09-admin-ops-tooling.md.
/// </summary>
public static class FlowPayClaimTypes
{
    /// <summary>Present with value "true" only on admin accounts' tokens — omitted otherwise.</summary>
    public const string IsAdmin = "is_admin";
}
