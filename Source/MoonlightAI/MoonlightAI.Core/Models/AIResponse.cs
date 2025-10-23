using System.Text.Json.Serialization;

namespace MoonlightAI.Core.Models;

/// <summary>
/// Represents a response from the AI server.
/// </summary>
public class AIResponse
{
    /// <summary>
    /// The model used for generation.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The generated response text.
    /// </summary>
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Whether the generation is complete.
    /// </summary>
    [JsonPropertyName("done")]
    public bool Done { get; set; }

    /// <summary>
    /// Timestamp when the response was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Total duration of the generation in nanoseconds.
    /// </summary>
    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    /// <summary>
    /// Number of tokens in the prompt (input tokens).
    /// </summary>
    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    /// <summary>
    /// Number of tokens in the response (output tokens).
    /// </summary>
    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }

    /// <summary>
    /// Total tokens (prompt + response).
    /// </summary>
    public int TotalTokens => (PromptEvalCount ?? 0) + (EvalCount ?? 0);
}
