using FluentValidation;
using TourFlow.Domain.Enums;

namespace TourFlow.Application.Authentication;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string PhoneNumber);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ProfileUpdate(string FirstName, string LastName, string PhoneNumber);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record UserAccessUpdate(UserRole? Role, bool IsDisabled);
public sealed record UserProfile(Guid Id, string Email, string? FirstName, string? LastName, string? PhoneNumber, string[] Roles, bool IsDisabled);
public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public interface IAccounts
{
    Task<UserProfile> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<UserProfile?> ProfileAsync(Guid id, CancellationToken cancellationToken);
    Task<UserProfile?> UpdateProfileAsync(Guid id, ProfileUpdate request, CancellationToken cancellationToken);
    Task RequestResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserProfile>> ListAsync(int page, CancellationToken cancellationToken);
    Task<bool> UpdateAccessAsync(Guid actorId, Guid id, UserAccessUpdate request, CancellationToken cancellationToken);
}

public interface ISessions
{
    Task<TokenPair?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<TokenPair?> RotateAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);
    Task<bool> IsCurrentAsync(Guid userId, string? securityStamp, IReadOnlyCollection<string> roles);
}

public interface IPasswordResetDelivery
{
    bool IsConfigured { get; }
    Task DeliverAsync(string email, string token, CancellationToken cancellationToken);
}

public sealed class AccountValidationException(IDictionary<string, string[]> errors) : Exception("Account validation failed.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AccountConflictException(string message) : Exception(message);
public sealed class AccountAccessDeniedException() : Exception("Account access denied.");

public sealed class ProfileUpdateValidator : AbstractValidator<ProfileUpdate>
{
    public ProfileUpdateValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(32);
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty().Length(12, 128);
        RuleFor(request => new ProfileUpdate(request.FirstName, request.LastName, request.PhoneNumber))
            .SetValidator(new ProfileUpdateValidator());
    }
}
