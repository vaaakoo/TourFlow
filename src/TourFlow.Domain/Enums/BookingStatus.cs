namespace TourFlow.Domain.Enums;

/// <summary>Defines the lifecycle state of a booking.</summary>
public enum BookingStatus
{
    Pending,
    Confirmed,
    Assigned,
    InProgress,
    Completed,
    Cancelled,
    Rejected
}
