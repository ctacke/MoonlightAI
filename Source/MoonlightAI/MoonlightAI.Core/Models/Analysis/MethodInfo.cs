namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Represents a method in a class.
/// </summary>
public class MethodInfo : MemberInfo
{
    /// <summary>
    /// Return type of the method.
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Parameters of the method.
    /// </summary>
    public List<ParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Whether the method is async.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <inheritdoc/>
    public override string MemberType => "Method";
}

/// <summary>
/// Represents a parameter in a method.
/// </summary>
public class ParameterInfo
{
    /// <summary>
    /// Name of the parameter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of the parameter.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the parameter has a default value.
    /// </summary>
    public bool HasDefaultValue { get; set; }

    /// <summary>
    /// Default value if present.
    /// </summary>
    public string? DefaultValue { get; set; }
}
