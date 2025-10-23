using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;
using MoonlightAI.Core.Workloads;
using MoonlightAI.Core.Workloads.Runners;

namespace MoonlightAI.Core.Orchestration;

/// <summary>
/// Orchestrates workload execution with git operations.
/// </summary>
public class WorkloadOrchestrator
{
    private readonly ILogger<WorkloadOrchestrator> _logger;
    private readonly GitManager _gitManager;
    private readonly CodeDocWorkloadRunner _codeDocRunner;

    public WorkloadOrchestrator(
        ILogger<WorkloadOrchestrator> logger,
        GitManager gitManager,
        CodeDocWorkloadRunner codeDocRunner)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitManager = gitManager ?? throw new ArgumentNullException(nameof(gitManager));
        _codeDocRunner = codeDocRunner ?? throw new ArgumentNullException(nameof(codeDocRunner));
    }

    /// <summary>
    /// Executes a code documentation workload with full git workflow.
    /// </summary>
    public async Task<WorkloadResult> ExecuteWorkloadAsync(CodeDocWorkload workload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workload orchestration for {WorkloadType} on {RepositoryUrl}",
            workload.WorkloadType, workload.RepositoryUrl);

        workload.Statistics.QueuedAt = DateTime.UtcNow;

        try
        {
            // Step 1: Clone or pull the repository
            var repoConfig = new RepositoryConfiguration
            {
                RepositoryUrl = workload.RepositoryUrl
            };

            _logger.LogInformation("Step 1: Cloning/pulling repository...");
            var repositoryPath = await _gitManager.CloneOrPullAsync(repoConfig, cancellationToken);

            // Step 2: Check for existing PRs to avoid duplicate work
            _logger.LogInformation("Step 2: Checking for existing PRs...");
            var existingPRs = await _gitManager.GetExistingPullRequestsAsync(repoConfig, cancellationToken);
            var branchName = workload.GetBranchName();

            if (existingPRs.Contains(branchName))
            {
                _logger.LogWarning("PR already exists for branch {BranchName}, skipping workload", branchName);
                return new WorkloadResult
                {
                    Workload = workload,
                    State = WorkloadState.Cancelled,
                    Summary = $"PR already exists for branch {branchName}"
                };
            }

            // Step 3: Create a new branch for this workload
            _logger.LogInformation("Step 3: Creating branch {BranchName}...", branchName);
            await _gitManager.CreateBranchAsync(repositoryPath, branchName, cancellationToken);

            // Step 4: Execute the workload runner
            _logger.LogInformation("Step 4: Executing workload runner...");
            var result = await _codeDocRunner.ExecuteAsync(workload, repositoryPath, cancellationToken);
            result.BranchName = branchName;

            // Step 5: If successful and files were modified, commit and create PR
            if (result.IsSuccess && result.Statistics.FilesProcessed > 0)
            {
                _logger.LogInformation("Step 5: Committing changes...");

                if (string.IsNullOrEmpty(result.CommitMessage))
                {
                    throw new InvalidOperationException("Workload runner did not provide a commit message");
                }

                // Commit only the files that were modified by the workload
                await _gitManager.CommitChangesAsync(repositoryPath, result.CommitMessage, result.ModifiedFiles, cancellationToken);

                _logger.LogInformation("Step 6: Pushing branch...");
                await _gitManager.PushBranchAsync(repositoryPath, branchName, cancellationToken);

                _logger.LogInformation("Step 7: Creating pull request...");

                if (string.IsNullOrEmpty(result.PullRequestTitle) || string.IsNullOrEmpty(result.PullRequestBody))
                {
                    throw new InvalidOperationException("Workload runner did not provide PR title or body");
                }

                var prUrl = await _gitManager.CreatePullRequestAsync(
                    repoConfig,
                    branchName,
                    result.PullRequestTitle,
                    result.PullRequestBody,
                    cancellationToken);

                result.PullRequestUrl = prUrl;

                _logger.LogInformation("Workload orchestration completed successfully. PR: {PrUrl}", prUrl);
            }
            else if (result.IsSuccess && result.Statistics.FilesProcessed == 0)
            {
                _logger.LogInformation("Workload completed but no files were modified, skipping PR creation");
                result.Summary += " (no changes to commit)";
            }
            else
            {
                _logger.LogWarning("Workload failed or was cancelled, skipping git operations");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during workload orchestration");

            workload.State = WorkloadState.Failed;
            workload.Statistics.CompletedAt = DateTime.UtcNow;
            workload.Statistics.ErrorCount++;
            workload.Statistics.Errors.Add($"Orchestration error: {ex.Message}");

            return new WorkloadResult
            {
                Workload = workload,
                State = WorkloadState.Failed,
                Statistics = workload.Statistics,
                Summary = $"Orchestration failed: {ex.Message}"
            };
        }
    }
}
