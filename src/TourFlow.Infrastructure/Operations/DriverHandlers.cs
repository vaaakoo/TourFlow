using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TourFlow.Domain.Enums;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Drivers;
using TourFlow.Domain.Entities;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Infrastructure.Persistence;
using static TourFlow.Infrastructure.Operations.OperationQueries;

namespace TourFlow.Infrastructure.Operations;

internal sealed class DriverHandlers(TourFlowDbContext db, UserManager<ApplicationUser> users) :
    IRequestHandler<ListDrivers, PageResult<DriverDto>>,
    IRequestHandler<GetDriver, DriverDto?>,
    IRequestHandler<UpdateDriver, DriverDto?>,
    IRequestHandler<CreateDriver, DriverDto?>,
    IRequestHandler<DeactivateDriver, bool>
{
    private const string Conflict = "This driver license number or user association already exists.";
    private static readonly Expression<Func<Driver, DriverDto>> Projection =
        row => new DriverDto(row.Id, row.UserId, row.FirstName, row.LastName, row.PhoneNumber, row.LicenseNumber, row.IsActive);

    public Task<PageResult<DriverDto>> Handle(ListDrivers request, CancellationToken cancellationToken)
    {
        var options = request.Options;
        var rows = db.Drivers.AsNoTracking();
        if (options.IsActive is { } active) rows = rows.Where(row => row.IsActive == active);
        if (options.Search?.Trim() is { Length: > 0 } search)
            rows = rows.Where(row => row.FirstName.Contains(search) || row.LastName.Contains(search) ||
                row.LicenseNumber.Contains(search) || row.PhoneNumber.Contains(search));

        var ordered = options.Sort?.ToLowerInvariant() switch
        {
            "licensenumber" => rows.Ordered(row => row.LicenseNumber, options.Descending == true),
            "active" => rows.Ordered(row => row.IsActive, options.Descending == true),
            _ => rows.Ordered(row => row.LastName, options.Descending == true)
        };
        return ReadPageAsync(rows, ordered, Projection, options, cancellationToken);
    }

    public Task<DriverDto?> Handle(GetDriver request, CancellationToken cancellationToken) =>
        db.Drivers.AsNoTracking().Where(row => row.Id == request.Id)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public async Task<DriverDto?> Handle(CreateDriver request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, request.UserId.ToString(), cancellationToken);
        var user = await db.Users.Include(user => user.Driver).SingleOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);
        if (user is null) return null;

        var roles = await users.GetRolesAsync(user);
        if (user.IsDisabled || user.Role != UserRole.Driver || roles.Any(role => role != "DRIVER") || user.Driver is not null)
            throw new OperationConflictException("Select an enabled driver account without an existing driver profile.");

        if (roles.Count == 0)
            AccountService.EnsureSucceeded(await users.AddToRoleAsync(user, "DRIVER"));

        var input = request.Input;
        var row = new Driver(user, input.FirstName, input.LastName, input.PhoneNumber, input.LicenseNumber.Trim().ToUpperInvariant());
        if (input.IsActive is false) row.SetActive(false);
        db.Drivers.Add(row);
        await SaveAsync(db, Conflict, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<DriverDto?> Handle(UpdateDriver request, CancellationToken cancellationToken)
    {
        var row = await db.Drivers.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return null;
        var input = request.Input;
        row.UpdateProfile(input.FirstName, input.LastName, input.PhoneNumber, input.LicenseNumber.Trim().ToUpperInvariant());
        if (input.IsActive is { } active) row.SetActive(active);
        await SaveAsync(db, Conflict, cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> Handle(DeactivateDriver request, CancellationToken cancellationToken)
    {
        var row = await db.Drivers.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return false;
        if (row.IsActive)
        {
            row.SetActive(false);
            await SaveAsync(db, null, cancellationToken);
        }

        return true;
    }

    private static DriverDto ToDto(Driver row) =>
        new(row.Id, row.UserId, row.FirstName, row.LastName, row.PhoneNumber, row.LicenseNumber, row.IsActive);
}
