using System.ComponentModel.DataAnnotations;

namespace FlowPay.Identity.Features.Accounts;

public class RegisterAccountRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [MinLength(8)]
    public required string Password { get; init; }
}
