using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents the domain profile associated with an authenticated TourFlow user.</summary>
public sealed class ApplicationUser : Entity
{
    private readonly List<RefreshToken> _refreshTokens = [];

    public ApplicationUser(string email, UserRole role)
    {
        Email = RequireValue(email, nameof(email));
        Role = role;
    }

    /// <summary>Gets the user's unique email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the user's assigned business role.</summary>
    public UserRole Role { get; private set; }

    /// <summary>Gets the optional customer profile.</summary>
    public Customer? Customer { get; private set; }

    /// <summary>Gets the optional driver profile.</summary>
    public Driver? Driver { get; private set; }

    /// <summary>Gets the user's refresh-token history.</summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>Updates the user's email address.</summary>
    public void ChangeEmail(string email)
    {
        Email = RequireValue(email, nameof(email));
        Touch();
    }

    /// <summary>Changes the user's assigned business role.</summary>
    public void ChangeRole(UserRole role)
    {
        Role = role;
        Touch();
    }

    internal void AttachCustomer(Customer customer)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
    }

    internal void AttachDriver(Driver driver)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    internal void AddRefreshToken(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _refreshTokens.Add(refreshToken);
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
