namespace Payments.Api.Configuration;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(24);
}
