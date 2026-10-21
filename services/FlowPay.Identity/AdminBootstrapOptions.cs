namespace FlowPay.Identity;

/// <summary>
/// Bound from the "AdminBootstrap" configuration section. See
/// docs/epics/09-admin-ops-tooling.md for why this is a config allowlist
/// rather than a promotion flow.
/// </summary>
public class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public required string[] Emails { get; init; }
}
