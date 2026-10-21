namespace Payments.Api.Domain;

public enum PaymentStatus
{
    Received,
    Submitted,
    Accepted,
    Rejected,
    // Unreachable until M2 introduces failure paths.
    Failed,
}
