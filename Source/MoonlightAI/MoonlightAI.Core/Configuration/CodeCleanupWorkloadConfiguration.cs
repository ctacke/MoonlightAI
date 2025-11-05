using MoonlightAI.Core.Workloads;

namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings specific to Code Cleanup workloads.
/// </summary>
public class CodeCleanupWorkloadConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Workload:CodeCleanup";

    /// <summary>
    /// Cleanup operation options.
    /// </summary>
    public CleanupOptions Options { get; set; } = new();
}
