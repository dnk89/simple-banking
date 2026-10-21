using System.ComponentModel.DataAnnotations;

namespace Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    // Kept small so the transaction that holds Kafka acks stays short (ADR-001).
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 20;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 10;

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
