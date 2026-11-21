using FluentValidation;
using MediatR;

namespace TourFlow.Application.Operations.Customers;

public sealed record CustomerDto(Guid Id, Guid UserId, string FirstName, string LastName, string Email, string PhoneNumber, DateTime CreatedAt);
public sealed record CustomerInput(string FirstName, string LastName, string PhoneNumber);

public sealed class CustomerListOptions : ListOptions
{
    public DateOnly? CreatedFrom { get; init; }
    public DateOnly? CreatedThrough { get; init; }
}

public sealed record ListCustomers(CustomerListOptions Options) : IRequest<PageResult<CustomerDto>>;
public sealed record GetCustomer(Guid Id) : IRequest<CustomerDto?>;
public sealed record GetOwnCustomer(Guid UserId) : IRequest<CustomerDto?>;
public sealed record UpdateCustomer(Guid Id, CustomerInput Input) : IRequest<CustomerDto?>;

public sealed class CustomerListOptionsValidator : ListValidator<CustomerListOptions>
{
    public CustomerListOptionsValidator() : base("name", "email", "createdAt")
    {
        RuleFor(query => query.CreatedThrough).GreaterThanOrEqualTo(query => query.CreatedFrom)
            .LessThan(DateOnly.MaxValue);
    
    }
}

public sealed class CustomerInputValidator : AbstractValidator<CustomerInput>
{
    public CustomerInputValidator()
    {
        RuleFor(input => input.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(input => input.LastName).NotEmpty().MaximumLength(100);
        RuleFor(input => input.PhoneNumber).NotEmpty().MaximumLength(32);
    }
}

internal sealed class ListCustomersValidator : AbstractValidator<ListCustomers>
{
    public ListCustomersValidator(CustomerListOptionsValidator validator) =>
        RuleFor(request => request.Options).SetValidator(validator);
}

internal sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomer>
{
    public UpdateCustomerValidator(CustomerInputValidator validator) =>
        RuleFor(request => request.Input).SetValidator(validator);
}
