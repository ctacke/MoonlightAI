namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Represents a class in a code file.
/// </summary>
public class ClassInfo
{
    /// <summary>
    /// Name of the class.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Namespace the class belongs to.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Accessibility level (public, internal, etc).
    /// </summary>
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// XML documentation comment if present.
    /// </summary>
    public string? XmlDocumentation { get; set; }

    /// <summary>
    /// Line number where the class is defined.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Properties in the class.
    /// </summary>
    public List<PropertyInfo> Properties { get; set; } = new();

    /// <summary>
    /// Methods in the class.
    /// </summary>
    public List<MethodInfo> Methods { get; set; } = new();

    /// <summary>
    /// Fields (including constants) in the class.
    /// </summary>
    public List<FieldInfo> Fields { get; set; } = new();

    /// <summary>
    /// Events in the class.
    /// </summary>
    public List<EventInfo> Events { get; set; } = new();

    /// <summary>
    /// Whether the class is static.
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Whether the class is abstract.
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Base class name if present.
    /// </summary>
    public string? BaseClass { get; set; }

    /// <summary>
    /// Interfaces implemented by the class.
    /// </summary>
    public List<string> Interfaces { get; set; } = new();
}
