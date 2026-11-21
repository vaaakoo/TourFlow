using FluentValidation;
using MediatR;
using TourFlow.Application.Operations;
using TourFlow.Domain.Enums;

namespace TourFlow.Application.Backend;

public sealed class BookingPricingOptions
{
    public decimal TransferBasePrice { get; init; } = 30m;
    public decimal TransferPerPassenger { get; init; } = 5m;
    public string Currency { get; init; } = "USD";
}

public sealed record Actor(Guid UserId, bool IsCustomer, bool IsDriver, bool IsStaff, bool IsManager);

public sealed class BookingListOptions : ListOptions
{
    public BookingStatus? Status { get; init; }
    public BookingType? Type { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CustomerId { get; init; }
}

public sealed record BookingInput(BookingType BookingType, Guid? TourId, Guid? CustomerId, DateOnly BookingDate, TimeOnly StartTime,
    int PassengerCount, string PickupLocation, string? DropoffLocation, string? SpecialRequests);
public sealed record BookingDto(Guid Id, string BookingNumber, Guid CustomerId, Guid? TourId, string? TourName,
    BookingType BookingType, DateOnly BookingDate, TimeOnly StartTime, int PassengerCount, string PickupLocation,
    string? DropoffLocation, string? SpecialRequests, decimal TotalPrice, BookingStatus Status,
    DateTime CreatedAt, DateTime? UpdatedAt, string Version);
public sealed record StatusChangeInput(BookingStatus Status, string? Comment, string Version);
public sealed record BookingStatusDto(Guid Id, BookingStatus PreviousStatus, BookingStatus NewStatus,
    Guid ChangedByUserId, DateTime ChangedAt, string? Comment);
public sealed record ListBookings(BookingListOptions Options, Actor Actor) : IRequest<PageResult<BookingDto>>;
public sealed record GetBooking(Guid Id, Actor Actor) : IRequest<BookingDto?>;
public sealed record CreateBooking(BookingInput Input, Actor Actor) : IRequest<BookingDto>;
public sealed record UpdateBooking(Guid Id, BookingInput Input, string Version, Actor Actor) : IRequest<BookingDto?>;
public sealed record ChangeBookingStatus(Guid Id, StatusChangeInput Input, Actor Actor) : IRequest<BookingDto?>;
public sealed record GetBookingHistory(Guid Id, Actor Actor) : IRequest<IReadOnlyList<BookingStatusDto>?>;

public sealed record AssignmentInput(Guid DriverId, Guid VehicleId, DateOnly AssignmentDate,
    TimeOnly StartTime, TimeOnly EndTime, string Version);
public sealed record AssignmentDto(Guid Id, Guid BookingId, string BookingNumber, Guid DriverId, string DriverName,
    Guid VehicleId, string VehicleName, DateOnly AssignmentDate, TimeOnly StartTime, TimeOnly EndTime,
    bool IsActive, DateTime? CancelledAt);
public sealed record AssignBooking(Guid BookingId, AssignmentInput Input, Actor Actor) : IRequest<AssignmentDto?>;
public sealed record CancelAssignment(Guid BookingId, Guid AssignmentId, string Version, Actor Actor) : IRequest<bool>;
public sealed record ListDriverAssignments(BookingListOptions Options, Actor Actor) : IRequest<PageResult<AssignmentDto>>;

public sealed class PaymentListOptions : ListOptions
{
    public Guid? BookingId { get; init; }
    public PaymentStatus? Status { get; init; }
}
public sealed record PaymentInput(Guid BookingId, decimal Amount, PaymentMethod PaymentMethod, string Currency);
public sealed record PaymentStatusInput(PaymentStatus Status, string? TransactionReference);
public sealed record PaymentDto(Guid Id, Guid BookingId, decimal Amount, string Currency, PaymentMethod PaymentMethod,
    PaymentStatus Status, string? TransactionReference, DateTime? PaidAt, DateTime CreatedAt);
public sealed record ListPayments(PaymentListOptions Options) : IRequest<PageResult<PaymentDto>>;
public sealed record GetPayment(Guid Id) : IRequest<PaymentDto?>;
public sealed record CreatePayment(PaymentInput Input) : IRequest<PaymentDto>;
public sealed record ChangePaymentStatus(Guid Id, PaymentStatusInput Input) : IRequest<PaymentDto?>;

public sealed record DashboardOptions(DateOnly? From, DateOnly? To);
public sealed record StatusCount(BookingStatus Status, int Count);
public sealed record PopularTour(Guid TourId, string Name, int Bookings, decimal Revenue);
public sealed record DashboardDto(int TotalBookings, IReadOnlyList<StatusCount> ByStatus, int TotalCustomers,
    int ActiveTours, int ActiveDrivers, int ActiveVehicles, decimal Revenue,
    IReadOnlyList<BookingDto> RecentBookings, IReadOnlyList<PopularTour> PopularTours,
    IReadOnlyList<AssignmentDto> UpcomingAssignments);
public sealed record DashboardQuery(DashboardOptions Options) : IRequest<DashboardDto>;
public sealed record BookingReportRow(DateOnly Date, int Total, int Completed, int Cancelled, decimal BookedValue);
public sealed record RevenueReportRow(DateOnly Date, decimal Revenue, int Payments);
public sealed record ResourceReportRow(Guid Id, string Name, int Assignments, decimal ScheduledHours);
public sealed record BookingReport(DashboardOptions Options) : IRequest<IReadOnlyList<BookingReportRow>>;
public sealed record RevenueReport(DashboardOptions Options) : IRequest<IReadOnlyList<RevenueReportRow>>;
public sealed record DriverAssignmentReport(DashboardOptions Options) : IRequest<IReadOnlyList<ResourceReportRow>>;
public sealed record VehicleUtilizationReport(DashboardOptions Options) : IRequest<IReadOnlyList<ResourceReportRow>>;

public sealed class BookingListValidator : ListValidator<BookingListOptions>
{
    public BookingListValidator() : base("date", "createdAt", "status", "totalPrice")
    {
        RuleFor(x => x.Status).IsInEnum(); RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("From must not be after To.");
    }
}
public sealed class BookingInputValidator : AbstractValidator<BookingInput>
{
    public BookingInputValidator()
    {
        RuleFor(x => x.BookingType).IsInEnum(); RuleFor(x => x.PassengerCount).InclusiveBetween(1, 100);
        RuleFor(x => x.PickupLocation).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DropoffLocation).MaximumLength(500); RuleFor(x => x.SpecialRequests).MaximumLength(2000);
        RuleFor(x => x.TourId).NotNull().When(x => x.BookingType == BookingType.Tour);
        RuleFor(x => x.TourId).Null().When(x => x.BookingType == BookingType.Transfer);
        RuleFor(x => x.DropoffLocation).NotEmpty().When(x => x.BookingType == BookingType.Transfer);
        RuleFor(x => x.BookingDate).Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Booking date cannot be in the past.");
    }
}
public sealed class AssignmentInputValidator : AbstractValidator<AssignmentInput>
{
    public AssignmentInputValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty(); RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime); RuleFor(x => x.Version).NotEmpty();
    }
}
public sealed class PaymentInputValidator : AbstractValidator<PaymentInput>
{
    public PaymentInputValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty(); RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.PaymentMethod).IsInEnum(); RuleFor(x => x.Currency).Matches("^[A-Za-z]{3}$");
    }
}
public sealed class PaymentListValidator : ListValidator<PaymentListOptions>
{
    public PaymentListValidator() : base("createdAt", "amount", "status") { RuleFor(x => x.Status).IsInEnum(); }
}
