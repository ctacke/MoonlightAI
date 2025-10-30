namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings for workload execution.
/// </summary>
public class WorkloadConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Workload";

    /// <summary>
    /// Maximum number of files to process in a single batch run.
    /// Default is 10 files per batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Whether to validate builds after modifying files.
    /// Default is true.
    /// </summary>
    public bool ValidateBuilds { get; set; } = true;

    /// <summary>
    /// Maximum number of times to retry fixing build errors with AI.
    /// Default is 2 attempts.
    /// </summary>
    public int MaxBuildRetries { get; set; } = 2;

    /// <summary>
    /// Whether to revert files that fail build validation after all retry attempts.
    /// Default is true.
    /// </summary>
    public bool RevertOnBuildFailure { get; set; } = true;

    /// <summary>
    /// Configuration specific to Code Documentation workloads.
    /// </summary>
    public CodeDocWorkloadConfiguration CodeDocumentation { get; set; } = new();
}
