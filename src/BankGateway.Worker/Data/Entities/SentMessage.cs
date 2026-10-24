namespace BankGateway.Worker.Data.Entities;

public sealed class SentMessage
{
    public Guid PaymentId { get; set; }

    // Deterministic per payment, so a redelivered event resubmits the same MsgId and the bank dedups it.
    public required string MessageId { get; set; }

    public required string PaymentInfoId { get; set; }

    public required string InstructionId { get; set; }

    // Null until M4 starts populating the signed pain.001; M4.11 tightens this to NOT NULL.
    public string? Xml { get; set; }

    public SentMessageStatus Status { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum SentMessageStatus
{
    Pending,
    Sent,
}
