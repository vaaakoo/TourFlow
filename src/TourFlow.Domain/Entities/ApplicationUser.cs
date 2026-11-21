using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace TourFlow.Domain.Entities;

/// <summary>Represents the domain profile associated with an authenticated TourFlow user.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private ApplicationUser()
    {
        Email = null!;
        UserName = null!;
        NormalizedEmail = null!;
        NormalizedUserName = null!;
    }

    public ApplicationUser(string email, UserRole role)
    {
        Email = RequireValue(email, nameof(email));
        UserName = Email;
        NormalizedEmail = Email.ToUpperInvariant();
        NormalizedUserName = NormalizedEmail;
        Id = Guid.NewGuid();
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Gets the UTC time at which the domain user profile was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the UTC time at which the domain user profile was last modified.</summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Gets the user's assigned business role.</summary>
    public UserRole Role { get; private set; }

    public bool IsDisabled { get; private set; }

    public string? StaffFirstName { get; private set; }

    public string? StaffLastName { get; private set; }

    public void UpdateStaffName(string firstName, string lastName)
    {
        StaffFirstName = RequireValue(firstName, nameof(firstName));
        StaffLastName = RequireValue(lastName, nameof(lastName));
        Touch();
    }

    public void SetDisabled(bool disabled)
    {
        IsDisabled = disabled;
        Touch();
    }

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
        UserName = Email;
        NormalizedEmail = Email.ToUpperInvariant();
        NormalizedUserName = NormalizedEmail;
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

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
