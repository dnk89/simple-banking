namespace BankGateway.Worker.Data.Entities;

public sealed class ReceivedStatus
{
    public long Id { get; set; }

    public required string MessageId { get; set; }

    public required string InstructionId { get; set; }

    // Raw pain.002 as received, kept for audit; the parsed status is stored alongside it.
    public required string RawXml { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
