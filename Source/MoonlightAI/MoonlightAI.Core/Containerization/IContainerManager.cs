namespace MoonlightAI.Core.Containerization;

/// <summary>
/// Interface for managing Docker container lifecycle.
/// </summary>
public interface IContainerManager
{
    /// <summary>
    /// Checks if the container is currently running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the container is running, false otherwise.</returns>
    Task<bool> IsContainerRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the container if it exists but is not running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the container was started successfully, false otherwise.</returns>
    Task<bool> StartContainerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the container if it's running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the container was stopped successfully, false otherwise.</returns>
    Task<bool> StopContainerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Prunes stopped containers to save resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if prune was successful, false otherwise.</returns>
    Task<bool> PruneContainerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the container is running, starting it if necessary based on configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the container is running or was started successfully, false otherwise.</returns>
    Task<bool> EnsureContainerRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up the container after workloads complete, based on configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if cleanup was successful, false otherwise.</returns>
    Task<bool> CleanupContainerAsync(CancellationToken cancellationToken = default);
}
