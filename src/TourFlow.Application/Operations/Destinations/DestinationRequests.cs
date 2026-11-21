using FluentValidation;
using MediatR;

namespace TourFlow.Application.Operations.Destinations;

public sealed record DestinationDto(Guid Id, string Name, string Country, string? Description, string? ImageUrl, bool IsActive);
public sealed record DestinationInput(string Name, string Country, string? Description, string? ImageUrl, bool? IsActive);

public sealed class DestinationListOptions : ListOptions
{
    public string? Country { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record ListDestinations(DestinationListOptions Options, bool IncludeInactive) : IRequest<PageResult<DestinationDto>>;
public sealed record GetDestination(Guid Id, bool IncludeInactive) : IRequest<DestinationDto?>;
public sealed record CreateDestination(DestinationInput Input) : IRequest<DestinationDto>;
public sealed record DeactivateDestination(Guid Id) : IRequest<bool>;
public sealed record UpdateDestination(Guid Id, DestinationInput Input) : IRequest<DestinationDto?>;

public sealed class DestinationListOptionsValidator : ListValidator<DestinationListOptions>
{
    public DestinationListOptionsValidator() : base("name", "country", "active")
    {
        RuleFor(query => query.Country).MaximumLength(100);
        RuleFor(query => query.Country).Must(country => country is null || DestinationCountries.Resolve(country) is not null)
            .WithMessage("Use a recognized country name or ISO country code.");
    
    }
}

public sealed class DestinationInputValidator : AbstractValidator<DestinationInput>
{
    public DestinationInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(160);
        RuleFor(input => input.Country).NotEmpty().MaximumLength(100)
            .Must(country => DestinationCountries.Resolve(country) is not null)
            .WithMessage("Use a recognized country name or ISO country code.");
        RuleFor(input => input.Description).MaximumLength(2000);
        RuleFor(input => input.ImageUrl).CatalogImage();
    }
}

internal sealed class ListDestinationsValidator : AbstractValidator<ListDestinations>
{
    public ListDestinationsValidator(DestinationListOptionsValidator validator) =>
        RuleFor(request => request.Options).SetValidator(validator);
}

internal sealed class CreateDestinationValidator : AbstractValidator<CreateDestination>
{
    public CreateDestinationValidator(DestinationInputValidator validator)
    {
        RuleFor(request => request.Input).SetValidator(validator);
    }
}

internal sealed class UpdateDestinationValidator : AbstractValidator<UpdateDestination>
{
    public UpdateDestinationValidator(DestinationInputValidator validator) =>
        RuleFor(request => request.Input).SetValidator(validator);
}
