using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourFlow.Application.Authentication;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Infrastructure.Persistence;
using Xunit;

namespace TourFlow.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TOURFLOW_TEST_SQLSERVER")))
            Skip = "Set TOURFLOW_TEST_SQLSERVER to a SQL Server connection with permission to create test databases.";
    }
}

public sealed class SqlServerAuthenticationTests
{
    private static readonly RegisterRequest Customer = new("customer@example.test", "Test-only_Password42!", "Tour", "Customer", "555-0100");

    [SqlServerFact]
    public async Task RegistrationLoginRoleIsolationAndLockout()
    {
        using var app = new SqlServerApp();
        await app.InitializeAsync();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var registration = await PostAsync(client, "/api/auth/register", Customer);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var duplicate = await PostAsync(client, "/api/auth/register", Customer);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var tokens = await LoginAsync(client, Customer.Email, Customer.Password);
        Authenticate(client, tokens);
        var profile = await client.GetFromJsonAsync<UserProfile>(new Uri("/api/auth/me", UriKind.Relative));
        Assert.NotNull(profile);
        Assert.Equal(Customer.Email, profile.Email);
        Assert.Equal(["CUSTOMER"], profile.Roles);
        var other = Customer with { Email = "another-customer@example.test" };
        using var anotherRegistration = await PostAsync(client, "/api/auth/register", other);
        Assert.Equal(HttpStatusCode.Created, anotherRegistration.StatusCode);
        var otherProfile = await anotherRegistration.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(otherProfile);
        using var otherUser = await client.GetAsync(new Uri($"/api/users/{otherProfile.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, otherUser.StatusCode);
        Assert.NotEqual(profile.Id, otherProfile.Id);
        using var denied = await client.GetAsync(new Uri("/api/users", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var invalid = await PostAsync(client, "/api/auth/login", new LoginRequest(Customer.Email, "Incorrect-test-password!"));
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        }

        using var locked = await PostAsync(client, "/api/auth/login", new LoginRequest(Customer.Email, Customer.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Authenticate(client, tokens);
        using var outdated = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, outdated.StatusCode);
    }

    [SqlServerFact]
    public async Task RotationReuseConcurrentAttemptsAndLogout()
    {
        using var app = new SqlServerApp();
        await app.InitializeAsync();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var registration = await PostAsync(client, "/api/auth/register", Customer);
        registration.EnsureSuccessStatusCode();
        var initial = await LoginAsync(client, Customer.Email, Customer.Password);
        using var rotation = await PostAsync(client, "/api/auth/refresh", new RefreshRequest(initial.RefreshToken));
        var replacement = await ReadTokensAsync(rotation);
        Assert.NotEqual(initial.RefreshToken, replacement.RefreshToken);
        using var reuse = await PostAsync(client, "/api/auth/refresh", new RefreshRequest(initial.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        using var compromised = await PostAsync(client, "/api/auth/refresh", new RefreshRequest(replacement.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, compromised.StatusCode);

        var simultaneous = await LoginAsync(client, Customer.Email, Customer.Password);
        var attempts = await Task.WhenAll(
            PostAsync(client, "/api/auth/refresh", new RefreshRequest(simultaneous.RefreshToken)),
            PostAsync(client, "/api/auth/refresh", new RefreshRequest(simultaneous.RefreshToken)));
        using var first = attempts[0];
        using var second = attempts[1];
        Assert.Single(attempts, result => result.IsSuccessStatusCode);
        Assert.Single(attempts, result => result.StatusCode == HttpStatusCode.Unauthorized);

        var active = await LoginAsync(client, Customer.Email, Customer.Password);
        Authenticate(client, active);
        using var logout = await PostAsync(client, "/api/auth/logout", new RefreshRequest(active.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revoked = await PostAsync(client, "/api/auth/refresh", new RefreshRequest(active.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        var expiring = await LoginAsync(client, Customer.Email, Customer.Password);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TourFlowDbContext>();
        var expiredAt = DateTime.UtcNow.AddMinutes(-1);
        await db.RefreshTokens.Where(token => token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.ExpiresAt, expiredAt));
        using var expired = await PostAsync(client, "/api/auth/refresh", new RefreshRequest(expiring.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [SqlServerFact]
    public async Task AdministratorCannotRemoveLastAdministratorAndCanDisableCustomer()
    {
        using var app = new SqlServerApp();
        await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var administrator = scope.ServiceProvider.GetRequiredService<DevelopmentAdministrator>();
        await administrator.CreateAsync("admin@example.test", Customer.Password, CancellationToken.None);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var registration = await PostAsync(client, "/api/auth/register", Customer);
        var customer = await registration.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(customer);
        var customerTokens = await LoginAsync(client, Customer.Email, Customer.Password);
        Authenticate(client, await LoginAsync(client, "admin@example.test", Customer.Password));
        var admin = await client.GetFromJsonAsync<UserProfile>(new Uri("/api/auth/me", UriKind.Relative));
        Assert.NotNull(admin);
        using var denial = await client.PutAsJsonAsync(new Uri($"/api/users/{admin.Id}/access", UriKind.Relative),
            new UserAccessUpdate(TourFlow.Domain.Enums.UserRole.Customer, false));
        Assert.Equal(HttpStatusCode.Conflict, denial.StatusCode);
        using var disabled = await client.PutAsJsonAsync(new Uri($"/api/users/{customer.Id}/access", UriKind.Relative),
            new UserAccessUpdate(TourFlow.Domain.Enums.UserRole.Customer, true));
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Authenticate(client, customerTokens);
        using var denied = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    private static void Authenticate(HttpClient client, TokenPair pair) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);

    private static Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string path, T payload) =>
        client.PostAsJsonAsync(new Uri(path, UriKind.Relative), payload);

    private static async Task<TokenPair> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await PostAsync(client, "/api/auth/login", new LoginRequest(email, password));
        return await ReadTokensAsync(response);
    }

    private static async Task<TokenPair> ReadTokensAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var pair = await response.Content.ReadFromJsonAsync<TokenPair>();
        Assert.NotNull(pair);
        return pair;
    }

    private sealed class SqlServerApp : WebApplicationFactory<Program>
    {
        private readonly SqlConnectionStringBuilder connection = new(Environment.GetEnvironmentVariable("TOURFLOW_TEST_SQLSERVER"))
        {
            InitialCatalog = $"TourFlowTest_{Guid.NewGuid():N}"
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:TourFlowDatabase", connection.ConnectionString);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("AuthBootstrap:Run", "false");
            builder.UseSetting("DevelopmentData:Enabled", "true");
        }

        internal async Task InitializeAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TourFlowDbContext>();
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            Assert.True(await db.Database.CanConnectAsync());
            Assert.Equal(1, await db.Bookings.CountAsync());
        }
    }
}
