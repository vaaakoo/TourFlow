using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourFlow.Domain.Entities;

namespace TourFlow.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.CreatedAt).HasColumnType("datetime2").HasConversion(UtcDateTimeConverter.Instance);
        builder.Property(user => user.UpdatedAt).HasColumnType("datetime2").HasConversion(UtcDateTimeConverter.NullableInstance);
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.UserName).IsRequired();
        builder.Property(user => user.NormalizedEmail).IsRequired();
        builder.Property(user => user.NormalizedUserName).IsRequired();
        builder.Property(user => user.StaffFirstName).HasMaxLength(100);
        builder.Property(user => user.StaffLastName).HasMaxLength(100);
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasOne(user => user.Customer).WithOne(customer => customer.User)
            .HasForeignKey<Customer>(customer => customer.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(user => user.Driver).WithOne(driver => driver.User)
            .HasForeignKey<Driver>(driver => driver.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(user => user.RefreshTokens).WithOne(token => token.User)
            .HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.ConfigureEntity();
        builder.Property(customer => customer.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(customer => customer.LastName).HasMaxLength(100).IsRequired();
        builder.Property(customer => customer.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(customer => customer.UserId).IsUnique();
        builder.HasIndex(customer => new { customer.LastName, customer.FirstName });
        builder.HasMany(customer => customer.Bookings).WithOne(booking => booking.Customer)
            .HasForeignKey(booking => booking.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");
        builder.ConfigureEntity();
        builder.Property(driver => driver.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(driver => driver.LastName).HasMaxLength(100).IsRequired();
        builder.Property(driver => driver.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(driver => driver.LicenseNumber).HasMaxLength(64).IsRequired();
        builder.HasIndex(driver => driver.UserId).IsUnique();
        builder.HasIndex(driver => driver.LicenseNumber).IsUnique();
        builder.HasIndex(driver => new { driver.IsActive, driver.LastName, driver.FirstName });
        builder.HasMany(driver => driver.Assignments).WithOne(assignment => assignment.Driver)
            .HasForeignKey(assignment => assignment.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.ConfigureEntity();
        builder.Property(vehicle => vehicle.Name).HasMaxLength(120).IsRequired();
        builder.Property(vehicle => vehicle.RegistrationNumber).HasMaxLength(32).IsRequired();
        builder.Property(vehicle => vehicle.VehicleType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(vehicle => vehicle.Description).HasMaxLength(1000);
        builder.HasIndex(vehicle => vehicle.RegistrationNumber).IsUnique();
        builder.HasIndex(vehicle => new { vehicle.IsActive, vehicle.VehicleType });
        builder.HasMany(vehicle => vehicle.Assignments).WithOne(assignment => assignment.Vehicle)
            .HasForeignKey(assignment => assignment.VehicleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> builder)
    {
        builder.ToTable("Destinations");
        builder.ConfigureEntity();
        builder.Property(destination => destination.Name).HasMaxLength(160).IsRequired();
        builder.Property(destination => destination.Country).HasMaxLength(100).IsRequired();
        builder.Property(destination => destination.Description).HasMaxLength(2000);
        builder.Property(destination => destination.ImageUrl).HasMaxLength(2048);
        builder.HasIndex(destination => new { destination.Country, destination.Name }).IsUnique();
        builder.HasIndex(destination => new { destination.IsActive, destination.Name });
        builder.HasMany(destination => destination.Tours).WithOne(tour => tour.Destination)
            .HasForeignKey(tour => tour.DestinationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("Tours");
        builder.ConfigureEntity();
        builder.Property(tour => tour.Name).HasMaxLength(200).IsRequired();
        builder.Property(tour => tour.Description).HasMaxLength(4000).IsRequired();
        builder.Property(tour => tour.ImageUrl).HasMaxLength(2048);
        builder.Property(tour => tour.Duration).HasConversion(value => value.Ticks, value => TimeSpan.FromTicks(value)).HasColumnType("bigint");
        builder.Property(tour => tour.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(tour => tour.TourType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(tour => new { tour.DestinationId, tour.IsActive, tour.TourType });
        builder.HasIndex(tour => tour.Name);
        builder.HasMany(tour => tour.Bookings).WithOne(booking => booking.Tour)
            .HasForeignKey(booking => booking.TourId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.ConfigureEntity();
        builder.Property(booking => booking.BookingNumber).HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.BookingType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(booking => booking.BookingDate).HasColumnType("date").IsRequired();
        builder.Property(booking => booking.StartTime).HasColumnType("time(0)").IsRequired();
        builder.Property(booking => booking.PickupLocation).HasMaxLength(500).IsRequired();
        builder.Property(booking => booking.DropoffLocation).HasMaxLength(500);
        builder.Property(booking => booking.SpecialRequests).HasMaxLength(2000);
        builder.Property(booking => booking.TotalPrice).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(booking => booking.BookingNumber).IsUnique();
        builder.HasIndex(booking => new { booking.BookingDate, booking.Status });
        builder.HasIndex(booking => booking.CustomerId);
        builder.HasIndex(booking => booking.TourId);
        builder.HasMany(booking => booking.Assignments).WithOne(assignment => assignment.Booking)
            .HasForeignKey(assignment => assignment.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(booking => booking.StatusHistory).WithOne(history => history.Booking)
            .HasForeignKey(history => history.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(booking => booking.Payments).WithOne(payment => payment.Booking)
            .HasForeignKey(payment => payment.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BookingAssignmentConfiguration : IEntityTypeConfiguration<BookingAssignment>
{
    public void Configure(EntityTypeBuilder<BookingAssignment> builder)
    {
        builder.ToTable("BookingAssignments");
        builder.ConfigureEntity();
        builder.Property(assignment => assignment.AssignmentDate).HasColumnType("date").IsRequired();
        builder.Property(assignment => assignment.StartTime).HasColumnType("time(0)").IsRequired();
        builder.Property(assignment => assignment.EndTime).HasColumnType("time(0)").IsRequired();
        builder.HasIndex(assignment => assignment.BookingId);
        builder.HasIndex(assignment => new { assignment.DriverId, assignment.AssignmentDate, assignment.StartTime, assignment.EndTime });
        builder.HasIndex(assignment => new { assignment.VehicleId, assignment.AssignmentDate, assignment.StartTime, assignment.EndTime });
    }
}

internal sealed class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("BookingStatusHistories");
        builder.ConfigureEntity();
        builder.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(history => history.NewStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(history => history.ChangedAt).HasColumnType("datetime2").HasConversion(UtcDateTimeConverter.Instance).IsRequired();
        builder.Property(history => history.Comment).HasMaxLength(1000);
        builder.HasIndex(history => new { history.BookingId, history.ChangedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.ConfigureEntity();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.PaymentMethod).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(payment => payment.TransactionReference).HasMaxLength(128);
        builder.Property(payment => payment.PaidAt).HasColumnType("datetime2").HasConversion(UtcDateTimeConverter.NullableInstance);
        builder.HasIndex(payment => payment.BookingId);
        builder.HasIndex(payment => payment.TransactionReference).IsUnique().HasFilter("[TransactionReference] IS NOT NULL");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.ConfigureEntity();
        builder.Property(token => token.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnType("datetime2").HasConversion(UtcDateTimeConverter.Instance).IsRequired();
        builder.Property(token => token.RevokedAt).HasColumnType("datetime2").HasConversion(UtcDateTimeConverter.NullableInstance);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.FamilyId, token.RevokedAt });
        builder.HasIndex(token => new { token.UserId, token.ExpiresAt, token.RevokedAt });
    }
}
