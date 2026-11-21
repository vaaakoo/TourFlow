using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Tours;
using TourFlow.Domain.Entities;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Infrastructure.Persistence;
using static TourFlow.Infrastructure.Operations.OperationQueries;

namespace TourFlow.Infrastructure.Operations;

internal sealed class TourHandlers(TourFlowDbContext db) :
    IRequestHandler<ListTours, PageResult<TourDto>>,
    IRequestHandler<GetTour, TourDto?>,
    IRequestHandler<UpdateTour, TourDto?>,
    IRequestHandler<CreateTour, TourDto?>,
    IRequestHandler<DeactivateTour, bool>
{
    private static readonly Expression<Func<Tour, TourDto>> Projection =
        row => new TourDto(row.Id, row.Name, row.Description, row.ImageUrl, (int)row.Duration.TotalMinutes, row.Price, row.MaxPassengers, row.DestinationId, row.TourType, row.IsActive, row.CreatedAt, row.UpdatedAt);

    public Task<PageResult<TourDto>> Handle(ListTours request, CancellationToken cancellationToken)
    {
        var options = request.Options;
        var rows = db.Tours.AsNoTracking();
        if (!request.IncludeInactive) rows = rows.Where(row => row.IsActive && row.Destination.IsActive);
        if (options.IsActive is { } active) rows = rows.Where(row => row.IsActive == active);
        if (options.DestinationId is { } destination) rows = rows.Where(row => row.DestinationId == destination);
        if (options.TourType is { } type) rows = rows.Where(row => row.TourType == type);
        if (options.MinPrice is { } min) rows = rows.Where(row => row.Price >= min);
        if (options.MaxPrice is { } max) rows = rows.Where(row => row.Price <= max);
        if (options.Search?.Trim() is { Length: > 0 } search)
            rows = rows.Where(row => row.Name.Contains(search) || row.Description.Contains(search));

        var ordered = options.Sort?.ToLowerInvariant() switch
        {
            "price" => rows.Ordered(row => row.Price, options.Descending == true),
            "duration" => rows.Ordered(row => row.Duration, options.Descending == true),
            "createdat" => rows.Ordered(row => row.CreatedAt, options.Descending == true),
            _ => rows.Ordered(row => row.Name, options.Descending == true)
        };
        return ReadPageAsync(rows, ordered, Projection, options, cancellationToken);
    }

    public Task<TourDto?> Handle(GetTour request, CancellationToken cancellationToken) =>
        db.Tours.AsNoTracking().Where(row => row.Id == request.Id && (request.IncludeInactive || (row.IsActive && row.Destination.IsActive)))
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public async Task<TourDto?> Handle(CreateTour request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, $"destination:{input.DestinationId}", cancellationToken);
        var destination = await db.Destinations.SingleOrDefaultAsync(row => row.Id == input.DestinationId, cancellationToken);
        if (destination is null) return null;
        var active = input.IsActive ?? true;
        EnsureDestination(destination, active);
        var row = new Tour(destination, input.Name, input.Description, TimeSpan.FromMinutes(input.DurationMinutes),
            input.Price, input.MaxPassengers, input.TourType!.Value, input.ImageUrl);
        if (!active) row.SetActive(false);
        db.Tours.Add(row);
        await SaveAsync(db, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<TourDto?> Handle(UpdateTour request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, $"tour:{request.Id}", cancellationToken);
        await SecurityLock.AcquireAsync(db, $"destination:{input.DestinationId}", cancellationToken);
        var row = await db.Tours.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return null;
        var destination = await db.Destinations.SingleOrDefaultAsync(row => row.Id == input.DestinationId, cancellationToken);
        if (destination is null) return null;
        var active = input.IsActive ?? row.IsActive;
        EnsureDestination(destination, active);
        row.Update(input.Name, input.Description, TimeSpan.FromMinutes(input.DurationMinutes), input.Price,
            input.MaxPassengers, input.TourType!.Value, input.ImageUrl);
        row.MoveTo(destination);
        if (row.IsActive != active) row.SetActive(active);
        await SaveAsync(db, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> Handle(DeactivateTour request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, $"tour:{request.Id}", cancellationToken);
        var row = await db.Tours.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return false;
        if (row.IsActive)
        {
            row.SetActive(false);
            await SaveAsync(db, null, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static void EnsureDestination(Destination destination, bool active)
    {
        if (active && !destination.IsActive)
            throw new OperationConflictException("An active tour requires an active destination.");
    }

    private static TourDto ToDto(Tour row) =>
        new(row.Id, row.Name, row.Description, row.ImageUrl, (int)row.Duration.TotalMinutes, row.Price, row.MaxPassengers, row.DestinationId, row.TourType, row.IsActive, row.CreatedAt, row.UpdatedAt);
}
