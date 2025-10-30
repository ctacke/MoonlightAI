namespace MoonlightAI.Core.Data.Models;

/// <summary>
/// Represents a complete workload execution run.
/// </summary>
public class WorkloadRunRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for this run.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// When the run started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the run completed.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Type of workload executed (e.g., "CodeDocumentation", "UnitTest", "Cleanup").
    /// </summary>
    public string WorkloadType { get; set; } = string.Empty;

    /// <summary>
    /// Repository URL that was processed.
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Git branch created for this run.
    /// </summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// AI model used (e.g., "codellama:13b-instruct", "mistral:7b-instruct").
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// AI server URL.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Total files discovered as candidates.
    /// </summary>
    public int TotalFilesDiscovered { get; set; }

    /// <summary>
    /// Files selected for processing in this run.
    /// </summary>
    public int FilesSelected { get; set; }

    /// <summary>
    /// Files successfully processed.
    /// </summary>
    public int FilesSuccessful { get; set; }

    /// <summary>
    /// Files that failed processing.
    /// </summary>
    public int FilesFailed { get; set; }

    /// <summary>
    /// Files skipped (e.g., already documented).
    /// </summary>
    public int FilesSkipped { get; set; }

    /// <summary>
    /// Total number of build failures across all files.
    /// </summary>
    public int TotalBuildFailures { get; set; }

    /// <summary>
    /// Total number of build retry attempts.
    /// </summary>
    public int TotalBuildRetries { get; set; }

    /// <summary>
    /// Total prompt tokens consumed.
    /// </summary>
    public long TotalPromptTokens { get; set; }

    /// <summary>
    /// Total response tokens generated.
    /// </summary>
    public long TotalResponseTokens { get; set; }

    /// <summary>
    /// Total number of items (methods, properties, fields, classes) documented across all files.
    /// </summary>
    public int TotalItemsDocumented { get; set; }

    /// <summary>
    /// Total number of sanitization fixes applied to AI responses (e.g., removing hallucinated parameters, fixing void return tags).
    /// Higher values indicate more AI hallucinations that needed correction.
    /// </summary>
    public int TotalSanitizationFixes { get; set; }

    /// <summary>
    /// Pull request URL if created.
    /// </summary>
    public string? PullRequestUrl { get; set; }

    /// <summary>
    /// Whether the run completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if run failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Configuration settings used for this run (JSON).
    /// </summary>
    public string ConfigurationJson { get; set; } = string.Empty;

    /// <summary>
    /// Individual file results for this run.
    /// </summary>
    public List<FileResultRecord> FileResults { get; set; } = new();
}
