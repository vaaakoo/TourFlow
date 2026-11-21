using TourFlow.Domain.Common;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Connects a booking with the driver and vehicle assigned to fulfil it.</summary>
public sealed class BookingAssignment : Entity
{
    public BookingAssignment(
        Booking booking,
        Driver driver,
        Vehicle vehicle,
        DateOnly assignmentDate,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        Booking = booking ?? throw new ArgumentNullException(nameof(booking));
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
        BookingId = booking.Id;
        DriverId = driver.Id;
        VehicleId = vehicle.Id;
        AssignmentDate = assignmentDate;
        StartTime = startTime;
        EndTime = endTime;

        if (endTime <= startTime)
        {
            throw new DomainException("Assignment end time must be after its start time.");
        }

        driver.EnsureAssignable();
        vehicle.EnsureAssignableFor(booking.PassengerCount);
        booking.AddAssignment(this);
        driver.AddAssignment(this);
        vehicle.AddAssignment(this);
    }

    /// <summary>Gets the booking identifier.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>Gets the booking being fulfilled.</summary>
    public Booking Booking { get; private set; }

    /// <summary>Gets the assigned driver identifier.</summary>
    public Guid DriverId { get; private set; }

    /// <summary>Gets the assigned driver.</summary>
    public Driver Driver { get; private set; }

    /// <summary>Gets the assigned vehicle identifier.</summary>
    public Guid VehicleId { get; private set; }

    /// <summary>Gets the assigned vehicle.</summary>
    public Vehicle Vehicle { get; private set; }

    /// <summary>Gets the date on which the assignment occurs.</summary>
    public DateOnly AssignmentDate { get; private set; }

    /// <summary>Gets the scheduled start time.</summary>
    public TimeOnly StartTime { get; private set; }

    /// <summary>Gets the scheduled end time.</summary>
    public TimeOnly EndTime { get; private set; }
}
