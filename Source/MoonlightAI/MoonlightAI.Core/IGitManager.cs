using MoonlightAI.Core.Models;

namespace MoonlightAI.Core;

/// <summary>
/// Interface for managing git operations and GitHub interactions.
/// </summary>
public interface IGitManager
{
    /// <summary>
    /// Clones a repository if it doesn't exist locally, or pulls latest changes if it does.
    /// </summary>
    /// <param name="repository">Repository configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Local path to the cloned/updated repository.</returns>
    Task<string> CloneOrPullAsync(RepositoryConfiguration repository, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of open pull request branch names for the repository.
    /// </summary>
    /// <param name="repository">Repository configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of branch names that have open pull requests.</returns>
    Task<IEnumerable<string>> GetExistingPullRequestsAsync(RepositoryConfiguration repository, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new branch in the local repository.
    /// </summary>
    /// <param name="repositoryPath">Local path to the repository.</param>
    /// <param name="branchName">Name of the branch to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if branch was created successfully.</returns>
    Task<bool> CreateBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits specific files or all changes in the working directory.
    /// </summary>
    /// <param name="repositoryPath">Local path to the repository.</param>
    /// <param name="message">Commit message.</param>
    /// <param name="filePaths">Optional list of file paths to commit (relative to repository root). If null or empty, commits all changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommitChangesAsync(string repositoryPath, string message, IEnumerable<string>? filePaths = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a branch to the remote repository.
    /// </summary>
    /// <param name="repositoryPath">Local path to the repository.</param>
    /// <param name="branchName">Name of the branch to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PushBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a pull request on GitHub.
    /// </summary>
    /// <param name="repository">Repository configuration.</param>
    /// <param name="branchName">Source branch name.</param>
    /// <param name="title">Pull request title.</param>
    /// <param name="body">Pull request description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>URL of the created pull request.</returns>
    Task<string> CreatePullRequestAsync(RepositoryConfiguration repository, string branchName, string title, string body, CancellationToken cancellationToken = default);
}
