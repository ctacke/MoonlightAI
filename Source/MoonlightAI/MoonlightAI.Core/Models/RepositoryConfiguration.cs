namespace MoonlightAI.Core.Models;

/// <summary>
/// Configuration for a specific repository to process.
/// </summary>
public class RepositoryConfiguration
{
    /// <summary>
    /// Full repository URL (e.g., "https://github.com/owner/repo").
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Repository owner/organization name.
    /// Parsed from RepositoryUrl.
    /// </summary>
    public string Owner
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RepositoryUrl))
                return string.Empty;

            var uri = new Uri(RepositoryUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            return segments.Length >= 1 ? segments[0] : string.Empty;
        }
    }

    /// <summary>
    /// Repository name.
    /// Parsed from RepositoryUrl.
    /// </summary>
    public string Name
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RepositoryUrl))
                return string.Empty;

            var uri = new Uri(RepositoryUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                // Remove .git suffix if present
                var name = segments[1];
                return name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                    ? name[..^4]
                    : name;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Local path where the repository will be cloned.
    /// Set after cloning.
    /// </summary>
    public string? LocalPath { get; set; }
}
