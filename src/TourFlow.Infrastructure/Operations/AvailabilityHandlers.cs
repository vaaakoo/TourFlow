using MediatR;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Operations;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Operations;

internal sealed class AvailabilityHandlers(TourFlowDbContext db) :
    IRequestHandler<GetVehicleAvailability, AvailabilityResult?>,
    IRequestHandler<GetDriverAvailability, AvailabilityResult?>
{
    public Task<AvailabilityResult?> Handle(GetVehicleAvailability request, CancellationToken cancellationToken)
    {
        var overlapping = db.LiveAssignments().Where(assignment => assignment.AssignmentDate == request.Date &&
            assignment.StartTime < request.End && request.Start < assignment.EndTime);
        return db.Vehicles.AsNoTracking().Where(vehicle => vehicle.Id == request.Id)
            .Select(vehicle => new AvailabilityResult(vehicle.Id, vehicle.IsActive &&
                (!request.Passengers.HasValue || vehicle.PassengerCapacity >= request.Passengers.Value) &&
                !overlapping.Any(assignment => assignment.VehicleId == vehicle.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<AvailabilityResult?> Handle(GetDriverAvailability request, CancellationToken cancellationToken)
    {
        var overlapping = db.LiveAssignments().Where(assignment => assignment.AssignmentDate == request.Date &&
            assignment.StartTime < request.End && request.Start < assignment.EndTime);
        return db.Drivers.AsNoTracking().Where(driver => driver.Id == request.Id)
            .Select(driver => new AvailabilityResult(driver.Id, driver.IsActive && !driver.User.IsDisabled &&
                !overlapping.Any(assignment => assignment.DriverId == driver.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
