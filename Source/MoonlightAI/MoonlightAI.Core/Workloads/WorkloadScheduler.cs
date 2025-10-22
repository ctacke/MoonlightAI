using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Analysis;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;

namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Schedules and generates workloads for repositories and projects.
/// </summary>
public class WorkloadScheduler
{
    private readonly ILogger<WorkloadScheduler> _logger;
    private readonly GitManager _gitManager;
    private readonly RoslynCodeAnalyzer _codeAnalyzer;

    public WorkloadScheduler(
        ILogger<WorkloadScheduler> logger,
        GitManager gitManager,
        RoslynCodeAnalyzer codeAnalyzer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitManager = gitManager ?? throw new ArgumentNullException(nameof(gitManager));
        _codeAnalyzer = codeAnalyzer ?? throw new ArgumentNullException(nameof(codeAnalyzer));
    }

    /// <summary>
    /// Scans a project and generates code documentation workloads for files that need documentation.
    /// </summary>
    /// <param name="repositoryUrl">Repository URL.</param>
    /// <param name="projectPath">Path to the project file (relative to repo root).</param>
    /// <param name="solutionPath">Path to the solution file (optional).</param>
    /// <param name="visibility">Member visibility to document (default: Public).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of workloads to execute.</returns>
    public async Task<List<CodeDocWorkload>> GenerateCodeDocWorkloadsAsync(
        string repositoryUrl,
        string projectPath,
        string? solutionPath = null,
        MemberVisibility visibility = MemberVisibility.Public,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating code documentation workloads for {ProjectPath}", projectPath);

        // Clone or pull the repository
        var repoConfig = new RepositoryConfiguration
        {
            RepositoryUrl = repositoryUrl
        };

        var repositoryPath = await _gitManager.CloneOrPullAsync(repoConfig, cancellationToken);

        // Get the project directory
        var projectDir = Path.Combine(repositoryPath, Path.GetDirectoryName(projectPath) ?? "");
        if (!Directory.Exists(projectDir))
        {
            throw new DirectoryNotFoundException($"Project directory not found: {projectDir}");
        }

        // Find all C# files in the project
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                       !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        _logger.LogInformation("Found {FileCount} C# files in project", csFiles.Count);

        var workloads = new List<CodeDocWorkload>();

        // Check existing PRs to avoid duplicate work
        var existingPRs = await _gitManager.GetExistingPullRequestsAsync(repoConfig, cancellationToken);
        var existingBranches = existingPRs.ToHashSet();

        foreach (var file in csFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Analyze the file to see if it needs documentation
                var analysis = await _codeAnalyzer.AnalyzeFileAsync(file, cancellationToken);

                if (!analysis.ParsedSuccessfully)
                {
                    _logger.LogDebug("Skipping file with parse errors: {FilePath}", file);
                    continue;
                }

                // Check if there are any members without documentation matching the visibility criteria
                var needsDocumentation = analysis.Classes
                    .Where(c => ShouldDocument(c.Accessibility, visibility))
                    .Any(c =>
                        c.Methods.Any(m => ShouldDocument(m.Accessibility, visibility) && m.XmlDocumentation == null) ||
                        c.Properties.Any(p => ShouldDocument(p.Accessibility, visibility) && p.XmlDocumentation == null) ||
                        c.XmlDocumentation == null);

                if (!needsDocumentation)
                {
                    _logger.LogDebug("File already fully documented: {FilePath}", file);
                    continue;
                }

                // Convert absolute path to relative path
                var relativePath = Path.GetRelativePath(repositoryPath, file);

                // Create workload
                var workload = new CodeDocWorkload
                {
                    RepositoryUrl = repositoryUrl,
                    SolutionPath = solutionPath ?? string.Empty,
                    ProjectPath = projectPath,
                    FilePath = relativePath,
                    DocumentVisibility = visibility
                };

                // Check if PR already exists for this file
                var branchName = workload.GetBranchName();
                if (existingBranches.Contains(branchName))
                {
                    _logger.LogDebug("PR already exists for file: {FilePath}", relativePath);
                    continue;
                }

                workloads.Add(workload);
                _logger.LogDebug("Generated workload for: {FilePath}", relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing file: {FilePath}", file);
            }
        }

        _logger.LogInformation("Generated {WorkloadCount} code documentation workloads", workloads.Count);
        return workloads;
    }

    /// <summary>
    /// Gets the next workload to execute from a list.
    /// </summary>
    public CodeDocWorkload? GetNextWorkload(List<CodeDocWorkload> workloads)
    {
        return workloads.FirstOrDefault(w => w.State == WorkloadState.Queued);
    }

    private bool ShouldDocument(string accessibility, MemberVisibility visibility)
    {
        return accessibility.ToLowerInvariant() switch
        {
            "public" => visibility.HasFlag(MemberVisibility.Public),
            "private" => visibility.HasFlag(MemberVisibility.Private),
            "protected" => visibility.HasFlag(MemberVisibility.Protected),
            "internal" => visibility.HasFlag(MemberVisibility.Internal),
            "protected internal" => visibility.HasFlag(MemberVisibility.ProtectedInternal),
            "private protected" => visibility.HasFlag(MemberVisibility.PrivateProtected),
            _ => false
        };
    }
}
