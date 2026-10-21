using System.ComponentModel.DataAnnotations;

namespace BankGateway.Worker.Configuration;

public sealed class SigningOptions
{
    public const string SectionName = "Signing";

    // Only the self-signed mode exists until a real certificate is needed (README simplifications).
    [Required]
    [RegularExpression("^EphemeralSelfSigned$")]
    public string Mode { get; set; } = "EphemeralSelfSigned";

    public string? PfxPath { get; set; }

    public string? PfxPassword { get; set; }
}
