namespace MapApp.Api.Configuration;

/// <summary>
/// CORS settings bound from the <c>Cors</c> configuration section.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "ClientPolicy";

    /// <summary>Browser origins allowed to call the API (the React client).</summary>
    public string[] AllowedOrigins { get; init; } = [];
}
