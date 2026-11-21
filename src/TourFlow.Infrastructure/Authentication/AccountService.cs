using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Authentication;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Authentication;

internal sealed class AccountService(
    TourFlowDbContext db, UserManager<ApplicationUser> users, IPasswordResetDelivery delivery) : IAccounts
{
    public async Task<UserProfile> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser(request.Email.Trim(), UserRole.Customer);
        await SecurityLock.AcquireAsync(db, $"registration:{users.NormalizeEmail(user.Email)}", cancellationToken);
        if (await users.FindByEmailAsync(user.Email!) is not null)
            throw new AccountConflictException("An account with this email already exists.");

        EnsureSucceeded(await users.CreateAsync(user, request.Password));
        EnsureSucceeded(await users.AddToRoleAsync(user, "CUSTOMER"));
        var customer = new Customer(user, request.FirstName, request.LastName, request.PhoneNumber);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToProfile(user, customer, null, ["CUSTOMER"]);
    }

    public async Task<UserProfile?> ProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Include(account => account.Customer).Include(account => account.Driver)
            .SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        return user is null ? null : ToProfile(user, user.Customer, user.Driver, await users.GetRolesAsync(user));
    }

    public async Task<UserProfile?> UpdateProfileAsync(Guid id, ProfileUpdate request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(account => account.Customer).Include(account => account.Driver)
            .SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (user is null) return null;
        if (user.Customer is not null)
            user.Customer.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber);
        else if (user.Driver is not null)
            user.Driver.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber, user.Driver.LicenseNumber);
        else
        {
            user.UpdateStaffName(request.FirstName, request.LastName);
            EnsureSucceeded(await users.SetPhoneNumberAsync(user, request.PhoneNumber));
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToProfile(user, user.Customer, user.Driver, await users.GetRolesAsync(user));
    }

    public async Task RequestResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!delivery.IsConfigured) return;
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: true, IsDisabled: false })
            await delivery.DeliverAsync(user.Email!, await users.GeneratePasswordResetTokenAsync(user), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var candidate = await users.FindByEmailAsync(request.Email.Trim());
        if (candidate is null) throw InvalidReset();
        db.ChangeTracker.Clear();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, candidate.Id.ToString(), cancellationToken);
        var user = await users.FindByIdAsync(candidate.Id.ToString());
        if (user is not { EmailConfirmed: true, IsDisabled: false }) throw InvalidReset();
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) throw InvalidReset();
        EnsureSucceeded(await users.ResetAccessFailedCountAsync(user));
        EnsureSucceeded(await users.SetLockoutEndDateAsync(user, null));
        await SessionService.RevokeAsync(db, user.Id, null, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfile>> ListAsync(int page, CancellationToken cancellationToken)
    {
        var accounts = await db.Users.AsNoTracking().Include(user => user.Customer).Include(user => user.Driver)
            .OrderBy(user => user.Id).Skip((page - 1) * 50).Take(50).ToListAsync(cancellationToken);
        var ids = accounts.Select(user => user.Id).ToArray();
        var roles = await (from membership in db.UserRoles
                           join role in db.Roles on membership.RoleId equals role.Id
                           where ids.Contains(membership.UserId)
                           select new { membership.UserId, role.Name }).ToListAsync(cancellationToken);
        var rolesByUser = roles.ToLookup(item => item.UserId, item => item.Name!);
        return accounts.Select(user => ToProfile(user, user.Customer, user.Driver, rolesByUser[user.Id])).ToArray();
    }

    public async Task<bool> UpdateAccessAsync(Guid actorId, Guid id, UserAccessUpdate request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, "tourflow-administrator-membership", cancellationToken);
        await SecurityLock.AcquireAsync(db, id.ToString(), cancellationToken);
        // JWT validation and the action share a request scope. Discard any previously tracked user.
        db.ChangeTracker.Clear();
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null || actor.IsDisabled || await users.IsLockedOutAsync(actor) ||
            !await users.IsInRoleAsync(actor, "ADMINISTRATOR"))
            throw new AccountAccessDeniedException();

        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return false;

        var currentRoles = await users.GetRolesAsync(user);
        var requestedRole = request.Role ?? throw new ArgumentException("A role is required.", nameof(request));
        var roleName = requestedRole.ToString().ToUpperInvariant();
        if (currentRoles.Contains("ADMINISTRATOR") && !user.IsDisabled && user.PasswordHash is not null &&
            (request.IsDisabled || request.Role != UserRole.Administrator))
        {
            var now = DateTimeOffset.UtcNow;
            var anotherAdministrator = await (from account in db.Users
                join membership in db.UserRoles on account.Id equals membership.UserId
                join role in db.Roles on membership.RoleId equals role.Id
                where role.NormalizedName == "ADMINISTRATOR" && account.Id != id && !account.IsDisabled &&
                    account.PasswordHash != null && (account.LockoutEnd == null || account.LockoutEnd <= now)
                select account.Id).AnyAsync(cancellationToken);
            if (!anotherAdministrator) throw new AccountConflictException("At least one active administrator is required.");
        }

        var removed = currentRoles.Where(role => role != roleName).ToArray();
        if (removed.Length > 0) EnsureSucceeded(await users.RemoveFromRolesAsync(user, removed));
        if (!currentRoles.Contains(roleName)) EnsureSucceeded(await users.AddToRoleAsync(user, roleName));
        user.ChangeRole(requestedRole);
        user.SetDisabled(request.IsDisabled);
        EnsureSucceeded(await users.UpdateSecurityStampAsync(user));
        await SessionService.RevokeAsync(db, user.Id, null, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    internal static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new AccountValidationException(new Dictionary<string, string[]>
            {
                ["account"] = result.Errors.Select(error => error.Description).ToArray()
            });
    }

    private static AccountValidationException InvalidReset() =>
        new(new Dictionary<string, string[]> { ["reset"] = ["The password reset could not be completed."] });

    private static UserProfile ToProfile(ApplicationUser user, Customer? customer, Driver? driver, IEnumerable<string> roles) =>
        new(user.Id, user.Email!, customer?.FirstName ?? driver?.FirstName ?? user.StaffFirstName,
            customer?.LastName ?? driver?.LastName ?? user.StaffLastName,
            customer?.PhoneNumber ?? driver?.PhoneNumber ?? user.PhoneNumber, roles.ToArray(), user.IsDisabled);
}

internal sealed class DisabledPasswordResetDelivery : IPasswordResetDelivery
{
    public bool IsConfigured => false;
    public Task DeliverAsync(string email, string token, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Password reset delivery has not been configured.");
}
