using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FlowPay.BuildingBlocks;
using FlowPay.Identity.Domain;
using Microsoft.IdentityModel.Tokens;

namespace FlowPay.Identity.Features.Auth;

public interface IJwtTokenGenerator
{
    LoginResponse GenerateFor(Account account);
}

public class JwtTokenGenerator(JwtOptions jwtOptions) : IJwtTokenGenerator
{
    public LoginResponse GenerateFor(Account account)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(accessToken, expiresAt);
    }
}
