using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core;
using MoonlightAI.Core.Analysis;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Containerization;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;
using MoonlightAI.Core.Orchestration;
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

// Register HttpClient and AI Server
services.AddHttpClient<IAIServer, CodeLlamaServer>();

// Register Container Manager
services.AddSingleton<IContainerManager, DockerContainerManager>();

// Register Workload Scheduler
services.AddSingleton<IWorkloadScheduler, WorkloadScheduler>();

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

    logger.LogInformation("MoonlightAI starting...");
    logger.LogInformation("AI Server URL: {ServerUrl}", aiServerConfig.ServerUrl);
    logger.LogInformation("Model: {ModelName}", aiServerConfig.ModelName);

    var orchestrator = serviceProvider.GetRequiredService<WorkloadOrchestrator>();

    // Execute code documentation for the repository
    var result = await orchestrator.ExecuteCodeDocumentationAsync(
        repositoryUrl: "https://github.com/LECS-Energy-LLC/solution-family",
        projectPath: @"src\Engine\Modules\MQTT\SolutionEngine.MQTT.Module\SolutionEngine.MQTT.Module.csproj",
        solutionPath: @"src\SolutionEngine.slnx");

    logger.LogInformation("Code documentation completed: {Summary}", result.Summary);
    if (!string.IsNullOrEmpty(result.PullRequestUrl))
    {
        logger.LogInformation("Pull Request: {PrUrl}", result.PullRequestUrl);
    }
}
catch (Exception ex)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while running MoonlightAI.");
    return 1;
}

return 0;
