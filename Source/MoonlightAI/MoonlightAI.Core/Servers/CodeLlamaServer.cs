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

        // Select prompt based on model type
        var prompt = GetModelSpecificPrompt(method);

        return SendPromptAsync(prompt, cancellationToken);
    }

    /// <summary>
    /// Gets the optimal prompt for the configured model.
    /// </summary>
    private string GetModelSpecificPrompt(string method)
    {
        var modelName = _configuration.ModelName.ToLowerInvariant();

        // Detect model family from name
        if (modelName.Contains("mistral"))
        {
            return GetMistralPrompt(method);
        }
        else if (modelName.Contains("llama") && modelName.Contains("instruct"))
        {
            return GetLlamaInstructPrompt(method);
        }
        else if (modelName.Contains("codellama") || modelName.Contains("code-llama"))
        {
            return GetCodeLlamaPrompt(method);
        }
        else if (modelName.Contains("deepseek"))
        {
            return GetDeepSeekPrompt(method);
        }
        else
        {
            // Default to CodeLlama-style prompt
            return GetCodeLlamaPrompt(method);
        }
    }

    /// <summary>
    /// Optimized prompt for CodeLlama models (code-completion focused).
    /// </summary>
    private string GetCodeLlamaPrompt(string method)
    {
        return $"""
            You are a C# XML documentation generator. Your task is to generate ONLY the XML documentation comments for the method below.

            C# Method to document:
            ```csharp
            {method}
            ```

            CRITICAL REQUIREMENTS:
            1. Output ONLY the XML documentation comment lines (starting with "///")
            2. DO NOT include the method code itself
            3. DO NOT add XML tags that don't match the method signature
            4. ONLY use these valid XML tags: <summary>, <param>, <returns>, <remarks>, <exception>
            5. For void methods or methods returning Task (with no generic parameter), DO NOT include a <returns> tag
            6. ONLY document parameters that actually exist in the method signature
            7. DO NOT add closing tags without matching opening tags
            8. Keep descriptions concise, clear, and accurate
            9. DO NOT include <member> tags or any other wrapper tags
            10. Your entire output must be wrapped in <doc></doc> markers

            VALIDATION CHECKLIST:
            - Check the method's return type before adding <returns>
            - Verify each parameter name exists in the signature
            - Ensure every opening tag has a matching closing tag
            - Count parameters: only add <param> tags for actual parameters

            EXAMPLE FORMAT (for a method with parameters and return value):
            <doc>
            /// <summary>
            /// Brief description of what the method does.
            /// </summary>
            /// <param name="actualParamName">Description of the parameter.</param>
            /// <returns>Description of what the method returns.</returns>
            </doc>

            EXAMPLE FORMAT (for a void method or method returning Task):
            <doc>
            /// <summary>
            /// Brief description of what the method does.
            /// </summary>
            /// <param name="actualParamName">Description of the parameter.</param>
            </doc>

            OUTPUT (XML documentation comments ONLY, wrapped in <doc></doc>):
            """;
    }

    /// <summary>
    /// Optimized prompt for Mistral Instruct models (direct, command-style).
    /// </summary>
    private string GetMistralPrompt(string method)
    {
        return $"""
            TASK: Generate C# XML documentation comments

            METHOD TO DOCUMENT:
            {method}

            INSTRUCTIONS:
            - Generate ONLY lines starting with ///
            - Use these XML tags: <summary>, <param>, <returns>, <remarks>
            - For void methods or methods returning Task: omit <returns> tag
            - Verify all parameter names match the method signature exactly
            - Do not include method code or any explanations
            - Wrap output in <doc></doc> tags

            RULES:
            • Every line must start with ///
            • Every opening tag needs a closing tag
            • No orphaned closing tags
            • No hallucinated parameters
            • Keep descriptions brief and accurate

            FORMAT EXAMPLE (method with return value):
            <doc>
            /// <summary>
            /// Description of method purpose.
            /// </summary>
            /// <param name="paramName">Parameter description.</param>
            /// <returns>Return value description.</returns>
            </doc>

            FORMAT EXAMPLE (void/Task method):
            <doc>
            /// <summary>
            /// Description of method purpose.
            /// </summary>
            /// <param name="paramName">Parameter description.</param>
            </doc>

            OUTPUT:
            """;
    }

    /// <summary>
    /// Optimized prompt for Llama 3+ Instruct models (balanced approach).
    /// </summary>
    private string GetLlamaInstructPrompt(string method)
    {
        return $"""
            Generate XML documentation comments for this C# method. Follow the instructions carefully.

            Method:
            ```csharp
            {method}
            ```

            Requirements:
            • Output only the XML doc comment lines (starting with ///)
            • Use only valid XML tags: <summary>, <param>, <returns>, <remarks>, <exception>
            • Do NOT include <returns> for void methods or methods returning Task
            • Only document parameters that exist in the signature
            • Ensure all tags are properly opened and closed
            • Keep descriptions clear and concise
            • Wrap your output in <doc></doc> tags

            Important checks:
            1. Is the return type void or Task? → Don't add <returns>
            2. Match each <param> tag to an actual parameter
            3. Verify tag balance (no orphaned closing tags)

            Example for method with return value:
            <doc>
            /// <summary>
            /// Describes what the method does.
            /// </summary>
            /// <param name="paramName">Describes the parameter.</param>
            /// <returns>Describes the return value.</returns>
            </doc>

            Example for void/Task method:
            <doc>
            /// <summary>
            /// Describes what the method does.
            /// </summary>
            /// <param name="paramName">Describes the parameter.</param>
            </doc>

            Generate documentation:
            """;
    }

    /// <summary>
    /// Optimized prompt for DeepSeek Coder models (code-understanding focused).
    /// </summary>
    private string GetDeepSeekPrompt(string method)
    {
        return $"""
            Task: Generate C# XML documentation

            Input method:
            ```csharp
            {method}
            ```

            Instructions:
            1. Analyze the method signature carefully
            2. Generate only /// comment lines
            3. Use standard XML doc tags: <summary>, <param>, <returns>
            4. For void/Task methods: skip <returns> tag
            5. Match parameter names exactly
            6. Ensure balanced XML tags
            7. Keep descriptions precise and technical
            8. Wrap output in <doc></doc>

            Quality checks:
            - Return type void or Task? → No <returns> tag
            - All parameters documented? → Match signature
            - All tags closed? → Check balance

            Output format (with return):
            <doc>
            /// <summary>Brief description.</summary>
            /// <param name="name">Parameter purpose.</param>
            /// <returns>Return value description.</returns>
            </doc>

            Output format (void/Task):
            <doc>
            /// <summary>Brief description.</summary>
            /// <param name="name">Parameter purpose.</param>
            </doc>

            Generate:
            """;
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

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Model '{_configuration.ModelName}' not found on AI server. " +
                        $"Please ensure the model is pulled. Error: {errorContent}");
                }
                throw new HttpRequestException(
                    $"AI server returned {response.StatusCode}: {errorContent}");
            }

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
