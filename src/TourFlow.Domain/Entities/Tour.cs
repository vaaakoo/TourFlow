using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a bookable tour.</summary>
public sealed class Tour : Entity
{
    private readonly List<Booking> _bookings = [];

    private Tour()
    {
        Name = null!;
        Description = null!;
        Destination = null!;
    }

    public Tour(
        Destination destination,
        string name,
        string description,
        TimeSpan duration,
        decimal price,
        int maxPassengers,
        TourType tourType,
        string? imageUrl = null)
    {
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));
        DestinationId = destination.Id;
        Name = RequireValue(name, nameof(name));
        Description = RequireValue(description, nameof(description));
        SetDuration(duration);
        SetPrice(price);
        SetMaxPassengers(maxPassengers);
        TourType = tourType;
        ImageUrl = imageUrl?.Trim();
        IsActive = true;
        destination.AddTour(this);
    }

    /// <summary>Gets the tour name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the tour description.</summary>
    public string Description { get; private set; }

    /// <summary>Gets the optional image URL.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>Gets the tour duration.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Gets the price per passenger.</summary>
    public decimal Price { get; private set; }

    /// <summary>Gets the maximum allowed passenger count.</summary>
    public int MaxPassengers { get; private set; }

    /// <summary>Gets the destination identifier.</summary>
    public Guid DestinationId { get; private set; }

    /// <summary>Gets the destination.</summary>
    public Destination Destination { get; private set; }

    /// <summary>Gets the tour category.</summary>
    public TourType TourType { get; private set; }

    /// <summary>Gets a value indicating whether the tour is bookable.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets bookings made for this tour.</summary>
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    /// <summary>Updates tour details.</summary>
    public void Update(
        string name,
        string description,
        TimeSpan duration,
        decimal price,
        int maxPassengers,
        TourType tourType,
        string? imageUrl)
    {
        Name = RequireValue(name, nameof(name));
        Description = RequireValue(description, nameof(description));
        SetDuration(duration);
        SetPrice(price);
        SetMaxPassengers(maxPassengers);
        TourType = tourType;
        ImageUrl = imageUrl?.Trim();
        Touch();
    }

    /// <summary>Changes whether the tour is available for new bookings.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    public void MoveTo(Destination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (DestinationId == destination.Id) return;
        Destination?.RemoveTour(this);
        Destination = destination;
        DestinationId = destination.Id;
        destination.AddTour(this);
        Touch();
    }

    internal void EnsureCanAccept(int passengerCount)
    {
        if (!IsActive)
        {
            throw new DomainException("Inactive tours cannot receive new bookings.");
        }

        if (passengerCount > MaxPassengers)
        {
            throw new DomainException("Booking passenger count exceeds the tour capacity.");
        }
    }

    internal void AddBooking(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);
        _bookings.Add(booking);
    }

    private void SetDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException("Tour duration must be greater than zero.");
        }

        Duration = duration;
    }

    private void SetPrice(decimal price)
    {
        if (price < 0)
        {
            throw new DomainException("Tour price cannot be negative.");
        }

        Price = price;
    }

    private void SetMaxPassengers(int maxPassengers)
    {
        if (maxPassengers <= 0)
        {
            throw new DomainException("Maximum passengers must be greater than zero.");
        }

        MaxPassengers = maxPassengers;
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
