using System.ComponentModel.DataAnnotations;

namespace BankGateway.Worker.Configuration;

public sealed class MockBankOptions
{
    public const string SectionName = "MockBank";

    [Required]
    public Uri? BaseUrl { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
