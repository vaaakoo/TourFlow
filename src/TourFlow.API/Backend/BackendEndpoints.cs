using System.Security.Claims;
using MediatR;
using TourFlow.Application.Backend;
using TourFlow.Domain.Enums;

namespace TourFlow.API.Backend;

internal static class BackendEndpoints
{
    internal static void MapBackend(this WebApplication app)
    {
        MapBookings(app); MapAssignments(app); MapPayments(app); MapReports(app);
    }

    private static void MapBookings(WebApplication app)
    {
        var group = app.MapGroup("/api/bookings").WithTags("Bookings").RequireAuthorization();
        group.MapGet("/", async ([AsParameters] BookingListOptions options, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListBookings(options, ToActor(user)), ct)));
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetBooking(id, ToActor(user)), ct)));
        group.MapGet("/{id:guid}/history", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetBookingHistory(id, ToActor(user)), ct)));
        group.MapPost("/", async (BookingInput input, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateBooking(input, ToActor(user)), ct);
            return Results.Created($"/api/bookings/{result.Id}", result);
        }).RequireAuthorization(policy => policy.RequireRole("CUSTOMER", "OPERATOR", "MANAGER", "ADMINISTRATOR"));
        group.MapPut("/{id:guid}", async (Guid id, BookingInput input, string version, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new UpdateBooking(id, input, version, ToActor(user)), ct)))
            .RequireAuthorization(policy => policy.RequireRole("CUSTOMER", "OPERATOR", "MANAGER", "ADMINISTRATOR"));
        group.MapPut("/{id:guid}/status", async (Guid id, StatusChangeInput input, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new ChangeBookingStatus(id, input, ToActor(user)), ct)));
        group.MapPost("/{id:guid}/confirm", async (Guid id, VersionBody input, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new ChangeBookingStatus(id, new(BookingStatus.Confirmed, input.Comment, input.Version), ToActor(user)), ct)))
            .RequireAuthorization("BookingOperations");
        group.MapPost("/{id:guid}/cancel", async (Guid id, VersionBody input, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new ChangeBookingStatus(id, new(BookingStatus.Cancelled, input.Comment, input.Version), ToActor(user)), ct)));
    }

    private static void MapAssignments(WebApplication app)
    {
        var group = app.MapGroup("/api/assignments").WithTags("Assignments").RequireAuthorization();
        group.MapGet("/", async ([AsParameters] BookingListOptions options, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListDriverAssignments(options, ToActor(user)), ct)))
            .RequireAuthorization(policy => policy.RequireRole("ADMINISTRATOR", "MANAGER", "OPERATOR", "DRIVER"));
        app.MapPost("/api/bookings/{id:guid}/assign", async (Guid id, AssignmentInput input, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new AssignBooking(id, input, ToActor(user)), ct)))
            .WithTags("Assignments").RequireAuthorization("ManagementAccess");
        app.MapDelete("/api/bookings/{bookingId:guid}/assignments/{assignmentId:guid}", async (
            Guid bookingId, Guid assignmentId, string version, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            await sender.Send(new CancelAssignment(bookingId, assignmentId, version, ToActor(user)), ct) ? Results.NoContent() : Results.NotFound())
            .WithTags("Assignments").RequireAuthorization("ManagementAccess");
    }

    private static void MapPayments(WebApplication app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments").RequireAuthorization("BookingOperations");
        group.MapGet("/", async ([AsParameters] PaymentListOptions options, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new ListPayments(options), ct)));
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => Found(await sender.Send(new GetPayment(id), ct)));
        group.MapPost("/", async (PaymentInput input, ISender sender, CancellationToken ct) =>
        { var result = await sender.Send(new CreatePayment(input), ct); return Results.Created($"/api/payments/{result.Id}", result); });
        group.MapPut("/{id:guid}/status", async (Guid id, PaymentStatusInput input, ISender sender, CancellationToken ct) => Found(await sender.Send(new ChangePaymentStatus(id, input), ct)));
    }

    private static void MapReports(WebApplication app)
    {
        var dashboard = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization("ManagementAccess");
        dashboard.MapGet("/", async ([AsParameters] DashboardOptions options, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new DashboardQuery(options), ct)));
        var reports = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization("ManagementAccess");
        reports.MapGet("/bookings", async ([AsParameters] DashboardOptions options, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new BookingReport(options), ct)));
        reports.MapGet("/revenue", async ([AsParameters] DashboardOptions options, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new RevenueReport(options), ct)));
        reports.MapGet("/drivers", async ([AsParameters] DashboardOptions options, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new DriverAssignmentReport(options), ct)));
        reports.MapGet("/vehicles", async ([AsParameters] DashboardOptions options, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new VehicleUtilizationReport(options), ct)));
    }

    private static Actor ToActor(ClaimsPrincipal user) => new(Guid.Parse(user.FindFirst("sub")!.Value),
        user.IsInRole("CUSTOMER"), user.IsInRole("DRIVER"),
        user.IsInRole("ADMINISTRATOR") || user.IsInRole("MANAGER") || user.IsInRole("OPERATOR"),
        user.IsInRole("ADMINISTRATOR") || user.IsInRole("MANAGER"));
    private static IResult Found<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
    internal sealed record VersionBody(string Version, string? Comment);
}
