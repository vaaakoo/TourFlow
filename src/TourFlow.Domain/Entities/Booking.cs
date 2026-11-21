using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a customer reservation for a tour or transfer.</summary>
public sealed class Booking : Entity
{
    private readonly List<BookingAssignment> _assignments = [];
    private readonly List<BookingStatusHistory> _statusHistory = [];
    private readonly List<Payment> _payments = [];

    private Booking()
    {
        BookingNumber = null!;
        Customer = null!;
        PickupLocation = null!;
    }

    public Booking(
        Customer customer,
        string bookingNumber,
        BookingType bookingType,
        DateOnly bookingDate,
        TimeOnly startTime,
        int passengerCount,
        string pickupLocation,
        string? dropoffLocation,
        decimal totalPrice,
        Tour? tour = null,
        string? specialRequests = null)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        CustomerId = customer.Id;
        BookingNumber = RequireValue(bookingNumber, nameof(bookingNumber));
        BookingType = bookingType;
        BookingDate = bookingDate;
        StartTime = startTime;
        SetPassengerCount(passengerCount);
        PickupLocation = RequireValue(pickupLocation, nameof(pickupLocation));
        DropoffLocation = string.IsNullOrWhiteSpace(dropoffLocation) ? null : dropoffLocation.Trim();
        SetTotalPrice(totalPrice);
        SpecialRequests = string.IsNullOrWhiteSpace(specialRequests) ? null : specialRequests.Trim();
        SetTour(bookingType, tour);
        Status = BookingStatus.Pending;

        customer.AddBooking(this);
        tour?.AddBooking(this);
    }

    /// <summary>Gets the externally visible booking number.</summary>
    public string BookingNumber { get; private set; }

    /// <summary>Gets the customer identifier.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Gets the customer who made the booking.</summary>
    public Customer Customer { get; private set; }

    /// <summary>Gets the optional tour identifier. It is null for transfer-only bookings.</summary>
    public Guid? TourId { get; private set; }

    /// <summary>Gets the optional booked tour.</summary>
    public Tour? Tour { get; private set; }

    /// <summary>Gets the commercial booking type.</summary>
    public BookingType BookingType { get; private set; }

    /// <summary>Gets the scheduled service date.</summary>
    public DateOnly BookingDate { get; private set; }

    /// <summary>Gets the scheduled start time.</summary>
    public TimeOnly StartTime { get; private set; }

    /// <summary>Gets the number of passengers.</summary>
    public int PassengerCount { get; private set; }

    /// <summary>Gets the required pickup location.</summary>
    public string PickupLocation { get; private set; }

    /// <summary>Gets the optional drop-off location.</summary>
    public string? DropoffLocation { get; private set; }

    /// <summary>Gets optional customer instructions.</summary>
    public string? SpecialRequests { get; private set; }

    /// <summary>Gets the server-calculated total booking price.</summary>
    public decimal TotalPrice { get; private set; }

    /// <summary>Gets the current booking lifecycle status.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Gets the SQL Server concurrency token used to reject stale writes.</summary>
    public byte[] Version { get; private set; } = [];

    /// <summary>Gets assignments made to fulfil this booking.</summary>
    public IReadOnlyCollection<BookingAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>Gets the immutable status-change history.</summary>
    public IReadOnlyCollection<BookingStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    /// <summary>Gets the payment records for this booking.</summary>
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    /// <summary>Creates a tour booking and calculates its price from the tour's current server-side price.</summary>
    public static Booking CreateTourBooking(
        Customer customer,
        Tour tour,
        string bookingNumber,
        DateOnly bookingDate,
        TimeOnly startTime,
        int passengerCount,
        string pickupLocation,
        string? dropoffLocation = null,
        string? specialRequests = null)
    {
        ArgumentNullException.ThrowIfNull(tour);
        tour.EnsureCanAccept(passengerCount);

        return new Booking(
            customer,
            bookingNumber,
            BookingType.Tour,
            bookingDate,
            startTime,
            passengerCount,
            pickupLocation,
            dropoffLocation,
            tour.Price * passengerCount,
            tour,
            specialRequests);
    }

    /// <summary>Updates mutable itinerary details before operations begin.</summary>
    public void UpdateItinerary(
        DateOnly bookingDate,
        TimeOnly startTime,
        int passengerCount,
        string pickupLocation,
        string? dropoffLocation,
        string? specialRequests)
    {
        EnsureCanModify();
        SetPassengerCount(passengerCount);
        BookingDate = bookingDate;
        StartTime = startTime;
        PickupLocation = RequireValue(pickupLocation, nameof(pickupLocation));
        DropoffLocation = string.IsNullOrWhiteSpace(dropoffLocation) ? null : dropoffLocation.Trim();
        SpecialRequests = string.IsNullOrWhiteSpace(specialRequests) ? null : specialRequests.Trim();
        Touch();
    }

    /// <summary>Updates mutable itinerary data and its server-calculated price atomically.</summary>
    public void UpdateItineraryAndPrice(
        DateOnly bookingDate, TimeOnly startTime, int passengerCount, string pickupLocation,
        string? dropoffLocation, string? specialRequests, decimal totalPrice)
    {
        UpdateItinerary(bookingDate, startTime, passengerCount, pickupLocation, dropoffLocation, specialRequests);
        if (Tour is not null) Tour.EnsureCanAccept(passengerCount);
        SetTotalPrice(totalPrice);
    }

    /// <summary>Moves the booking through a supported lifecycle transition and records it.</summary>
    public void TransitionTo(BookingStatus nextStatus, Guid changedByUserId, string? comment = null)
    {
        if (changedByUserId == Guid.Empty)
        {
            throw new DomainException("The user changing a booking status is required.");
        }

        if (!CanTransitionTo(nextStatus))
        {
            throw new InvalidBookingStatusTransitionException(Status, nextStatus);
        }

        var previousStatus = Status;
        Status = nextStatus;
        _statusHistory.Add(new BookingStatusHistory(this, previousStatus, nextStatus, changedByUserId, comment));
        Touch();
    }

    /// <summary>Cancels a booking when the lifecycle permits cancellation.</summary>
    public void Cancel(Guid changedByUserId, string? comment = null)
    {
        TransitionTo(BookingStatus.Cancelled, changedByUserId, comment);
    }

    /// <summary>Records an assignment and validates its compatibility with this booking.</summary>
    internal void AddAssignment(BookingAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.BookingId != Id)
        {
            throw new DomainException("An assignment can only be added to its own booking.");
        }

        EnsureAssignable();
        _assignments.Add(assignment);
    }

    internal void AddPayment(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (payment.BookingId != Id)
        {
            throw new DomainException("A payment can only be added to its own booking.");
        }

        _payments.Add(payment);
    }

    private void SetTour(BookingType bookingType, Tour? tour)
    {
        if (bookingType == BookingType.Tour && tour is null)
        {
            throw new DomainException("Tour bookings require a tour.");
        }

        if (bookingType == BookingType.Transfer && tour is not null)
        {
            throw new DomainException("Transfer-only bookings cannot reference a tour.");
        }

        if (tour is not null)
        {
            tour.EnsureCanAccept(PassengerCount);
        }

        Tour = tour;
        TourId = tour?.Id;
    }

    private void EnsureCanModify()
    {
        if (Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            throw new DomainException($"A booking with status '{Status}' cannot be modified.");
        }
    }

    private void EnsureAssignable()
    {
        if (Status is not (BookingStatus.Confirmed or BookingStatus.Assigned))
        {
            throw new DomainException($"A booking with status '{Status}' cannot be assigned.");
        }
    }

    private bool CanTransitionTo(BookingStatus nextStatus) => (Status, nextStatus) switch
    {
        (BookingStatus.Pending, BookingStatus.Confirmed) => true,
        (BookingStatus.Pending, BookingStatus.Cancelled) => true,
        (BookingStatus.Pending, BookingStatus.Rejected) => true,
        (BookingStatus.Confirmed, BookingStatus.Assigned) => true,
        (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
        (BookingStatus.Assigned, BookingStatus.InProgress) => true,
        (BookingStatus.Assigned, BookingStatus.Confirmed) => true,
        (BookingStatus.Assigned, BookingStatus.Cancelled) => true,
        (BookingStatus.InProgress, BookingStatus.Completed) => true,
        _ => false
    };

    private void SetPassengerCount(int passengerCount)
    {
        if (passengerCount <= 0)
        {
            throw new DomainException("Passenger count must be greater than zero.");
        }

        PassengerCount = passengerCount;
    }

    private void SetTotalPrice(decimal totalPrice)
    {
        if (totalPrice < 0)
        {
            throw new DomainException("Booking total price cannot be negative.");
        }

        TotalPrice = totalPrice;
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{parameterName} is required.");
        }

        return value.Trim();
    }
}
