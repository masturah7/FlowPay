using System.ComponentModel.DataAnnotations;

namespace FlowPay.Identity.Features.Kyc;

public class RejectKycRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Reason { get; init; }
}
