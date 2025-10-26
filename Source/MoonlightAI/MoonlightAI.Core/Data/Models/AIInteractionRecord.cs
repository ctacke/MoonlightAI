namespace MoonlightAI.Core.Data.Models;

/// <summary>
/// Represents a single interaction with the AI server.
/// </summary>
public class AIInteractionRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to FileResultRecord.
    /// </summary>
    public int FileResultId { get; set; }

    /// <summary>
    /// Navigation property to parent file result.
    /// </summary>
    public FileResultRecord FileResult { get; set; } = null!;

    /// <summary>
    /// Type of interaction (e.g., "Documentation", "BuildFix").
    /// </summary>
    public string InteractionType { get; set; } = string.Empty;

    /// <summary>
    /// When the interaction started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// How long the AI took to respond.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Prompt sent to the AI.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Response received from the AI.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Number of prompt tokens.
    /// </summary>
    public long PromptTokens { get; set; }

    /// <summary>
    /// Number of response tokens.
    /// </summary>
    public long ResponseTokens { get; set; }

    /// <summary>
    /// Whether the AI response was successfully applied.
    /// </summary>
    public bool Applied { get; set; }

    /// <summary>
    /// For build fixes, the attempt number.
    /// </summary>
    public int? BuildFixAttempt { get; set; }
}
