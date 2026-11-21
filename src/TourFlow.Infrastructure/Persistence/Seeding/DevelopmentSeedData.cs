using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TourFlow.Domain.Common;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;

namespace TourFlow.Infrastructure.Persistence.Seeding;

internal static class DevelopmentSeedData
{
    private static readonly Guid BookingId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly DateTime SeededAt = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

    internal static void Configure(DbContextOptionsBuilder options)
    {
        options.UseSeeding((context, _) =>
        {
            var db = (TourFlowDbContext)context;
            if (!db.Bookings.Any(booking => booking.Id == BookingId))
            {
                AddDemo(db);
                db.SaveChanges();
            }
        });
        options.UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            var db = (TourFlowDbContext)context;
            if (!await db.Bookings.AnyAsync(booking => booking.Id == BookingId, cancellationToken))
            {
                AddDemo(db);
                await db.SaveChangesAsync(cancellationToken);
            }
        });
    }

    private static void AddDemo(TourFlowDbContext db)
    {
        var customerUser = DemoUser(db, "nino.beridze@example.test", UserRole.Customer, "00000000-0000-0000-0000-000000000002");
        var driverUser = DemoUser(db, "giorgi.kapanadze@example.test", UserRole.Driver, "00000000-0000-0000-0000-000000000003");
        var customer = SetIdentity(db, new Customer(customerUser, "Nino", "Beridze", "+995555010101"), "10000000-0000-0000-0000-000000000001");
        var driver = SetIdentity(db, new Driver(driverUser, "Giorgi", "Kapanadze", "+995555020202", "GE-TBL-DR-1001"), "20000000-0000-0000-0000-000000000001");
        var vehicle = SetIdentity(db, new Vehicle("Mercedes Sprinter", "TF-001-TF", VehicleType.Van, 16), "30000000-0000-0000-0000-000000000001");
        var destination = SetIdentity(db, new Destination("Tbilisi", "Georgia"), "40000000-0000-0000-0000-000000000001");
        var tour = SetIdentity(db, new Tour(destination, "Tbilisi Old Town Discovery", "Guided historic landmarks tour.", TimeSpan.FromHours(4), 85m, 16, TourType.Sightseeing), "50000000-0000-0000-0000-000000000001");
        var serviceDate = new DateOnly(2026, 12, 5);
        var booking = SetIdentity(db, Booking.CreateTourBooking(customer, tour, "TF-2026-0001", serviceDate, new TimeOnly(10, 0), 2, "Freedom Square, Tbilisi"), BookingId.ToString());
        booking.TransitionTo(BookingStatus.Confirmed, customerUser.Id);
        var assignment = new BookingAssignment(booking, driver, vehicle, serviceDate, new TimeOnly(9, 45), new TimeOnly(14, 30));
        SetIdentity(db, assignment, "70000000-0000-0000-0000-000000000001");
        booking.TransitionTo(BookingStatus.Assigned, driverUser.Id, "Driver and vehicle assigned.");
        var historyIndex = 0;
        foreach (var history in booking.StatusHistory)
        {
            SetIdentity(db, history, historyIndex++ == 0
                ? "80000000-0000-0000-0000-000000000001"
                : "80000000-0000-0000-0000-000000000002");
            db.Entry(history).Property(record => record.ChangedAt).CurrentValue = SeededAt;
        }

        var payment = SetIdentity(db, new Payment(booking, 170m, PaymentMethod.Card), "90000000-0000-0000-0000-000000000001");
        payment.MarkPaid("DEMO-TF-2026-0001", SeededAt);
        db.Entry(booking).Property(record => record.UpdatedAt).CurrentValue = SeededAt;
        db.Bookings.Add(booking);
        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = customerUser.Id, RoleId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
            new IdentityUserRole<Guid> { UserId = driverUser.Id, RoleId = Guid.Parse("10000000-0000-0000-0000-000000000004") });
    }

    private static T SetIdentity<T>(TourFlowDbContext db, T entity, string id) where T : Entity
    {
        var entry = db.Entry(entity);
        entry.Property(record => record.Id).CurrentValue = Guid.Parse(id);
        entry.Property(record => record.CreatedAt).CurrentValue = SeededAt;
        return entity;
    }

    private static ApplicationUser DemoUser(TourFlowDbContext db, string email, UserRole role, string id)
    {
        var user = new ApplicationUser(email, role) { Id = Guid.Parse(id), SecurityStamp = id, ConcurrencyStamp = id };
        db.Entry(user).Property(record => record.CreatedAt).CurrentValue = SeededAt;
        return user;
    }
}
