namespace MoonlightAI.Core.Models.Analysis;

/// <summary>
/// Represents an event in a class.
/// </summary>
public class EventInfo : MemberInfo
{
    /// <summary>
    /// Type of the event (delegate type).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string MemberType => "Event";
}
