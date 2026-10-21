using System.ComponentModel.DataAnnotations;

namespace BankGateway.Worker.Configuration;

public sealed class Iso20022Options
{
    public const string SectionName = "Iso20022";

    // Maps to InitgPty/Nm in pain.001, which is Max70Text.
    [Required(AllowEmptyStrings = false)]
    [StringLength(70)]
    public string InitiatingPartyName { get; set; } = "";

    public bool ValidateBeforeSend { get; set; } = true;
}
