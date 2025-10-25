namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Represents a field (including constants) in a class.
/// </summary>
public class FieldInfo : MemberInfo
{
    /// <inheritdoc/>
    public override string MemberType => "Field";

    /// <summary>
    /// Type of the field.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the field is const.
    /// </summary>
    public bool IsConst { get; set; }

    /// <summary>
    /// Whether the field is readonly.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Whether the field is static.
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// The field's initializer value if available.
    /// </summary>
    public string? InitializerValue { get; set; }
}
