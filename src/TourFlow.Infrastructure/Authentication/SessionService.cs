using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TourFlow.Application.Authentication;
using TourFlow.Domain.Entities;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Authentication;

public sealed record JwtSettings(string Issuer, string Audience, byte[] SigningKey, int AccessMinutes, int RefreshDays)
{
    public SecurityKey Key => new SymmetricSecurityKey(SigningKey);
}

internal sealed class SessionService(TourFlowDbContext db, UserManager<ApplicationUser> users, JwtSettings settings) : ISessions
{
    public async Task<TokenPair?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var candidate = await users.FindByEmailAsync(request.Email.Trim());
        if (candidate is null) return null;
        db.ChangeTracker.Clear();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, candidate.Id.ToString(), cancellationToken);
        var user = await users.FindByIdAsync(candidate.Id.ToString());
        if (user is null || user.IsDisabled || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            AccountService.EnsureSucceeded(await users.AccessFailedAsync(user));
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        AccountService.EnsureSucceeded(await users.ResetAccessFailedCountAsync(user));
        var now = DateTime.UtcNow;
        var pair = await IssueAsync(user, now, now.AddDays(settings.RefreshDays), null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return pair;
    }

    public async Task<TokenPair?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var owner = await db.RefreshTokens.Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, owner.Value.ToString(), cancellationToken);
        var current = await db.RefreshTokens.Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        var now = DateTime.UtcNow;
        if (current is null) return null;
        if (current.WasRotated && current.ExpiresAt > now)
        {
            await RevokeAsync(db, owner.Value, current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current.RevokedAt.HasValue || current.ExpiresAt <= now || current.User.IsDisabled ||
            await users.IsLockedOutAsync(current.User)) return null;

        current.MarkRotated(now);
        var pair = await IssueAsync(current.User, now, current.ExpiresAt, current.FamilyId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return pair;
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, userId.ToString(), cancellationToken);
        var hash = Hash(refreshToken);
        var family = await db.RefreshTokens.Where(token => token.UserId == userId && token.TokenHash == hash)
            .Select(token => (Guid?)token.FamilyId).SingleOrDefaultAsync(cancellationToken);
        if (family.HasValue) await RevokeAsync(db, userId, family.Value, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> IsCurrentAsync(Guid userId, string? securityStamp, IReadOnlyCollection<string> roles)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDisabled || await users.IsLockedOutAsync(user) ||
            user.SecurityStamp is null || !string.Equals(Hash(user.SecurityStamp), securityStamp, StringComparison.Ordinal))
        {
            return false;
        }

        return roles.ToHashSet(StringComparer.Ordinal).SetEquals(await users.GetRolesAsync(user));
    }

    internal static Task<int> RevokeAsync(TourFlowDbContext db, Guid user, Guid? family, DateTime now, CancellationToken cancellationToken) =>
        db.RefreshTokens.Where(token => token.UserId == user && (!family.HasValue || token.FamilyId == family) && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.UpdatedAt, now), cancellationToken);

    private async Task<TokenPair> IssueAsync(ApplicationUser user, DateTime now, DateTime refreshExpiresAt, Guid? family, CancellationToken cancellationToken)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var expiresAt = now.AddMinutes(settings.AccessMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("security_stamp", Hash(user.SecurityStamp ?? throw new InvalidOperationException("The account has no security stamp.")))
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim("role", role)));
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expiresAt,
            new SigningCredentials(settings.Key, SecurityAlgorithms.HmacSha256));
        db.RefreshTokens.Add(new RefreshToken(user, Hash(token), refreshExpiresAt, family));
        await db.SaveChangesAsync(cancellationToken);
        return new TokenPair(new JwtSecurityTokenHandler().WriteToken(jwt), token, expiresAt);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal static class SecurityLock
{
    internal static Task<int> AcquireAsync(TourFlowDbContext db, string resource, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51000, 'Could not acquire an account lock.', 1;
            """, cancellationToken);
}
