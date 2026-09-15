namespace WeddingBooking.Api.Configuration;

/// e-conomic authenticates with two header tokens, not OAuth.
/// Neither token belongs in appsettings — see appsettings.json.
public sealed class EconomicOptions
{
    public const string SectionName = "Economic";

    public string BaseUrl { get; init; } = "https://restapi.e-conomic.com";
    public string AppSecretToken { get; init; } = string.Empty;
    public string AgreementGrantToken { get; init; } = string.Empty;

    /// Required on every e-conomic customer and sales document.
    public string DefaultCustomerGroupNumber { get; init; } = string.Empty;
    public string DefaultPaymentTermsNumber { get; init; } = string.Empty;
    public string DefaultLayoutNumber { get; init; } = string.Empty;
    public string DefaultVatZoneNumber { get; init; } = string.Empty;
}
