using System.ComponentModel.DataAnnotations;
using FlowPay.Identity.Domain;

namespace FlowPay.Identity.Features.Kyc;

public class SubmitKycRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string FullName { get; init; }

    [Required]
    public DateOnly DateOfBirth { get; init; }

    [Required]
    public KycDocumentType DocumentType { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string DocumentNumber { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult(
                "Date of birth cannot be in the future.", [nameof(DateOfBirth)]);
        }
    }
}
