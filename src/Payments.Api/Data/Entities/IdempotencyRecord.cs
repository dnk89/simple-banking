namespace Payments.Api.Data.Entities;

public sealed class IdempotencyRecord
{
    public required string ClientId { get; set; }

    public required string Key { get; set; }

    // Written as empty until fingerprinting lands in M3; the column exists now so M3 adds no migration.
    public required string RequestFingerprint { get; set; }

    public Guid PaymentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
