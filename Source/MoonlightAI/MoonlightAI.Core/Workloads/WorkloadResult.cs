namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Result of a workload execution.
/// </summary>
public class WorkloadResult
{
    /// <summary>
    /// The workload that was executed.
    /// </summary>
    public required Workload Workload { get; init; }

    /// <summary>
    /// Final state of the workload.
    /// </summary>
    public WorkloadState State { get; set; }

    /// <summary>
    /// Statistics about the workload execution.
    /// </summary>
    public WorkloadStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Branch name created for this workload.
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// Pull request URL if created.
    /// </summary>
    public string? PullRequestUrl { get; set; }

    /// <summary>
    /// Commit SHA if changes were committed.
    /// </summary>
    public string? CommitSha { get; set; }

    /// <summary>
    /// Whether the workload was successful.
    /// </summary>
    public bool IsSuccess => State == WorkloadState.Completed;

    /// <summary>
    /// Summary message about the workload execution.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Commit message to use when committing changes.
    /// </summary>
    public string? CommitMessage { get; set; }

    /// <summary>
    /// Pull request title.
    /// </summary>
    public string? PullRequestTitle { get; set; }

    /// <summary>
    /// Pull request body/description.
    /// </summary>
    public string? PullRequestBody { get; set; }

    /// <summary>
    /// List of file paths (relative to repository root) that were modified by this workload.
    /// </summary>
    public List<string> ModifiedFiles { get; set; } = new();
}
