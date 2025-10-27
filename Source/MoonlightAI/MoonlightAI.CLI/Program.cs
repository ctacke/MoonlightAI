using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core;
using MoonlightAI.Core.Analysis;
using MoonlightAI.Core.Build;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Containerization;
using MoonlightAI.Core.Data;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;
using MoonlightAI.Core.Orchestration;
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

// Configure services
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddConfiguration(configuration.GetSection("Logging"));
});

// Bind AI Server configuration
var aiServerConfig = new AIServerConfiguration();
configuration.GetSection(AIServerConfiguration.SectionName).Bind(aiServerConfig);
services.AddSingleton(aiServerConfig);

// Bind Github configuration
var gitHubConfig = new GitHubConfiguration();
configuration.GetSection(GitHubConfiguration.SectionName).Bind(gitHubConfig);
services.AddSingleton(gitHubConfig);

// Bind Repository configuration
var repoConfig = new RepositoryConfigurations();
configuration.GetSection(RepositoryConfigurations.SectionName).Bind(repoConfig);
services.AddSingleton(repoConfig);

// Bind Container configuration
var containerConfig = new ContainerConfiguration();
configuration.GetSection(ContainerConfiguration.SectionName).Bind(containerConfig);
services.AddSingleton(containerConfig);

// Bind Workload configuration
var workloadConfig = new WorkloadConfiguration();
configuration.GetSection(WorkloadConfiguration.SectionName).Bind(workloadConfig);
services.AddSingleton(workloadConfig);

// Bind Database configuration
var databaseConfig = new DatabaseConfiguration();
configuration.GetSection(DatabaseConfiguration.SectionName).Bind(databaseConfig);
services.AddSingleton(databaseConfig);

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
});
services.AddScoped<IDataService, SQLiteDataService>();

// Register Reporting
services.AddScoped<IReporter, ModelComparisonReporter>();

// Register Core Services
services.AddSingleton<WorkloadOrchestrator>();
services.AddSingleton<IGitManager, GitManager>();
services.AddSingleton<RepositoryManager>();
services.AddSingleton<ICodeAnalyzer, RoslynCodeAnalyzer>();
services.AddSingleton<CodeDocWorkloadRunner>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Run the application
try
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    // Initialize database
    using (var scope = serviceProvider.CreateScope())
    {
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        await dataService.InitializeAsync();
    }

    // Main menu loop
    bool exit = false;
    while (!exit)
    {
        DisplayMainMenu(aiServerConfig, repoConfig, workloadConfig);

        var choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                await ExecuteWorkloadAsync(serviceProvider, logger, aiServerConfig);
                break;
            case "2":
                await DisplayComparisonReportAsync(serviceProvider, logger);
                break;
            case "3":
                exit = true;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Goodbye!");
                Console.ResetColor();
                Console.WriteLine();
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid option. Please select 1, 2, or 3.");
                Console.ResetColor();
                Console.WriteLine();
                break;
        }
    }
}
catch (Exception ex)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while running MoonlightAI.");
    return 1;
}

return 0;

// Helper methods
static void DisplayMainMenu(AIServerConfiguration aiServerConfig, RepositoryConfigurations repoConfig, WorkloadConfiguration workloadConfig)
{
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                  MoonlightAI - Main Menu                     ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine("Current Configuration:");
    Console.WriteLine($"  Repository: {repoConfig.Repositories.FirstOrDefault()?.RepositoryUrl ?? "Not configured"}");
    Console.WriteLine($"  Model: {aiServerConfig.ModelName}");
    Console.WriteLine($"  Batch Size: {workloadConfig.BatchSize}");
    Console.ResetColor();
    Console.WriteLine();

    Console.WriteLine("Options:");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("  [1] Run Code Documentation Workload");
    Console.WriteLine("  [2] View Model Comparison Report");
    Console.WriteLine("  [3] Exit");
    Console.ResetColor();
    Console.WriteLine();
    Console.Write("Select option: ");
}

static async Task ExecuteWorkloadAsync(ServiceProvider serviceProvider, ILogger<Program> logger, AIServerConfiguration aiServerConfig)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Starting workload execution...");
    Console.ResetColor();
    Console.WriteLine();

    logger.LogInformation("MoonlightAI workload starting...");
    logger.LogInformation("AI Server URL: {ServerUrl}", aiServerConfig.ServerUrl);
    logger.LogInformation("Model: {ModelName}", aiServerConfig.ModelName);

    var orchestrator = serviceProvider.GetRequiredService<WorkloadOrchestrator>();

    // Execute code documentation for the repository
    var result = await orchestrator.ExecuteCodeDocumentationAsync(
        repositoryUrl: "https://github.com/LECS-Energy-LLC/solution-family",
        projectPath: @"src\Engine\Modules\MQTT\SolutionEngine.MQTT.Module\SolutionEngine.MQTT.Module.csproj",
        solutionPath: @"src\SolutionEngine.slnx");

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Workload completed: {result.Summary}");
    Console.ResetColor();

    if (!string.IsNullOrEmpty(result.PullRequestUrl))
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Pull Request: {result.PullRequestUrl}");
        Console.ResetColor();
    }

    logger.LogInformation("Code documentation completed: {Summary}", result.Summary);
    if (!string.IsNullOrEmpty(result.PullRequestUrl))
    {
        logger.LogInformation("Pull Request: {PrUrl}", result.PullRequestUrl);
    }

    Console.WriteLine();
    Console.Write("Press any key to return to menu...");
    Console.ReadKey();
}

static async Task DisplayComparisonReportAsync(ServiceProvider serviceProvider, ILogger<Program> logger)
{
    Console.Clear();
    using (var scope = serviceProvider.CreateScope())
    {
        var reporter = scope.ServiceProvider.GetRequiredService<IReporter>();
        await reporter.DisplayReportAsync();
    }

    Console.Write("Press any key to return to menu...");
    Console.ReadKey();
}
