namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Workload for cleaning up C# code by performing refactoring operations.
/// </summary>
public class CodeCleanupWorkload : Workload
{
    /// <summary>
    /// Path to the solution file (relative to repository root).
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the specific C# file to clean up (relative to repository root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string WorkloadType => "code-cleanup";

    /// <summary>
    /// Cleanup operations to perform.
    /// </summary>
    public CleanupOptions Options { get; set; } = new CleanupOptions();
}
