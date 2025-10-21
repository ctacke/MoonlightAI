namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Represents a C# source code file.
/// </summary>
public class CodeFile
{
    /// <summary>
    /// Full path to the file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the file (without path).
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Classes found in the file.
    /// </summary>
    public List<ClassInfo> Classes { get; set; } = new();

    /// <summary>
    /// Usings/imports at the top of the file.
    /// </summary>
    public List<string> Usings { get; set; } = new();

    /// <summary>
    /// Whether the file could be successfully parsed.
    /// </summary>
    public bool ParsedSuccessfully { get; set; }

    /// <summary>
    /// Parse errors if any.
    /// </summary>
    public List<string> ParseErrors { get; set; } = new();
}
