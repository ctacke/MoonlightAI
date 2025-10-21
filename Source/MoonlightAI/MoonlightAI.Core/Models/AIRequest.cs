using System.Text.Json.Serialization;

namespace MoonlightAI.Core.Models;

/// <summary>
/// Represents a request to the AI server.
/// </summary>
public class AIRequest
{
    /// <summary>
    /// The model to use for generation.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The prompt to send to the model.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// If false, the response will be returned as a single response object.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}
