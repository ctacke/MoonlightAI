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
    /// Path to the project file (relative to repository root).
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the specific C# file to document (relative to repository root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string WorkloadType => "code-documentation";

    public MemberVisibility DocumentVisibility { get; set; } = MemberVisibility.Public;

    /// <summary>
    /// Gets a more specific branch name including the file being processed.
    /// </summary>
    public override string GetBranchName()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new InvalidOperationException("FilePath must be set before generating branch name");
        }

        var fileName = Path.GetFileNameWithoutExtension(FilePath).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException($"Could not extract filename from FilePath: {FilePath}");
        }

        return $"moonlight/{date}-code-doc-{fileName}";
    }
}
