namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Workload for adding XML documentation to a single C# file.
/// </summary>
public class CodeDocWorkload : Workload
{
    /// <summary>
    /// Path to the solution file (relative to repository root).
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the specific C# file to document (relative to repository root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string WorkloadType => "code-documentation";

    public MemberVisibility DocumentVisibility { get; set; } = MemberVisibility.Public;
}
