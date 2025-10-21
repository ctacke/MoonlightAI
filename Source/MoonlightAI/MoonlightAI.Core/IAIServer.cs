using MoonlightAI.Core.Models;

namespace MoonlightAI.Core;

/// <summary>
/// Interface for AI server connections that can process prompts and return responses.
/// </summary>
public interface IAIServer
{
    /// <summary>
    /// Sends a prompt to the AI server and returns the response.
    /// </summary>
    /// <param name="prompt">The prompt to send to the AI server.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The AI server response.</returns>
    Task<AIResponse> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the AI server is reachable and healthy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>True if the server is healthy, false otherwise.</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
