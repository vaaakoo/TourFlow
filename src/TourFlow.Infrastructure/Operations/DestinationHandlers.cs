using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Destinations;
using TourFlow.Domain.Entities;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Infrastructure.Persistence;
using static TourFlow.Infrastructure.Operations.OperationQueries;

namespace TourFlow.Infrastructure.Operations;

internal sealed class DestinationHandlers(TourFlowDbContext db) :
    IRequestHandler<ListDestinations, PageResult<DestinationDto>>,
    IRequestHandler<GetDestination, DestinationDto?>,
    IRequestHandler<UpdateDestination, DestinationDto?>,
    IRequestHandler<CreateDestination, DestinationDto>,
    IRequestHandler<DeactivateDestination, bool>
{
    private const string Conflict = "A destination with this name already exists in that country.";
    private static readonly Expression<Func<Destination, DestinationDto>> Projection =
        row => new DestinationDto(row.Id, row.Name, row.Country, row.Description, row.ImageUrl, row.IsActive);

    public Task<PageResult<DestinationDto>> Handle(ListDestinations request, CancellationToken cancellationToken)
    {
        var options = request.Options;
        var rows = db.Destinations.AsNoTracking();
        if (!request.IncludeInactive) rows = rows.Where(row => row.IsActive);
        if (options.IsActive is { } active) rows = rows.Where(row => row.IsActive == active);
        if (DestinationCountries.Resolve(options.Country) is { } country) rows = rows.Where(row => row.Country == country);
        if (options.Search?.Trim() is { Length: > 0 } search)
            rows = rows.Where(row => row.Name.Contains(search) || row.Country.Contains(search));

        var ordered = options.Sort?.ToLowerInvariant() switch
        {
            "country" => rows.Ordered(row => row.Country, options.Descending == true),
            "active" => rows.Ordered(row => row.IsActive, options.Descending == true),
            _ => rows.Ordered(row => row.Name, options.Descending == true)
        };
        return ReadPageAsync(rows, ordered, Projection, options, cancellationToken);
    }

    public Task<DestinationDto?> Handle(GetDestination request, CancellationToken cancellationToken) =>
        db.Destinations.AsNoTracking().Where(row => row.Id == request.Id && (request.IncludeInactive || row.IsActive))
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public async Task<DestinationDto> Handle(CreateDestination request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var row = new Destination(input.Name, DestinationCountries.Resolve(input.Country)!, input.Description, input.ImageUrl);
        if (input.IsActive is false) row.SetActive(false);
        db.Destinations.Add(row);
        await SaveAsync(db, Conflict, cancellationToken);
        return ToDto(row);
    }

    public async Task<DestinationDto?> Handle(UpdateDestination request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, $"destination:{request.Id}", cancellationToken);
        var row = await db.Destinations.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return null;
        var input = request.Input;
        row.Update(input.Name, DestinationCountries.Resolve(input.Country)!, input.Description, input.ImageUrl);
        if (input.IsActive is { } active) row.SetActive(active);
        await SaveAsync(db, Conflict, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> Handle(DeactivateDestination request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SecurityLock.AcquireAsync(db, $"destination:{request.Id}", cancellationToken);
        var row = await db.Destinations.SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return false;
        if (row.IsActive)
        {
            row.SetActive(false);
            await SaveAsync(db, null, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static DestinationDto ToDto(Destination row) =>
        new(row.Id, row.Name, row.Country, row.Description, row.ImageUrl, row.IsActive);
}
