namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Represents a property in a class.
/// </summary>
public class PropertyInfo : MemberInfo
{
    /// <summary>
    /// Type of the property.
    /// </summary>
    public string PropertyType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the property has a getter.
    /// </summary>
    public bool HasGetter { get; set; }

    /// <summary>
    /// Whether the property has a setter.
    /// </summary>
    public bool HasSetter { get; set; }

    /// <inheritdoc/>
    public override string MemberType => "Property";
}
