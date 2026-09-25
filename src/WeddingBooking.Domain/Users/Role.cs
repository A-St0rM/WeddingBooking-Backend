namespace WeddingBooking.Domain.Users;

/// <summary>
/// The five kinds of staff. What each may see and change is decided in the API
/// (readyprompt.md §6); the roles themselves carry no behaviour.
/// </summary>
public enum Role
{
    EventManager,
    Owner,
    Bookkeeper,
    Chef,
    EventProducer
}
