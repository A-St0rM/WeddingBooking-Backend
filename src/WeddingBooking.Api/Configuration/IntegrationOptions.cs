namespace WeddingBooking.Api.Configuration;

/// Which implementation of an integration port to resolve at startup.
/// Fake is not test scaffolding: it is how the system runs for a demo
/// with no credentials. See ARCHITECTURE.md.
public enum IntegrationMode
{
    Fake,
    Real
}

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    public IntegrationMode Economic { get; init; } = IntegrationMode.Fake;
    public IntegrationMode Trello { get; init; } = IntegrationMode.Fake;
    public IntegrationMode Mail { get; init; } = IntegrationMode.Fake;
    public IntegrationMode Summarisation { get; init; } = IntegrationMode.Fake;
}
