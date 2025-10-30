using MoonlightAI.Core.Data.Models;

namespace MoonlightAI.Core.Data;

/// <summary>
/// Service for storing and retrieving workload execution results.
/// </summary>
public interface IDataService
{
    /// <summary>
    /// Initializes the database (creates tables if needed).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new workload run and returns the record.
    /// </summary>
    Task<WorkloadRunRecord> StartRunAsync(
        string workloadType,
        string repositoryUrl,
        string branchName,
        string modelName,
        string serverUrl,
        string configurationJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a workload run with completion information.
    /// </summary>
    Task UpdateRunAsync(WorkloadRunRecord run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a file result to a workload run.
    /// </summary>
    Task AddFileResultAsync(FileResultRecord fileResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a build attempt to a file result.
    /// </summary>
    Task AddBuildAttemptAsync(BuildAttemptRecord buildAttempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an AI interaction to a file result.
    /// </summary>
    Task AddAIInteractionAsync(AIInteractionRecord interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all workload runs, ordered by most recent first.
    /// </summary>
    Task<List<WorkloadRunRecord>> GetAllRunsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific run by ID with all related data.
    /// </summary>
    Task<WorkloadRunRecord?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets runs filtered by model name.
    /// </summary>
    Task<List<WorkloadRunRecord>> GetRunsByModelAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets runs filtered by workload type.
    /// </summary>
    Task<List<WorkloadRunRecord>> GetRunsByWorkloadTypeAsync(string workloadType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics comparing different models.
    /// </summary>
    Task<Dictionary<string, ModelStatistics>> GetModelComparisonAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated statistics for a model across all runs.
/// </summary>
public class ModelStatistics
{
    public string ModelName { get; set; } = string.Empty;
    public int TotalRuns { get; set; }
    public int SuccessfulRuns { get; set; }
    public int TotalFilesProcessed { get; set; }
    public int TotalFilesSuccessful { get; set; }
    public int TotalFilesFailed { get; set; }
    public double SuccessRate { get; set; }
    public int TotalBuildFailures { get; set; }
    public int TotalBuildRetries { get; set; }
    public long TotalPromptTokens { get; set; }
    public long TotalResponseTokens { get; set; }
    public double AverageTokensPerFile { get; set; }
    public int TotalItemsDocumented { get; set; }
    public int TotalSanitizationFixes { get; set; }
    public double AverageSanitizationFixesPerItem { get; set; }
}
