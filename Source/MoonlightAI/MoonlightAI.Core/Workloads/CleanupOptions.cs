namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Configuration options for code cleanup operations.
/// </summary>
public class CleanupOptions
{
    /// <summary>
    /// Remove variables that are declared but never used.
    /// </summary>
    public bool RemoveUnusedVariables { get; set; } = true;

    /// <summary>
    /// Remove using statements that are not referenced in the file.
    /// </summary>
    public bool RemoveUnusedUsings { get; set; } = true;

    /// <summary>
    /// Convert public fields to PascalCase properties.
    /// </summary>
    public bool ConvertPublicFieldsToProperties { get; set; } = true;

    /// <summary>
    /// Reorder private fields to the top of the class definition.
    /// </summary>
    public bool ReorderPrivateFields { get; set; } = true;

    /// <summary>
    /// Extract magic numbers to named constants.
    /// </summary>
    public bool ExtractMagicNumbers { get; set; } = false;

    /// <summary>
    /// Minimum occurrences of a magic number before extracting (default: 2).
    /// </summary>
    public int MagicNumberThreshold { get; set; } = 2;

    /// <summary>
    /// Simplify boolean expressions (e.g., "== true" → direct check).
    /// </summary>
    public bool SimplifyBooleanExpressions { get; set; } = false;

    /// <summary>
    /// Remove redundant code (empty blocks, unnecessary casts, etc.).
    /// </summary>
    public bool RemoveRedundantCode { get; set; } = false;

    /// <summary>
    /// Simplify string operations (use interpolation, IsNullOrEmpty, etc.).
    /// </summary>
    public bool SimplifyStringOperations { get; set; } = false;

    /// <summary>
    /// Convert simple methods to expression-bodied members.
    /// </summary>
    public bool UseExpressionBodiedMembers { get; set; } = false;

    /// <summary>
    /// Maximum number of cleanup operations per run to avoid overwhelming changes.
    /// Default is 1 for conservative, incremental cleanup.
    /// </summary>
    public int MaxOperationsPerRun { get; set; } = 1;
}
