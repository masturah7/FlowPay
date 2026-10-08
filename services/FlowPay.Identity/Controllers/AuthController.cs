using Asp.Versioning;
using FlowPay.Identity.Features.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Identity.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);

        return result.Outcome switch
        {
            LoginOutcome.Success => Ok(result.Response),
            LoginOutcome.AccountSuspended => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account suspended",
                detail: "This account has been suspended."),
            LoginOutcome.InvalidCredentials => Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials",
                detail: "The email or password is incorrect."),
            // Every named LoginOutcome is handled explicitly above — this is
            // not a shorthand for one of them. If it fires, a new case was
            // added without updating this switch; fail loudly rather than
            // silently reporting it as "invalid credentials".
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled LoginOutcome."),
        };
    }
}
