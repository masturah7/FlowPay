namespace FlowPay.BuildingBlocks;

/// <summary>
/// Bound from the "Jwt" configuration section. The same signing key, issuer,
/// and audience must be configured identically on every service that needs
/// to validate FlowPay-issued tokens — today that's only
/// FlowPay.Identity (which also issues them), but any service later added
/// behind [Authorize] needs the same three values.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; init; }

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public int AccessTokenLifetimeMinutes { get; init; } = 60;
}
