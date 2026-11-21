using TourFlow.Domain.Common;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a destination to which tours can belong.</summary>
public sealed class Destination : Entity
{
    private readonly List<Tour> _tours = [];

    public Destination(string name, string country, string? description = null, string? imageUrl = null)
    {
        Name = RequireValue(name, nameof(name));
        Country = RequireValue(country, nameof(country));
        Description = description?.Trim();
        ImageUrl = imageUrl?.Trim();
        IsActive = true;
    }

    /// <summary>Gets the destination name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the optional destination description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the destination country.</summary>
    public string Country { get; private set; }

    /// <summary>Gets the optional image URL.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>Gets a value indicating whether the destination is available for new tours.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the tours associated with this destination.</summary>
    public IReadOnlyCollection<Tour> Tours => _tours.AsReadOnly();

    /// <summary>Updates destination details.</summary>
    public void Update(string name, string country, string? description, string? imageUrl)
    {
        Name = RequireValue(name, nameof(name));
        Country = RequireValue(country, nameof(country));
        Description = description?.Trim();
        ImageUrl = imageUrl?.Trim();
        Touch();
    }

    /// <summary>Changes whether the destination is available for new tours.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    internal void AddTour(Tour tour)
    {
        ArgumentNullException.ThrowIfNull(tour);
        _tours.Add(tour);
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
