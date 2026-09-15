namespace WeddingBooking.Api.Configuration;

/// Supabase provides Postgres and login only. The API validates the token
/// and is the single writer. See ADR-0002.
public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// Project URL, used as the JWT authority.
    public string Url { get; init; } = string.Empty;
    public string Audience { get; init; } = "authenticated";
}
