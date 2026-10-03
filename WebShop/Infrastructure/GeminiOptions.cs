namespace WebShop.Infrastructure;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>API key from Google AI Studio. Prefer env Gemini__ApiKey or appsettings.Local.json.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Primary model id, e.g. gemini-3.8-flash.</summary>
    public string Model { get; set; } = "gemini-3.8-flash";

    /// <summary>Fallback models when primary is overloaded or unavailable.</summary>
    public string[] FallbackModels { get; set; } =
    [
        "gemini-3.1-flash-lite",
        "gemini-flash-latest",
        "gemini-2.5-flash-lite"
    ];
}
