using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace MoonlightAI.Core.Servers;

/// <summary>
/// Implementation of IAIServer for CodeLlama via Ollama API.
/// </summary>
public class CodeLlamaServer : IAIServer, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AIServerConfiguration _configuration;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the CodeLlamaServer class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API calls.</param>
    /// <param name="configuration">The AI server configuration.</param>
    public CodeLlamaServer(HttpClient httpClient, AIServerConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _httpClient.BaseAddress = new Uri(_configuration.ServerUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
    }

    public Task<AIResponse> GenerateMethodXmlDocumentationAsync(string method, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Prompt cannot be null or empty.", nameof(method));
        }

        // codellama tries to be helpful and returns the method.  this is to try to constrain it to not do that
        var prompt = $"""
            Generate XML documentation comments for the following C# method:

            {method}

            Output **only** the XML documentation comment block.

            Rules:
            1. Output must begin with "/// <summary>".
            2. Every line must start with "///".
            3. Document all parameters and return values.
            4. Do NOT include the method signature or any code.
            5. Do NOT include <member> tags.
            6. Do NOT explain, describe, or add any extra text.
            7. Your entire output must be between <doc> and </doc> markers.

            <doc>
            /// ...
            </doc>
            """;

        return SendPromptAsync(prompt, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIResponse> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
        }

        try
        {
            var request = new AIRequest
            {
                Model = _configuration.ModelName,
                Prompt = prompt,
                Stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var aiResponse = await response.Content.ReadFromJsonAsync<AIResponse>(cancellationToken);

            if (aiResponse == null)
            {
                throw new InvalidOperationException("Received null response from AI server.");
            }

            return aiResponse;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to communicate with AI server at {_configuration.ServerUrl}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"Request to AI server timed out after {_configuration.TimeoutSeconds} seconds.", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse AI server response.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Ollama API doesn't have a dedicated health endpoint, so we'll try to get the tags list
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes of the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the resources used by this instance.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
    }
}
