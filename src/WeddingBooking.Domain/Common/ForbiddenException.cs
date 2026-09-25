namespace WeddingBooking.Domain.Common;

/// <summary>A rule about who may do something, rather than what may be done. Becomes a 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
