namespace TourFlow.Domain.Enums;

/// <summary>Defines the processing state of a payment.</summary>
public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded
}
