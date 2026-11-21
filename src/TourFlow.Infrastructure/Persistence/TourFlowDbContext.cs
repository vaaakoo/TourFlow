using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TourFlow.Domain.Entities;

namespace TourFlow.Infrastructure.Persistence;

/// <summary>Entity Framework database context for the TourFlow operational model.</summary>
public sealed class TourFlowDbContext(DbContextOptions<TourFlowDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingAssignment> BookingAssignments => Set<BookingAssignment>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(TourFlowDbContext).Assembly);
        builder.Entity<IdentityRole<Guid>>().HasData(
            Role("10000000-0000-0000-0000-000000000001", "ADMINISTRATOR"),
            Role("10000000-0000-0000-0000-000000000002", "MANAGER"),
            Role("10000000-0000-0000-0000-000000000003", "OPERATOR"),
            Role("10000000-0000-0000-0000-000000000004", "DRIVER"),
            Role("10000000-0000-0000-0000-000000000005", "CUSTOMER"));
    }

    private static IdentityRole<Guid> Role(string id, string name) =>
        new(name) { Id = Guid.Parse(id), NormalizedName = name, ConcurrencyStamp = id };
}
