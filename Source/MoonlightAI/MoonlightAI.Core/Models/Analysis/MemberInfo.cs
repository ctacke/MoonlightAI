namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Base class for code members (properties, methods, etc).
/// </summary>
public abstract class MemberInfo
{
    /// <summary>
    /// Name of the member.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Accessibility level (public, private, protected, internal).
    /// </summary>
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// XML documentation comment if present.
    /// </summary>
    public string? XmlDocumentation { get; set; }

    /// <summary>
    /// Line number where the member is defined.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Type of the member (Property, Method, Field, etc).
    /// </summary>
    public abstract string MemberType { get; }
}
