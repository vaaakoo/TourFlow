using System.Security.Claims;
using MediatR;
using TourFlow.Application.Operations;
using TourFlow.Application.Operations.Customers;
using TourFlow.Application.Operations.Destinations;
using TourFlow.Application.Operations.Drivers;
using TourFlow.Application.Operations.Tours;
using TourFlow.Application.Operations.Vehicles;

namespace TourFlow.API.Operations;

internal static class OperationsEndpoints
{
    internal static void MapOperations(this WebApplication app)
    {
        MapDestinations(app);
        MapTours(app);
        MapVehicles(app);
        MapDrivers(app);
        MapCustomers(app);
    }

    private static void MapDestinations(WebApplication app)
    {
        var group = app.MapGroup("/api/destinations").WithTags("Destinations");
        group.MapGet("/", async ([AsParameters] DestinationListOptions options, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListDestinations(options, IsStaff(user)), ct)));
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetDestination(id, IsStaff(user)), ct)));

        var write = group.MapGroup("").RequireAuthorization("ManagementAccess");
        write.MapPost("/", async (DestinationInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateDestination(input), ct);
            return Results.Created($"/api/destinations/{created.Id}", created);
        });
        write.MapPut("/{id:guid}", async (Guid id, DestinationInput input, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new UpdateDestination(id, input), ct)));
        write.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new DeactivateDestination(id), ct) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapTours(WebApplication app)
    {
        var group = app.MapGroup("/api/tours").WithTags("Tours");
        group.MapGet("/", async ([AsParameters] TourListOptions options, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListTours(options, IsStaff(user)), ct)));
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetTour(id, IsStaff(user)), ct)));

        var write = group.MapGroup("").RequireAuthorization("ManagementAccess");
        write.MapPost("/", async (TourInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateTour(input), ct);
            return created is null ? Results.NotFound() : Results.Created($"/api/tours/{created.Id}", created);
        });
        write.MapPut("/{id:guid}", async (Guid id, TourInput input, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new UpdateTour(id, input), ct)));
        write.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new DeactivateTour(id), ct) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapVehicles(WebApplication app)
    {
        var group = app.MapGroup("/api/vehicles").WithTags("Vehicles").RequireAuthorization("BookingOperations");
        group.MapGet("/", async ([AsParameters] VehicleListOptions options, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListVehicles(options), ct)));
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetVehicle(id), ct)));
        group.MapGet("/{id:guid}/availability", async (Guid id, DateOnly date, TimeOnly start, TimeOnly end, int? passengers,
            ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetVehicleAvailability(id, date, start, end, passengers), ct)));

        var write = group.MapGroup("").RequireAuthorization("ManagementAccess");
        write.MapPost("/", async (VehicleInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateVehicle(input), ct);
            return Results.Created($"/api/vehicles/{created.Id}", created);
        });
        write.MapPut("/{id:guid}", async (Guid id, VehicleInput input, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new UpdateVehicle(id, input), ct)));
        write.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new DeactivateVehicle(id), ct) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapDrivers(WebApplication app)
    {
        var group = app.MapGroup("/api/drivers").WithTags("Drivers").RequireAuthorization("BookingOperations");
        group.MapGet("/", async ([AsParameters] DriverListOptions options, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListDrivers(options), ct)));
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetDriver(id), ct)));
        group.MapGet("/{id:guid}/availability", async (Guid id, DateOnly date, TimeOnly start, TimeOnly end,
            ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetDriverAvailability(id, date, start, end), ct)));

        var write = group.MapGroup("").RequireAuthorization("ManagementAccess");
        write.MapPost("/", async (CreateDriverBody body, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateDriver(new DriverInput(body.FirstName, body.LastName, body.PhoneNumber, body.LicenseNumber, body.IsActive), body.UserId), ct);
            return created is null ? Results.NotFound() : Results.Created($"/api/drivers/{created.Id}", created);
        });
        write.MapPut("/{id:guid}", async (Guid id, DriverInput input, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new UpdateDriver(id, input), ct)));
        write.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new DeactivateDriver(id), ct) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapCustomers(WebApplication app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers");
        group.MapGet("/me", async (ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetOwnCustomer(Guid.Parse(user.FindFirst("sub")!.Value)), ct)))
            .RequireAuthorization("CustomerAccess");

        var staff = group.MapGroup("").RequireAuthorization("BookingOperations");
        staff.MapGet("/", async ([AsParameters] CustomerListOptions options, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListCustomers(options), ct)));
        staff.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new GetCustomer(id), ct)));
        staff.MapPut("/{id:guid}", async (Guid id, CustomerInput input, ISender sender, CancellationToken ct) =>
            Found(await sender.Send(new UpdateCustomer(id, input), ct)));
    }

    private static bool IsStaff(ClaimsPrincipal user) =>
        user.IsInRole("ADMINISTRATOR") || user.IsInRole("MANAGER") || user.IsInRole("OPERATOR");

    private static IResult Found<T>(T? value) where T : class =>
        value is null ? Results.NotFound() : Results.Ok(value);

    internal sealed record CreateDriverBody(Guid UserId, string FirstName, string LastName, string PhoneNumber,
        string LicenseNumber, bool? IsActive);
}
