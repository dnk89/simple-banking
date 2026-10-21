using System.ComponentModel.DataAnnotations;

namespace BankGateway.Worker.Configuration;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(AllowEmptyStrings = false)]
    public string BankGateway { get; set; } = "";
}
