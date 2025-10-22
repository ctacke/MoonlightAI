namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Base class for all workloads.
/// </summary>
public abstract class Workload
{
    /// <summary>
    /// Unique identifier for this workload instance.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Repository URL to process.
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the workload.
    /// </summary>
    public WorkloadState State { get; set; } = WorkloadState.Queued;

    /// <summary>
    /// Statistics for this workload execution.
    /// </summary>
    public WorkloadStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Human-readable name for this workload type.
    /// </summary>
    public abstract string WorkloadType { get; }

    /// <summary>
    /// Gets the branch name pattern for this workload.
    /// Format: moonlight/{date}-{workload-type}
    /// </summary>
    public virtual string GetBranchName()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var type = WorkloadType.ToLowerInvariant().Replace(" ", "-");
        return $"moonlight/{date}-{type}";
    }
}
