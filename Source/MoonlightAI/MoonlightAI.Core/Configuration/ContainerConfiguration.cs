namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration settings for Docker container management.
/// </summary>
public class ContainerConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Container";

    /// <summary>
    /// Whether to use a local container for the AI server.
    /// </summary>
    public bool UseLocalContainer { get; set; } = false;

    /// <summary>
    /// The name of the container image to use.
    /// </summary>
    public string ImageName { get; set; } = "moonlight-llm-server";

    /// <summary>
    /// The name to assign to the running container instance.
    /// </summary>
    public string ContainerName { get; set; } = "moonlight-llm-server";

    /// <summary>
    /// Host port to map to container port 11434.
    /// </summary>
    public int HostPort { get; set; } = 11434;

    /// <summary>
    /// Whether to automatically start the container if it's not running.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Whether to automatically stop the container after all workloads complete.
    /// </summary>
    public bool AutoStop { get; set; } = true;

    /// <summary>
    /// Whether to prune the container after stopping to save resources.
    /// </summary>
    public bool PruneAfterStop { get; set; } = true;

    /// <summary>
    /// Path to the local folder containing Ollama models.
    /// This folder will be mounted as a volume in the container.
    /// </summary>
    public string ModelsPath { get; set; } = "./ollama-models";
}
