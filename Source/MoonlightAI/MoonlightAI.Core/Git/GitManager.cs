using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Models;
using Octokit;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

namespace MoonlightAI.Core.Git;

/// <summary>
/// Manages git operations and GitHub interactions.
/// </summary>
public class GitManager : IGitManager
{
    private readonly GitHubConfiguration _config;
    private readonly ILogger<GitManager> _logger;
    private readonly GitHubClient _githubClient;

    /// <summary>
    /// Initializes a new instance of the GitManager class.
    /// </summary>
    /// <param name="config">GitHub configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public GitManager(GitHubConfiguration config, ILogger<GitManager> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize GitHub client
        _githubClient = new GitHubClient(new ProductHeaderValue("MoonlightAI"))
        {
            Credentials = new Octokit.Credentials(_config.PersonalAccessToken)
        };
    }

    /// <inheritdoc/>
    public async Task<string> CloneOrPullAsync(RepositoryConfiguration repository, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository.RepositoryUrl))
        {
            throw new ArgumentException("Repository URL cannot be null or empty.", nameof(repository));
        }

        // Ensure working directory exists
        if (!Directory.Exists(_config.WorkingDirectory))
        {
            Directory.CreateDirectory(_config.WorkingDirectory);
            _logger.LogInformation("Created working directory: {WorkingDirectory}", _config.WorkingDirectory);
        }

        var localPath = Path.Combine(_config.WorkingDirectory, repository.Name);
        repository.LocalPath = localPath;

        if (Directory.Exists(localPath) && Directory.Exists(Path.Combine(localPath, ".git")))
        {
            // Repository exists, pull latest changes
            _logger.LogInformation("Repository already exists at {LocalPath}, pulling latest changes...", localPath);
            await Task.Run(() => PullLatestChanges(localPath), cancellationToken);
        }
        else
        {
            // Clone the repository
            _logger.LogInformation("Cloning repository {RepositoryUrl} to {LocalPath}...", repository.RepositoryUrl, localPath);
            await Task.Run(() => CloneRepository(repository.RepositoryUrl, localPath), cancellationToken);
        }

        return localPath;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetExistingPullRequestsAsync(RepositoryConfiguration repository, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching open pull requests for {Owner}/{Name}...", repository.Owner, repository.Name);

            var pullRequests = await _githubClient.PullRequest.GetAllForRepository(
                repository.Owner,
                repository.Name,
                new PullRequestRequest { State = ItemStateFilter.Open });

            var branchNames = pullRequests.Select(pr => pr.Head.Ref).ToList();

            _logger.LogInformation("Found {Count} open pull requests", branchNames.Count);
            return branchNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch pull requests for {Owner}/{Name}", repository.Owner, repository.Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> CreateBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var repo = new Repository(repositoryPath);

                // Check for uncommitted changes and reset them
                var status = repo.RetrieveStatus();
                if (status.IsDirty)
                {
                    _logger.LogWarning("Working directory has uncommitted changes. Resetting to clean state before creating branch.");

                    // Reset all changes to match HEAD
                    repo.Reset(ResetMode.Hard);

                    // Clean untracked files
                    var untrackedFiles = status.Untracked.Select(item => item.FilePath).ToList();
                    foreach (var file in untrackedFiles)
                    {
                        var fullPath = Path.Combine(repositoryPath, file);
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                            _logger.LogDebug("Deleted untracked file: {FilePath}", file);
                        }
                    }

                    _logger.LogInformation("Reset working directory to clean state");
                }

                // Check if branch already exists
                var existingBranch = repo.Branches[branchName];
                if (existingBranch != null)
                {
                    _logger.LogWarning("Branch {BranchName} already exists, checking it out", branchName);
                    Commands.Checkout(repo, existingBranch);
                    return true;
                }

                // Create and checkout new branch
                var branch = repo.CreateBranch(branchName);
                Commands.Checkout(repo, branch);

                _logger.LogInformation("Created and checked out branch: {BranchName}", branchName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create branch {BranchName} in {RepositoryPath}", branchName, repositoryPath);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CommitChangesAsync(string repositoryPath, string message, IEnumerable<string>? filePaths = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var repo = new Repository(repositoryPath);

                // Stage specified files or all changes
                if (filePaths != null && filePaths.Any())
                {
                    foreach (var filePath in filePaths)
                    {
                        Commands.Stage(repo, filePath);
                        _logger.LogDebug("Staged file: {FilePath}", filePath);
                    }
                }
                else
                {
                    Commands.Stage(repo, "*");
                    _logger.LogDebug("Staged all changes");
                }

                // Check if there are any changes to commit
                var status = repo.RetrieveStatus();
                if (!status.IsDirty)
                {
                    _logger.LogInformation("No changes to commit in {RepositoryPath}", repositoryPath);
                    return;
                }

                // Create signature
                var signature = new Signature(_config.UserName, _config.UserEmail, DateTimeOffset.Now);

                // Commit changes
                var commit = repo.Commit(message, signature, signature);

                _logger.LogInformation("Committed changes: {CommitSha} - {Message}", commit.Sha, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit changes in {RepositoryPath}", repositoryPath);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PushBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var repo = new Repository(repositoryPath);

                var branch = repo.Branches[branchName];
                if (branch == null)
                {
                    throw new InvalidOperationException($"Branch {branchName} does not exist");
                }

                var remote = repo.Network.Remotes["origin"];
                var options = new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = _config.PersonalAccessToken,
                            Password = string.Empty
                        }
                };

                _logger.LogInformation("Pushing branch {BranchName} to remote...", branchName);

                // Push using the refspec format to set up tracking
                var pushRefSpec = $"refs/heads/{branchName}:refs/heads/{branchName}";
                repo.Network.Push(remote, pushRefSpec, options);

                // Set up branch tracking
                repo.Branches.Update(branch, b => b.Remote = remote.Name, b => b.UpstreamBranch = branch.CanonicalName);

                _logger.LogInformation("Successfully pushed branch: {BranchName}", branchName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push branch {BranchName} in {RepositoryPath}", branchName, repositoryPath);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> CreatePullRequestAsync(RepositoryConfiguration repository, string branchName, string title, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating pull request for branch {BranchName} in {Owner}/{Name}...", branchName, repository.Owner, repository.Name);
            _logger.LogInformation("PR Details - Head: {Head}, Base: {Base}, Title: {Title}", branchName, _config.DefaultBranch, title);

            var newPr = new NewPullRequest(title, branchName, _config.DefaultBranch)
            {
                Body = body
            };

            var pullRequest = await _githubClient.PullRequest.Create(repository.Owner, repository.Name, newPr);

            _logger.LogInformation("Created pull request #{Number}: {Url}", pullRequest.Number, pullRequest.HtmlUrl);
            return pullRequest.HtmlUrl;
        }
        catch (Octokit.ApiValidationException validationEx)
        {
            _logger.LogError("GitHub API validation failed for PR creation:");
            _logger.LogError("  Branch: {Branch}", branchName);
            _logger.LogError("  Base: {Base}", _config.DefaultBranch);
            _logger.LogError("  Repo: {Owner}/{Name}", repository.Owner, repository.Name);
            _logger.LogError("  Message: {Message}", validationEx.Message);
            if (validationEx.ApiError?.Errors != null)
            {
                foreach (var error in validationEx.ApiError.Errors)
                {
                    _logger.LogError("  Error: {Resource} - {Field}: {Code} - {Message}",
                        error.Resource, error.Field, error.Code, error.Message);
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create pull request for branch {BranchName}", branchName);
            throw;
        }
    }

    private void CloneRepository(string url, string path)
    {
        var options = new CloneOptions();
        options.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials
            {
                Username = _config.PersonalAccessToken,
                Password = string.Empty
            };

        Repository.Clone(url, path, options);
        _logger.LogInformation("Successfully cloned repository to {Path}", path);
    }

    private void PullLatestChanges(string repositoryPath)
    {
        using var repo = new Repository(repositoryPath);

        // Check for uncommitted changes and reset them
        var status = repo.RetrieveStatus();
        if (status.IsDirty)
        {
            _logger.LogWarning("Working directory has uncommitted changes. Resetting to clean state before pulling.");

            // Reset all changes to match HEAD
            repo.Reset(ResetMode.Hard);

            // Clean untracked files
            var untrackedFiles = status.Untracked.Select(item => item.FilePath).ToList();
            foreach (var file in untrackedFiles)
            {
                var fullPath = Path.Combine(repositoryPath, file);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogDebug("Deleted untracked file: {FilePath}", file);
                }
            }

            _logger.LogInformation("Reset working directory to clean state");
        }

        // Fetch from remote
        var remote = repo.Network.Remotes["origin"];
        var fetchOptions = new FetchOptions
        {
            CredentialsProvider = (url, usernameFromUrl, types) =>
                new UsernamePasswordCredentials
                {
                    Username = _config.PersonalAccessToken,
                    Password = string.Empty
                }
        };

        Commands.Fetch(repo, remote.Name, Array.Empty<string>(), fetchOptions, null);

        // Checkout default branch
        var defaultBranch = repo.Branches[_config.DefaultBranch] ?? repo.Branches.FirstOrDefault();
        if (defaultBranch != null)
        {
            Commands.Checkout(repo, defaultBranch);

            // Pull changes (merge)
            var signature = new Signature(_config.UserName, _config.UserEmail, DateTimeOffset.Now);
            var pullOptions = new PullOptions
            {
                FetchOptions = fetchOptions
            };

            Commands.Pull(repo, signature, pullOptions);
            _logger.LogInformation("Successfully pulled latest changes");
        }
    }

    /// <inheritdoc/>
    public async Task RevertFileAsync(string repositoryPath, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            throw new ArgumentException("Repository path cannot be null or empty.", nameof(repositoryPath));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        _logger.LogInformation("Reverting file: {FilePath}", filePath);

        using var repo = new Repository(repositoryPath);

        // Check if file exists in the repository
        var fullPath = Path.Combine(repositoryPath, filePath);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found, cannot revert: {FilePath}", filePath);
            return;
        }

        // Restore file to HEAD state (undo uncommitted changes)
        var checkoutOptions = new CheckoutOptions
        {
            CheckoutModifiers = CheckoutModifiers.Force
        };

        repo.CheckoutPaths("HEAD", new[] { filePath }, checkoutOptions);

        _logger.LogInformation("Successfully reverted file: {FilePath}", filePath);

        await Task.CompletedTask;
    }
}
