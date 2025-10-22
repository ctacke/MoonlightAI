using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core;
using MoonlightAI.Core.Analysis;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;
using MoonlightAI.Core.Orchestration;
using MoonlightAI.Core.Servers;
using MoonlightAI.Core.Workloads;

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

// Register HttpClient and AI Server
services.AddHttpClient<IAIServer, CodeLlamaServer>();

services.AddSingleton<WorkloadOrchestrator>();
services.AddSingleton<GitManager>();
services.AddSingleton<RepositoryManager>();
services.AddSingleton<RoslynCodeAnalyzer>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Run the application
try
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var aiServer = serviceProvider.GetRequiredService<IAIServer>();

    logger.LogInformation("MoonlightAI starting...");
    logger.LogInformation("AI Server URL: {ServerUrl}", aiServerConfig.ServerUrl);
    logger.LogInformation("Model: {ModelName}", aiServerConfig.ModelName);

    // Test health check
    logger.LogInformation("Performing health check...");
    var isHealthy = await aiServer.HealthCheckAsync();

    if (isHealthy)
    {
        logger.LogInformation("AI Server is healthy and reachable.");

        var workload = new CodeDocWorkload
        {
            SolutionPath = @"src\SolutionEngine.slnx",
            ProjectPath = @"src\Engine\Modules\MQTT\SolutionEngine.MQTT.Module\SolutionEngine.MQTT.Module.csproj",
            RepositoryUrl = "https://github.com/LECS-Energy-LLC/solution-family"
        };

        var orchestrator = serviceProvider.GetRequiredService<WorkloadOrchestrator>();
        await orchestrator.EnqueueWorkload(workload);

    }
    else
    {
        logger.LogWarning("AI Server health check failed. Server may be unreachable.");
    }
}
catch (Exception ex)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while running MoonlightAI.");
    return 1;
}

return 0;
