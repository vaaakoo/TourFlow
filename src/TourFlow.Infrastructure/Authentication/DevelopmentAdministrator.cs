using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Authentication;

public sealed class DevelopmentAdministrator(TourFlowDbContext db, UserManager<ApplicationUser> users)
{
    public async Task CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, "tourflow-administrator-membership", cancellationToken);
        await SecurityLock.AcquireAsync(db, $"registration:{users.NormalizeEmail(email)}", cancellationToken);
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!existing.IsDisabled && existing.PasswordHash is not null && await users.IsInRoleAsync(existing, "ADMINISTRATOR"))
                return;
            throw new InvalidOperationException("Choose an unused email address for the development administrator.");
        }

        var user = new ApplicationUser(email, UserRole.Administrator) { EmailConfirmed = true };
        AccountService.EnsureSucceeded(await users.CreateAsync(user, password));
        AccountService.EnsureSucceeded(await users.AddToRoleAsync(user, "ADMINISTRATOR"));
        await transaction.CommitAsync(cancellationToken);
    }
}
