namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings for GitHub integration.
/// </summary>
public class GitHubConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "GitHub";

    /// <summary>
    /// GitHub Personal Access Token for authentication.
    /// Required permissions: repo (full control of private repositories).
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Default branch name (e.g., "main" or "master").
    /// </summary>
    public string DefaultBranch { get; set; } = "main";

    /// <summary>
    /// Working directory where repositories will be cloned.
    /// </summary>
    public string WorkingDirectory { get; set; } = "./repositories";

    /// <summary>
    /// User name for git commits.
    /// </summary>
    public string UserName { get; set; } = "MoonlightAI";

    /// <summary>
    /// User email for git commits.
    /// </summary>
    public string UserEmail { get; set; } = "moonlight@example.com";
}
