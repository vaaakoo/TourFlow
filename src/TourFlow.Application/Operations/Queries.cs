using FluentValidation;
using MediatR;

namespace TourFlow.Application.Operations;

public class ListOptions
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public bool? Descending { get; init; }
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class OperationConflictException(string message) : Exception(message);
public sealed class OperationForbiddenException(string message) : Exception(message);

public abstract class ListValidator<T> : AbstractValidator<T> where T : ListOptions
{
    protected ListValidator(params string[] allowedSorts)
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 100_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(100);
        RuleFor(query => query.Sort).Must(sort => sort is null || allowedSorts.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

public sealed class ValidateRequest<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
        }

        return await next(cancellationToken);
    }
}
