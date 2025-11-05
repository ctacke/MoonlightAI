using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.CLI.UI;
using MoonlightAI.Core;
using MoonlightAI.Core.Analysis;
using MoonlightAI.Core.Build;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Containerization;
using MoonlightAI.Core.Data;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;
using MoonlightAI.Core.Orchestration;
using MoonlightAI.Core.Prompts;
using MoonlightAI.Core.Reporting;
using MoonlightAI.Core.Servers;
using MoonlightAI.Core.Workloads;
using MoonlightAI.Core.Workloads.Runners;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .Build();

// Bind configurations
var aiServerConfig = new AIServerConfiguration();
configuration.GetSection(AIServerConfiguration.SectionName).Bind(aiServerConfig);

var gitHubConfig = new GitHubConfiguration();
configuration.GetSection(GitHubConfiguration.SectionName).Bind(gitHubConfig);

var repoConfig = new RepositoryConfigurations
{
    Repositories = configuration.GetSection("Repositories").Get<List<RepositoryConfiguration>>() ?? new List<RepositoryConfiguration>()
};

var containerConfig = new ContainerConfiguration();
configuration.GetSection(ContainerConfiguration.SectionName).Bind(containerConfig);

var workloadConfig = new WorkloadConfiguration();
configuration.GetSection(WorkloadConfiguration.SectionName).Bind(workloadConfig);

var databaseConfig = new DatabaseConfiguration();
configuration.GetSection(DatabaseConfiguration.SectionName).Bind(databaseConfig);

var promptConfig = new PromptConfiguration();
configuration.GetSection(PromptConfiguration.SectionName).Bind(promptConfig);

// Create Terminal UI
using var ui = new MoonlightTerminalUI(aiServerConfig, repoConfig, workloadConfig, containerConfig, databaseConfig);

// Configure services
var services = new ServiceCollection();

// Add logging with Terminal UI logger
services.AddLogging(builder =>
{
    builder.AddProvider(new TerminalUILoggerProvider(ui, LogLevel.Information));
    builder.SetMinimumLevel(LogLevel.Information);

    // Suppress Entity Framework SQL command logging
    builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
});

// Register configurations
services.AddSingleton(aiServerConfig);
services.AddSingleton(gitHubConfig);
services.AddSingleton(repoConfig);
services.AddSingleton(containerConfig);
services.AddSingleton(workloadConfig);
services.AddSingleton(databaseConfig);
services.AddSingleton(promptConfig);

// Register HttpClient and AI Server
services.AddHttpClient<IAIServer, CodeLlamaServer>();

// Register Container Manager
services.AddSingleton<IContainerManager, DockerContainerManager>();

// Register Workload Scheduler
services.AddSingleton<IWorkloadScheduler, WorkloadScheduler>();

// Register Build Validator
services.AddSingleton<IBuildValidator, DotNetBuildValidator>();

// Register Database
services.AddDbContext<MoonlightDbContext>(options =>
{
    options.UseSqlite($"Data Source={databaseConfig.DatabasePath}");
    if (databaseConfig.EnableDetailedLogging)
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
    else
    {
        // Suppress SQL command logging
        options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted));
    }
});
services.AddScoped<IDataService, SQLiteDataService>();

// Register Reporting
services.AddScoped<IReporter, ModelComparisonReporter>();

// Register Core Services
services.AddSingleton<WorkloadOrchestrator>();
services.AddSingleton<IGitManager, GitManager>();
services.AddSingleton<RepositoryManager>();
services.AddSingleton<ICodeAnalyzer, RoslynCodeAnalyzer>();
services.AddSingleton<PromptService>();
services.AddSingleton<CodeDocSanitizer>();

// Register Workload Runners
services.AddSingleton<CodeDocWorkloadRunner>();
services.AddSingleton<CodeCleanupWorkloadRunner>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Cancellation token holder for workload cancellation
var cancellationHolder = new WorkloadCancellationHolder();

// Initialize database
try
{
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        await dataService.InitializeAsync();

        ui.AppendLog("Database initialized successfully");

        // Load initial model statistics for the configured model
        var modelComparison = await dataService.GetModelComparisonAsync();
        if (modelComparison.TryGetValue(aiServerConfig.ModelName, out var modelStats))
        {
            ui.UpdateModelStatistics(modelStats);
        }
    }
}
catch (Exception ex)
{
    ui.AppendLog($"ERROR: Failed to initialize database: {ex.Message}");
    ui.AppendLog("Press Ctrl+C to exit");
    ui.Run();
    return 1;
}

// Handle commands from UI
ui.CommandEntered += async (sender, command) =>
{
    await HandleCommandAsync(command, ui, serviceProvider, repoConfig, cancellationHolder);
};

// Show welcome message
ui.AppendLog("═══════════════════════════════════════════════════════════════");
ui.AppendLog("  MoonlightAI - AI-Powered Code Documentation");
ui.AppendLog("═══════════════════════════════════════════════════════════════");
ui.AppendLog("");
ui.AppendLog("Available Commands:");
ui.AppendLog("  run doc      - Run code documentation workload");
ui.AppendLog("  run cleanup  - Run code cleanup workload");
ui.AppendLog("  stop         - Stop current workload (saves completed work)");
ui.AppendLog("  report       - View model comparison report");
ui.AppendLog("  stats        - Show current statistics");
ui.AppendLog("  batch X      - Set batch size to X files");
ui.AppendLog("  clear        - Clear log output");
ui.AppendLog("  exit         - Exit application");
ui.AppendLog("");
ui.AppendLog("Type a command and press Enter to execute.");
ui.AppendLog("═══════════════════════════════════════════════════════════════");

// Run the Terminal UI
ui.Run();

return 0;

// Command handler
static async Task HandleCommandAsync(string command, MoonlightTerminalUI ui, ServiceProvider serviceProvider, RepositoryConfigurations repoConfig, WorkloadCancellationHolder cancellationHolder)
{
    var cmd = command.Trim().ToLowerInvariant();

    try
    {
        // Handle 'run' command with subcommands
        if (cmd.StartsWith("run "))
        {
            var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                ui.AppendLog("ERROR: 'run' requires a workload type. Usage: run <doc|cleanup>");
                return;
            }

            var workloadType = parts[1].ToLowerInvariant();

            if (cancellationHolder.CancellationTokenSource != null)
            {
                ui.AppendLog("ERROR: A workload is already running. Use 'stop' to cancel it first.");
                return;
            }

            cancellationHolder.CancellationTokenSource = new CancellationTokenSource();

            switch (workloadType)
            {
                case "doc":
                case "documentation":
                    await ExecuteDocumentationWorkloadAsync(ui, serviceProvider, repoConfig, cancellationHolder.CancellationTokenSource.Token);
                    break;

                case "cleanup":
                case "clean":
                    await ExecuteCleanupWorkloadAsync(ui, serviceProvider, repoConfig, cancellationHolder.CancellationTokenSource.Token);
                    break;

                default:
                    ui.AppendLog($"ERROR: Unknown workload type '{workloadType}'. Available: doc, cleanup");
                    break;
            }

            cancellationHolder.CancellationTokenSource?.Dispose();
            cancellationHolder.CancellationTokenSource = null;
            return;
        }

        switch (cmd)
        {
            case "stop":
                if (cancellationHolder.CancellationTokenSource == null)
                {
                    ui.AppendLog("No workload is currently running.");
                }
                else
                {
                    ui.AppendLog("Requesting workload cancellation...");
                    ui.AppendLog("Completing current file and saving progress...");
                    cancellationHolder.CancellationTokenSource.Cancel();
                }
                break;

            case "report":
                await DisplayComparisonReportAsync(ui, serviceProvider);
                break;

            case "stats":
                await DisplayStatisticsAsync(ui, serviceProvider);
                break;

            case "clear":
                // Clear log - this is a bit tricky with Terminal.Gui, so we'll just add a separator
                ui.AppendLog("");
                ui.AppendLog("═══════════════════════════════════════════════════════════════");
                ui.AppendLog("");
                break;

            case "exit":
            case "quit":
                ui.AppendLog("Shutting down MoonlightAI...");
                ui.Stop();
                break;

            case "help":
            case "?":
                ui.AppendLog("");
                ui.AppendLog("Available Commands:");
                ui.AppendLog("  run doc      - Run code documentation workload");
                ui.AppendLog("  run cleanup  - Run code cleanup workload");
                ui.AppendLog("  stop         - Stop current workload (saves completed work)");
                ui.AppendLog("  report       - View model comparison report");
                ui.AppendLog("  stats        - Show current statistics");
                ui.AppendLog("  batch X      - Set batch size to X files");
                ui.AppendLog("  clear        - Clear log output");
                ui.AppendLog("  exit         - Exit application");
                ui.AppendLog("");
                break;

            default:
                // Check if it's a batch command
                if (cmd.StartsWith("batch "))
                {
                    var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int newBatchSize))
                    {
                        if (newBatchSize > 0)
                        {
                            var workloadConfig = serviceProvider.GetRequiredService<WorkloadConfiguration>();
                            workloadConfig.BatchSize = newBatchSize;
                            ui.AppendLog($"Batch size updated to {newBatchSize} files.");
                            ui.UpdateStatus();
                        }
                        else
                        {
                            ui.AppendLog("ERROR: Batch size must be greater than 0.");
                        }
                    }
                    else
                    {
                        ui.AppendLog("ERROR: Invalid batch command. Usage: batch <number>");
                    }
                }
                else
                {
                    ui.AppendLog($"Unknown command: '{command}'. Type 'help' for available commands.");
                }
                break;
        }
    }
    catch (Exception ex)
    {
        ui.AppendLog($"ERROR: {ex.Message}");
    }
}

static async Task ExecuteDocumentationWorkloadAsync(MoonlightTerminalUI ui, ServiceProvider serviceProvider, RepositoryConfigurations repoConfig, CancellationToken cancellationToken)
{
    ui.SetStatus("Running workload... (type 'stop' to cancel)");
    ui.AppendLog("Starting code documentation workload...");

    // Get initial statistics
    int initialRunCount = 0;
    int initialFileCount = 0;
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var allRuns = await dataService.GetAllRunsAsync();
        initialRunCount = allRuns.Count;
        initialFileCount = allRuns.Sum(r => r.FilesSuccessful + r.FilesFailed);
    }

    var orchestrator = serviceProvider.GetRequiredService<WorkloadOrchestrator>();

    // Track progress during execution
    int filesProcessedThisRun = 0;
    int batchTotal = 0;
    var progressCallback = new Progress<int>(async value =>
    {
        // Negative value indicates batch total (sent at start)
        if (value < 0)
        {
            batchTotal = -value;
            ui.UpdateBatchProgress(0, batchTotal);
        }
        else
        {
            // Positive value indicates files completed
            filesProcessedThisRun = value;
            ui.UpdateStatistics(initialRunCount + 1, initialFileCount + filesProcessedThisRun);
            ui.UpdateBatchProgress(filesProcessedThisRun, batchTotal);

            // Refresh model statistics from database after each file
            using (var scope = serviceProvider.CreateScope())
            {
                try
                {
                    var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
                    var modelComparison = await dataService.GetModelComparisonAsync();
                    var aiConfig = serviceProvider.GetRequiredService<AIServerConfiguration>();
                    if (modelComparison.TryGetValue(aiConfig.ModelName, out var modelStats))
                    {
                        ui.UpdateModelStatistics(modelStats);
                    }
                }
                catch (Exception ex)
                {
                    // Don't let database errors stop progress updates
                    ui.AppendLog($"Warning: Failed to refresh model statistics: {ex.Message}");
                }
            }
        }
    });

    try
    {
        // Get repository URL from configuration
        var repositoryUrl = repoConfig.Repositories.FirstOrDefault()?.RepositoryUrl;
        if (string.IsNullOrEmpty(repositoryUrl))
        {
            ui.AppendLog("ERROR: No repository configured in appsettings.json");
            ui.SetStatus("Idle");
            return;
        }

        // Execute code documentation for the repository with progress updates
        // Solution path and ignore projects come from workload configuration
        var result = await orchestrator.ExecuteCodeDocumentationAsync(
            repositoryUrl: repositoryUrl,
            solutionPath: null, // Uses configuration value
            ignoreProjects: null, // Uses configuration value
            progress: progressCallback,
            cancellationToken: cancellationToken);

        ui.AppendLog("");
        if (cancellationToken.IsCancellationRequested)
        {
            ui.AppendLog($"Workload STOPPED by user: {result.Summary}");
        }
        else
        {
            ui.AppendLog($"Workload completed: {result.Summary}");
        }

        if (!string.IsNullOrEmpty(result.PullRequestUrl))
        {
            ui.AppendLog($"Pull Request: {result.PullRequestUrl}");
        }
    }
    catch (OperationCanceledException)
    {
        ui.AppendLog("");
        ui.AppendLog("Workload cancelled. Completed files have been saved and committed.");
    }
    catch (Exception ex)
    {
        ui.AppendLog("");
        ui.AppendLog($"ERROR: {ex.Message}");
    }

    ui.SetStatus("Idle");
    ui.ClearBatchProgress();

    // Final statistics and model stats update from database
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var allRuns = await dataService.GetAllRunsAsync();
        var totalFiles = allRuns.Sum(r => r.FilesSuccessful + r.FilesFailed);
        ui.UpdateStatistics(allRuns.Count, totalFiles);

        // Refresh model statistics after run completes
        var modelComparison = await dataService.GetModelComparisonAsync();
        if (modelComparison.TryGetValue(serviceProvider.GetRequiredService<AIServerConfiguration>().ModelName, out var modelStats))
        {
            ui.UpdateModelStatistics(modelStats);
        }
    }
}

static async Task ExecuteCleanupWorkloadAsync(MoonlightTerminalUI ui, ServiceProvider serviceProvider, RepositoryConfigurations repoConfig, CancellationToken cancellationToken)
{
    ui.SetStatus("Running cleanup workload... (type 'stop' to cancel)");
    ui.AppendLog("Starting code cleanup workload...");

    // Get initial statistics
    int initialRunCount = 0;
    int initialFileCount = 0;
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var allRuns = await dataService.GetAllRunsAsync();
        initialRunCount = allRuns.Count;
        initialFileCount = allRuns.Sum(r => r.FilesSuccessful + r.FilesFailed);
    }

    var orchestrator = serviceProvider.GetRequiredService<WorkloadOrchestrator>();

    // Track progress during execution
    int filesProcessedThisRun = 0;
    int batchTotal = 0;
    var progressCallback = new Progress<int>(async value =>
    {
        // Negative value indicates batch total (sent at start)
        if (value < 0)
        {
            batchTotal = -value;
            ui.UpdateBatchProgress(0, batchTotal);
        }
        else
        {
            // Positive value indicates files completed
            filesProcessedThisRun = value;
            ui.UpdateStatistics(initialRunCount + 1, initialFileCount + filesProcessedThisRun);
            ui.UpdateBatchProgress(filesProcessedThisRun, batchTotal);

            // Refresh model statistics from database after each file
            using (var scope = serviceProvider.CreateScope())
            {
                try
                {
                    var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
                    var modelComparison = await dataService.GetModelComparisonAsync();
                    var aiConfig = serviceProvider.GetRequiredService<AIServerConfiguration>();
                    if (modelComparison.TryGetValue(aiConfig.ModelName, out var modelStats))
                    {
                        ui.UpdateModelStatistics(modelStats);
                    }
                }
                catch (Exception ex)
                {
                    // Don't let database errors stop progress updates
                    ui.AppendLog($"Warning: Failed to refresh model statistics: {ex.Message}");
                }
            }
        }
    });

    try
    {
        // Get repository URL from configuration
        var repositoryUrl = repoConfig.Repositories.FirstOrDefault()?.RepositoryUrl;
        if (string.IsNullOrEmpty(repositoryUrl))
        {
            ui.AppendLog("ERROR: No repository configured in appsettings.json");
            ui.SetStatus("Idle");
            return;
        }

        // Execute code cleanup for the repository with progress updates
        // Solution path and ignore projects come from workload configuration
        var result = await orchestrator.ExecuteCodeCleanupAsync(
            repositoryUrl: repositoryUrl,
            solutionPath: null, // Uses configuration value
            ignoreProjects: null, // Uses configuration value
            progress: progressCallback,
            cancellationToken: cancellationToken);

        ui.AppendLog("");
        if (cancellationToken.IsCancellationRequested)
        {
            ui.AppendLog($"Workload STOPPED by user: {result.Summary}");
        }
        else
        {
            ui.AppendLog($"Workload completed: {result.Summary}");
        }

        if (!string.IsNullOrEmpty(result.PullRequestUrl))
        {
            ui.AppendLog($"Pull Request: {result.PullRequestUrl}");
        }
    }
    catch (OperationCanceledException)
    {
        ui.AppendLog("");
        ui.AppendLog("Workload cancelled. Completed files have been saved and committed.");
    }
    catch (Exception ex)
    {
        ui.AppendLog("");
        ui.AppendLog($"ERROR: {ex.Message}");
    }

    ui.SetStatus("Idle");
    ui.ClearBatchProgress();

    // Final statistics and model stats update from database
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var allRuns = await dataService.GetAllRunsAsync();
        var totalFiles = allRuns.Sum(r => r.FilesSuccessful + r.FilesFailed);
        ui.UpdateStatistics(allRuns.Count, totalFiles);

        // Refresh model statistics after run completes
        var modelComparison = await dataService.GetModelComparisonAsync();
        if (modelComparison.TryGetValue(serviceProvider.GetRequiredService<AIServerConfiguration>().ModelName, out var modelStats))
        {
            ui.UpdateModelStatistics(modelStats);
        }
    }
}

static async Task DisplayComparisonReportAsync(MoonlightTerminalUI ui, ServiceProvider serviceProvider)
{
    ui.AppendLog("");
    ui.AppendLog("═══════════════════════════════════════════════════════════════");
    ui.AppendLog("              Model Comparison Report");
    ui.AppendLog("═══════════════════════════════════════════════════════════════");

    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var comparison = await dataService.GetModelComparisonAsync();

        if (!comparison.Any())
        {
            ui.AppendLog("No workload runs found in database.");
            ui.AppendLog("Run a workload first to see comparison data.");
            return;
        }

        var sortedModels = comparison.Values.OrderByDescending(m => m.SuccessRate).ToList();

        ui.AppendLog("");
        ui.AppendLog($"{"Model",-30} {"Runs",6} {"Success",8} {"Files",7} {"Failures",9} {"SanitFix",9}");
        ui.AppendLog(new string('-', 80));

        foreach (var model in sortedModels)
        {
            var line = $"{model.ModelName,-30} " +
                      $"{model.TotalRuns,6} " +
                      $"{model.SuccessRate,7:F1}% " +
                      $"{model.TotalFilesProcessed,7} " +
                      $"{model.TotalBuildFailures,9} " +
                      $"{model.AverageSanitizationFixesPerItem,9:F2}";

            ui.AppendLog(line);
        }

        ui.AppendLog("");
        ui.AppendLog("Legend: SanitFix = Avg sanitization fixes per item (hallucinations)");
    }

    ui.AppendLog("═══════════════════════════════════════════════════════════════");
    ui.AppendLog("");
}

static async Task DisplayStatisticsAsync(MoonlightTerminalUI ui, ServiceProvider serviceProvider)
{
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var allRuns = await dataService.GetAllRunsAsync();

        ui.AppendLog("");
        ui.AppendLog("═══════════════════════════════════════════════════════════════");
        ui.AppendLog("              Current Statistics");
        ui.AppendLog("═══════════════════════════════════════════════════════════════");
        ui.AppendLog($"Total Runs: {allRuns.Count}");
        ui.AppendLog($"Successful Runs: {allRuns.Count(r => r.Success)}");
        ui.AppendLog($"Failed Runs: {allRuns.Count(r => !r.Success)}");
        ui.AppendLog($"Total Files Processed: {allRuns.Sum(r => r.FilesSuccessful + r.FilesFailed)}");
        ui.AppendLog($"Total Sanitization Fixes: {allRuns.Sum(r => r.TotalSanitizationFixes)}");
        ui.AppendLog("═══════════════════════════════════════════════════════════════");
        ui.AppendLog("");
    }
}

// Helper class to hold cancellation token source
class WorkloadCancellationHolder
{
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}
