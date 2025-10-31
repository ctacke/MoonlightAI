namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Types of code cleanup operations that can be performed.
/// </summary>
public enum CleanupType
{
    /// <summary>
    /// Remove variables that are declared but never used.
    /// </summary>
    UnusedVariable,

    /// <summary>
    /// Remove using statements that are not referenced in the file.
    /// </summary>
    UnusedUsing,

    /// <summary>
    /// Convert public fields to PascalCase properties.
    /// </summary>
    PublicFieldToProperty,

    /// <summary>
    /// Reorder private fields to the top of the class definition.
    /// </summary>
    ReorderPrivateFields,

    /// <summary>
    /// Extract magic numbers to named constants.
    /// </summary>
    ExtractMagicNumber,

    /// <summary>
    /// Simplify boolean expressions (e.g., "== true" → direct check).
    /// </summary>
    SimplifyBooleanExpression,

    /// <summary>
    /// Remove redundant code (empty blocks, unnecessary casts, etc.).
    /// </summary>
    RemoveRedundantCode,

    /// <summary>
    /// Simplify string operations (use interpolation, IsNullOrEmpty, etc.).
    /// </summary>
    SimplifyStringOperation,

    /// <summary>
    /// Convert simple methods to expression-bodied members.
    /// </summary>
    UseExpressionBodiedMember
}
