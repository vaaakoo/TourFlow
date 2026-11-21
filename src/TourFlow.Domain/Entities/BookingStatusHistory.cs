using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Records an immutable booking status transition.</summary>
public sealed class BookingStatusHistory : Entity
{
    internal BookingStatusHistory(
        Booking booking,
        BookingStatus previousStatus,
        BookingStatus newStatus,
        Guid changedByUserId,
        string? comment)
    {
        if (changedByUserId == Guid.Empty)
        {
            throw new DomainException("The user changing a booking status is required.");
        }

        Booking = booking ?? throw new ArgumentNullException(nameof(booking));
        BookingId = booking.Id;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        ChangedAt = DateTime.UtcNow;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    /// <summary>Gets the booking identifier.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>Gets the booking whose status changed.</summary>
    public Booking Booking { get; private set; }

    /// <summary>Gets the status before the transition.</summary>
    public BookingStatus PreviousStatus { get; private set; }

    /// <summary>Gets the resulting status.</summary>
    public BookingStatus NewStatus { get; private set; }

    /// <summary>Gets the user identifier that initiated the transition.</summary>
    public Guid ChangedByUserId { get; private set; }

    /// <summary>Gets the UTC time of the transition.</summary>
    public DateTime ChangedAt { get; private set; }

    /// <summary>Gets the optional status-change comment.</summary>
    public string? Comment { get; private set; }
}
