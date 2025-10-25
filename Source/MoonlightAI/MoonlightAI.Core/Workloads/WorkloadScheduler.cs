using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;

namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Schedules workloads by selecting files and preventing duplicate work across open PRs.
/// </summary>
public class WorkloadScheduler : IWorkloadScheduler
{
    private readonly ILogger<WorkloadScheduler> _logger;
    private readonly IGitManager _gitManager;
    private readonly ICodeAnalyzer _codeAnalyzer;

    public WorkloadScheduler(
        ILogger<WorkloadScheduler> logger,
        IGitManager gitManager,
        ICodeAnalyzer codeAnalyzer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitManager = gitManager ?? throw new ArgumentNullException(nameof(gitManager));
        _codeAnalyzer = codeAnalyzer ?? throw new ArgumentNullException(nameof(codeAnalyzer));
    }

    /// <inheritdoc/>
    public async Task<List<string>> SelectFilesForWorkloadAsync(
        string repositoryPath,
        RepositoryConfiguration repoConfig,
        string workloadType,
        string? projectPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Selecting files for workload type: {WorkloadType}", workloadType);

        // Discover candidate files
        var candidateFiles = DiscoverCandidateFiles(repositoryPath, projectPath);
        _logger.LogInformation("Found {Count} candidate files", candidateFiles.Count);

        // Get files that are already in open PRs
        var filesInOpenPRs = await GetFilesInOpenPullRequestsAsync(repoConfig, workloadType, cancellationToken);
        _logger.LogInformation("Found {Count} files in open PRs", filesInOpenPRs.Count);

        // Filter out files that are already in PRs and validate remaining files
        var selectedFiles = new List<string>();

        foreach (var file in candidateFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Normalize path for comparison
                var relativePath = Path.GetRelativePath(repositoryPath, file);
                var normalizedPath = NormalizePath(relativePath);

                // Skip if file is already in an open PR
                if (filesInOpenPRs.Contains(normalizedPath))
                {
                    _logger.LogDebug("Skipping file already in PR: {FilePath}", relativePath);
                    continue;
                }

                // Check if file should be processed for this workload type
                if (await ShouldProcessFileAsync(file, repositoryPath, workloadType, cancellationToken))
                {
                    selectedFiles.Add(file);
                    _logger.LogDebug("Selected file: {FilePath}", relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating file: {FilePath}", file);
            }
        }

        _logger.LogInformation("Selected {Count} files for processing", selectedFiles.Count);
        return selectedFiles;
    }

    /// <inheritdoc/>
    public async Task<HashSet<string>> GetFilesInOpenPullRequestsAsync(
        RepositoryConfiguration repoConfig,
        string workloadType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all open PRs
            var openPRBranches = await _gitManager.GetExistingPullRequestsAsync(repoConfig, cancellationToken);

            var filesInPRs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Extract file paths from branch names
            // Branch naming convention: moonlight/{workload-type}/{relative-file-path}
            var branchPrefix = $"moonlight/{workloadType}/";

            foreach (var branch in openPRBranches)
            {
                if (branch.StartsWith(branchPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var filePath = branch.Substring(branchPrefix.Length);
                    var normalizedPath = NormalizePath(filePath);
                    filesInPRs.Add(normalizedPath);
                    _logger.LogDebug("Found file in open PR: {FilePath}", filePath);
                }
            }

            return filesInPRs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving files from open PRs");
            // Return empty set to allow processing to continue
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldProcessFileAsync(
        string filePath,
        string repositoryPath,
        string workloadType,
        CancellationToken cancellationToken = default)
    {
        // Validate the file is a valid candidate
        if (!IsValidCandidateFile(filePath, workloadType))
        {
            return false;
        }

        // Workload-specific validation
        switch (workloadType.ToLowerInvariant())
        {
            case "codedoc":
            case "code-documentation":
                return await ShouldDocumentFileAsync(filePath, cancellationToken);

            // Add more workload types here in the future
            // case "cleanup":
            //     return await ShouldCleanupFileAsync(filePath, cancellationToken);
            // case "unittest":
            //     return await ShouldGenerateTestsForFileAsync(filePath, cancellationToken);

            default:
                _logger.LogWarning("Unknown workload type: {WorkloadType}", workloadType);
                return false;
        }
    }

    private List<string> DiscoverCandidateFiles(string repositoryPath, string? projectPath)
    {
        var searchPath = repositoryPath;

        // If projectPath is provided, limit scope to that project directory
        if (!string.IsNullOrEmpty(projectPath))
        {
            var projectDir = Path.GetDirectoryName(Path.Combine(repositoryPath, projectPath));
            if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
            {
                searchPath = projectDir;
            }
        }

        // Find all C# files, excluding obj and bin directories
        var csFiles = Directory.GetFiles(searchPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                       !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        return csFiles;
    }

    private bool IsValidCandidateFile(string filePath, string workloadType)
    {
        // Must be a C# file
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Exclude obj and bin directories
        if (filePath.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
            filePath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
        {
            return false;
        }

        // Workload-specific exclusions
        switch (workloadType.ToLowerInvariant())
        {
            case "codedoc":
            case "code-documentation":
                // Exclude generated files, designer files, etc.
                if (filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                break;
        }

        return true;
    }

    private async Task<bool> ShouldDocumentFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            // Analyze the file to see if it needs documentation
            var analysis = await _codeAnalyzer.AnalyzeFileAsync(filePath, cancellationToken);

            if (!analysis.ParsedSuccessfully)
            {
                _logger.LogDebug("Skipping file with parse errors: {FilePath}", filePath);
                return false;
            }

            // Check if there are any public members without documentation
            var needsDocumentation = analysis.Classes
                .Where(c => c.Accessibility == "public")
                .Any(c =>
                    // Class itself needs documentation
                    c.XmlDocumentation == null ||
                    // Public methods need documentation
                    c.Methods.Any(m => m.Accessibility == "public" && m.XmlDocumentation == null) ||
                    // Public properties need documentation
                    c.Properties.Any(p => p.Accessibility == "public" && p.XmlDocumentation == null) ||
                    // Public const/readonly fields need documentation
                    c.Fields.Any(f => f.Accessibility == "public" && (f.IsConst || f.IsReadOnly) && f.XmlDocumentation == null));

            if (!needsDocumentation)
            {
                _logger.LogDebug("File already fully documented: {FilePath}", filePath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing file: {FilePath}", filePath);
            return false;
        }
    }

    private string NormalizePath(string path)
    {
        // Normalize path separators and remove leading/trailing separators
        return path.Replace('\\', '/').Trim('/');
    }
}
