using Payments.Api.Domain;

namespace Payments.Api.Data.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }

    public required string ClientId { get; set; }

    public PaymentStatus Status { get; set; }

    public required string DebtorIban { get; set; }

    public required string DebtorName { get; set; }

    public required string CreditorName { get; set; }

    public required string CreditorIban { get; set; }

    public string? CreditorBic { get; set; }

    public decimal Amount { get; set; }

    public required string Currency { get; set; }

    public string? RemittanceInfo { get; set; }

    public DateOnly RequestedExecutionDate { get; set; }

    public required string EndToEndId { get; set; }

    public string? RejectReasonCode { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
