namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Represents the execution state of a workload.
/// </summary>
public enum WorkloadState
{
    /// <summary>
    /// Workload is queued but not yet started.
    /// </summary>
    Queued,

    /// <summary>
    /// Workload is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Workload completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Workload failed with errors.
    /// </summary>
    Failed,

    /// <summary>
    /// Workload was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Workload timed out.
    /// </summary>
    TimedOut
}
