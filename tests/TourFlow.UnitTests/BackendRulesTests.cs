using FluentValidation.TestHelper;
using TourFlow.Application.Backend;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;
using Xunit;

namespace TourFlow.UnitTests;

public sealed class BackendRulesTests
{
    [Fact]
    public void TourBookingCalculatesPriceAndEnforcesCapacity()
    {
        var (_, customer, tour, _, _) = Graph();
        var booking = Booking.CreateTourBooking(customer, tour, "TF-TEST-1", new(2026, 12, 10), new(10, 0), 3, "Hotel");
        Assert.Equal(150m, booking.TotalPrice);
        Assert.Throws<DomainException>(() => Booking.CreateTourBooking(customer, tour, "TF-TEST-2", new(2026, 12, 10), new(10, 0), 9, "Hotel"));
    }

    [Fact]
    public void BookingRejectsInvalidTransitionAndCompletedCancellation()
    {
        var (user, customer, tour, driver, vehicle) = Graph();
        var booking = Booking.CreateTourBooking(customer, tour, "TF-TEST-3", new(2026, 12, 10), new(10, 0), 2, "Hotel");
        Assert.Throws<InvalidBookingStatusTransitionException>(() => booking.TransitionTo(BookingStatus.Completed, user.Id));
        booking.TransitionTo(BookingStatus.Confirmed, user.Id);
        _ = new BookingAssignment(booking, driver, vehicle, booking.BookingDate, booking.StartTime, new(12, 0));
        booking.TransitionTo(BookingStatus.Assigned, user.Id); booking.TransitionTo(BookingStatus.InProgress, user.Id);
        booking.TransitionTo(BookingStatus.Completed, user.Id);
        Assert.Throws<InvalidBookingStatusTransitionException>(() => booking.Cancel(user.Id));
    }

    [Fact]
    public void AssignmentCancellationRetainsHistory()
    {
        var (user, customer, tour, driver, vehicle) = Graph();
        var booking = Booking.CreateTourBooking(customer, tour, "TF-TEST-4", new(2026, 12, 10), new(10, 0), 2, "Hotel");
        booking.TransitionTo(BookingStatus.Confirmed, user.Id);
        var assignment = new BookingAssignment(booking, driver, vehicle, booking.BookingDate, booking.StartTime, new(12, 0));
        assignment.Cancel();
        Assert.False(assignment.IsActive); Assert.NotNull(assignment.CancelledAt); Assert.Single(booking.Assignments);
    }

    [Fact]
    public void PaymentLifecycleAndCurrencyAreValidated()
    {
        var (_, customer, tour, _, _) = Graph();
        var booking = Booking.CreateTourBooking(customer, tour, "TF-TEST-5", new(2026, 12, 10), new(10, 0), 2, "Hotel");
        Assert.Throws<DomainException>(() => new Payment(booking, 20m, PaymentMethod.Card, "US"));
        var payment = new Payment(booking, 20m, PaymentMethod.Card, "usd"); payment.MarkPaid("REF-1"); payment.Refund();
        Assert.Equal("USD", payment.Currency); Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Throws<DomainException>(() => payment.MarkFailed());
    }

    [Fact]
    public void BackendValidatorsRejectUnsafeInputs()
    {
        var booking = new BookingInputValidator().TestValidate(new BookingInput(BookingType.Tour, null, null, new(2026, 12, 10), new(10, 0), 0, "", null, null));
        Assert.False(booking.IsValid);
        var assignment = new AssignmentInputValidator().TestValidate(new AssignmentInput(Guid.Empty, Guid.Empty, new(2026, 12, 10), new(12, 0), new(10, 0), ""));
        Assert.False(assignment.IsValid);
        var payment = new PaymentInputValidator().TestValidate(new PaymentInput(Guid.Empty, 0m, (PaymentMethod)999, "dollar"));
        Assert.False(payment.IsValid);
    }

    private static (ApplicationUser User, Customer Customer, Tour Tour, Driver Driver, Vehicle Vehicle) Graph()
    {
        var user = new ApplicationUser("customer@example.test", UserRole.Customer);
        var customer = new Customer(user, "Test", "Customer", "555-0100");
        var driverUser = new ApplicationUser("driver@example.test", UserRole.Driver);
        var driver = new Driver(driverUser, "Test", "Driver", "555-0101", "LIC-1");
        var tour = new Tour(new Destination("Tbilisi", "Georgia"), "City", "Tour", TimeSpan.FromHours(2), 50m, 8, TourType.Cultural);
        return (user, customer, tour, driver, new Vehicle("Van", "REG-1", VehicleType.Van, 8));
    }
}
