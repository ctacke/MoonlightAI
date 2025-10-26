namespace MoonlightAI.Core.Models;

/// <summary>
/// Result of a build operation.
/// </summary>
public class BuildResult
{
    /// <summary>
    /// Whether the build succeeded without errors.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// List of build errors.
    /// </summary>
    public List<BuildError> Errors { get; set; } = new();

    /// <summary>
    /// List of build warnings.
    /// </summary>
    public List<BuildWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Raw output from the build process.
    /// </summary>
    public string RawOutput { get; set; } = string.Empty;

    /// <summary>
    /// How long the build took.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Represents a build error.
/// </summary>
public class BuildError
{
    /// <summary>
    /// File path where the error occurred (relative to repository root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the error occurred.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Compiler error code (e.g., CS1002).
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Full error line from build output.
    /// </summary>
    public string FullText { get; set; } = string.Empty;
}

/// <summary>
/// Represents a build warning.
/// </summary>
public class BuildWarning
{
    /// <summary>
    /// File path where the warning occurred (relative to repository root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the warning occurred.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Compiler warning code (e.g., CS0168).
    /// </summary>
    public string WarningCode { get; set; } = string.Empty;

    /// <summary>
    /// Warning message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Full warning line from build output.
    /// </summary>
    public string FullText { get; set; } = string.Empty;
}
