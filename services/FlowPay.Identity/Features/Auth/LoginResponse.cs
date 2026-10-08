namespace FlowPay.Identity.Features.Auth;

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
