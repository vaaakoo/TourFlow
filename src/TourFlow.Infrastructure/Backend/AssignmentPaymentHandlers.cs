using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Backend;
using TourFlow.Application.Operations;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Backend;

internal sealed class AssignBookingHandler(TourFlowDbContext db) : IRequestHandler<AssignBooking, AssignmentDto?>
{
    public async Task<AssignmentDto?> Handle(AssignBooking request, CancellationToken ct)
    {
        if (!request.Actor.IsManager) throw new OperationForbiddenException("Only managers and administrators may assign resources.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(db, $"TourFlow:assignment:{request.Input.DriverId}", ct);
        await LockAsync(db, $"TourFlow:assignment:{request.Input.VehicleId}", ct);
        var booking = await db.Bookings.Include(x => x.Assignments).Include(x => x.Tour)
            .SingleOrDefaultAsync(x => x.Id == request.BookingId, ct);
        if (booking is null) return null; UpdateBookingHandler.EnsureVersion(booking, request.Input.Version);
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == request.Input.DriverId, ct);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == request.Input.VehicleId, ct);
        if (driver is null || vehicle is null) throw new OperationConflictException("The selected driver or vehicle does not exist.");
        if (request.Input.AssignmentDate != booking.BookingDate)
            throw new OperationConflictException("Assignment date must match the booking itinerary.");
        var overlap = await db.BookingAssignments.AnyAsync(x => x.IsActive && x.BookingId != booking.Id &&
            x.AssignmentDate == request.Input.AssignmentDate && x.StartTime < request.Input.EndTime && x.EndTime > request.Input.StartTime &&
            (x.DriverId == driver.Id || x.VehicleId == vehicle.Id) && x.Booking.Status != BookingStatus.Cancelled &&
            x.Booking.Status != BookingStatus.Rejected && x.Booking.Status != BookingStatus.Completed, ct);
        if (overlap) throw new OperationConflictException("The driver or vehicle already has an overlapping assignment.");
        foreach (var existing in booking.Assignments.Where(x => x.IsActive)) existing.Cancel();
        var assignment = new BookingAssignment(booking, driver, vehicle, request.Input.AssignmentDate,
            request.Input.StartTime, request.Input.EndTime);
        db.BookingAssignments.Add(assignment);
        if (booking.Status == BookingStatus.Confirmed)
            booking.TransitionTo(BookingStatus.Assigned, request.Actor.UserId, "Resources assigned.");
        await UpdateBookingHandler.SaveConcurrencyAsync(db, ct); await transaction.CommitAsync(ct);
        await db.Entry(assignment).Reference(x => x.Booking).LoadAsync(ct);
        await db.Entry(assignment).Reference(x => x.Driver).LoadAsync(ct);
        await db.Entry(assignment).Reference(x => x.Vehicle).LoadAsync(ct);
        return BackendMaps.Assignment(assignment);
    }

    internal static async Task LockAsync(TourFlowDbContext db, string resource, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51000, 'Could not acquire an operational lock.', 1;
            """, ct);
    }
}

internal sealed class CancelAssignmentHandler(TourFlowDbContext db) : IRequestHandler<CancelAssignment, bool>
{
    public async Task<bool> Handle(CancelAssignment request, CancellationToken ct)
    {
        if (!request.Actor.IsManager) throw new OperationForbiddenException("Only managers and administrators may cancel assignments.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var booking = await db.Bookings.Include(x => x.Assignments).SingleOrDefaultAsync(x => x.Id == request.BookingId, ct);
        if (booking is null) return false; UpdateBookingHandler.EnsureVersion(booking, request.Version);
        var assignment = booking.Assignments.SingleOrDefault(x => x.Id == request.AssignmentId && x.IsActive);
        if (assignment is null) return false; assignment.Cancel();
        if (!booking.Assignments.Any(x => x.IsActive && x.Id != assignment.Id))
            booking.TransitionTo(BookingStatus.Confirmed, request.Actor.UserId, "Assignment cancelled.");
        await UpdateBookingHandler.SaveConcurrencyAsync(db, ct); await transaction.CommitAsync(ct); return true;
    }
}

internal sealed class ListDriverAssignmentsHandler(TourFlowDbContext db)
    : IRequestHandler<ListDriverAssignments, PageResult<AssignmentDto>>
{
    public async Task<PageResult<AssignmentDto>> Handle(ListDriverAssignments request, CancellationToken ct)
    {
        var rows = db.BookingAssignments.AsNoTracking().Include(x => x.Booking).Include(x => x.Driver).Include(x => x.Vehicle).AsQueryable();
        if (request.Actor.IsDriver) rows = rows.Where(x => x.Driver.UserId == request.Actor.UserId);
        else if (!request.Actor.IsStaff) rows = rows.Where(_ => false);
        var options = request.Options;
        if (options.From is not null) rows = rows.Where(x => x.AssignmentDate >= options.From);
        if (options.To is not null) rows = rows.Where(x => x.AssignmentDate <= options.To);
        if (options.Status is not null) rows = rows.Where(x => x.Booking.Status == options.Status);
        var page = options.Page ?? 1; var size = options.PageSize ?? 20; var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.AssignmentDate).ThenBy(x => x.StartTime).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select(BackendMaps.Assignment).ToList(), page, size, count);
    }
}

internal sealed class ListPaymentsHandler(TourFlowDbContext db) : IRequestHandler<ListPayments, PageResult<PaymentDto>>
{
    public async Task<PageResult<PaymentDto>> Handle(ListPayments request, CancellationToken ct)
    {
        var o = request.Options; var rows = db.Payments.AsNoTracking().AsQueryable();
        if (o.BookingId is not null) rows = rows.Where(x => x.BookingId == o.BookingId);
        if (o.Status is not null) rows = rows.Where(x => x.Status == o.Status);
        if (!string.IsNullOrWhiteSpace(o.Search)) rows = rows.Where(x => x.TransactionReference != null && x.TransactionReference.Contains(o.Search));
        var desc = o.Descending ?? true; IOrderedQueryable<Payment> ordered = o.Sort?.ToLowerInvariant() switch
        {
            "amount" => desc ? rows.OrderByDescending(x => x.Amount) : rows.OrderBy(x => x.Amount),
            "status" => desc ? rows.OrderByDescending(x => x.Status) : rows.OrderBy(x => x.Status),
            _ => desc ? rows.OrderByDescending(x => x.CreatedAt) : rows.OrderBy(x => x.CreatedAt)
        };
        var page = o.Page ?? 1; var size = o.PageSize ?? 20; var count = await rows.CountAsync(ct);
        var items = await ordered.ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select(BackendMaps.Payment).ToList(), page, size, count);
    }
}
internal sealed class GetPaymentHandler(TourFlowDbContext db) : IRequestHandler<GetPayment, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(GetPayment request, CancellationToken ct)
    { var row = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, ct); return row is null ? null : BackendMaps.Payment(row); }
}
internal sealed class CreatePaymentHandler(TourFlowDbContext db, Microsoft.Extensions.Options.IOptions<BookingPricingOptions> pricing) : IRequestHandler<CreatePayment, PaymentDto>
{
    public async Task<PaymentDto> Handle(CreatePayment request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AssignBookingHandler.LockAsync(db, $"TourFlow:payment:{request.Input.BookingId}", ct);
        var booking = await db.Bookings.Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == request.Input.BookingId, ct)
            ?? throw new OperationConflictException("The booking does not exist.");
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
            throw new OperationConflictException("Payments cannot be added to a cancelled or rejected booking.");
        if (!string.Equals(request.Input.Currency, pricing.Value.Currency, StringComparison.OrdinalIgnoreCase))
            throw new OperationConflictException($"Payments for bookings must use {pricing.Value.Currency.ToUpperInvariant()}.");
        var committed = booking.Payments.Where(x => x.Status is PaymentStatus.Pending or PaymentStatus.Paid).Sum(x => x.Amount);
        if (committed + request.Input.Amount > booking.TotalPrice)
            throw new OperationConflictException("Payment records cannot exceed the booking total.");
        var payment = new Payment(booking, request.Input.Amount, request.Input.PaymentMethod, request.Input.Currency);
        db.Payments.Add(payment); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return BackendMaps.Payment(payment);
    }
}
internal sealed class ChangePaymentStatusHandler(TourFlowDbContext db) : IRequestHandler<ChangePaymentStatus, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(ChangePaymentStatus request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AssignBookingHandler.LockAsync(db, $"TourFlow:payment-record:{request.Id}", ct);
        var row = await db.Payments.SingleOrDefaultAsync(x => x.Id == request.Id, ct); if (row is null) return null;
        switch (request.Input.Status)
        {
            case PaymentStatus.Paid: row.MarkPaid(request.Input.TransactionReference ?? string.Empty); break;
            case PaymentStatus.Failed: row.MarkFailed(); break;
            case PaymentStatus.Refunded: row.Refund(); break;
            default: throw new OperationConflictException("A payment cannot transition back to pending.");
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new OperationConflictException("The transaction reference is already in use."); }
        await transaction.CommitAsync(ct);
        return BackendMaps.Payment(row);
    }
}
