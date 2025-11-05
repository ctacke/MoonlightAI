using MoonlightAI.Core.Models;

namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Responsible for selecting files for workloads and preventing duplicate work.
/// </summary>
public interface IWorkloadScheduler
{
    /// <summary>
    /// Discovers files that need documentation workloads, excluding files already in open PRs.
    /// </summary>
    /// <param name="repositoryPath">Path to the local repository.</param>
    /// <param name="repoConfig">Repository configuration.</param>
    /// <param name="workloadType">Type of workload to schedule.</param>
    /// <param name="ignoreProjects">Optional set of project names to exclude from processing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file paths that need the specified workload.</returns>
    Task<List<string>> SelectFilesForWorkloadAsync(
        string repositoryPath,
        RepositoryConfiguration repoConfig,
        string workloadType,
        HashSet<string>? ignoreProjects = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files that are currently being processed in open pull requests for a specific workload type.
    /// </summary>
    /// <param name="repoConfig">Repository configuration.</param>
    /// <param name="workloadType">Type of workload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Set of file paths currently in open PRs.</returns>
    Task<HashSet<string>> GetFilesInOpenPullRequestsAsync(
        RepositoryConfiguration repoConfig,
        string workloadType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific file should be processed for a given workload type.
    /// </summary>
    /// <param name="filePath">Relative path to the file.</param>
    /// <param name="repositoryPath">Path to the local repository.</param>
    /// <param name="workloadType">Type of workload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file should be processed.</returns>
    Task<bool> ShouldProcessFileAsync(
        string filePath,
        string repositoryPath,
        string workloadType,
        CancellationToken cancellationToken = default);
}
