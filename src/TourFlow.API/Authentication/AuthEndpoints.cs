using System.Security.Claims;
using FluentValidation;
using TourFlow.Application.Authentication;

namespace TourFlow.API.Authentication;

internal static class AuthEndpoints
{
    internal static void MapTourFlowAccounts(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        }).AddEndpointFilter<AccountErrors>();
        var publicAuth = auth.MapGroup("").AllowAnonymous().RequireRateLimiting("AuthenticationAttempts");
        publicAuth.MapPost("/register", async (RegisterRequest request, IAccounts accounts, CancellationToken ct) =>
            Results.Created("/api/auth/me", await accounts.RegisterAsync(request, ct)));
        publicAuth.MapPost("/login", async (LoginRequest request, ISessions sessions, CancellationToken ct) =>
            await sessions.LoginAsync(request, ct) is { } pair ? Results.Ok(pair) : Results.Unauthorized());
        publicAuth.MapPost("/refresh", async (RefreshRequest request, ISessions sessions, CancellationToken ct) =>
            await sessions.RotateAsync(request.RefreshToken, ct) is { } pair ? Results.Ok(pair) : Results.Unauthorized());
        publicAuth.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAccounts accounts, CancellationToken ct) =>
        {
            await accounts.RequestResetAsync(request, ct);
            return Results.Accepted();
        });
        publicAuth.MapPost("/reset-password", async (ResetPasswordRequest request, IAccounts accounts, CancellationToken ct) =>
        {
            await accounts.ResetPasswordAsync(request, ct);
            return Results.NoContent();
        });
        auth.MapPost("/logout", async (RefreshRequest request, ClaimsPrincipal user, ISessions sessions, CancellationToken ct) =>
        {
            await sessions.LogoutAsync(UserId(user), request.RefreshToken, ct);
            return Results.NoContent();
        }).RequireAuthorization();
        auth.MapGet("/me", async (ClaimsPrincipal user, IAccounts accounts, CancellationToken ct) =>
            await accounts.ProfileAsync(UserId(user), ct) is { } profile ? Results.Ok(profile) : Results.NotFound()).RequireAuthorization();
        auth.MapPut("/me", async (ProfileUpdate request, ClaimsPrincipal user, IAccounts accounts, CancellationToken ct) =>
            await accounts.UpdateProfileAsync(UserId(user), request, ct) is { } profile ? Results.Ok(profile) : Results.NotFound()).RequireAuthorization();

        var users = app.MapGroup("/api/users").RequireAuthorization("AdministratorOnly").AddEndpointFilter<AccountErrors>();
        users.MapGet("/", async (int? page, IAccounts accounts, CancellationToken ct) =>
            page is < 1 or > 100000 ? Results.BadRequest() : Results.Ok(await accounts.ListAsync(page ?? 1, ct)));
        users.MapGet("/{id:guid}", async (Guid id, IAccounts accounts, CancellationToken ct) =>
            await accounts.ProfileAsync(id, ct) is { } profile ? Results.Ok(profile) : Results.NotFound());
        users.MapPut("/{id:guid}/access", async (Guid id, UserAccessUpdate request, ClaimsPrincipal actor, IAccounts accounts, CancellationToken ct) =>
            await accounts.UpdateAccessAsync(UserId(actor), id, request, ct) ? Results.NoContent() : Results.NotFound());
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirst("sub")!.Value);
}

internal sealed class AccountErrors : IEndpointFilter
{
    private static readonly RegisterRequestValidator Registration = new();
    private static readonly ProfileUpdateValidator Profile = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            var errors = argument switch
            {
                RegisterRequest request => Registration.Validate(request).ToDictionary(),
                ProfileUpdate request => Profile.Validate(request).ToDictionary(),
                LoginRequest request when !ValidEmail(request.Email) || !HasLength(request.Password, 128) => Invalid("credentials"),
                RefreshRequest request when !HasLength(request.RefreshToken, 512) => Invalid("refreshToken"),
                ForgotPasswordRequest request when !ValidEmail(request.Email) => Invalid("email"),
                ResetPasswordRequest request when !ValidEmail(request.Email) || !HasLength(request.Token, 4096) ||
                    request.NewPassword is null || request.NewPassword.Length is < 12 or > 128 => Invalid("reset"),
                UserAccessUpdate request when request.Role is null || !Enum.IsDefined(request.Role.Value) => Invalid("role"),
                UserAccessUpdate request when request.IsDisabled is null => Invalid("isDisabled"),
                _ => null
            };
            if (errors is { Count: > 0 }) return Results.ValidationProblem(errors);
        }

        try
        {
            return await next(context);
        }
        catch (AccountValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors);
        }
        catch (AccountConflictException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (AccountAccessDeniedException)
        {
            return Results.Forbid();
        }
    }

    private static bool HasLength(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    private static bool ValidEmail(string? email) => HasLength(email, 256) && email!.Contains('@', StringComparison.Ordinal);
    private static Dictionary<string, string[]> Invalid(string field) => new() { [field] = ["The value is missing or invalid."] };
}
