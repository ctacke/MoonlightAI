namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings for workload execution.
/// </summary>
public class WorkloadConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Workload";

    /// <summary>
    /// Maximum number of files to process in a single batch run.
    /// Default is 10 files per batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;
}
