using TourFlow.Domain.Common;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a customer profile and its booking history.</summary>
public sealed class Customer : Entity
{
    private readonly List<Booking> _bookings = [];

    private Customer()
    {
        User = null!;
        FirstName = null!;
        LastName = null!;
        PhoneNumber = null!;
    }

    public Customer(ApplicationUser user, string firstName, string lastName, string phoneNumber)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
        UserId = user.Id;
        FirstName = RequireValue(firstName, nameof(firstName));
        LastName = RequireValue(lastName, nameof(lastName));
        PhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        user.AttachCustomer(this);
    }

    /// <summary>Gets the authenticated user identifier associated with this profile.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the authenticated user profile.</summary>
    public ApplicationUser User { get; private set; }

    /// <summary>Gets the customer's first name.</summary>
    public string FirstName { get; private set; }

    /// <summary>Gets the customer's last name.</summary>
    public string LastName { get; private set; }

    /// <summary>Gets the customer's contact phone number.</summary>
    public string PhoneNumber { get; private set; }

    /// <summary>Gets the contact email from the associated authenticated user without duplicating it.</summary>
    public string Email => User.Email!;

    /// <summary>Gets the customer's bookings.</summary>
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    /// <summary>Updates customer contact details.</summary>
    public void UpdateProfile(string firstName, string lastName, string phoneNumber)
    {
        FirstName = RequireValue(firstName, nameof(firstName));
        LastName = RequireValue(lastName, nameof(lastName));
        PhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        Touch();
    }

    internal void AddBooking(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);
        _bookings.Add(booking);
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
