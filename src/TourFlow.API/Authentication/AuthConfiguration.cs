using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TourFlow.Application.Authentication;
using TourFlow.Infrastructure.Authentication;

namespace TourFlow.API.Authentication;

internal static class AuthConfiguration
{
    internal static void AddTourFlowSecurity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        services.AddSingleton(_ => ReadSettings(config));
        services.AddScoped<DevelopmentAdministrator>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((options, settings) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = settings.Key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(15),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessions>();
                        if (principal is null || !Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ||
                            !await sessions.IsCurrentAsync(id, principal.FindFirst("security_stamp")?.Value,
                                principal.FindAll("role").Select(claim => claim.Value).ToArray()))
                        {
                            context.Fail("The account session is no longer valid.");
                        }
                    }
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy("AdministratorOnly", policy => policy.RequireRole("ADMINISTRATOR"))
            .AddPolicy("ManagementAccess", policy => policy.RequireRole("ADMINISTRATOR", "MANAGER"))
            .AddPolicy("BookingOperations", policy => policy.RequireRole("ADMINISTRATOR", "MANAGER", "OPERATOR"))
            .AddPolicy("DriverAccess", policy => policy.RequireRole("DRIVER"))
            .AddPolicy("CustomerAccess", policy => policy.RequireRole("CUSTOMER"));

        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT").WithHeaders("Authorization", "Content-Type")));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("AuthenticationAttempts", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1)
                }));
        });
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer")] = [] }];
                }

                return Task.CompletedTask;
            });
        });
    }

    private static JwtSettings ReadSettings(ConfigurationManager config)
    {
        var issuer = config["Jwt:Issuer"] ?? "TourFlow";
        var audience = config["Jwt:Audience"] ?? "TourFlow.Client";
        var value = config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Configure Jwt:SigningKey with a base64-encoded secret.");
        var key = Convert.FromBase64String(value);
        var accessMinutes = config.GetValue("Jwt:AccessMinutes", 10);
        var refreshDays = config.GetValue("Jwt:RefreshDays", 14);
        if (key.Length < 32 || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            accessMinutes is < 1 or > 30 || refreshDays is < 1 or > 90)
            throw new InvalidOperationException("JWT configuration is invalid.");
        return new JwtSettings(issuer, audience, key, accessMinutes, refreshDays);
    }
}
