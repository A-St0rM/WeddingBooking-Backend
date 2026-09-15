namespace WeddingBooking.Api.Tests.Fixtures;

/// <summary>
/// The tests run against a real Postgres, supplied by connection string.
/// There is no test database configured on this machine yet; set the variable
/// below to a Supabase (or any other) Postgres and the suite runs unchanged.
/// </summary>
public static class TestDatabase
{
    public const string VariableName = "WEDDINGBOOKING_TEST_POSTGRES";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(VariableName);

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>
/// A test that needs the database. Skips — loudly and by name — when none is
/// configured, so an unconfigured machine never reports these as passing.
/// </summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public RequiresDatabaseFactAttribute()
    {
        if (!TestDatabase.IsConfigured)
        {
            Skip = $"Ingen testdatabase. Sæt {TestDatabase.VariableName} for at køre denne test.";
        }
    }
}
