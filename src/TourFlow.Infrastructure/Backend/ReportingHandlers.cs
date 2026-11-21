using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Backend;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Backend;

internal static class ReportScope
{
    internal static IQueryable<Domain.Entities.Booking> Bookings(TourFlowDbContext db, DashboardOptions options)
    {
        var rows = db.Bookings.AsNoTracking();
        if (options.From is not null) rows = rows.Where(x => x.BookingDate >= options.From);
        if (options.To is not null) rows = rows.Where(x => x.BookingDate <= options.To);
        return rows;
    }
}

internal sealed class DashboardHandler(TourFlowDbContext db) : IRequestHandler<DashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(DashboardQuery request, CancellationToken ct)
    {
        var bookings = ReportScope.Bookings(db, request.Options);
        var byStatus = await bookings.GroupBy(x => x.Status).Select(x => new StatusCount(x.Key, x.Count())).ToListAsync(ct);
        var recentRows = await bookings.Include(x => x.Tour).OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
        var popularRows = await bookings.Where(x => x.TourId != null && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.Rejected)
            .GroupBy(x => new { x.TourId, x.Tour!.Name }).Select(x => new { x.Key.TourId, x.Key.Name,
                Bookings = x.Count(), Revenue = x.Sum(y => y.TotalPrice) })
            .OrderByDescending(x => x.Bookings).ThenByDescending(x => x.Revenue).Take(10).ToListAsync(ct);
        var popular = popularRows.Select(x => new PopularTour(x.TourId!.Value, x.Name, x.Bookings, x.Revenue)).ToList();
        var revenue = await db.Payments.AsNoTracking().Where(x => x.Status == PaymentStatus.Paid &&
            (request.Options.From == null || x.Booking.BookingDate >= request.Options.From) &&
            (request.Options.To == null || x.Booking.BookingDate <= request.Options.To)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcomingRows = await db.BookingAssignments.AsNoTracking().Include(x => x.Booking).Include(x => x.Driver).Include(x => x.Vehicle)
            .Where(x => x.IsActive && x.AssignmentDate >= today && x.Booking.Status != BookingStatus.Cancelled && x.Booking.Status != BookingStatus.Rejected)
            .OrderBy(x => x.AssignmentDate).ThenBy(x => x.StartTime).Take(10).ToListAsync(ct);
        return new DashboardDto(await bookings.CountAsync(ct), byStatus, await db.Customers.CountAsync(ct),
            await db.Tours.CountAsync(x => x.IsActive, ct), await db.Drivers.CountAsync(x => x.IsActive, ct),
            await db.Vehicles.CountAsync(x => x.IsActive, ct), revenue, recentRows.Select(BackendMaps.Booking).ToList(), popular,
            upcomingRows.Select(BackendMaps.Assignment).ToList());
    }
}

internal sealed class BookingReportHandler(TourFlowDbContext db) : IRequestHandler<BookingReport, IReadOnlyList<BookingReportRow>>
{
    public async Task<IReadOnlyList<BookingReportRow>> Handle(BookingReport request, CancellationToken ct)
    {
        var rows = await ReportScope.Bookings(db, request.Options).GroupBy(x => x.BookingDate)
            .Select(x => new { Date = x.Key, Total = x.Count(), Completed = x.Count(y => y.Status == BookingStatus.Completed),
                Cancelled = x.Count(y => y.Status == BookingStatus.Cancelled), BookedValue = x.Sum(y => y.TotalPrice) })
            .OrderBy(x => x.Date).ToListAsync(ct);
        return rows.Select(x => new BookingReportRow(x.Date, x.Total, x.Completed, x.Cancelled, x.BookedValue)).ToList();
    }
}

internal sealed class RevenueReportHandler(TourFlowDbContext db) : IRequestHandler<RevenueReport, IReadOnlyList<RevenueReportRow>>
{
    public async Task<IReadOnlyList<RevenueReportRow>> Handle(RevenueReport request, CancellationToken ct)
    {
        var rows = await db.Payments.AsNoTracking().Where(x => x.Status == PaymentStatus.Paid && x.PaidAt != null &&
            (request.Options.From == null || x.Booking.BookingDate >= request.Options.From) &&
            (request.Options.To == null || x.Booking.BookingDate <= request.Options.To))
            .GroupBy(x => x.Booking.BookingDate).Select(x => new { Date = x.Key, Revenue = x.Sum(y => y.Amount), Payments = x.Count() })
            .OrderBy(x => x.Date).ToListAsync(ct);
        return rows.Select(x => new RevenueReportRow(x.Date, x.Revenue, x.Payments)).ToList();
    }
}

internal sealed class DriverAssignmentReportHandler(TourFlowDbContext db)
    : IRequestHandler<DriverAssignmentReport, IReadOnlyList<ResourceReportRow>>
{
    public async Task<IReadOnlyList<ResourceReportRow>> Handle(DriverAssignmentReport request, CancellationToken ct)
    {
        var rows = await Rows(db, request.Options).GroupBy(x => new { x.DriverId, x.Driver.FirstName, x.Driver.LastName })
            .Select(x => new { Id = x.Key.DriverId, Name = x.Key.FirstName + " " + x.Key.LastName, Assignments = x.Count(),
                Minutes = x.Sum(y => EF.Functions.DateDiffMinute(y.StartTime, y.EndTime)) }).OrderByDescending(x => x.Assignments).ToListAsync(ct);
        return rows.Select(x => new ResourceReportRow(x.Id, x.Name, x.Assignments, x.Minutes / 60m)).ToList();
    }
    internal static IQueryable<Domain.Entities.BookingAssignment> Rows(TourFlowDbContext db, DashboardOptions options)
    {
        var rows = db.BookingAssignments.AsNoTracking().Where(x => x.IsActive);
        if (options.From is not null) rows = rows.Where(x => x.AssignmentDate >= options.From);
        if (options.To is not null) rows = rows.Where(x => x.AssignmentDate <= options.To);
        return rows;
    }
}

internal sealed class VehicleUtilizationReportHandler(TourFlowDbContext db)
    : IRequestHandler<VehicleUtilizationReport, IReadOnlyList<ResourceReportRow>>
{
    public async Task<IReadOnlyList<ResourceReportRow>> Handle(VehicleUtilizationReport request, CancellationToken ct)
    {
        var rows = await DriverAssignmentReportHandler.Rows(db, request.Options).GroupBy(x => new { x.VehicleId, x.Vehicle.Name })
            .Select(x => new { Id = x.Key.VehicleId, x.Key.Name, Assignments = x.Count(),
                Minutes = x.Sum(y => EF.Functions.DateDiffMinute(y.StartTime, y.EndTime)) }).OrderByDescending(x => x.Assignments).ToListAsync(ct);
        return rows.Select(x => new ResourceReportRow(x.Id, x.Name, x.Assignments, x.Minutes / 60m)).ToList();
    }
}
