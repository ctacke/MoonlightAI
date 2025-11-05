using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Containerization;
using MoonlightAI.Core.Data;
using MoonlightAI.Core.Data.Models;
using MoonlightAI.Core.Models;
using MoonlightAI.Core.Workloads;
using MoonlightAI.Core.Workloads.Runners;

namespace MoonlightAI.Core.Orchestration;

/// <summary>
/// Orchestrates workload execution with git operations and container management.
/// </summary>
public class WorkloadOrchestrator
{
    private readonly ILogger<WorkloadOrchestrator> _logger;
    private readonly IGitManager _gitManager;
    private readonly CodeDocWorkloadRunner _codeDocRunner;
    private readonly CodeCleanupWorkloadRunner _codeCleanupRunner;
    private readonly IContainerManager _containerManager;
    private readonly IAIServer _aiServer;
    private readonly AIServerConfiguration _aiServerConfig;
    private readonly IWorkloadScheduler _workloadScheduler;
    private readonly WorkloadConfiguration _workloadConfig;
    private readonly IServiceProvider _serviceProvider;

    public WorkloadOrchestrator(
        ILogger<WorkloadOrchestrator> logger,
        IGitManager gitManager,
        CodeDocWorkloadRunner codeDocRunner,
        CodeCleanupWorkloadRunner codeCleanupRunner,
        IContainerManager containerManager,
        IAIServer aiServer,
        AIServerConfiguration aiServerConfig,
        IWorkloadScheduler workloadScheduler,
        WorkloadConfiguration workloadConfig,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitManager = gitManager ?? throw new ArgumentNullException(nameof(gitManager));
        _codeDocRunner = codeDocRunner ?? throw new ArgumentNullException(nameof(codeDocRunner));
        _codeCleanupRunner = codeCleanupRunner ?? throw new ArgumentNullException(nameof(codeCleanupRunner));
        _containerManager = containerManager ?? throw new ArgumentNullException(nameof(containerManager));
        _aiServer = aiServer ?? throw new ArgumentNullException(nameof(aiServer));
        _aiServerConfig = aiServerConfig ?? throw new ArgumentNullException(nameof(aiServerConfig));
        _workloadScheduler = workloadScheduler ?? throw new ArgumentNullException(nameof(workloadScheduler));
        _workloadConfig = workloadConfig ?? throw new ArgumentNullException(nameof(workloadConfig));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Executes a code documentation workload with full git workflow and container management.
    /// </summary>
    public async Task<WorkloadResult> ExecuteWorkloadAsync(CodeDocWorkload workload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workload orchestration for {WorkloadType} on {RepositoryUrl}",
            workload.WorkloadType, workload.RepositoryUrl);

        workload.Statistics.QueuedAt = DateTime.UtcNow;

        try
        {
            // Step 0: Ensure container is running if configured
            _logger.LogInformation("Step 0: Ensuring AI container is running...");
            var containerReady = await _containerManager.EnsureContainerRunningAsync(cancellationToken);
            if (!containerReady)
            {
                _logger.LogError("AI container is not running and could not be started");
                return new WorkloadResult
                {
                    Workload = workload,
                    State = WorkloadState.Failed,
                    Summary = "AI container is not running and could not be started"
                };
            }

            // Step 0.5: Wait for AI server to be healthy with retries
            _logger.LogInformation("Step 0.5: Verifying AI server health...");
            var serverHealthy = await WaitForAIServerHealthAsync(cancellationToken);
            if (!serverHealthy)
            {
                _logger.LogError("AI server is not responding after multiple health check attempts");
                return new WorkloadResult
                {
                    Workload = workload,
                    State = WorkloadState.Failed,
                    Summary = "AI server is not responding to health checks"
                };
            }

            // Step 0.6: Verify AI model is available
            _logger.LogInformation("Step 0.6: Verifying AI model availability...");
            var modelValidation = await ValidateModelAvailabilityAsync(cancellationToken);
            if (!modelValidation.IsValid)
            {
                _logger.LogError("AI model validation failed: {Error}", modelValidation.ErrorMessage);
                return new WorkloadResult
                {
                    Workload = workload,
                    State = WorkloadState.Failed,
                    Summary = modelValidation.ErrorMessage
                };
            }

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

    /// <summary>
    /// Executes code documentation workload by discovering files and creating individual workloads per file.
    /// All workloads are executed serially on a single git branch.
    /// Uses configuration from WorkloadConfiguration.CodeDocumentation for solution and project paths.
    /// Parameters can override configuration if provided.
    /// </summary>
    public async Task<BatchWorkloadResult> ExecuteCodeDocumentationAsync(
        string repositoryUrl,
        string? projectPath = null,
        string? solutionPath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WorkloadResult>();
        var batchResult = new BatchWorkloadResult { WorkloadResults = results };

        // Use configuration values if parameters not provided
        var effectiveProjectPath = projectPath ?? _workloadConfig.ProjectPath;
        var effectiveSolutionPath = solutionPath ?? _workloadConfig.SolutionPath;

        _logger.LogInformation("Starting code documentation for repository {RepositoryUrl}", repositoryUrl);
        _logger.LogInformation("Using project path: {ProjectPath}", effectiveProjectPath);
        _logger.LogInformation("Using solution path: {SolutionPath}", effectiveSolutionPath);

        // Variables for database tracking and workload execution
        WorkloadRunRecord? runRecord = null;
        IServiceScope? scope = null;
        IDataService? dataService = null;
        List<string> allFilesToDocument = new();
        List<string> filesToDocument = new();
        string branchName = string.Empty;

        try
        {
            // Start database tracking
            scope = _serviceProvider.CreateScope();
            dataService = scope.ServiceProvider.GetRequiredService<IDataService>();

            var configJson = JsonSerializer.Serialize(new
            {
                BatchSize = _workloadConfig.BatchSize,
                ValidateBuilds = _workloadConfig.ValidateBuilds,
                MaxBuildRetries = _workloadConfig.MaxBuildRetries,
                RevertOnBuildFailure = _workloadConfig.RevertOnBuildFailure
            });

            runRecord = await dataService.StartRunAsync(
                workloadType: "CodeDocumentation",
                repositoryUrl: repositoryUrl,
                branchName: $"moonlight/{DateTime.UtcNow:yyyy-MM-dd-HHmmss}-code-documentation",
                modelName: _aiServerConfig.ModelName,
                serverUrl: _aiServerConfig.ServerUrl,
                configurationJson: configJson,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Started database tracking for run {RunId}", runRecord.RunId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start database tracking, continuing without it");
        }

        try
        {
            // Step 0: Ensure container is running
            _logger.LogInformation("Step 0: Ensuring AI container is running...");
            var containerReady = await _containerManager.EnsureContainerRunningAsync(cancellationToken);
            if (!containerReady)
            {
                _logger.LogError("AI container is not running and could not be started");
                batchResult.Success = false;
                batchResult.Summary = "AI container is not running and could not be started";
                return batchResult;
            }

            // Step 0.5: Wait for AI server health
            _logger.LogInformation("Step 0.5: Verifying AI server health...");
            var serverHealthy = await WaitForAIServerHealthAsync(cancellationToken);
            if (!serverHealthy)
            {
                _logger.LogError("AI server is not responding");
                batchResult.Success = false;
                batchResult.Summary = "AI server is not responding to health checks";
                return batchResult;
            }

            // Step 0.55: Ensure model is available (download if needed)
            _logger.LogInformation("Step 0.55: Ensuring model '{ModelName}' is available...", _aiServerConfig.ModelName);
            var modelDownloaded = await _containerManager.EnsureModelAvailableAsync(_aiServerConfig.ModelName, cancellationToken);
            if (!modelDownloaded)
            {
                _logger.LogError("Failed to ensure model '{ModelName}' is available", _aiServerConfig.ModelName);
                batchResult.Success = false;
                batchResult.Summary = $"Failed to download or verify model: {_aiServerConfig.ModelName}";
                return batchResult;
            }

            // Step 0.6: Verify model availability
            _logger.LogInformation("Step 0.6: Verifying AI model availability...");
            var modelValidation = await ValidateModelAvailabilityAsync(cancellationToken);
            if (!modelValidation.IsValid)
            {
                _logger.LogError("AI model validation failed");
                batchResult.Success = false;
                batchResult.Summary = modelValidation.ErrorMessage;
                return batchResult;
            }

            // Step 1: Clone or pull repository
            var repoConfig = new RepositoryConfiguration { RepositoryUrl = repositoryUrl };
            _logger.LogInformation("Step 1: Cloning/pulling repository...");
            var repositoryPath = await _gitManager.CloneOrPullAsync(repoConfig, cancellationToken);

            // Step 2: Use scheduler to select files that need documentation
            _logger.LogInformation("Step 2: Selecting files needing documentation using scheduler...");
            allFilesToDocument = await _workloadScheduler.SelectFilesForWorkloadAsync(
                repositoryPath,
                repoConfig,
                "codedoc",
                effectiveProjectPath,
                cancellationToken);

            if (!allFilesToDocument.Any())
            {
                _logger.LogInformation("No files found to document");
                batchResult.Success = true;
                batchResult.Summary = "No files found to document";
                return batchResult;
            }

            // Step 2.5: Process files until we reach batch size of MODIFIED files
            // We'll process more files than the batch size if needed to account for files that don't need changes
            _logger.LogInformation("Target: {BatchSize} modified files from {TotalCount} available files",
                _workloadConfig.BatchSize, allFilesToDocument.Count);

            // Step 3: Create single branch for all workloads with timestamp for uniqueness
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
            branchName = $"moonlight/{timestamp}-code-documentation";
            _logger.LogInformation("Step 3: Creating branch {BranchName}...", branchName);

            await _gitManager.CreateBranchAsync(repositoryPath, branchName, cancellationToken);

            var allModifiedFiles = new List<string>();
            var successfulWorkloads = 0;

            // Step 4: Execute workloads serially until we reach batch size of modified files
            _logger.LogInformation("Step 4: Processing files serially until {BatchSize} files are modified...", _workloadConfig.BatchSize);

            // Report initial batch size to progress callback (using -1 to indicate batch total)
            progress?.Report(-_workloadConfig.BatchSize);

            int fileIndex = 0;
            while (fileIndex < allFilesToDocument.Count &&
                   results.Count(r => r.IsSuccess && r.ModifiedFiles.Any()) < _workloadConfig.BatchSize)
            {
                var filePath = allFilesToDocument[fileIndex];
                fileIndex++;

                // Create workload for this file
                var workload = new CodeDocWorkload
                {
                    RepositoryUrl = repositoryUrl,
                    ProjectPath = effectiveProjectPath,
                    SolutionPath = effectiveSolutionPath,
                    FilePath = filePath,
                    DocumentVisibility = ParseDocumentVisibility(_workloadConfig.CodeDocumentation.DocumentVisibility)
                };

                try
                {
                    var currentModifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    _logger.LogInformation("Processing file {FilePath} (evaluated: {Evaluated}, modified: {Modified}/{Target})",
                        filePath, fileIndex, currentModifiedCount, _workloadConfig.BatchSize);

                    // Execute workload (serial execution - one at a time)
                    var result = await _codeDocRunner.ExecuteAsync(workload, repositoryPath, cancellationToken);
                    result.BranchName = branchName;
                    results.Add(result);

                    if (result.IsSuccess && result.ModifiedFiles.Any())
                    {
                        allModifiedFiles.AddRange(result.ModifiedFiles);
                        successfulWorkloads++;
                    }

                    // Update database after each file completes to prevent data loss
                    // Only count files that were actually modified
                    if (runRecord != null && dataService != null)
                    {
                        try
                        {
                            runRecord.FilesSuccessful = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                            runRecord.FilesFailed = results.Count(r => !r.IsSuccess);
                            runRecord.FilesSkipped = results.Count(r => r.IsSuccess && !r.ModifiedFiles.Any());
                            runRecord.TotalBuildFailures = results.Sum(r => r.Statistics?.BuildFailures ?? 0);
                            runRecord.TotalBuildRetries = results.Sum(r => r.Statistics?.BuildRetries ?? 0);
                            runRecord.TotalPromptTokens = results.Sum(r => r.Statistics?.TotalPromptTokens ?? 0);
                            runRecord.TotalResponseTokens = results.Sum(r => r.Statistics?.TotalResponseTokens ?? 0);
                            runRecord.TotalItemsDocumented = results.Sum(r => r.Statistics?.ItemsModified ?? 0);
                            runRecord.TotalSanitizationFixes = results.Sum(r => r.Statistics?.TotalSanitizationFixes ?? 0);

                            await dataService.UpdateRunAsync(runRecord, cancellationToken);
                            _logger.LogDebug("Updated database after evaluating {Evaluated} files", results.Count);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogWarning(dbEx, "Failed to update database after file completion, will retry at end");
                        }
                    }

                    // Report progress after each file completes (only count modified files)
                    currentModifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    progress?.Report(currentModifiedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing workload for file {FilePath}", filePath);
                    results.Add(new WorkloadResult
                    {
                        Workload = workload,
                        State = WorkloadState.Failed,
                        Summary = $"Workload execution failed: {ex.Message}"
                    });

                    // Update database even for failed files
                    if (runRecord != null && dataService != null)
                    {
                        try
                        {
                            runRecord.FilesSuccessful = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                            runRecord.FilesFailed = results.Count(r => !r.IsSuccess);
                            runRecord.FilesSkipped = results.Count(r => r.IsSuccess && !r.ModifiedFiles.Any());
                            runRecord.TotalBuildFailures = results.Sum(r => r.Statistics?.BuildFailures ?? 0);
                            runRecord.TotalBuildRetries = results.Sum(r => r.Statistics?.BuildRetries ?? 0);
                            runRecord.TotalPromptTokens = results.Sum(r => r.Statistics?.TotalPromptTokens ?? 0);
                            runRecord.TotalResponseTokens = results.Sum(r => r.Statistics?.TotalResponseTokens ?? 0);
                            runRecord.TotalItemsDocumented = results.Sum(r => r.Statistics?.ItemsModified ?? 0);
                            runRecord.TotalSanitizationFixes = results.Sum(r => r.Statistics?.TotalSanitizationFixes ?? 0);

                            await dataService.UpdateRunAsync(runRecord, cancellationToken);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogWarning(dbEx, "Failed to update database after file failure");
                        }
                    }

                    // Report progress (only count modified files)
                    var modifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    progress?.Report(modifiedCount);
                }

                // Check for cancellation after each file completes
                if (cancellationToken.IsCancellationRequested)
                {
                    var modifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    _logger.LogWarning("Workload cancellation requested. Evaluated {Evaluated} files, modified {Modified}/{Target}. Saving completed work...",
                        results.Count, modifiedCount, _workloadConfig.BatchSize);
                    break;
                }
            }

            // Step 5: Commit all changes if any workloads succeeded
            if (allModifiedFiles.Any())
            {
                _logger.LogInformation("Step 5: Committing {Count} modified files from {SuccessCount} workloads",
                    allModifiedFiles.Distinct().Count(), successfulWorkloads);

                var commitMessage = $"Add XML documentation - {DateTime.UtcNow:yyyy-MM-dd}\n\n" +
                                  $"Processed {successfulWorkloads} file(s)\n" +
                                  $"Modified {allModifiedFiles.Distinct().Count()} file(s)";

                await _gitManager.CommitChangesAsync(repositoryPath, commitMessage, allModifiedFiles.Distinct(), cancellationToken);

                _logger.LogInformation("Step 6: Pushing branch...");
                await _gitManager.PushBranchAsync(repositoryPath, branchName, cancellationToken);

                _logger.LogInformation("Step 7: Creating pull request...");
                try
                {
                    var totalItemsDocumented = results.Sum(r => r.Statistics?.ItemsModified ?? 0);
                    var filesEvaluated = results.Count;
                    var prTitle = $"[MoonlightAI] Add XML Documentation - {DateTime.UtcNow:yyyy-MM-dd}";
                    var prBody = $"Automated XML documentation added by MoonlightAI\n\n" +
                                $"**Files documented:** {successfulWorkloads} (evaluated {filesEvaluated} files)\n" +
                                $"**Items documented:** {totalItemsDocumented} (methods, properties, fields, classes)\n" +
                                $"**Total changes:** {allModifiedFiles.Distinct().Count()} file(s)\n";

                    var prUrl = await _gitManager.CreatePullRequestAsync(repoConfig, branchName, prTitle, prBody, cancellationToken);
                    batchResult.PullRequestUrl = prUrl;

                    _logger.LogInformation("Code documentation completed successfully. PR: {PrUrl}", prUrl);
                }
                catch (Exception prEx)
                {
                    _logger.LogError(prEx, "Failed to create pull request. Branch {BranchName} was pushed successfully but PR creation failed", branchName);
                    batchResult.Success = false;
                    batchResult.Summary += $" (PR creation failed: {prEx.Message})";
                }
            }
            else
            {
                _logger.LogInformation("No files were modified, skipping commit and PR");
            }

            batchResult.Success = results.Any(r => r.IsSuccess && r.ModifiedFiles.Any());
            var filesModified = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
            var filesNoChange = results.Count(r => r.IsSuccess && !r.ModifiedFiles.Any());
            var filesFailed = results.Count(r => !r.IsSuccess);
            batchResult.Summary = $"Modified {filesModified} file(s), {filesNoChange} already complete, {filesFailed} failed (evaluated {results.Count} files)";

            // Update database tracking with final results
            if (runRecord != null && dataService != null)
            {
                try
                {
                    runRecord.EndTime = DateTime.UtcNow;
                    runRecord.Success = batchResult.Success;
                    runRecord.TotalFilesDiscovered = allFilesToDocument.Count;
                    runRecord.FilesSelected = results.Count;  // Files actually evaluated
                    runRecord.FilesSuccessful = filesModified;  // Only count files that were actually modified
                    runRecord.FilesFailed = filesFailed;
                    runRecord.FilesSkipped = allFilesToDocument.Count - results.Count + filesNoChange;  // Files not evaluated + files already complete
                    runRecord.TotalBuildFailures = results.Sum(r => r.Statistics?.BuildFailures ?? 0);
                    runRecord.TotalBuildRetries = results.Sum(r => r.Statistics?.BuildRetries ?? 0);
                    runRecord.TotalPromptTokens = results.Sum(r => r.Statistics?.TotalPromptTokens ?? 0);
                    runRecord.TotalResponseTokens = results.Sum(r => r.Statistics?.TotalResponseTokens ?? 0);
                    runRecord.TotalItemsDocumented = results.Sum(r => r.Statistics?.ItemsModified ?? 0);
                    runRecord.TotalSanitizationFixes = results.Sum(r => r.Statistics?.TotalSanitizationFixes ?? 0);
                    runRecord.PullRequestUrl = batchResult.PullRequestUrl;
                    runRecord.BranchName = branchName;

                    // Diagnostic logging for statistics
                    _logger.LogInformation("Database update stats: Sanitization={Sanit}, PromptTokens={Prompt}, ResponseTokens={Response}, BuildFailures={BuildFail}, BuildRetries={BuildRetry}",
                        runRecord.TotalSanitizationFixes, runRecord.TotalPromptTokens, runRecord.TotalResponseTokens,
                        runRecord.TotalBuildFailures, runRecord.TotalBuildRetries);

                    await dataService.UpdateRunAsync(runRecord, cancellationToken);
                    _logger.LogInformation("Updated database tracking for run {RunId}", runRecord.RunId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update database tracking");
                }
            }

            return batchResult;
        }
        catch (Exception ex)
        {
            // Update run record with failure
            if (runRecord != null && dataService != null)
            {
                try
                {
                    runRecord.EndTime = DateTime.UtcNow;
                    runRecord.Success = false;
                    runRecord.ErrorMessage = ex.Message;
                    await dataService.UpdateRunAsync(runRecord, cancellationToken);
                }
                catch
                {
                    // Ignore errors updating database on failure
                }
            }
            throw;
        }
        finally
        {
            // Clean up container after all workloads complete
            _logger.LogInformation("All workloads completed. Cleaning up AI container...");
            await _containerManager.CleanupContainerAsync(cancellationToken);

            // Dispose scope
            scope?.Dispose();
        }
    }

    /// <summary>
    /// Executes code cleanup workload by discovering files and creating individual workloads per file.
    /// All workloads are executed serially on a single git branch.
    /// Uses configuration from WorkloadConfiguration.CodeCleanup for solution and project paths.
    /// Parameters can override configuration if provided.
    /// </summary>
    public async Task<BatchWorkloadResult> ExecuteCodeCleanupAsync(
        string repositoryUrl,
        string? projectPath = null,
        string? solutionPath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WorkloadResult>();
        var batchResult = new BatchWorkloadResult { WorkloadResults = results };

        // Use configuration values if parameters not provided
        var effectiveProjectPath = projectPath ?? _workloadConfig.ProjectPath;
        var effectiveSolutionPath = solutionPath ?? _workloadConfig.SolutionPath;

        _logger.LogInformation("Starting code cleanup for repository {RepositoryUrl}", repositoryUrl);
        _logger.LogInformation("Using project path: {ProjectPath}", effectiveProjectPath);
        _logger.LogInformation("Using solution path: {SolutionPath}", effectiveSolutionPath);

        // Variables for database tracking and workload execution
        WorkloadRunRecord? runRecord = null;
        IServiceScope? scope = null;
        IDataService? dataService = null;
        List<string> allFilesToClean = new();
        string branchName = string.Empty;

        try
        {
            // Start database tracking
            scope = _serviceProvider.CreateScope();
            dataService = scope.ServiceProvider.GetRequiredService<IDataService>();

            var configJson = JsonSerializer.Serialize(new
            {
                BatchSize = _workloadConfig.BatchSize,
                ValidateBuilds = _workloadConfig.ValidateBuilds,
                MaxBuildRetries = _workloadConfig.MaxBuildRetries,
                RevertOnBuildFailure = _workloadConfig.RevertOnBuildFailure,
                CleanupOptions = _workloadConfig.CodeCleanup.Options
            });

            runRecord = await dataService.StartRunAsync(
                workloadType: "CodeCleanup",
                repositoryUrl: repositoryUrl,
                branchName: $"moonlight/{DateTime.UtcNow:yyyy-MM-dd-HHmmss}-code-cleanup",
                modelName: _aiServerConfig.ModelName,
                serverUrl: _aiServerConfig.ServerUrl,
                configurationJson: configJson,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Started database tracking for run {RunId}", runRecord.RunId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start database tracking, continuing without it");
        }

        try
        {
            // Step 0: Ensure container is running
            _logger.LogInformation("Step 0: Ensuring AI container is running...");
            var containerReady = await _containerManager.EnsureContainerRunningAsync(cancellationToken);
            if (!containerReady)
            {
                _logger.LogError("AI container is not running and could not be started");
                batchResult.Success = false;
                batchResult.Summary = "AI container is not running and could not be started";
                return batchResult;
            }

            // Step 0.5: Wait for AI server health
            _logger.LogInformation("Step 0.5: Verifying AI server health...");
            var serverHealthy = await WaitForAIServerHealthAsync(cancellationToken);
            if (!serverHealthy)
            {
                _logger.LogError("AI server is not responding");
                batchResult.Success = false;
                batchResult.Summary = "AI server is not responding to health checks";
                return batchResult;
            }

            // Step 0.55: Ensure model is available (download if needed)
            _logger.LogInformation("Step 0.55: Ensuring model '{ModelName}' is available...", _aiServerConfig.ModelName);
            var modelDownloaded = await _containerManager.EnsureModelAvailableAsync(_aiServerConfig.ModelName, cancellationToken);
            if (!modelDownloaded)
            {
                _logger.LogError("Failed to ensure model '{ModelName}' is available", _aiServerConfig.ModelName);
                batchResult.Success = false;
                batchResult.Summary = $"Failed to download or verify model: {_aiServerConfig.ModelName}";
                return batchResult;
            }

            // Step 0.6: Verify model availability
            _logger.LogInformation("Step 0.6: Verifying AI model availability...");
            var modelValidation = await ValidateModelAvailabilityAsync(cancellationToken);
            if (!modelValidation.IsValid)
            {
                _logger.LogError("AI model validation failed");
                batchResult.Success = false;
                batchResult.Summary = modelValidation.ErrorMessage;
                return batchResult;
            }

            // Step 1: Clone or pull repository
            var repoConfig = new RepositoryConfiguration { RepositoryUrl = repositoryUrl };
            _logger.LogInformation("Step 1: Cloning/pulling repository...");
            var repositoryPath = await _gitManager.CloneOrPullAsync(repoConfig, cancellationToken);

            // Step 2: Use scheduler to select files that need cleanup
            _logger.LogInformation("Step 2: Selecting files needing cleanup...");
            allFilesToClean = await _workloadScheduler.SelectFilesForWorkloadAsync(
                repositoryPath,
                repoConfig,
                "codeclean",
                effectiveProjectPath,
                cancellationToken);

            if (!allFilesToClean.Any())
            {
                _logger.LogInformation("No files found to clean up");
                batchResult.Success = true;
                batchResult.Summary = "No files found to clean up";
                return batchResult;
            }

            _logger.LogInformation("Target: {BatchSize} modified files from {TotalCount} available files",
                _workloadConfig.BatchSize, allFilesToClean.Count);

            // Step 3: Create single branch for all workloads with timestamp for uniqueness
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
            branchName = $"moonlight/{timestamp}-code-cleanup";
            _logger.LogInformation("Step 3: Creating branch {BranchName}...", branchName);

            await _gitManager.CreateBranchAsync(repositoryPath, branchName, cancellationToken);

            var allModifiedFiles = new List<string>();
            var successfulWorkloads = 0;

            // Step 4: Execute workloads serially until we reach batch size of modified files
            _logger.LogInformation("Step 4: Processing files serially until {BatchSize} files are cleaned...", _workloadConfig.BatchSize);

            // Report initial batch size to progress callback (using -1 to indicate batch total)
            progress?.Report(-_workloadConfig.BatchSize);

            int fileIndex = 0;
            while (fileIndex < allFilesToClean.Count &&
                   results.Count(r => r.IsSuccess && r.ModifiedFiles.Any()) < _workloadConfig.BatchSize)
            {
                var filePath = allFilesToClean[fileIndex];
                fileIndex++;

                // Create workload for this file
                var workload = new CodeCleanupWorkload
                {
                    RepositoryUrl = repositoryUrl,
                    ProjectPath = effectiveProjectPath,
                    SolutionPath = effectiveSolutionPath,
                    FilePath = filePath,
                    Options = _workloadConfig.CodeCleanup.Options
                };

                try
                {
                    var currentModifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    _logger.LogInformation("Processing file {FilePath} (evaluated: {Evaluated}, modified: {Modified}/{Target})",
                        filePath, fileIndex, currentModifiedCount, _workloadConfig.BatchSize);

                    // Execute workload (serial execution - one at a time)
                    var result = await _codeCleanupRunner.ExecuteAsync(workload, repositoryPath, cancellationToken);
                    result.BranchName = branchName;
                    results.Add(result);

                    if (result.IsSuccess && result.ModifiedFiles.Any())
                    {
                        allModifiedFiles.AddRange(result.ModifiedFiles);
                        successfulWorkloads++;
                    }

                    // Update database after each file completes
                    if (runRecord != null && dataService != null)
                    {
                        try
                        {
                            runRecord.FilesSuccessful = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                            runRecord.FilesFailed = results.Count(r => !r.IsSuccess);
                            runRecord.FilesSkipped = results.Count(r => r.IsSuccess && !r.ModifiedFiles.Any());
                            runRecord.TotalPromptTokens = results.Sum(r => r.Statistics?.TotalPromptTokens ?? 0);
                            runRecord.TotalResponseTokens = results.Sum(r => r.Statistics?.TotalResponseTokens ?? 0);
                            runRecord.TotalItemsDocumented = results.Sum(r => r.Statistics?.ItemsModified ?? 0);

                            await dataService.UpdateRunAsync(runRecord, cancellationToken);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogWarning(dbEx, "Failed to update database after file completion");
                        }
                    }

                    // Report progress after each file completes (only count modified files)
                    currentModifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    progress?.Report(currentModifiedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing cleanup for file {FilePath}", filePath);
                    results.Add(new WorkloadResult
                    {
                        Workload = workload,
                        State = WorkloadState.Failed,
                        Summary = $"Workload execution failed: {ex.Message}"
                    });

                    // Update database even for failed files
                    if (runRecord != null && dataService != null)
                    {
                        try
                        {
                            runRecord.FilesSuccessful = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                            runRecord.FilesFailed = results.Count(r => !r.IsSuccess);
                            runRecord.FilesSkipped = results.Count(r => r.IsSuccess && !r.ModifiedFiles.Any());
                            await dataService.UpdateRunAsync(runRecord, cancellationToken);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogWarning(dbEx, "Failed to update database after file failure");
                        }
                    }

                    // Report progress (only count modified files)
                    var modifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    progress?.Report(modifiedCount);
                }

                // Check for cancellation after each file completes
                if (cancellationToken.IsCancellationRequested)
                {
                    var modifiedCount = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
                    _logger.LogWarning("Workload cancellation requested. Evaluated {Evaluated} files, modified {Modified}/{Target}. Saving completed work...",
                        results.Count, modifiedCount, _workloadConfig.BatchSize);
                    break;
                }
            }

            // Step 5: Commit all changes if any workloads succeeded
            if (allModifiedFiles.Any())
            {
                _logger.LogInformation("Step 5: Committing {Count} modified files from {SuccessCount} workloads",
                    allModifiedFiles.Distinct().Count(), successfulWorkloads);

                var commitMessage = $"Code cleanup - {DateTime.UtcNow:yyyy-MM-dd}\n\n" +
                                  $"Processed {successfulWorkloads} file(s)\n" +
                                  $"Modified {allModifiedFiles.Distinct().Count()} file(s)";

                await _gitManager.CommitChangesAsync(repositoryPath, commitMessage, allModifiedFiles.Distinct(), cancellationToken);

                _logger.LogInformation("Step 6: Pushing branch...");
                await _gitManager.PushBranchAsync(repositoryPath, branchName, cancellationToken);

                _logger.LogInformation("Step 7: Creating pull request...");
                try
                {
                    var totalItemsCleaned = results.Sum(r => r.Statistics?.ItemsModified ?? 0);
                    var filesEvaluated = results.Count;
                    var prTitle = $"[MoonlightAI] Code Cleanup - {DateTime.UtcNow:yyyy-MM-dd}";
                    var prBody = $"Automated code cleanup by MoonlightAI\n\n" +
                                $"**Files cleaned:** {successfulWorkloads} (evaluated {filesEvaluated} files)\n" +
                                $"**Items cleaned:** {totalItemsCleaned}\n" +
                                $"**Total changes:** {allModifiedFiles.Distinct().Count()} file(s)\n";

                    var prUrl = await _gitManager.CreatePullRequestAsync(repoConfig, branchName, prTitle, prBody, cancellationToken);
                    batchResult.PullRequestUrl = prUrl;

                    _logger.LogInformation("Code cleanup completed successfully. PR: {PrUrl}", prUrl);
                }
                catch (Exception prEx)
                {
                    _logger.LogError(prEx, "Failed to create pull request. Branch {BranchName} was pushed successfully but PR creation failed", branchName);
                    batchResult.Success = false;
                    batchResult.Summary += $" (PR creation failed: {prEx.Message})";
                }
            }
            else
            {
                _logger.LogInformation("No files were modified, skipping commit and PR");
            }

            batchResult.Success = results.Any(r => r.IsSuccess && r.ModifiedFiles.Any());
            var filesModified = results.Count(r => r.IsSuccess && r.ModifiedFiles.Any());
            var filesNoChange = results.Count(r => r.IsSuccess && !r.ModifiedFiles.Any());
            var filesFailed = results.Count(r => !r.IsSuccess);
            batchResult.Summary = $"Modified {filesModified} file(s), {filesNoChange} no changes needed, {filesFailed} failed (evaluated {results.Count} files)";

            // Update database tracking with final results
            if (runRecord != null && dataService != null)
            {
                try
                {
                    runRecord.EndTime = DateTime.UtcNow;
                    runRecord.Success = batchResult.Success;
                    runRecord.TotalFilesDiscovered = allFilesToClean.Count;
                    runRecord.FilesSelected = results.Count;
                    runRecord.FilesSuccessful = filesModified;
                    runRecord.FilesFailed = filesFailed;
                    runRecord.FilesSkipped = allFilesToClean.Count - results.Count + filesNoChange;
                    runRecord.TotalPromptTokens = results.Sum(r => r.Statistics?.TotalPromptTokens ?? 0);
                    runRecord.TotalResponseTokens = results.Sum(r => r.Statistics?.TotalResponseTokens ?? 0);
                    runRecord.TotalItemsDocumented = results.Sum(r => r.Statistics?.ItemsModified ?? 0);
                    runRecord.PullRequestUrl = batchResult.PullRequestUrl;
                    runRecord.BranchName = branchName;

                    await dataService.UpdateRunAsync(runRecord, cancellationToken);
                    _logger.LogInformation("Updated database tracking for run {RunId}", runRecord.RunId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update database tracking");
                }
            }

            return batchResult;
        }
        catch (Exception ex)
        {
            // Update run record with failure
            if (runRecord != null && dataService != null)
            {
                try
                {
                    runRecord.EndTime = DateTime.UtcNow;
                    runRecord.Success = false;
                    runRecord.ErrorMessage = ex.Message;
                    await dataService.UpdateRunAsync(runRecord, cancellationToken);
                }
                catch
                {
                    // Ignore errors updating database on failure
                }
            }
            throw;
        }
        finally
        {
            // Clean up container after all workloads complete
            _logger.LogInformation("All workloads completed. Cleaning up AI container...");
            await _containerManager.CleanupContainerAsync(cancellationToken);

            // Dispose scope
            scope?.Dispose();
        }
    }

    /// <summary>
    /// Executes multiple workloads sequentially on a single git branch and manages container lifecycle.
    /// </summary>
    public async Task<BatchWorkloadResult> ExecuteWorkloadsAsync(
        IEnumerable<CodeDocWorkload> workloads,
        CancellationToken cancellationToken = default)
    {
        var workloadList = workloads.ToList();
        var results = new List<WorkloadResult>();
        var batchResult = new BatchWorkloadResult { WorkloadResults = results };

        _logger.LogInformation("Starting batch workload execution for {Count} workloads", workloadList.Count);

        try
        {
            // Step 0: Ensure container is running
            _logger.LogInformation("Step 0: Ensuring AI container is running...");
            var containerReady = await _containerManager.EnsureContainerRunningAsync(cancellationToken);
            if (!containerReady)
            {
                _logger.LogError("AI container is not running and could not be started");
                batchResult.Success = false;
                batchResult.Summary = "AI container is not running and could not be started";
                return batchResult;
            }

            // Step 0.5: Wait for AI server health
            _logger.LogInformation("Step 0.5: Verifying AI server health...");
            var serverHealthy = await WaitForAIServerHealthAsync(cancellationToken);
            if (!serverHealthy)
            {
                _logger.LogError("AI server is not responding");
                batchResult.Success = false;
                batchResult.Summary = "AI server is not responding to health checks";
                return batchResult;
            }

            // Step 0.55: Ensure model is available (download if needed)
            _logger.LogInformation("Step 0.55: Ensuring model '{ModelName}' is available...", _aiServerConfig.ModelName);
            var modelDownloaded = await _containerManager.EnsureModelAvailableAsync(_aiServerConfig.ModelName, cancellationToken);
            if (!modelDownloaded)
            {
                _logger.LogError("Failed to ensure model '{ModelName}' is available", _aiServerConfig.ModelName);
                batchResult.Success = false;
                batchResult.Summary = $"Failed to download or verify model: {_aiServerConfig.ModelName}";
                return batchResult;
            }

            // Step 0.6: Verify model availability
            _logger.LogInformation("Step 0.6: Verifying AI model availability...");
            var modelValidation = await ValidateModelAvailabilityAsync(cancellationToken);
            if (!modelValidation.IsValid)
            {
                _logger.LogError("AI model validation failed");
                batchResult.Success = false;
                batchResult.Summary = modelValidation.ErrorMessage;
                return batchResult;
            }

            // Group workloads by repository
            var workloadsByRepo = workloadList.GroupBy(w => w.RepositoryUrl).ToList();

            foreach (var repoGroup in workloadsByRepo)
            {
                var repositoryUrl = repoGroup.Key;
                var repoWorkloads = repoGroup.ToList();

                _logger.LogInformation("Processing {Count} workloads for repository {RepositoryUrl}", repoWorkloads.Count, repositoryUrl);

                // Step 1: Clone or pull repository
                var repoConfig = new RepositoryConfiguration { RepositoryUrl = repositoryUrl };
                _logger.LogInformation("Step 1: Cloning/pulling repository...");
                var repositoryPath = await _gitManager.CloneOrPullAsync(repoConfig, cancellationToken);

                // Step 2: Create single branch for all workloads in this repo
                var branchName = $"moonlight/{DateTime.UtcNow:yyyy-MM-dd}-batch";
                _logger.LogInformation("Step 2: Creating branch {BranchName}...", branchName);

                var existingPRs = await _gitManager.GetExistingPullRequestsAsync(repoConfig, cancellationToken);
                if (existingPRs.Contains(branchName))
                {
                    _logger.LogWarning("PR already exists for branch {BranchName}, skipping repository", branchName);
                    continue;
                }

                await _gitManager.CreateBranchAsync(repositoryPath, branchName, cancellationToken);

                var allModifiedFiles = new List<string>();
                var successfulWorkloads = 0;

                // Step 3: Execute all workloads for this repository
                foreach (var workload in repoWorkloads)
                {
                    try
                    {
                        _logger.LogInformation("Executing workload: {WorkloadType}", workload.WorkloadType);
                        var result = await _codeDocRunner.ExecuteAsync(workload, repositoryPath, cancellationToken);
                        result.BranchName = branchName;
                        results.Add(result);

                        if (result.IsSuccess && result.ModifiedFiles.Any())
                        {
                            allModifiedFiles.AddRange(result.ModifiedFiles);
                            successfulWorkloads++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing workload");
                        results.Add(new WorkloadResult
                        {
                            Workload = workload,
                            State = WorkloadState.Failed,
                            Summary = $"Workload execution failed: {ex.Message}"
                        });
                    }
                }

                // Step 4: Commit all changes if any workloads succeeded
                if (allModifiedFiles.Any())
                {
                    _logger.LogInformation("Step 4: Committing {Count} modified files from {SuccessCount} workloads",
                        allModifiedFiles.Distinct().Count(), successfulWorkloads);

                    var commitMessage = $"MoonlightAI batch run - {DateTime.UtcNow:yyyy-MM-dd}\n\n" +
                                      $"Processed {successfulWorkloads} workload(s)\n" +
                                      $"Modified {allModifiedFiles.Distinct().Count()} file(s)";

                    await _gitManager.CommitChangesAsync(repositoryPath, commitMessage, allModifiedFiles.Distinct(), cancellationToken);

                    _logger.LogInformation("Step 5: Pushing branch...");
                    await _gitManager.PushBranchAsync(repositoryPath, branchName, cancellationToken);

                    _logger.LogInformation("Step 6: Creating pull request...");
                    var prTitle = $"MoonlightAI Batch Update - {DateTime.UtcNow:yyyy-MM-dd}";
                    var prBody = $"Automated changes from MoonlightAI batch run\n\n" +
                                $"**Workloads completed:** {successfulWorkloads}/{repoWorkloads.Count}\n" +
                                $"**Files modified:** {allModifiedFiles.Distinct().Count()}\n";

                    var prUrl = await _gitManager.CreatePullRequestAsync(repoConfig, branchName, prTitle, prBody, cancellationToken);
                    batchResult.PullRequestUrl = prUrl;

                    _logger.LogInformation("Batch workload completed successfully. PR: {PrUrl}", prUrl);
                }
                else
                {
                    _logger.LogInformation("No files were modified, skipping commit and PR");
                }
            }

            batchResult.Success = results.Any(r => r.IsSuccess);
            batchResult.Summary = $"Completed {results.Count(r => r.IsSuccess)}/{results.Count} workloads";
            return batchResult;
        }
        finally
        {
            // Clean up container after all workloads complete
            _logger.LogInformation("All workloads completed. Cleaning up AI container...");
            await _containerManager.CleanupContainerAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Result of a batch workload execution.
    /// </summary>
    public class BatchWorkloadResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? PullRequestUrl { get; set; }
        public List<WorkloadResult> WorkloadResults { get; set; } = new();
    }

    /// <summary>
    /// Waits for the AI server to become healthy with retries.
    /// </summary>
    private async Task<bool> WaitForAIServerHealthAsync(CancellationToken cancellationToken, int maxAttempts = 10, int delaySeconds = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogDebug("Health check attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                var isHealthy = await _aiServer.HealthCheckAsync(cancellationToken);

                if (isHealthy)
                {
                    _logger.LogInformation("AI server is healthy and ready");
                    return true;
                }

                _logger.LogWarning("AI server health check failed on attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during health check attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
            }

            if (attempt < maxAttempts)
            {
                _logger.LogDebug("Waiting {DelaySeconds} seconds before next health check attempt", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that the configured AI model is available on the server.
    /// </summary>
    private async Task<ModelValidationResult> ValidateModelAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try a simple test prompt to validate the model
            _logger.LogDebug("Testing model '{ModelName}' availability", _aiServerConfig.ModelName);

            var testResponse = await _aiServer.SendPromptAsync("test", cancellationToken);

            _logger.LogInformation("Model '{ModelName}' is available and responding", _aiServerConfig.ModelName);
            return new ModelValidationResult { IsValid = true };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            // Model not found - provide helpful error message
            var errorMessage = $"AI model '{_aiServerConfig.ModelName}' is not available on the server. " +
                              $"Please check your configuration or pull the model using: ollama pull {_aiServerConfig.ModelName}";

            _logger.LogError(errorMessage);
            return new ModelValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            // Other errors during validation
            var errorMessage = $"Failed to validate AI model availability: {ex.Message}";
            _logger.LogError(ex, "Model validation failed");
            return new ModelValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Parses the DocumentVisibility configuration string into MemberVisibility flags.
    /// </summary>
    private MemberVisibility ParseDocumentVisibility(string visibilityConfig)
    {
        if (string.IsNullOrWhiteSpace(visibilityConfig))
        {
            return MemberVisibility.Public;
        }

        var visibility = MemberVisibility.None;
        var parts = visibilityConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Enum.TryParse<MemberVisibility>(part, ignoreCase: true, out var parsed))
            {
                visibility |= parsed;
            }
            else
            {
                _logger.LogWarning("Unknown visibility value in configuration: {Value}. Ignoring.", part);
            }
        }

        // Default to Public if nothing was parsed
        return visibility == MemberVisibility.None ? MemberVisibility.Public : visibility;
    }

    /// <summary>
    /// Result of model validation check.
    /// </summary>
    private class ModelValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
