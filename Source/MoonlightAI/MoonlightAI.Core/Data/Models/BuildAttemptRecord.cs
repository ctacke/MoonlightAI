namespace MoonlightAI.Core.Data.Models;

/// <summary>
/// Represents a single build validation attempt.
/// </summary>
public class BuildAttemptRecord
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
    /// Attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// When the build attempt started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// How long the build took.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether the build succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of errors in this build.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of warnings in this build.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Build errors (JSON array).
    /// </summary>
    public string? ErrorsJson { get; set; }

    /// <summary>
    /// Whether AI fix was attempted after this build.
    /// </summary>
    public bool AIFixAttempted { get; set; }

    /// <summary>
    /// Raw build output.
    /// </summary>
    public string? RawOutput { get; set; }
}
