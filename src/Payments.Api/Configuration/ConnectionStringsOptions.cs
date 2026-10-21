using System.ComponentModel.DataAnnotations;

namespace Payments.Api.Configuration;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(AllowEmptyStrings = false)]
    public string Payments { get; set; } = "";
}
