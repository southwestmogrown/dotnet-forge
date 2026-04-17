using System.ComponentModel.DataAnnotations;

namespace Core.Options;

public class JwtOptions : IValidatableObject
{
    [Required]
    public string Secret { get; init; } = "";

    [Required]
    public string Issuer { get; init; } = "";

    [Required]
    public string Audience { get; init; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Secret) || Secret.Length < 32)
            yield return new ValidationResult(
                "Jwt:Secret must be at least 32 characters.",
                new[] { nameof(Secret) });
    }
}