using FluentValidation;
using MediatR;

namespace TourFlow.Application.Operations.Drivers;

public sealed record DriverDto(Guid Id, Guid UserId, string FirstName, string LastName, string PhoneNumber, string LicenseNumber, bool IsActive);
public sealed record DriverInput(string FirstName, string LastName, string PhoneNumber, string LicenseNumber, bool? IsActive);

public sealed class DriverListOptions : ListOptions
{
    public bool? IsActive { get; init; }
}

public sealed record ListDrivers(DriverListOptions Options) : IRequest<PageResult<DriverDto>>;
public sealed record GetDriver(Guid Id) : IRequest<DriverDto?>;
public sealed record CreateDriver(DriverInput Input, Guid UserId) : IRequest<DriverDto?>;
public sealed record DeactivateDriver(Guid Id) : IRequest<bool>;
public sealed record UpdateDriver(Guid Id, DriverInput Input) : IRequest<DriverDto?>;

public sealed class DriverListOptionsValidator : ListValidator<DriverListOptions>
{
    public DriverListOptionsValidator() : base("name", "licenseNumber", "active")
    { 
    }
}

public sealed class DriverInputValidator : AbstractValidator<DriverInput>
{
    public DriverInputValidator()
    {
        RuleFor(input => input.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(input => input.LastName).NotEmpty().MaximumLength(100);
        RuleFor(input => input.PhoneNumber).NotEmpty().MaximumLength(32);
        RuleFor(input => input.LicenseNumber).NotEmpty().MaximumLength(64);
    }
}

internal sealed class ListDriversValidator : AbstractValidator<ListDrivers>
{
    public ListDriversValidator(DriverListOptionsValidator validator) =>
        RuleFor(request => request.Options).SetValidator(validator);
}

internal sealed class CreateDriverValidator : AbstractValidator<CreateDriver>
{
    public CreateDriverValidator(DriverInputValidator validator)
    {
        RuleFor(request => request.Input).SetValidator(validator);
        RuleFor(request => request.UserId).NotEmpty();
    }
}

internal sealed class UpdateDriverValidator : AbstractValidator<UpdateDriver>
{
    public UpdateDriverValidator(DriverInputValidator validator) =>
        RuleFor(request => request.Input).SetValidator(validator);
}
