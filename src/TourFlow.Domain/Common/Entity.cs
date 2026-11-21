namespace TourFlow.Domain.Common;

/// <summary>Base type for domain entities with an identity and UTC audit timestamps.</summary>
public abstract class Entity
{
    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Gets the entity identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the UTC time at which the entity was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the UTC time at which the entity was last modified.</summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Updates the entity audit timestamp.</summary>
    protected void Touch(DateTime? utcNow = null)
    {
        var timestamp = utcNow ?? DateTime.UtcNow;
        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Domain timestamps must be UTC.", nameof(utcNow));
        }

        UpdatedAt = timestamp;
    }
}
