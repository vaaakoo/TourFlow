using TourFlow.Domain.Common;
using TourFlow.Domain.Enums;
using TourFlow.Domain.Exceptions;

namespace TourFlow.Domain.Entities;

/// <summary>Represents a simulated payment record for a booking.</summary>
public sealed class Payment : Entity
{
    private Payment()
    {
        Booking = null!;
    }

    public Payment(Booking booking, decimal amount, PaymentMethod paymentMethod, string currency = "USD")
    {
        if (amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        Booking = booking ?? throw new ArgumentNullException(nameof(booking));
        BookingId = booking.Id;
        Amount = amount;
        PaymentMethod = paymentMethod;
        Currency = NormalizeCurrency(currency);
        Status = PaymentStatus.Pending;
        booking.AddPayment(this);
    }

    /// <summary>Gets the booking identifier.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>Gets the booking being paid for.</summary>
    public Booking Booking { get; private set; }

    /// <summary>Gets the payment amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the simulated payment method.</summary>
    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Gets the payment processing status.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Gets the ISO 4217 currency code for this internal payment record.</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Gets the optional simulated transaction reference.</summary>
    public string? TransactionReference { get; private set; }

    /// <summary>Gets the UTC time at which the payment was recorded as paid.</summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>Marks a payment as paid and records its transaction reference.</summary>
    public void MarkPaid(string transactionReference, DateTime? paidAt = null)
    {
        if (Status is PaymentStatus.Paid or PaymentStatus.Refunded)
        {
            throw new DomainException($"A payment with status '{Status}' cannot be marked as paid.");
        }

        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            throw new DomainException("A transaction reference is required when marking a payment as paid.");
        }

        var timestamp = paidAt ?? DateTime.UtcNow;
        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Payment timestamps must be UTC.");
        }

        TransactionReference = transactionReference.Trim();
        PaidAt = timestamp;
        Status = PaymentStatus.Paid;
        Touch(timestamp);
    }

    /// <summary>Marks a pending payment as failed.</summary>
    public void MarkFailed()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException("Only pending payments can be marked as failed.");
        }

        Status = PaymentStatus.Failed;
        Touch();
    }

    /// <summary>Refunds a paid payment.</summary>
    public void Refund()
    {
        if (Status != PaymentStatus.Paid)
        {
            throw new DomainException("Only paid payments can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        Touch();
    }

    private static string NormalizeCurrency(string currency)
    {
        var value = currency?.Trim().ToUpperInvariant();
        if (value is null || value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
            throw new DomainException("Currency must be a three-letter ISO code.");
        return value;
    }
}
