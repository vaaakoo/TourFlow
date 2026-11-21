using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a vehicle available for transfer assignments.</summary>
public sealed class Vehicle : Entity
{
    private readonly List<BookingAssignment> _assignments = [];

    private Vehicle()
    {
        Name = null!;
        RegistrationNumber = null!;
    }

    public Vehicle(
        string name,
        string registrationNumber,
        VehicleType vehicleType,
        int passengerCapacity,
        string? description = null)
    {
        Name = RequireValue(name, nameof(name));
        RegistrationNumber = RequireValue(registrationNumber, nameof(registrationNumber));
        VehicleType = vehicleType;
        SetPassengerCapacity(passengerCapacity);
        Description = description?.Trim();
        IsActive = true;
    }

    /// <summary>Gets the vehicle's display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the unique vehicle registration number.</summary>
    public string RegistrationNumber { get; private set; }

    /// <summary>Gets the vehicle type.</summary>
    public VehicleType VehicleType { get; private set; }

    /// <summary>Gets the maximum passenger capacity.</summary>
    public int PassengerCapacity { get; private set; }

    /// <summary>Gets the optional vehicle description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets a value indicating whether the vehicle can receive new assignments.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the vehicle's assignments.</summary>
    public IReadOnlyCollection<BookingAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>Updates vehicle operational details.</summary>
    public void Update(string name, VehicleType vehicleType, int passengerCapacity, string? description)
    {
        Name = RequireValue(name, nameof(name));
        VehicleType = vehicleType;
        SetPassengerCapacity(passengerCapacity);
        Description = description?.Trim();
        Touch();
    }

    /// <summary>Changes whether the vehicle can receive new assignments.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    public void ChangeRegistration(string registrationNumber)
    {
        RegistrationNumber = RequireValue(registrationNumber, nameof(registrationNumber));
        Touch();
    }

    internal void EnsureAssignableFor(int passengerCount)
    {
        if (!IsActive)
        {
            throw new DomainException("Inactive vehicles cannot be assigned to bookings.");
        }

        if (passengerCount > PassengerCapacity)
        {
            throw new DomainException("Booking passenger count exceeds vehicle capacity.");
        }
    }

    internal void AddAssignment(BookingAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _assignments.Add(assignment);
    }

    private void SetPassengerCapacity(int passengerCapacity)
    {
        if (passengerCapacity <= 0)
        {
            throw new DomainException("Passenger capacity must be greater than zero.");
        }

        PassengerCapacity = passengerCapacity;
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
