using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Configuration;

namespace MoonlightAI.Core.Containerization;

/// <summary>
/// Manages Docker container lifecycle for AI server.
/// </summary>
public class DockerContainerManager : IContainerManager
{
    private readonly ILogger<DockerContainerManager> _logger;
    private readonly ContainerConfiguration _config;

    public DockerContainerManager(
        ILogger<DockerContainerManager> logger,
        ContainerConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public async Task<bool> IsContainerRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.UseLocalContainer)
        {
            _logger.LogDebug("Local container is not enabled in configuration");
            return false;
        }

        try
        {
            _logger.LogDebug("Checking if container '{ContainerName}' is running", _config.ContainerName);

            var result = await ExecuteDockerCommandAsync(
                $"ps --filter \"name={_config.ContainerName}\" --filter \"status=running\" --format \"{{{{.Names}}}}\"",
                cancellationToken);

            var isRunning = !string.IsNullOrWhiteSpace(result) && result.Contains(_config.ContainerName);

            _logger.LogDebug("Container '{ContainerName}' running status: {IsRunning}", _config.ContainerName, isRunning);
            return isRunning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if container '{ContainerName}' is running", _config.ContainerName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StartContainerAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.UseLocalContainer)
        {
            _logger.LogDebug("Local container is not enabled in configuration");
            return false;
        }

        try
        {
            // Check if container exists (running or stopped)
            var containerExists = await ContainerExistsAsync(cancellationToken);

            if (containerExists)
            {
                // Container exists, just start it
                _logger.LogInformation("Starting existing container '{ContainerName}'", _config.ContainerName);

                var result = await ExecuteDockerCommandAsync(
                    $"start {_config.ContainerName}",
                    cancellationToken);

                var success = !string.IsNullOrWhiteSpace(result) && result.Contains(_config.ContainerName);

                if (success)
                {
                    _logger.LogInformation("Container '{ContainerName}' started successfully", _config.ContainerName);
                }
                else
                {
                    _logger.LogWarning("Failed to start container '{ContainerName}'. Output: {Output}", _config.ContainerName, result);
                }

                return success;
            }
            else
            {
                // Container doesn't exist, create and run it from the image
                _logger.LogInformation("Container '{ContainerName}' does not exist. Creating from image '{ImageName}'", _config.ContainerName, _config.ImageName);

                // Run container with GPU support and port mapping
                // Note: No command specified - uses the ENTRYPOINT from Dockerfile (ollama serve)
                var result = await ExecuteDockerCommandAsync(
                    $"run -d --name {_config.ContainerName} --gpus all -p {_config.HostPort}:11434 --restart unless-stopped {_config.ImageName}",
                    cancellationToken);

                var success = !string.IsNullOrWhiteSpace(result);

                if (success)
                {
                    _logger.LogInformation("Container '{ContainerName}' created and started successfully with port mapping {HostPort}:11434", _config.ContainerName, _config.HostPort);
                }
                else
                {
                    _logger.LogError("Failed to create and start container '{ContainerName}' from image '{ImageName}'. Output: {Output}", _config.ContainerName, _config.ImageName, result);
                }

                return success;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting container '{ContainerName}'", _config.ContainerName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StopContainerAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.UseLocalContainer)
        {
            _logger.LogDebug("Local container is not enabled in configuration");
            return false;
        }

        try
        {
            _logger.LogInformation("Stopping container '{ContainerName}'", _config.ContainerName);

            var result = await ExecuteDockerCommandAsync(
                $"stop {_config.ContainerName}",
                cancellationToken);

            var success = !string.IsNullOrWhiteSpace(result) && result.Contains(_config.ContainerName);

            if (success)
            {
                _logger.LogInformation("Container '{ContainerName}' stopped successfully", _config.ContainerName);
            }
            else
            {
                _logger.LogWarning("Failed to stop container '{ContainerName}'. Output: {Output}", _config.ContainerName, result);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping container '{ContainerName}'", _config.ContainerName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PruneContainerAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.UseLocalContainer)
        {
            _logger.LogDebug("Local container is not enabled in configuration");
            return false;
        }

        try
        {
            _logger.LogInformation("Pruning stopped containers");

            var result = await ExecuteDockerCommandAsync(
                "container prune -f",
                cancellationToken);

            _logger.LogInformation("Container prune completed. Output: {Output}", result);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pruning containers");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> EnsureContainerRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.UseLocalContainer)
        {
            _logger.LogDebug("Local container is not enabled in configuration, skipping container checks");
            return true;
        }

        _logger.LogInformation("Ensuring container '{ContainerName}' is running", _config.ContainerName);

        // Check if container is already running
        var isRunning = await IsContainerRunningAsync(cancellationToken);
        if (isRunning)
        {
            _logger.LogInformation("Container '{ContainerName}' is already running", _config.ContainerName);
            return true;
        }

        // If not running and AutoStart is enabled, try to start it
        if (_config.AutoStart)
        {
            _logger.LogInformation("Container '{ContainerName}' is not running. AutoStart is enabled, attempting to start", _config.ContainerName);
            var started = await StartContainerAsync(cancellationToken);

            if (started)
            {
                // Wait a few seconds for the container to fully initialize
                _logger.LogInformation("Waiting for container '{ContainerName}' to initialize", _config.ContainerName);
                await Task.Delay(5000, cancellationToken);
                return true;
            }
            else
            {
                _logger.LogError("Failed to start container '{ContainerName}'", _config.ContainerName);
                return false;
            }
        }
        else
        {
            _logger.LogWarning("Container '{ContainerName}' is not running and AutoStart is disabled", _config.ContainerName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CleanupContainerAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.UseLocalContainer)
        {
            _logger.LogDebug("Local container is not enabled in configuration, skipping cleanup");
            return true;
        }

        _logger.LogInformation("Cleaning up container '{ContainerName}'", _config.ContainerName);

        bool success = true;

        // Stop container if configured
        if (_config.AutoStop)
        {
            var stopped = await StopContainerAsync(cancellationToken);
            if (!stopped)
            {
                _logger.LogWarning("Failed to stop container '{ContainerName}' during cleanup", _config.ContainerName);
                success = false;
            }
        }

        // Prune containers if configured
        if (_config.PruneAfterStop && _config.AutoStop)
        {
            var pruned = await PruneContainerAsync(cancellationToken);
            if (!pruned)
            {
                _logger.LogWarning("Failed to prune containers during cleanup");
                success = false;
            }
        }

        if (success)
        {
            _logger.LogInformation("Container cleanup completed successfully");
        }
        else
        {
            _logger.LogWarning("Container cleanup completed with warnings");
        }

        return success;
    }

    /// <summary>
    /// Checks if a container with the configured name exists (running or stopped).
    /// </summary>
    private async Task<bool> ContainerExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteDockerCommandAsync(
                $"ps -a --filter \"name=^{_config.ContainerName}$\" --format \"{{{{.Names}}}}\"",
                cancellationToken);

            return !string.IsNullOrWhiteSpace(result) && result.Trim() == _config.ContainerName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if container '{ContainerName}' exists", _config.ContainerName);
            return false;
        }
    }

    /// <summary>
    /// Executes a Docker command and returns the output.
    /// </summary>
    private async Task<string> ExecuteDockerCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _logger.LogDebug("Executing docker command: docker {Arguments}", arguments);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Docker command failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
        }

        return output.Trim();
    }
}
