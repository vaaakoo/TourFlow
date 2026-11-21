using TourFlow.Domain.Common;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a driver profile and its transfer assignments.</summary>
public sealed class Driver : Entity
{
    private readonly List<BookingAssignment> _assignments = [];

    public Driver(
        ApplicationUser user,
        string firstName,
        string lastName,
        string phoneNumber,
        string licenseNumber)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
        UserId = user.Id;
        FirstName = RequireValue(firstName, nameof(firstName));
        LastName = RequireValue(lastName, nameof(lastName));
        PhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        LicenseNumber = RequireValue(licenseNumber, nameof(licenseNumber));
        IsActive = true;
        user.AttachDriver(this);
    }

    /// <summary>Gets the authenticated user identifier associated with this profile.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the authenticated user profile.</summary>
    public ApplicationUser User { get; private set; }

    /// <summary>Gets the driver's first name.</summary>
    public string FirstName { get; private set; }

    /// <summary>Gets the driver's last name.</summary>
    public string LastName { get; private set; }

    /// <summary>Gets the driver's contact phone number.</summary>
    public string PhoneNumber { get; private set; }

    /// <summary>Gets the driver's license number.</summary>
    public string LicenseNumber { get; private set; }

    /// <summary>Gets a value indicating whether the driver can receive new assignments.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the driver's assignments.</summary>
    public IReadOnlyCollection<BookingAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>Updates the driver's contact details.</summary>
    public void UpdateProfile(string firstName, string lastName, string phoneNumber, string licenseNumber)
    {
        FirstName = RequireValue(firstName, nameof(firstName));
        LastName = RequireValue(lastName, nameof(lastName));
        PhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        LicenseNumber = RequireValue(licenseNumber, nameof(licenseNumber));
        Touch();
    }

    /// <summary>Changes whether the driver can receive new assignments.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    internal void EnsureAssignable()
    {
        if (!IsActive)
        {
            throw new DomainException("Inactive drivers cannot be assigned to bookings.");
        }
    }

    internal void AddAssignment(BookingAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _assignments.Add(assignment);
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
