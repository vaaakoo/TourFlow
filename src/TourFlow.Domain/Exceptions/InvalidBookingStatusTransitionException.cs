using TourFlow.Domain.Enums;

namespace TourFlow.Domain.Exceptions;

/// <summary>Thrown when a booking status transition is not part of the supported lifecycle.</summary>
public sealed class InvalidBookingStatusTransitionException(BookingStatus from, BookingStatus to)
    : DomainException($"Booking status cannot transition from '{from}' to '{to}'.");
