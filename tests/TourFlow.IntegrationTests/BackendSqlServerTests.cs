using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TourFlow.Application.Authentication;
using TourFlow.Application.Backend;
using TourFlow.Application.Operations;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Authentication;
using Xunit;

namespace TourFlow.IntegrationTests;

public sealed class BackendSqlServerTests
{
    private const string Password = "Test-only_Backend42!";
    private const string AdminEmail = "backend-admin@example.test";
    private static readonly Guid DriverId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid VehicleId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [SqlServerFact]
    public async Task BookingPriceOwnershipCancellationAndConcurrencyAreEnforced()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp(); await app.InitializeAsync();
        using var customer = Client(app); using var other = Client(app); using var admin = await AdministratorAsync(app);
        await RegisterAsync(customer, "booking-owner@example.test"); await RegisterAsync(other, "booking-other@example.test");
        var tour = Assert.Single((await ReadAsync<PageResult<TourFlow.Application.Operations.Tours.TourDto>>(customer, "/api/tours")).Items);
        var input = new BookingInput(BookingType.Tour, tour.Id, null, new(2027, 1, 10), new(10, 0), 2, "Hotel", null, "Window seat");
        var booking = await CreateBookingAsync(customer, input);
        Assert.Equal(tour.Price * 2, booking.TotalPrice);
        using var isolated = await other.GetAsync($"/api/bookings/{booking.Id}"); Assert.Equal(HttpStatusCode.NotFound, isolated.StatusCode);
        using var tampered = await customer.PutAsJsonAsync($"/api/bookings/{booking.Id}?version={Uri.EscapeDataString(booking.Version)}", input with { PassengerCount = 3 });
        var updated = await BodyAsync<BookingDto>(tampered); Assert.Equal(tour.Price * 3, updated.TotalPrice);
        using var stale = await customer.PutAsJsonAsync($"/api/bookings/{booking.Id}?version={Uri.EscapeDataString(booking.Version)}", input);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var cancelled = await customer.PostAsJsonAsync($"/api/bookings/{booking.Id}/cancel", new { version = updated.Version, comment = "Plans changed" });
        Assert.Equal(BookingStatus.Cancelled, (await BodyAsync<BookingDto>(cancelled)).Status);
        var history = await ReadAsync<IReadOnlyList<BookingStatusDto>>(customer, $"/api/bookings/{booking.Id}/history");
        Assert.Single(history); Assert.Equal(BookingStatus.Cancelled, history[0].NewStatus);
        using var dashboardDenied = await customer.GetAsync("/api/dashboard"); Assert.Equal(HttpStatusCode.Forbidden, dashboardDenied.StatusCode);
        using var adminView = await admin.GetAsync($"/api/bookings/{booking.Id}"); Assert.Equal(HttpStatusCode.OK, adminView.StatusCode);
    }

    [SqlServerFact]
    public async Task AssignmentLifecycleRejectsOverlapsAndPreservesHistory()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp(); await app.InitializeAsync();
        using var customer = Client(app); using var customer2 = Client(app); using var admin = await AdministratorAsync(app);
        await RegisterAsync(customer, "assignment-one@example.test"); await RegisterAsync(customer2, "assignment-two@example.test");
        var date = new DateOnly(2027, 2, 1);
        var first = await CreateAndConfirmTransfer(customer, admin, date, new(9, 0));
        var second = await CreateAndConfirmTransfer(customer2, admin, date, new(9, 30));
        var firstInput = new AssignmentInput(DriverId, VehicleId, date, new(9, 0), new(11, 0), first.Version);
        using var assignedResponse = await admin.PostAsJsonAsync($"/api/bookings/{first.Id}/assign", firstInput);
        var assignment = await BodyAsync<AssignmentDto>(assignedResponse);
        var refreshedSecond = await ReadAsync<BookingDto>(admin, $"/api/bookings/{second.Id}");
        using var overlap = await admin.PostAsJsonAsync($"/api/bookings/{second.Id}/assign",
            new AssignmentInput(DriverId, VehicleId, date, new(9, 30), new(10, 30), refreshedSecond.Version));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        var assignedBooking = await ReadAsync<BookingDto>(admin, $"/api/bookings/{first.Id}");
        using var cancelled = await admin.DeleteAsync($"/api/bookings/{first.Id}/assignments/{assignment.Id}?version={Uri.EscapeDataString(assignedBooking.Version)}");
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        var after = await ReadAsync<BookingDto>(admin, $"/api/bookings/{first.Id}"); Assert.Equal(BookingStatus.Confirmed, after.Status);
        var assignments = await ReadAsync<PageResult<AssignmentDto>>(admin, "/api/assignments?from=2027-02-01&to=2027-02-01");
        Assert.Contains(assignments.Items, x => x.Id == assignment.Id && !x.IsActive && x.CancelledAt != null);
        _ = await ReadAsync<IReadOnlyList<ResourceReportRow>>(admin, "/api/reports/drivers?from=2027-02-01&to=2027-02-01");
        _ = await ReadAsync<IReadOnlyList<ResourceReportRow>>(admin, "/api/reports/vehicles?from=2027-02-01&to=2027-02-01");
        var history = await ReadAsync<IReadOnlyList<BookingStatusDto>>(admin, $"/api/bookings/{first.Id}/history");
        Assert.Equal(new[] { BookingStatus.Confirmed, BookingStatus.Assigned, BookingStatus.Confirmed }, history.Select(x => x.NewStatus));
    }

    [SqlServerFact]
    public async Task PaymentsDashboardAndReportsUsePersistedSqlAggregates()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp(); await app.InitializeAsync();
        using var customer = Client(app); using var admin = await AdministratorAsync(app);
        await RegisterAsync(customer, "payment-owner@example.test");
        var booking = await CreateAndConfirmTransfer(customer, admin, new(2027, 3, 1), new(12, 0));
        using var createdResponse = await admin.PostAsJsonAsync("/api/payments", new PaymentInput(booking.Id, booking.TotalPrice, PaymentMethod.Card, "usd"));
        var payment = await BodyAsync<PaymentDto>(createdResponse); Assert.Equal("USD", payment.Currency);
        using var duplicateAmount = await admin.PostAsJsonAsync("/api/payments", new PaymentInput(booking.Id, 1m, PaymentMethod.Cash, "USD"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateAmount.StatusCode);
        using var paidResponse = await admin.PutAsJsonAsync($"/api/payments/{payment.Id}/status", new PaymentStatusInput(PaymentStatus.Paid, "TX-BACKEND-1"));
        Assert.Equal(PaymentStatus.Paid, (await BodyAsync<PaymentDto>(paidResponse)).Status);
        using var repeat = await admin.PutAsJsonAsync($"/api/payments/{payment.Id}/status", new PaymentStatusInput(PaymentStatus.Paid, "TX-BACKEND-1"));
        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);
        var dashboard = await ReadAsync<DashboardDto>(admin, "/api/dashboard?from=2027-03-01&to=2027-03-01");
        Assert.Equal(1, dashboard.TotalBookings); Assert.Equal(booking.TotalPrice, dashboard.Revenue);
        Assert.NotEmpty(await ReadAsync<IReadOnlyList<BookingReportRow>>(admin, "/api/reports/bookings?from=2027-03-01&to=2027-03-01"));
        Assert.Equal(booking.TotalPrice, Assert.Single(await ReadAsync<IReadOnlyList<RevenueReportRow>>(admin,
            "/api/reports/revenue?from=2027-03-01&to=2027-03-01")).Revenue);
        using var health = await admin.GetAsync("/health"); Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [SqlServerFact]
    public async Task DriverCanSeeOwnAssignmentAndOnlyAdvanceTripStatuses()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp(); await app.InitializeAsync();
        using var customer = Client(app); using var driver = Client(app); using var admin = await AdministratorAsync(app);
        await RegisterAsync(customer, "driver-flow-customer@example.test");
        var driverUser = await RegisterProfileAsync(driver, "driver-flow@example.test");
        using var role = await admin.PutAsJsonAsync($"/api/users/{driverUser.Id}/access", new UserAccessUpdate(UserRole.Driver, false));
        role.EnsureSuccessStatusCode();
        using var profileResponse = await admin.PostAsJsonAsync("/api/drivers", new
        {
            userId = driverUser.Id, firstName = "Flow", lastName = "Driver", phoneNumber = "555-0199",
            licenseNumber = "FLOW-LICENSE-1", isActive = true
        });
        var profile = await BodyAsync<TourFlow.Application.Operations.Drivers.DriverDto>(profileResponse);
        await SignInAsync(driver, driverUser.Email);
        var booking = await CreateAndConfirmTransfer(customer, admin, new(2027, 4, 1), new(8, 0));
        using var assignResponse = await admin.PostAsJsonAsync($"/api/bookings/{booking.Id}/assign",
            new AssignmentInput(profile.Id, VehicleId, booking.BookingDate, new(7, 45), new(9, 0), booking.Version));
        _ = await BodyAsync<AssignmentDto>(assignResponse);
        var assigned = await ReadAsync<BookingDto>(admin, $"/api/bookings/{booking.Id}");
        using var customerComplete = await customer.PutAsJsonAsync($"/api/bookings/{booking.Id}/status",
            new StatusChangeInput(BookingStatus.Completed, null, assigned.Version));
        Assert.Equal(HttpStatusCode.Forbidden, customerComplete.StatusCode);
        Assert.Single((await ReadAsync<PageResult<AssignmentDto>>(driver, "/api/assignments?from=2027-04-01&to=2027-04-01")).Items);
        using var startedResponse = await driver.PutAsJsonAsync($"/api/bookings/{booking.Id}/status",
            new StatusChangeInput(BookingStatus.InProgress, "Passengers collected", assigned.Version));
        var started = await BodyAsync<BookingDto>(startedResponse);
        using var completedResponse = await driver.PutAsJsonAsync($"/api/bookings/{booking.Id}/status",
            new StatusChangeInput(BookingStatus.Completed, "Trip completed", started.Version));
        Assert.Equal(BookingStatus.Completed, (await BodyAsync<BookingDto>(completedResponse)).Status);
    }

    private static async Task<BookingDto> CreateAndConfirmTransfer(HttpClient customer, HttpClient admin, DateOnly date, TimeOnly time)
    {
        var booking = await CreateBookingAsync(customer, new(BookingType.Transfer, null, null, date, time, 2, "Airport", "Hotel", null));
        using var response = await admin.PostAsJsonAsync($"/api/bookings/{booking.Id}/confirm", new { version = booking.Version, comment = "Confirmed" });
        return await BodyAsync<BookingDto>(response);
    }
    private static async Task<BookingDto> CreateBookingAsync(HttpClient client, BookingInput input)
    { using var response = await client.PostAsJsonAsync("/api/bookings", input); return await BodyAsync<BookingDto>(response); }
    private static HttpClient Client(SqlServerAuthenticationTests.SqlServerApp app) => app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
    private static async Task<HttpClient> AdministratorAsync(SqlServerAuthenticationTests.SqlServerApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAdministrator>().CreateAsync(AdminEmail, Password, CancellationToken.None);
        var client = Client(app); await SignInAsync(client, AdminEmail); return client;
    }
    private static async Task RegisterAsync(HttpClient client, string email)
    { _ = await RegisterProfileAsync(client, email); await SignInAsync(client, email); }
    private static async Task<UserProfile> RegisterProfileAsync(HttpClient client, string email)
    { using var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password, "Backend", "Customer", "555-0100")); return await BodyAsync<UserProfile>(response); }
    private static async Task SignInAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        var pair = await BodyAsync<TokenPair>(response); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
    }
    private static async Task<T> ReadAsync<T>(HttpClient client, string path) { using var response = await client.GetAsync(path); return await BodyAsync<T>(response); }
    private static async Task<T> BodyAsync<T>(HttpResponseMessage response)
    { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>())!; }
}
