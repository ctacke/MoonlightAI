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
    /// Path to the solution file (.sln or .slnx) for build validation.
    /// This path is shared across all workload types.
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// List of project names to exclude from workload processing.
    /// Specify just the project file name (e.g., "MyProject.Tests.csproj").
    /// All projects in the solution are processed by default unless listed here.
    /// </summary>
    public HashSet<string> IgnoreProjects { get; set; } = new();

    /// <summary>
    /// Configuration specific to Code Documentation workloads.
    /// </summary>
    public CodeDocWorkloadConfiguration CodeDocumentation { get; set; } = new();

    /// <summary>
    /// Configuration specific to Code Cleanup workloads.
    /// </summary>
    public CodeCleanupWorkloadConfiguration CodeCleanup { get; set; } = new();
}
