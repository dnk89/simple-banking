using System.ComponentModel.DataAnnotations;

namespace Messaging.Retry;

public sealed class RetryOptions
{
    public const string SectionName = "Retry";

    [Range(0, 10)]
    public int InProcessAttempts { get; set; } = 3;

    [Range(1, 50)]
    public int MaxCycles { get; set; } = 5;

    [MinLength(1)]
    public TimeSpan[] Delays { get; set; } =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
    ];
}
