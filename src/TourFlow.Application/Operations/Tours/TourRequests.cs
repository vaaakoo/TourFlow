using FluentValidation;
using MediatR;
using TourFlow.Domain.Enums;

namespace TourFlow.Application.Operations.Tours;

public sealed record TourDto(Guid Id, string Name, string Description, string? ImageUrl, int DurationMinutes, decimal Price, int MaxPassengers, Guid DestinationId, TourType TourType, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record TourInput(string Name, string Description, string? ImageUrl, int DurationMinutes, decimal Price, int MaxPassengers, Guid DestinationId, TourType? TourType, bool? IsActive);

public sealed class TourListOptions : ListOptions
{
    public Guid? DestinationId { get; init; }
    public TourType? TourType { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record ListTours(TourListOptions Options, bool IncludeInactive) : IRequest<PageResult<TourDto>>;
public sealed record GetTour(Guid Id, bool IncludeInactive) : IRequest<TourDto?>;
public sealed record CreateTour(TourInput Input) : IRequest<TourDto?>;
public sealed record DeactivateTour(Guid Id) : IRequest<bool>;
public sealed record UpdateTour(Guid Id, TourInput Input) : IRequest<TourDto?>;

public sealed class TourListOptionsValidator : ListValidator<TourListOptions>
{
    public TourListOptionsValidator() : base("name", "price", "duration", "createdAt")
    {
        RuleFor(query => query.TourType).IsInEnum();
        RuleFor(query => query.DestinationId).NotEqual(Guid.Empty);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(query => query.MinPrice);
    
    }
}

public sealed class TourInputValidator : AbstractValidator<TourInput>
{
    public TourInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Description).NotEmpty().MaximumLength(4000);
        RuleFor(input => input.ImageUrl).CatalogImage();
        RuleFor(input => input.DurationMinutes).GreaterThan(0);
        RuleFor(input => input.Price).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(input => input.MaxPassengers).GreaterThan(0);
        RuleFor(input => input.DestinationId).NotEmpty();
        RuleFor(input => input.TourType).NotNull().IsInEnum();
    }
}

internal sealed class ListToursValidator : AbstractValidator<ListTours>
{
    public ListToursValidator(TourListOptionsValidator validator) =>
        RuleFor(request => request.Options).SetValidator(validator);
}

internal sealed class CreateTourValidator : AbstractValidator<CreateTour>
{
    public CreateTourValidator(TourInputValidator validator)
    {
        RuleFor(request => request.Input).SetValidator(validator);
    }
}

internal sealed class UpdateTourValidator : AbstractValidator<UpdateTour>
{
    public UpdateTourValidator(TourInputValidator validator) =>
        RuleFor(request => request.Input).SetValidator(validator);
}
