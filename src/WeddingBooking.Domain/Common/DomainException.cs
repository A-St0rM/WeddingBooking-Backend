namespace WeddingBooking.Domain.Common;

/// <summary>
/// A business rule refused the operation. The API turns this into a 400 with
/// the message, so each rule is stated once — here — rather than restated in a
/// controller.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
