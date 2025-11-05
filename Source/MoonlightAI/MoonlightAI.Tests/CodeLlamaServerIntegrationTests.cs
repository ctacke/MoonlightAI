using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Prompts;
using MoonlightAI.Core.Servers;

namespace MoonlightAI.Tests;

/// <summary>
/// Integration tests for CodeLlamaServer.
/// These tests require a running CodeLlama server at the configured URL.
/// </summary>
public class CodeLlamaServerIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IAIServer _aiServer;
    private readonly AIServerConfiguration _config;

    public CodeLlamaServerIntegrationTests()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        // Configure services
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Bind AI Server configuration
        _config = new AIServerConfiguration();
        configuration.GetSection(AIServerConfiguration.SectionName).Bind(_config);
        services.AddSingleton(_config);

        // Bind and register Prompt configuration (required by CodeLlamaServer)
        var promptConfig = new PromptConfiguration();
        configuration.GetSection(PromptConfiguration.SectionName).Bind(promptConfig);
        services.AddSingleton(promptConfig);

        // Register PromptService (required by CodeLlamaServer)
        services.AddSingleton<PromptService>();

        // Register HttpClient and AI Server
        services.AddHttpClient<IAIServer, CodeLlamaServer>();

        _serviceProvider = services.BuildServiceProvider();
        _aiServer = _serviceProvider.GetRequiredService<IAIServer>();
    }

    [Fact]
    public async Task HealthCheckAsync_ServerReachable_ReturnsTrue()
    {
        // Act
        var result = await _aiServer.HealthCheckAsync();

        // Assert
        Assert.True(result, "Health check should return true when server is reachable.");
    }

    [Fact]
    public async Task SendPromptAsync_ValidPrompt_ReturnsResponse()
    {
        // Arrange
        var prompt = "Write a simple C# function that adds two integers.";

        // Act
        var response = await _aiServer.SendPromptAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Response);
        Assert.NotEmpty(response.Response);
        Assert.Equal(_config.ModelName, response.Model);
        Assert.True(response.Done, "Response should be marked as done.");
    }

    [Fact]
    public async Task SendPromptAsync_EmptyPrompt_ThrowsArgumentException()
    {
        // Arrange
        var prompt = string.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _aiServer.SendPromptAsync(prompt));
    }

    [Fact]
    public async Task SendPromptAsync_NullPrompt_ThrowsArgumentException()
    {
        // Arrange
        string? prompt = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _aiServer.SendPromptAsync(prompt!));
    }

    [Fact]
    public async Task SendPromptAsync_MultipleRequests_AllSucceed()
    {
        // Arrange
        var prompts = new[]
        {
            "What is 2 + 2?",
            "Write a hello world function.",
            "Explain recursion in one sentence."
        };

        // Act
        var tasks = prompts.Select(p => _aiServer.SendPromptAsync(p));
        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(prompts.Length, responses.Length);
        Assert.All(responses, response =>
        {
            Assert.NotNull(response);
            Assert.NotNull(response.Response);
            Assert.NotEmpty(response.Response);
        });
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
