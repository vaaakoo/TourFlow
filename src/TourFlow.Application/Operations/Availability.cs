using FluentValidation;
using MediatR;

namespace TourFlow.Application.Operations;

public sealed record AvailabilityResult(Guid Id, bool IsAvailable);

public sealed record GetVehicleAvailability(Guid Id, DateOnly Date, TimeOnly Start, TimeOnly End, int? Passengers)
    : IRequest<AvailabilityResult?>;

public sealed record GetDriverAvailability(Guid Id, DateOnly Date, TimeOnly Start, TimeOnly End)
    : IRequest<AvailabilityResult?>;

internal sealed class VehicleAvailabilityValidator : AbstractValidator<GetVehicleAvailability>
{
    public VehicleAvailabilityValidator()
    {
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.End).GreaterThan(request => request.Start);
        RuleFor(request => request.Passengers).GreaterThan(0);
    }
}

internal sealed class DriverAvailabilityValidator : AbstractValidator<GetDriverAvailability>
{
    public DriverAvailabilityValidator()
    {
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.End).GreaterThan(request => request.Start);
    }
}
