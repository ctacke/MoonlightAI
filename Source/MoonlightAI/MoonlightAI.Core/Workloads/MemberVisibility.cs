namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Visibility flags for determining which members to document.
/// </summary>
[Flags]
public enum MemberVisibility
{
    /// <summary>
    /// No visibility (none selected).
    /// </summary>
    None = 0,

    /// <summary>
    /// Public members.
    /// </summary>
    Public = 1,

    /// <summary>
    /// Private members.
    /// </summary>
    Private = 2,

    /// <summary>
    /// Protected members.
    /// </summary>
    Protected = 4,

    /// <summary>
    /// Internal members.
    /// </summary>
    Internal = 8,

    /// <summary>
    /// Protected internal members.
    /// </summary>
    ProtectedInternal = 16,

    /// <summary>
    /// Private protected members.
    /// </summary>
    PrivateProtected = 32
}
