using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Data.Models;

namespace MoonlightAI.Core.Data;

/// <summary>
/// SQLite implementation of the data service.
/// </summary>
public class SQLiteDataService : IDataService
{
    private readonly MoonlightDbContext _context;
    private readonly ILogger<SQLiteDataService> _logger;

    public SQLiteDataService(MoonlightDbContext context, ILogger<SQLiteDataService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing database...");
        await _context.Database.EnsureCreatedAsync(cancellationToken);
        _logger.LogInformation("Database initialized successfully");
    }

    /// <inheritdoc/>
    public async Task<WorkloadRunRecord> StartRunAsync(
        string workloadType,
        string repositoryUrl,
        string branchName,
        string modelName,
        string serverUrl,
        string configurationJson,
        CancellationToken cancellationToken = default)
    {
        var run = new WorkloadRunRecord
        {
            RunId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            WorkloadType = workloadType,
            RepositoryUrl = repositoryUrl,
            BranchName = branchName,
            ModelName = modelName,
            ServerUrl = serverUrl,
            ConfigurationJson = configurationJson
        };

        _context.WorkloadRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started workload run {RunId} for {WorkloadType}", run.RunId, workloadType);
        return run;
    }

    /// <inheritdoc/>
    public async Task UpdateRunAsync(WorkloadRunRecord run, CancellationToken cancellationToken = default)
    {
        _context.WorkloadRuns.Update(run);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated workload run {RunId}", run.RunId);
    }

    /// <inheritdoc/>
    public async Task AddFileResultAsync(FileResultRecord fileResult, CancellationToken cancellationToken = default)
    {
        _context.FileResults.Add(fileResult);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added file result for {FilePath} to run {WorkloadRunId}",
            fileResult.FilePath, fileResult.WorkloadRunId);
    }

    /// <inheritdoc/>
    public async Task AddBuildAttemptAsync(BuildAttemptRecord buildAttempt, CancellationToken cancellationToken = default)
    {
        _context.BuildAttempts.Add(buildAttempt);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added build attempt {AttemptNumber} for file result {FileResultId}",
            buildAttempt.AttemptNumber, buildAttempt.FileResultId);
    }

    /// <inheritdoc/>
    public async Task AddAIInteractionAsync(AIInteractionRecord interaction, CancellationToken cancellationToken = default)
    {
        _context.AIInteractions.Add(interaction);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added AI interaction of type {InteractionType} for file result {FileResultId}",
            interaction.InteractionType, interaction.FileResultId);
    }

    /// <inheritdoc/>
    public async Task<List<WorkloadRunRecord>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.WorkloadRuns
            .OrderByDescending(r => r.StartTime)
            .Include(r => r.FileResults)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<WorkloadRunRecord?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkloadRuns
            .Include(r => r.FileResults)
                .ThenInclude(f => f.BuildAttempts)
            .Include(r => r.FileResults)
                .ThenInclude(f => f.AIInteractions)
            .FirstOrDefaultAsync(r => r.RunId == runId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<WorkloadRunRecord>> GetRunsByModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        return await _context.WorkloadRuns
            .Where(r => r.ModelName == modelName)
            .OrderByDescending(r => r.StartTime)
            .Include(r => r.FileResults)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<WorkloadRunRecord>> GetRunsByWorkloadTypeAsync(string workloadType, CancellationToken cancellationToken = default)
    {
        return await _context.WorkloadRuns
            .Where(r => r.WorkloadType == workloadType)
            .OrderByDescending(r => r.StartTime)
            .Include(r => r.FileResults)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, ModelStatistics>> GetModelComparisonAsync(CancellationToken cancellationToken = default)
    {
        var runs = await _context.WorkloadRuns
            .Include(r => r.FileResults)
            .ToListAsync(cancellationToken);

        var grouped = runs.GroupBy(r => r.ModelName);

        var statistics = new Dictionary<string, ModelStatistics>();

        foreach (var group in grouped)
        {
            var modelRuns = group.ToList();
            var totalFilesProcessed = modelRuns.Sum(r => r.FilesSuccessful + r.FilesFailed);
            var totalFilesSuccessful = modelRuns.Sum(r => r.FilesSuccessful);

            statistics[group.Key] = new ModelStatistics
            {
                ModelName = group.Key,
                TotalRuns = modelRuns.Count,
                SuccessfulRuns = modelRuns.Count(r => r.Success),
                TotalFilesProcessed = totalFilesProcessed,
                TotalFilesSuccessful = totalFilesSuccessful,
                TotalFilesFailed = modelRuns.Sum(r => r.FilesFailed),
                SuccessRate = totalFilesProcessed > 0
                    ? (double)totalFilesSuccessful / totalFilesProcessed * 100
                    : 0,
                TotalBuildFailures = modelRuns.Sum(r => r.TotalBuildFailures),
                TotalBuildRetries = modelRuns.Sum(r => r.TotalBuildRetries),
                TotalPromptTokens = modelRuns.Sum(r => r.TotalPromptTokens),
                TotalResponseTokens = modelRuns.Sum(r => r.TotalResponseTokens),
                AverageTokensPerFile = totalFilesProcessed > 0
                    ? (double)modelRuns.Sum(r => r.TotalPromptTokens + r.TotalResponseTokens) / totalFilesProcessed
                    : 0
            };
        }

        _logger.LogInformation("Generated model comparison for {Count} models", statistics.Count);
        return statistics;
    }
}
