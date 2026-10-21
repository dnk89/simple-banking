using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Payments.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [MinLength(1)]
    [ValidateEnumeratedItems]
    public List<ApiKeyEntry> ApiKeys { get; set; } = [];
}

public sealed class ApiKeyEntry
{
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Key { get; set; } = "";
}
