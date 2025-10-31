namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Represents a detected opportunity for code cleanup.
/// </summary>
public class CleanupOpportunity
{
    /// <summary>
    /// Type of cleanup operation.
    /// </summary>
    public CleanupType Type { get; set; }

    /// <summary>
    /// Line number where the cleanup opportunity was detected.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Original code that needs cleanup.
    /// </summary>
    public string OriginalCode { get; set; } = string.Empty;

    /// <summary>
    /// Surrounding code context for AI analysis.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata specific to the cleanup type.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Priority score for this cleanup (higher = more important).
    /// </summary>
    public int Priority { get; set; }
}
