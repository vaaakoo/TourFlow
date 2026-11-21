using Microsoft.EntityFrameworkCore;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;

namespace TourFlow.Infrastructure.Persistence.Seeding;

/// <summary>Provides deterministic, non-secret development records for the initial schema.</summary>
internal static class DevelopmentSeedData
{
    private static readonly DateTime SeededAt = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

    private static readonly Guid AdministratorUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CustomerUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DriverUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid CustomerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DriverId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid VehicleId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid DestinationId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TourId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid BookingId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid AssignmentId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid HistoryId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid PaymentId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    internal static void SeedDevelopmentData(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().HasData(
            new { Id = AdministratorUserId, Email = "admin@tourflow.local", Role = UserRole.Administrator, CreatedAt = SeededAt, UpdatedAt = (DateTime?)null },
            new { Id = CustomerUserId, Email = "nino.beridze@tourflow.local", Role = UserRole.Customer, CreatedAt = SeededAt, UpdatedAt = (DateTime?)null },
            new { Id = DriverUserId, Email = "giorgi.kapanadze@tourflow.local", Role = UserRole.Driver, CreatedAt = SeededAt, UpdatedAt = (DateTime?)null });

        modelBuilder.Entity<Customer>().HasData(new
        {
            Id = CustomerId,
            UserId = CustomerUserId,
            FirstName = "Nino",
            LastName = "Beridze",
            PhoneNumber = "+995555010101",
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<Driver>().HasData(new
        {
            Id = DriverId,
            UserId = DriverUserId,
            FirstName = "Giorgi",
            LastName = "Kapanadze",
            PhoneNumber = "+995555020202",
            LicenseNumber = "GE-TBL-DR-1001",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<Vehicle>().HasData(new
        {
            Id = VehicleId,
            Name = "Mercedes Sprinter",
            RegistrationNumber = "TF-001-TF",
            VehicleType = VehicleType.Van,
            PassengerCapacity = 16,
            Description = "Comfortable airport and group-transfer van.",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<Destination>().HasData(new
        {
            Id = DestinationId,
            Name = "Tbilisi",
            Country = "Georgia",
            Description = "Historic capital city with old-town, cultural, and culinary experiences.",
            ImageUrl = "https://images.example.test/tbilisi.jpg",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<Tour>().HasData(new
        {
            Id = TourId,
            DestinationId,
            Name = "Tbilisi Old Town Discovery",
            Description = "A guided walking and driving introduction to Tbilisi's historic landmarks.",
            ImageUrl = "https://images.example.test/tbilisi-old-town.jpg",
            Duration = TimeSpan.FromHours(4),
            Price = 85.00m,
            MaxPassengers = 16,
            TourType = TourType.Sightseeing,
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<Booking>().HasData(new
        {
            Id = BookingId,
            BookingNumber = "TF-2026-0001",
            CustomerId,
            TourId,
            BookingType = BookingType.Tour,
            BookingDate = new DateOnly(2026, 12, 5),
            StartTime = new TimeOnly(10, 0),
            PassengerCount = 2,
            PickupLocation = "Freedom Square, Tbilisi",
            DropoffLocation = "Old Town, Tbilisi",
            SpecialRequests = "English-speaking guide requested.",
            TotalPrice = 170.00m,
            Status = BookingStatus.Assigned,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });

        modelBuilder.Entity<BookingAssignment>().HasData(new
        {
            Id = AssignmentId,
            BookingId,
            DriverId,
            VehicleId,
            AssignmentDate = new DateOnly(2026, 12, 5),
            StartTime = new TimeOnly(9, 45),
            EndTime = new TimeOnly(14, 30),
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<BookingStatusHistory>().HasData(new
        {
            Id = HistoryId,
            BookingId,
            PreviousStatus = BookingStatus.Confirmed,
            NewStatus = BookingStatus.Assigned,
            ChangedByUserId = AdministratorUserId,
            ChangedAt = SeededAt,
            Comment = "Driver and vehicle assigned for the tour.",
            CreatedAt = SeededAt,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<Payment>().HasData(new
        {
            Id = PaymentId,
            BookingId,
            Amount = 170.00m,
            PaymentMethod = PaymentMethod.Card,
            Status = PaymentStatus.Paid,
            TransactionReference = "DEMO-TF-2026-0001",
            PaidAt = SeededAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });
    }
}
