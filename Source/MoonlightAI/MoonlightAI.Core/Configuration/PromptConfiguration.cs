namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings for AI prompt management.
/// </summary>
public class PromptConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Prompts";

    /// <summary>
    /// Directory containing prompt template files.
    /// Default is "./prompts" relative to the application directory.
    /// </summary>
    public string Directory { get; set; } = "./prompts";

    /// <summary>
    /// Whether to enable loading custom prompts from files.
    /// If false, only hardcoded default prompts will be used.
    /// Default is true.
    /// </summary>
    public bool EnableCustomPrompts { get; set; } = true;
}
