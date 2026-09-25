namespace WeddingBooking.Api.Configuration;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>Project URL. Public by design — it appears in every browser request.</summary>
    public string Url { get; init; } = string.Empty;

    public string Audience { get; init; } = "authenticated";

    /// <summary>
    /// Supabase issues tokens from /auth/v1, not from the project root. Pointing
    /// the authority at the root means no token ever validates.
    /// </summary>
    public string? Authority => string.IsNullOrWhiteSpace(Url) ? null : $"{Url.TrimEnd('/')}/auth/v1";
}
