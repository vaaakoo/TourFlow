using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Customers;
using TourFlow.Domain.Entities;
using TourFlow.Infrastructure.Persistence;
using static TourFlow.Infrastructure.Operations.OperationQueries;

namespace TourFlow.Infrastructure.Operations;

internal sealed class CustomerHandlers(TourFlowDbContext db) :
    IRequestHandler<ListCustomers, PageResult<CustomerDto>>,
    IRequestHandler<GetCustomer, CustomerDto?>,
    IRequestHandler<UpdateCustomer, CustomerDto?>,
    IRequestHandler<GetOwnCustomer, CustomerDto?>
{
    private static readonly Expression<Func<Customer, CustomerDto>> Projection =
        row => new CustomerDto(row.Id, row.UserId, row.FirstName, row.LastName, row.User.Email!, row.PhoneNumber, row.CreatedAt);

    public Task<PageResult<CustomerDto>> Handle(ListCustomers request, CancellationToken cancellationToken)
    {
        var options = request.Options;
        var rows = db.Customers.AsNoTracking();
        if (options.CreatedFrom is { } from)
        {
            var lower = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            rows = rows.Where(row => row.CreatedAt >= lower);
        }

        if (options.CreatedThrough is { } through)
        {
            var upper = through.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            rows = rows.Where(row => row.CreatedAt < upper);
        }

        if (options.Search?.Trim() is { Length: > 0 } search)
            rows = rows.Where(row => row.FirstName.Contains(search) || row.LastName.Contains(search) ||
                row.User.Email!.Contains(search) || row.PhoneNumber.Contains(search));

        var ordered = options.Sort?.ToLowerInvariant() switch
        {
            "email" => rows.Ordered(row => row.User.Email, options.Descending == true),
            "createdat" => rows.Ordered(row => row.CreatedAt, options.Descending == true),
            _ => rows.Ordered(row => row.LastName, options.Descending == true)
        };
        return ReadPageAsync(rows, ordered, Projection, options, cancellationToken);
    }

    public Task<CustomerDto?> Handle(GetCustomer request, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Where(row => row.Id == request.Id)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public Task<CustomerDto?> Handle(GetOwnCustomer request, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Where(row => row.UserId == request.UserId).Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CustomerDto?> Handle(UpdateCustomer request, CancellationToken cancellationToken)
    {
        var row = await db.Customers.Include(row => row.User).SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken);
        if (row is null) return null;
        var input = request.Input;
        row.UpdateProfile(input.FirstName, input.LastName, input.PhoneNumber);
        await SaveAsync(db, null, cancellationToken);
        return ToDto(row);
    }

    private static CustomerDto ToDto(Customer row) =>
        new(row.Id, row.UserId, row.FirstName, row.LastName, row.User.Email!, row.PhoneNumber, row.CreatedAt);
}
