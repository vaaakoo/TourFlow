namespace TourFlow.Domain.Exceptions;

/// <summary>Represents a violation of a domain invariant.</summary>
public class DomainException(string message) : Exception(message);
