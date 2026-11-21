using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Vehicles;
using TourFlow.Domain.Entities;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Infrastructure.Persistence;
using static TourFlow.Infrastructure.Operations.OperationQueries;

namespace TourFlow.Infrastructure.Operations;

internal sealed class VehicleHandlers(TourFlowDbContext db) :
    IRequestHandler<ListVehicles, PageResult<VehicleDto>>,
    IRequestHandler<GetVehicle, VehicleDto?>,
    IRequestHandler<UpdateVehicle, VehicleDto?>,
    IRequestHandler<CreateVehicle, VehicleDto>,
    IRequestHandler<DeactivateVehicle, bool>
{
    private const string Conflict = "This vehicle registration number already exists.";
    private static readonly Expression<Func<Vehicle, VehicleDto>> Projection =
        row => new VehicleDto(row.Id, row.Name, row.RegistrationNumber, row.VehicleType, row.PassengerCapacity, row.Description, row.IsActive);

    public Task<PageResult<VehicleDto>> Handle(ListVehicles request, CancellationToken cancellationToken)
    {
        var options = request.Options;
        var rows = db.Vehicles.AsNoTracking();
        if (options.IsActive is { } active) rows = rows.Where(row => row.IsActive == active);
        if (options.VehicleType is { } type) rows = rows.Where(row => row.VehicleType == type);
        if (options.MinCapacity is { } min) rows = rows.Where(row => row.PassengerCapacity >= min);
        if (options.MaxCapacity is { } max) rows = rows.Where(row => row.PassengerCapacity <= max);
        if (options.Search?.Trim() is { Length: > 0 } search)
            rows = rows.Where(row => row.Name.Contains(search) || row.RegistrationNumber.Contains(search));

        var ordered = options.Sort?.ToLowerInvariant() switch
        {
            "registrationnumber" => rows.Ordered(row => row.RegistrationNumber, options.Descending == true),
            "capacity" => rows.Ordered(row => row.PassengerCapacity, options.Descending == true),
            _ => rows.Ordered(row => row.Name, options.Descending == true)
        };
        return ReadPageAsync(rows, ordered, Projection, options, cancellationToken);
    }

    public Task<VehicleDto?> Handle(GetVehicle request, CancellationToken cancellationToken) =>
        db.Vehicles.AsNoTracking().Where(row => row.Id == request.Id)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public async Task<VehicleDto> Handle(CreateVehicle request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var row = new Vehicle(input.Name, input.RegistrationNumber.Trim().ToUpperInvariant(), input.VehicleType!.Value,
            input.PassengerCapacity, input.Description);
        if (input.IsActive is false) row.SetActive(false);
        db.Vehicles.Add(row);
        await SaveAsync(db, Conflict, cancellationToken);
        return ToDto(row);
    }

    public async Task<VehicleDto?> Handle(UpdateVehicle request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await SecurityLock.AcquireAsync(db, $"vehicle:{request.Id}", cancellationToken);
        var row = await db.Vehicles.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return null;
        var input = request.Input;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (input.PassengerCapacity < row.PassengerCapacity &&
            await db.LiveAssignments().AnyAsync(assignment => assignment.VehicleId == row.Id &&
                assignment.AssignmentDate >= today && assignment.Booking.PassengerCount > input.PassengerCapacity, cancellationToken))
        {
            throw new OperationConflictException("The new capacity is too low for an active assignment.");
        }

        row.Update(input.Name, input.VehicleType!.Value, input.PassengerCapacity, input.Description);
        row.ChangeRegistration(input.RegistrationNumber.Trim().ToUpperInvariant());
        if (input.IsActive is { } active) row.SetActive(active);
        await SaveAsync(db, Conflict, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> Handle(DeactivateVehicle request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, $"vehicle:{request.Id}", cancellationToken);
        var row = await db.Vehicles.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return false;
        if (row.IsActive)
        {
            row.SetActive(false);
            await SaveAsync(db, null, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static VehicleDto ToDto(Vehicle row) =>
        new(row.Id, row.Name, row.RegistrationNumber, row.VehicleType, row.PassengerCapacity, row.Description, row.IsActive);
}
