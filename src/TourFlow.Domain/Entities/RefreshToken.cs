using TourFlow.Domain.Common;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a persisted refresh-token record. Token issuance and rotation are implemented later.</summary>
public sealed class RefreshToken : Entity
{
    public RefreshToken(ApplicationUser user, string tokenHash, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A refresh token hash is required.");
        }

        if (expiresAt.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Refresh token expiration must be UTC.");
        }

        if (expiresAt <= DateTime.UtcNow)
        {
            throw new DomainException("Refresh token expiration must be in the future.");
        }

        User = user ?? throw new ArgumentNullException(nameof(user));
        UserId = user.Id;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        user.AddRefreshToken(this);
    }

    /// <summary>Gets the owning user identifier.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the user that owns the token.</summary>
    public ApplicationUser User { get; private set; }

    /// <summary>Gets the stored hash of the refresh token.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Gets the UTC expiration time.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Gets the optional UTC revocation time.</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>Gets a value indicating whether the token is currently usable.</summary>
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    /// <summary>Revokes the token at the supplied UTC time.</summary>
    public void Revoke(DateTime? revokedAt = null)
    {
        if (RevokedAt is not null)
        {
            throw new DomainException("Refresh token has already been revoked.");
        }

        var timestamp = revokedAt ?? DateTime.UtcNow;
        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Refresh token revocation time must be UTC.");
        }

        RevokedAt = timestamp;
        Touch(timestamp);
    }
}
