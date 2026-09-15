namespace WeddingBooking.Api.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-4o-mini";
}
