using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TourFlow.Application.Authentication;
using Xunit;

namespace TourFlow.IntegrationTests;

public sealed class AuthHttpTests : IClassFixture<AuthHttpFixture>
{
    private readonly AuthHttpFixture fixture;

    public AuthHttpTests(AuthHttpFixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/users")]
    public async Task AnonymousRequestsAreDenied(string path)
    {
        using var client = fixture.Client();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("CUSTOMER", HttpStatusCode.Forbidden)]
    [InlineData("DRIVER", HttpStatusCode.Forbidden)]
    [InlineData("OPERATOR", HttpStatusCode.Forbidden)]
    [InlineData("ADMINISTRATOR", HttpStatusCode.OK)]
    public async Task OnlyAdministratorCanListUsers(string role, HttpStatusCode expected)
    {
        using var client = fixture.Client(role);
        using var response = await client.GetAsync(new Uri("/api/users", UriKind.Relative));
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RejectsBadSignatureOrExpiredToken(bool invalidSignature, bool expired)
    {
        using var client = fixture.Client("CUSTOMER", invalidSignature, expired);
        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnProfileUsesJwtSubject()
    {
        using var client = fixture.Client("CUSTOMER");
        var profile = await client.GetFromJsonAsync<UserProfile>(new Uri("/api/auth/me", UriKind.Relative));
        Assert.Equal(AuthHttpFixture.TestId, profile?.Id);
    }

    [Fact]
    public async Task RejectsInvalidRegistrationBeforeCallingAccountService()
    {
        using var client = fixture.Client();
        using var response = await client.PostAsJsonAsync(new Uri("/api/auth/register", UriKind.Relative),
            new RegisterRequest("invalid", "short", "", "", ""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OperatorCannotChangeRoles()
    {
        using var client = fixture.Client("OPERATOR");
        using var response = await client.PutAsJsonAsync(new Uri($"/api/users/{AuthHttpFixture.TestId}/access", UriKind.Relative),
            new UserAccessUpdate(TourFlow.Domain.Enums.UserRole.Administrator, false));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDescribesBearerAuthentication()
    {
        using var client = fixture.Client();
        using var document = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
        var root = document.RootElement;
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(root.GetProperty("paths").GetProperty("/api/auth/me").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(root.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task HealthIsUnhealthyWhenSqlServerCannotBeReached()
    {
        using var client = fixture.Client();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

public sealed class AuthHttpFixture : WebApplicationFactory<Program>
{
    internal static readonly Guid TestId = Guid.Parse("706fa1d9-4da2-4300-a0ad-2456157151bd");
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(key));
        builder.UseSetting("AuthBootstrap:Run", "false");
        builder.UseSetting("ConnectionStrings:TourFlowDatabase",
            "Server=127.0.0.1,1;Database=TourFlow;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=1");
        builder.ConfigureServices(services =>
        {
            var accounts = new Mock<IAccounts>(MockBehavior.Strict);
            accounts.Setup(service => service.ProfileAsync(TestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserProfile(TestId, "test@example.test", "Test", "Customer", null, ["CUSTOMER"], false));
            accounts.Setup(service => service.ListAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            var sessions = new Mock<ISessions>(MockBehavior.Strict);
            sessions.Setup(service => service.IsCurrentAsync(TestId, "test-stamp", It.IsAny<IReadOnlyCollection<string>>())).ReturnsAsync(true);
            services.RemoveAll<IAccounts>();
            services.RemoveAll<ISessions>();
            services.AddSingleton(accounts.Object);
            services.AddSingleton(sessions.Object);
        });
    }

    internal HttpClient Client(string? role = null, bool invalidSignature = false, bool expired = false)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (role is null) return client;
        var now = DateTime.UtcNow;
        var signingKey = new SymmetricSecurityKey(invalidSignature ? RandomNumberGenerator.GetBytes(32) : key);
        var jwt = new JwtSecurityToken("TourFlow", "TourFlow.Client",
            [new Claim("sub", TestId.ToString()), new Claim("role", role), new Claim("security_stamp", "test-stamp")],
            now.AddMinutes(-5), expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        return client;
    }
}
