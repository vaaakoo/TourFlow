using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourFlow.Application.Authentication;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Customers;
using TourFlow.Application.Operations.Destinations;
using TourFlow.Application.Operations.Drivers;
using TourFlow.Application.Operations.Tours;
using TourFlow.Application.Operations.Vehicles;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Infrastructure.Persistence;
using Xunit;

namespace TourFlow.IntegrationTests;

public sealed class OperationsSqlServerTests
{
    private const string Password = "Test-only_Operations42!";
    private const string AdminEmail = "operations-admin@example.test";

    [SqlServerFact]
    public async Task DestinationsEnforceUniquenessVisibilityAndPaging()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp();
        await app.InitializeAsync();
        using var admin = await AdministratorAsync(app);
        using var publicClient = Client(app);
        using var health = await GetAsync(publicClient, "/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var request = new DestinationInput("Kutaisi", "Georgia", "Cultural destination", null, true);
        using var denied = await PostAsync(publicClient, "/api/destinations", request);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var created = await PostAsync(admin, "/api/destinations", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var destination = await BodyAsync<DestinationDto>(created);
        using var duplicate = await PostAsync(admin, "/api/destinations", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var updated = await PutAsync(admin, $"/api/destinations/{destination.Id}", request with { Name = "Kutaisi City" });
        Assert.Equal("Kutaisi City", (await BodyAsync<DestinationDto>(updated)).Name);
        using var search = await GetAsync(publicClient, "/api/destinations?search=Kutaisi&country=Georgia&page=1&pageSize=1&sort=country");
        var results = await BodyAsync<PageResult<DestinationDto>>(search);
        Assert.Equal(destination.Id, Assert.Single(results.Items).Id);
        Assert.Equal(1, results.TotalCount);
        Assert.Equal(1, results.PageSize);
        using var badSort = await GetAsync(admin, "/api/destinations?sort=unsupported");
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        using var badPage = await GetAsync(admin, "/api/destinations?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        using var deleted = await DeleteAsync(admin, $"/api/destinations/{destination.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var hidden = await GetAsync(publicClient, $"/api/destinations/{destination.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var visibleToStaff = await GetAsync(admin, $"/api/destinations/{destination.Id}");
        Assert.False((await BodyAsync<DestinationDto>(visibleToStaff)).IsActive);

        using var operatorClient = Client(app);
        var operatorUser = await RegisterAsync(operatorClient, "destination-operator@example.test");
        using var grantOperator = await PutAsync(admin, $"/api/users/{operatorUser.Id}/access", new UserAccessUpdate(UserRole.Operator, false));
        Assert.Equal(HttpStatusCode.NoContent, grantOperator.StatusCode);
        await SignInAsync(operatorClient, operatorUser.Email);
        Assert.Equal(destination.Id, (await ReadAsync<DestinationDto>(operatorClient, $"/api/destinations/{destination.Id}")).Id);
        using var operatorWrite = await PostAsync(operatorClient, "/api/destinations", request);
        Assert.Equal(HttpStatusCode.Forbidden, operatorWrite.StatusCode);
        using var managerClient = Client(app);
        var managerUser = await RegisterAsync(managerClient, "destination-manager@example.test");
        using var grantManager = await PutAsync(admin, $"/api/users/{managerUser.Id}/access", new UserAccessUpdate(UserRole.Manager, false));
        Assert.Equal(HttpStatusCode.NoContent, grantManager.StatusCode);
        await SignInAsync(managerClient, managerUser.Email);
        using var allowed = await PostAsync(managerClient, "/api/destinations", request with { Name = "Batumi" });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [SqlServerFact]
    public async Task ToursValidateCatalogInputsAndHideInactiveRows()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp();
        await app.InitializeAsync();
        using var admin = await AdministratorAsync(app);
        using var publicClient = Client(app);
        var destination = (await ReadAsync<PageResult<DestinationDto>>(admin, "/api/destinations")).Items.Single();
        var request = new TourInput("Night Market", "Guided visit", null, 90, 35.50m, 8, destination.Id, TourType.Sightseeing, true);
        using var denied = await PostAsync(publicClient, "/api/tours", request);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        foreach (var invalid in new[] { request with { Price = 0m }, request with { MaxPassengers = 0 },
                     request with { TourType = (TourType)999 }, request with { Price = 0.001m } })
        {
            using var response = await PostAsync(admin, "/api/tours", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var missing = await PostAsync(admin, "/api/tours", request with { DestinationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var created = await PostAsync(admin, "/api/tours", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tour = await BodyAsync<TourDto>(created);
        using var updated = await PutAsync(admin, $"/api/tours/{tour.Id}", request with { Price = 42.25m, DurationMinutes = 120 });
        Assert.Equal(120, (await BodyAsync<TourDto>(updated)).DurationMinutes);
        var page = await ReadAsync<PageResult<TourDto>>(publicClient, "/api/tours?search=Market&minPrice=40&maxPrice=45&pageSize=1&sort=duration");
        Assert.Equal(42.25m, Assert.Single(page.Items).Price);
        Assert.Equal(tour.Id, page.Items[0].Id);
        using var deactivated = await DeleteAsync(admin, $"/api/tours/{tour.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        using var hidden = await GetAsync(publicClient, $"/api/tours/{tour.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var destinationDisabled = await DeleteAsync(admin, $"/api/destinations/{destination.Id}");
        Assert.Equal(HttpStatusCode.NoContent, destinationDisabled.StatusCode);
        using var refused = await PostAsync(admin, "/api/tours", request);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Empty((await ReadAsync<PageResult<TourDto>>(publicClient, "/api/tours")).Items);
    }

    [SqlServerFact]
    public async Task CustomerEndpointsIsolateProfilesAndKeepIdentityEmail()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp();
        await app.InitializeAsync();
        using var admin = await AdministratorAsync(app);
        using var publicClient = Client(app);
        using var first = Client(app);
        using var second = Client(app);
        var firstUser = await RegisterAsync(first, "first-customer@example.test");
        await RegisterAsync(second, "second-customer@example.test");
        var profile = await ReadAsync<CustomerDto>(first, "/api/customers/me");
        Assert.Equal(firstUser.Id, profile.UserId);
        var other = await ReadAsync<CustomerDto>(second, "/api/customers/me");
        Assert.NotEqual(profile.Id, other.Id);
        using var denied = await GetAsync(first, $"/api/customers/{other.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var anonymous = await GetAsync(publicClient, "/api/customers/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var updated = await PutAsync(admin, $"/api/customers/{profile.Id}", new CustomerInput("Updated", "Customer", "555-0123"));
        Assert.Equal(firstUser.Email, (await BodyAsync<CustomerDto>(updated)).Email);
        var authProfile = await ReadAsync<UserProfile>(first, "/api/auth/me");
        Assert.Equal("555-0123", authProfile.PhoneNumber);
        Assert.Equal(firstUser.Email, authProfile.Email);
        var page = await ReadAsync<PageResult<CustomerDto>>(admin, "/api/customers?search=first-customer&sort=email");
        Assert.Equal(profile.Id, Assert.Single(page.Items).Id);
    }

    [SqlServerFact]
    public async Task VehiclesEnforceRegistrationAndProtectAssignments()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp();
        await app.InitializeAsync();
        using var admin = await AdministratorAsync(app);
        using var noAccess = Client(app);
        await RegisterAsync(noAccess, "vehicle-customer@example.test");
        using var denied = await GetAsync(noAccess, "/api/vehicles");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var request = new VehicleInput("Airport Van", "ops-001", VehicleType.Van, 10, "Airport transfers", true);
        using var invalid = await PostAsync(admin, "/api/vehicles", request with { PassengerCapacity = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var requests = await Task.WhenAll(PostAsync(admin, "/api/vehicles", request),
            PostAsync(admin, "/api/vehicles", request with { RegistrationNumber = "OPS-001" }));
        using var first = requests[0];
        using var second = requests[1];
        var vehicle = await BodyAsync<VehicleDto>(Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Created));
        Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Conflict);
        using var updated = await PutAsync(admin, $"/api/vehicles/{vehicle.Id}", request with { PassengerCapacity = 12 });
        Assert.Equal(12, (await BodyAsync<VehicleDto>(updated)).PassengerCapacity);
        var list = await ReadAsync<PageResult<VehicleDto>>(admin, "/api/vehicles?search=ops-001&minCapacity=11&sort=capacity");
        Assert.Equal(vehicle.Id, Assert.Single(list.Items).Id);

        var assignedId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TourFlowDbContext>();
        var upcoming = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await db.BookingAssignments.Where(assignment => assignment.VehicleId == assignedId)
            .ExecuteUpdateAsync(update => update.SetProperty(assignment => assignment.AssignmentDate, upcoming));
        using var reduction = await PutAsync(admin, $"/api/vehicles/{assignedId}", request with { PassengerCapacity = 1, RegistrationNumber = "TF-001-TF" });
        Assert.Equal(HttpStatusCode.Conflict, reduction.StatusCode);
        var booked = await ReadAsync<AvailabilityResult>(admin,
            FormattableString.Invariant($"/api/vehicles/{assignedId}/availability?date={upcoming:yyyy-MM-dd}&start=10:00&end=11:00"));
        Assert.False(booked.IsAvailable);
        var adjacent = await ReadAsync<AvailabilityResult>(admin,
            FormattableString.Invariant($"/api/vehicles/{assignedId}/availability?date={upcoming:yyyy-MM-dd}&start=14:30&end=15:00&passengers=2"));
        Assert.True(adjacent.IsAvailable);
        using var deactivated = await DeleteAsync(admin, $"/api/vehicles/{assignedId}");
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.False((await ReadAsync<VehicleDto>(admin, $"/api/vehicles/{assignedId}")).IsActive);
        Assert.True(await db.BookingAssignments.AnyAsync(assignment => assignment.VehicleId == assignedId));
    }

    [SqlServerFact]
    public async Task DriversRequireEligibleAccountsAndPreserveIdentityRole()
    {
        using var app = new SqlServerAuthenticationTests.SqlServerApp();
        await app.InitializeAsync();
        using var admin = await AdministratorAsync(app);
        using var driver = Client(app);
        var user = await RegisterAsync(driver, "new-driver@example.test");
        var payload = new { userId = user.Id, firstName = "New", lastName = "Driver", phoneNumber = "555-0183", licenseNumber = "ops-license" };
        using var denied = await PostAsync(driver, "/api/drivers", payload);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var unsuitable = await PostAsync(admin, "/api/drivers", payload);
        Assert.Equal(HttpStatusCode.Conflict, unsuitable.StatusCode);
        using var granted = await PutAsync(admin, $"/api/users/{user.Id}/access", new UserAccessUpdate(UserRole.Driver, false));
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        using var created = await PostAsync(admin, "/api/drivers", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var profile = await BodyAsync<DriverDto>(created);
        Assert.Equal(user.Id, profile.UserId);
        using var duplicate = await PutAsync(admin, $"/api/drivers/{profile.Id}",
            new DriverInput("New", "Driver", "555-0183", "GE-TBL-DR-1001", true));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var updated = await PutAsync(admin, $"/api/drivers/{profile.Id}",
            new DriverInput("Updated", "Driver", "555-0183", "ops-license", true));
        Assert.Equal("Updated", (await BodyAsync<DriverDto>(updated)).FirstName);
        var directory = await ReadAsync<PageResult<DriverDto>>(admin, "/api/drivers?search=OPS-LICENSE&isActive=true&sort=licenseNumber");
        Assert.Equal(profile.Id, Assert.Single(directory.Items).Id);
        var available = await ReadAsync<AvailabilityResult>(admin, $"/api/drivers/{profile.Id}/availability?date=2026-12-05&start=10:00&end=11:00");
        Assert.True(available.IsAvailable);
        await SignInAsync(driver, user.Email);
        var self = await ReadAsync<UserProfile>(driver, "/api/auth/me");
        Assert.Equal("DRIVER", Assert.Single(self.Roles));
        Assert.Equal("Updated", self.FirstName);
        using var selfUpdate = await PutAsync(driver, "/api/auth/me", new ProfileUpdate("Driver self", "Driver", "555-0109"));
        Assert.Equal("555-0109", (await BodyAsync<UserProfile>(selfUpdate)).PhoneNumber);
        Assert.Equal("Driver self", (await ReadAsync<DriverDto>(admin, $"/api/drivers/{profile.Id}")).FirstName);
        using var directoryDenied = await GetAsync(driver, "/api/drivers");
        Assert.Equal(HttpStatusCode.Forbidden, directoryDenied.StatusCode);
        using var deactivated = await DeleteAsync(admin, $"/api/drivers/{profile.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.False((await ReadAsync<DriverDto>(admin, $"/api/drivers/{profile.Id}")).IsActive);
    }

    private static HttpClient Client(SqlServerAuthenticationTests.SqlServerApp app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<HttpClient> AdministratorAsync(SqlServerAuthenticationTests.SqlServerApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAdministrator>()
            .CreateAsync(AdminEmail, Password, CancellationToken.None);
        var client = Client(app);
        await SignInAsync(client, AdminEmail);
        return client;
    }

    private static async Task<UserProfile> RegisterAsync(HttpClient client, string email)
    {
        using var response = await PostAsync(client, "/api/auth/register",
            new RegisterRequest(email, Password, "Operations", "Customer", "555-0100"));
        var profile = await BodyAsync<UserProfile>(response);
        await SignInAsync(client, email);
        return profile;
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await PostAsync(client, "/api/auth/login", new LoginRequest(email, Password));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await BodyAsync<TokenPair>(response)).AccessToken);
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, string path)
    {
        using var response = await GetAsync(client, path);
        return await BodyAsync<T>(response);
    }

    private static async Task<T> BodyAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(body);
        return body;
    }

    private static Uri Path(string path) => new(path, UriKind.Relative);
    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path) => client.GetAsync(Path(path));
    private static Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string path, T payload) => client.PostAsJsonAsync(Path(path), payload);
    private static Task<HttpResponseMessage> PutAsync<T>(HttpClient client, string path, T payload) => client.PutAsJsonAsync(Path(path), payload);
    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path) => client.DeleteAsync(Path(path));
}
