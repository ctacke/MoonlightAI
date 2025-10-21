using MoonlightAI.Core.Models.Analysis;

namespace MoonlightAI.Core;

/// <summary>
/// Interface for analyzing C# source code.
/// </summary>
public interface ICodeAnalyzer
{
    /// <summary>
    /// Analyzes all .cs files in a directory and its subdirectories.
    /// </summary>
    /// <param name="directoryPath">Path to the directory to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of analyzed code files.</returns>
    Task<IEnumerable<CodeFile>> AnalyzeDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a single C# file.
    /// </summary>
    /// <param name="filePath">Path to the .cs file to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analyzed code file information.</returns>
    Task<CodeFile> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets only the public classes from a code file.
    /// </summary>
    /// <param name="codeFile">The code file to filter.</param>
    /// <returns>Public classes only.</returns>
    IEnumerable<ClassInfo> GetPublicClasses(CodeFile codeFile);

    /// <summary>
    /// Gets only the public members (properties and methods) from a class.
    /// </summary>
    /// <param name="classInfo">The class to filter.</param>
    /// <returns>Public members only.</returns>
    IEnumerable<MemberInfo> GetPublicMembers(ClassInfo classInfo);
}
