using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Models;

namespace MoonlightAI.Core.Git;

public class RepositoryManager
{
    private readonly RepositoryConfigurations _config;
    private readonly ILogger<RepositoryManager> _logger;

    public RepositoryManager(RepositoryConfigurations config, ILogger<RepositoryManager> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
