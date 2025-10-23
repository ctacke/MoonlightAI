namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Statistics and metrics for a workload execution.
/// </summary>
public class WorkloadStatistics
{
    /// <summary>
    /// When the workload was queued.
    /// </summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// When the workload started executing.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the workload completed or failed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total execution time.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    /// <summary>
    /// Total time in queue before starting.
    /// </summary>
    public TimeSpan? QueueTime => StartedAt.HasValue
        ? StartedAt.Value - QueuedAt
        : null;

    /// <summary>
    /// Number of files processed.
    /// </summary>
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Number of items modified (methods, properties, classes, etc).
    /// </summary>
    public int ItemsModified { get; set; }

    /// <summary>
    /// Number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// List of error messages.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total AI API calls made.
    /// </summary>
    public int AIApiCalls { get; set; }

    /// <summary>
    /// Total AI processing time.
    /// </summary>
    public TimeSpan TotalAIProcessingTime { get; set; }

    /// <summary>
    /// Total number of prompt tokens (input tokens) used across all AI API calls.
    /// </summary>
    public int TotalPromptTokens { get; set; }

    /// <summary>
    /// Total number of response tokens (output tokens) generated across all AI API calls.
    /// </summary>
    public int TotalResponseTokens { get; set; }

    /// <summary>
    /// Total number of tokens (prompt + response) used across all AI API calls.
    /// </summary>
    public int TotalTokens => TotalPromptTokens + TotalResponseTokens;
}
