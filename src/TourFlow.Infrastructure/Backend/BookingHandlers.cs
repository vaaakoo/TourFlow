using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourFlow.Application.Backend;
using TourFlow.Application.Operations;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Backend;

internal static class BackendMaps
{
    internal static BookingDto Booking(Booking row) => new(row.Id, row.BookingNumber, row.CustomerId, row.TourId,
        row.Tour?.Name, row.BookingType, row.BookingDate, row.StartTime, row.PassengerCount, row.PickupLocation,
        row.DropoffLocation, row.SpecialRequests, row.TotalPrice, row.Status, row.CreatedAt, row.UpdatedAt,
        Convert.ToBase64String(row.Version));
    internal static AssignmentDto Assignment(BookingAssignment row) => new(row.Id, row.BookingId,
        row.Booking.BookingNumber, row.DriverId, row.Driver.FirstName + " " + row.Driver.LastName, row.VehicleId,
        row.Vehicle.Name, row.AssignmentDate, row.StartTime, row.EndTime, row.IsActive, row.CancelledAt);
    internal static PaymentDto Payment(Payment row) => new(row.Id, row.BookingId, row.Amount, row.Currency,
        row.PaymentMethod, row.Status, row.TransactionReference, row.PaidAt, row.CreatedAt);
}

internal sealed class ListBookingsHandler(TourFlowDbContext db) : IRequestHandler<ListBookings, PageResult<BookingDto>>
{
    public async Task<PageResult<BookingDto>> Handle(ListBookings request, CancellationToken ct)
    {
        var options = request.Options; var rows = db.Bookings.AsNoTracking().Include(x => x.Tour).AsQueryable();
        rows = Scope(rows, request.Actor);
        if (options.Status is not null) rows = rows.Where(x => x.Status == options.Status);
        if (options.Type is not null) rows = rows.Where(x => x.BookingType == options.Type);
        if (options.From is not null) rows = rows.Where(x => x.BookingDate >= options.From);
        if (options.To is not null) rows = rows.Where(x => x.BookingDate <= options.To);
        if (options.CustomerId is not null && request.Actor.IsStaff) rows = rows.Where(x => x.CustomerId == options.CustomerId);
        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var term = options.Search.Trim();
            rows = rows.Where(x => x.BookingNumber.Contains(term) || x.PickupLocation.Contains(term) ||
                (x.DropoffLocation != null && x.DropoffLocation.Contains(term)));
        }
        var descending = options.Descending ?? true;
        IOrderedQueryable<Booking> ordered = options.Sort?.ToLowerInvariant() switch
        {
            "createdat" => descending ? rows.OrderByDescending(x => x.CreatedAt) : rows.OrderBy(x => x.CreatedAt),
            "status" => descending ? rows.OrderByDescending(x => x.Status) : rows.OrderBy(x => x.Status),
            "totalprice" => descending ? rows.OrderByDescending(x => x.TotalPrice) : rows.OrderBy(x => x.TotalPrice),
            _ => descending ? rows.OrderByDescending(x => x.BookingDate).ThenByDescending(x => x.StartTime) : rows.OrderBy(x => x.BookingDate).ThenBy(x => x.StartTime)
        };
        var count = await rows.CountAsync(ct); var page = options.Page ?? 1; var size = options.PageSize ?? 20;
        var items = await ordered.ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select(BackendMaps.Booking).ToList(), page, size, count);
    }

    internal static IQueryable<Booking> Scope(IQueryable<Booking> rows, Actor actor)
    {
        if (actor.IsStaff) return rows;
        if (actor.IsCustomer) return rows.Where(x => x.Customer.UserId == actor.UserId);
        if (actor.IsDriver) return rows.Where(x => x.Assignments.Any(a => a.IsActive && a.Driver.UserId == actor.UserId));
        return rows.Where(_ => false);
    }
}

internal sealed class GetBookingHandler(TourFlowDbContext db) : IRequestHandler<GetBooking, BookingDto?>
{
    public async Task<BookingDto?> Handle(GetBooking request, CancellationToken ct)
    {
        var row = await ListBookingsHandler.Scope(db.Bookings.AsNoTracking().Include(x => x.Tour), request.Actor)
            .SingleOrDefaultAsync(x => x.Id == request.Id, ct);
        return row is null ? null : BackendMaps.Booking(row);
    }
}

internal sealed class CreateBookingHandler(TourFlowDbContext db, IOptions<BookingPricingOptions> pricing)
    : IRequestHandler<CreateBooking, BookingDto>
{
    public async Task<BookingDto> Handle(CreateBooking request, CancellationToken ct)
    {
        if (!request.Actor.IsCustomer && !request.Actor.IsStaff) throw new OperationForbiddenException("Booking creation is not permitted.");
        Customer? customer;
        if (request.Actor.IsCustomer)
        {
            if (request.Input.CustomerId is not null) throw new OperationForbiddenException("Customers cannot select another customer profile.");
            customer = await db.Customers.SingleOrDefaultAsync(x => x.UserId == request.Actor.UserId, ct);
        }
        else customer = request.Input.CustomerId is null ? null : await db.Customers.SingleOrDefaultAsync(x => x.Id == request.Input.CustomerId, ct);
        if (customer is null) throw new OperationConflictException("The authenticated customer profile is unavailable.");
        var input = request.Input; Tour? tour = null;
        if (input.BookingType == BookingType.Tour)
        {
            tour = await db.Tours.Include(x => x.Destination).SingleOrDefaultAsync(x => x.Id == input.TourId, ct);
            if (tour is null) throw new OperationConflictException("The selected tour does not exist.");
            if (!tour.Destination.IsActive) throw new OperationConflictException("The selected tour destination is inactive.");
        }
        var number = $"TF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..29];
        var booking = tour is null
            ? new Booking(customer, number, BookingType.Transfer, input.BookingDate, input.StartTime, input.PassengerCount,
                input.PickupLocation, input.DropoffLocation, PriceTransfer(input.PassengerCount, pricing.Value), null, input.SpecialRequests)
            : Booking.CreateTourBooking(customer, tour, number, input.BookingDate, input.StartTime, input.PassengerCount,
                input.PickupLocation, input.DropoffLocation, input.SpecialRequests);
        db.Bookings.Add(booking); await db.SaveChangesAsync(ct); return BackendMaps.Booking(booking);
    }
    private static decimal PriceTransfer(int passengers, BookingPricingOptions value) =>
        decimal.Round(value.TransferBasePrice + value.TransferPerPassenger * passengers, 2, MidpointRounding.AwayFromZero);
}

internal sealed class UpdateBookingHandler(TourFlowDbContext db, IOptions<BookingPricingOptions> pricing)
    : IRequestHandler<UpdateBooking, BookingDto?>
{
    public async Task<BookingDto?> Handle(UpdateBooking request, CancellationToken ct)
    {
        var row = await db.Bookings.Include(x => x.Customer).ThenInclude(x => x.User).Include(x => x.Tour)
            .SingleOrDefaultAsync(x => x.Id == request.Id, ct);
        if (row is null) return null; EnsureOwnerOrStaff(row, request.Actor); EnsureVersion(row, request.Version);
        if (request.Actor.IsCustomer && row.Status != BookingStatus.Pending)
            throw new OperationForbiddenException("Customers may only edit pending bookings.");
        var input = request.Input;
        if (input.BookingType != row.BookingType || input.TourId != row.TourId)
            throw new OperationConflictException("A booking's type and tour cannot be changed.");
        var price = row.Tour is null ? pricing.Value.TransferBasePrice + pricing.Value.TransferPerPassenger * input.PassengerCount
            : row.Tour.Price * input.PassengerCount;
        row.UpdateItineraryAndPrice(input.BookingDate, input.StartTime, input.PassengerCount, input.PickupLocation,
            input.DropoffLocation, input.SpecialRequests, decimal.Round(price, 2));
        await SaveConcurrencyAsync(db, ct); return BackendMaps.Booking(row);
    }
    internal static void EnsureOwnerOrStaff(Booking row, Actor actor)
    {
        if (!actor.IsStaff && (!actor.IsCustomer || row.Customer.UserId != actor.UserId))
            throw new OperationForbiddenException("This booking belongs to another customer.");
    }
    internal static void EnsureVersion(Booking row, string version)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(version); } catch (FormatException) { throw new OperationConflictException("The booking version is invalid."); }
        if (!row.Version.SequenceEqual(supplied)) throw new OperationConflictException("The booking was changed by another request. Reload it and retry.");
    }
    internal static async Task SaveConcurrencyAsync(TourFlowDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new OperationConflictException("The booking was changed by another request. Reload it and retry."); }
    }
}

internal sealed class ChangeBookingStatusHandler(TourFlowDbContext db) : IRequestHandler<ChangeBookingStatus, BookingDto?>
{
    public async Task<BookingDto?> Handle(ChangeBookingStatus request, CancellationToken ct)
    {
        var row = await db.Bookings.Include(x => x.Customer).ThenInclude(x => x.User).Include(x => x.Tour)
            .Include(x => x.Assignments).ThenInclude(x => x.Driver).SingleOrDefaultAsync(x => x.Id == request.Id, ct);
        if (row is null) return null; UpdateBookingHandler.EnsureVersion(row, request.Input.Version);
        if (request.Actor.IsCustomer)
        {
            UpdateBookingHandler.EnsureOwnerOrStaff(row, request.Actor);
            if (request.Input.Status != BookingStatus.Cancelled) throw new OperationForbiddenException("Customers may only cancel eligible bookings.");
        }
        else if (request.Actor.IsDriver)
        {
            if (!row.Assignments.Any(x => x.IsActive && x.Driver.UserId == request.Actor.UserId) ||
                request.Input.Status is not (BookingStatus.InProgress or BookingStatus.Completed))
                throw new OperationForbiddenException("This status change is not permitted for the driver.");
        }
        else if (!request.Actor.IsStaff) throw new OperationForbiddenException("Booking status changes are not permitted.");
        else if (!request.Actor.IsManager && request.Input.Status is not (BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Rejected))
            throw new OperationForbiddenException("Operators may confirm, reject, or cancel bookings only.");
        row.TransitionTo(request.Input.Status, request.Actor.UserId, request.Input.Comment);
        if (request.Input.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
            foreach (var assignment in row.Assignments.Where(x => x.IsActive)) assignment.Cancel();
        await UpdateBookingHandler.SaveConcurrencyAsync(db, ct); return BackendMaps.Booking(row);
    }
}

internal sealed class GetBookingHistoryHandler(TourFlowDbContext db) : IRequestHandler<GetBookingHistory, IReadOnlyList<BookingStatusDto>?>
{
    public async Task<IReadOnlyList<BookingStatusDto>?> Handle(GetBookingHistory request, CancellationToken ct)
    {
        var allowed = await ListBookingsHandler.Scope(db.Bookings.AsNoTracking(), request.Actor).AnyAsync(x => x.Id == request.Id, ct);
        if (!allowed) return null;
        return await db.BookingStatusHistories.AsNoTracking().Where(x => x.BookingId == request.Id).OrderBy(x => x.ChangedAt)
            .Select(x => new BookingStatusDto(x.Id, x.PreviousStatus, x.NewStatus, x.ChangedByUserId, x.ChangedAt, x.Comment)).ToListAsync(ct);
    }
}
