using System.ComponentModel.DataAnnotations;

namespace FlowPay.Identity.Features.Auth;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}
