using FluentValidation;
using MediatR;
using TourFlow.Domain.Enums;

namespace TourFlow.Application.Operations.Vehicles;

public sealed record VehicleDto(Guid Id, string Name, string RegistrationNumber, VehicleType VehicleType, int PassengerCapacity, string? Description, bool IsActive);
public sealed record VehicleInput(string Name, string RegistrationNumber, VehicleType? VehicleType, int PassengerCapacity, string? Description, bool? IsActive);

public sealed class VehicleListOptions : ListOptions
{
    public VehicleType? VehicleType { get; init; }
    public int? MinCapacity { get; init; }
    public int? MaxCapacity { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record ListVehicles(VehicleListOptions Options) : IRequest<PageResult<VehicleDto>>;
public sealed record GetVehicle(Guid Id) : IRequest<VehicleDto?>;
public sealed record CreateVehicle(VehicleInput Input) : IRequest<VehicleDto>;
public sealed record DeactivateVehicle(Guid Id) : IRequest<bool>;
public sealed record UpdateVehicle(Guid Id, VehicleInput Input) : IRequest<VehicleDto?>;

public sealed class VehicleListOptionsValidator : ListValidator<VehicleListOptions>
{
    public VehicleListOptionsValidator() : base("name", "registrationNumber", "capacity")
    {
        RuleFor(query => query.VehicleType).IsInEnum();
        RuleFor(query => query.MinCapacity).GreaterThan(0);
        RuleFor(query => query.MaxCapacity).GreaterThan(0);
        RuleFor(query => query.MaxCapacity).GreaterThanOrEqualTo(query => query.MinCapacity);
    
    }
}

public sealed class VehicleInputValidator : AbstractValidator<VehicleInput>
{
    public VehicleInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(120);
        RuleFor(input => input.RegistrationNumber).NotEmpty().MaximumLength(32);
        RuleFor(input => input.VehicleType).NotNull().IsInEnum();
        RuleFor(input => input.PassengerCapacity).GreaterThan(0);
        RuleFor(input => input.Description).MaximumLength(1000);
    }
}

internal sealed class ListVehiclesValidator : AbstractValidator<ListVehicles>
{
    public ListVehiclesValidator(VehicleListOptionsValidator validator) =>
        RuleFor(request => request.Options).SetValidator(validator);
}

internal sealed class CreateVehicleValidator : AbstractValidator<CreateVehicle>
{
    public CreateVehicleValidator(VehicleInputValidator validator)
    {
        RuleFor(request => request.Input).SetValidator(validator);
    }
}

internal sealed class UpdateVehicleValidator : AbstractValidator<UpdateVehicle>
{
    public UpdateVehicleValidator(VehicleInputValidator validator) =>
        RuleFor(request => request.Input).SetValidator(validator);
}
