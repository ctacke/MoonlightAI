using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Servers;

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

// Register HttpClient and AI Server
services.AddHttpClient<IAIServer, CodeLlamaServer>();

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

        // Test a simple prompt
        logger.LogInformation("Sending test prompt...");
        var response = await aiServer.SendPromptAsync("Write a simple C# function that adds two numbers.");

        logger.LogInformation("Response received:");
        Console.WriteLine(response.Response);
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
