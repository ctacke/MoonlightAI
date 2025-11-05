namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings specific to Code Documentation workloads.
/// </summary>
public class CodeDocWorkloadConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Workload:CodeDocumentation";

    /// <summary>
    /// Visibility level of members to document (Public, Internal, All).
    /// Default is Public.
    /// </summary>
    public string DocumentVisibility { get; set; } = "Public";
}
