namespace MoonlightAI.Core.Data.Models;

/// <summary>
/// Represents the processing result for a single file.
/// </summary>
public class FileResultRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to WorkloadRunRecord.
    /// </summary>
    public int WorkloadRunId { get; set; }

    /// <summary>
    /// Navigation property to parent run.
    /// </summary>
    public WorkloadRunRecord WorkloadRun { get; set; } = null!;

    /// <summary>
    /// Relative path to the file within the repository.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// When processing started for this file.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When processing completed for this file.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Whether the file was successfully processed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Whether the file was modified.
    /// </summary>
    public bool WasModified { get; set; }

    /// <summary>
    /// Number of members (methods/properties) processed.
    /// </summary>
    public int MembersProcessed { get; set; }

    /// <summary>
    /// Number of members that were already documented.
    /// </summary>
    public int MembersAlreadyDocumented { get; set; }

    /// <summary>
    /// Number of members that were newly documented.
    /// </summary>
    public int MembersDocumented { get; set; }

    /// <summary>
    /// Prompt tokens used for this file.
    /// </summary>
    public long PromptTokens { get; set; }

    /// <summary>
    /// Response tokens generated for this file.
    /// </summary>
    public long ResponseTokens { get; set; }

    /// <summary>
    /// Number of build attempts for this file.
    /// </summary>
    public int BuildAttemptCount { get; set; }

    /// <summary>
    /// Whether final build validation passed.
    /// </summary>
    public bool BuildPassed { get; set; }

    /// <summary>
    /// Whether the file was reverted due to build failure.
    /// </summary>
    public bool WasReverted { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Build attempts for this file.
    /// </summary>
    public List<BuildAttemptRecord> BuildAttempts { get; set; } = new();

    /// <summary>
    /// AI interactions for this file.
    /// </summary>
    public List<AIInteractionRecord> AIInteractions { get; set; } = new();
}
