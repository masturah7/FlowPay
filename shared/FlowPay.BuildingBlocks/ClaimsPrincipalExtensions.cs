using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FlowPay.BuildingBlocks;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The account id every FlowPay-issued JWT carries as its `sub` claim
    /// (see FlowPay.Identity's JwtTokenGenerator). Null if the claim is
    /// missing or isn't a parseable Guid — callers should treat that as
    /// unauthenticated, not assume it can't happen.
    /// </summary>
    public static Guid? GetAccountId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var accountId) ? accountId : null;
    }
}
