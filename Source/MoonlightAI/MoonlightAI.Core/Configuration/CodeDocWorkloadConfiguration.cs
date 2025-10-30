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
    /// Path to the solution file that needs to build for success (relative to repository root).
    /// Example: "src/SolutionEngine.slnx" or "MySolution.sln"
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the specific project file of interest within the solution (relative to repository root).
    /// Only files in this project will be processed by the workload.
    /// Example: "src/Engine/Modules/MQTT/SolutionEngine.MQTT.Module/SolutionEngine.MQTT.Module.csproj"
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Visibility level of members to document (Public, Internal, All).
    /// Default is Public.
    /// </summary>
    public string DocumentVisibility { get; set; } = "Public";
}
