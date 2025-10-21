namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings for the AI server connection.
/// </summary>
public class AIServerConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "AIServer";

    /// <summary>
    /// The URL of the AI server API endpoint.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The name of the model to use.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Timeout in seconds for AI server requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}
