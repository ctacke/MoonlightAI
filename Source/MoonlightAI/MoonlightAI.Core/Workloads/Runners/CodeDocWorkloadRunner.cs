using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Analysis;
using System.Text;

namespace MoonlightAI.Core.Workloads.Runners;

/// <summary>
/// Workload runner for generating XML documentation for C# code.
/// </summary>
public class CodeDocWorkloadRunner : IWorkloadRunner<CodeDocWorkload>
{
    private readonly ILogger<CodeDocWorkloadRunner> _logger;
    private readonly IAIServer _aiServer;
    private readonly RoslynCodeAnalyzer _codeAnalyzer;

    public CodeDocWorkloadRunner(
        ILogger<CodeDocWorkloadRunner> logger,
        IAIServer aiServer,
        RoslynCodeAnalyzer codeAnalyzer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiServer = aiServer ?? throw new ArgumentNullException(nameof(aiServer));
        _codeAnalyzer = codeAnalyzer ?? throw new ArgumentNullException(nameof(codeAnalyzer));
    }

    /// <inheritdoc/>
    public async Task<WorkloadResult> ExecuteAsync(CodeDocWorkload workload, string repositoryPath, CancellationToken cancellationToken = default)
    {
        var result = new WorkloadResult
        {
            Workload = workload
        };

        // TODO: find a file to work on
        workload.FilePath = @"src\Engine\Modules\MQTT\SolutionEngine.MQTT.Module\Services\MqttPublisherService.cs";

        // TODO: add a configuration for this
        workload.DocumentVisibility = MemberVisibility.Public | MemberVisibility.Internal;

        try
        {
            workload.State = WorkloadState.Running;
            workload.Statistics.StartedAt = DateTime.UtcNow;

            _logger.LogInformation("Starting code documentation workload for file: {FilePath}", workload.FilePath);

            // Validate the file path
            if (string.IsNullOrWhiteSpace(workload.FilePath))
            {
                throw new InvalidOperationException("FilePath is required for code documentation workload");
            }

            var fullFilePath = Path.Combine(repositoryPath, workload.FilePath);

            if (!File.Exists(fullFilePath))
            {
                throw new FileNotFoundException($"File not found: {fullFilePath}");
            }

            // Process the single file
            var modified = await ProcessFileAsync(fullFilePath, workload, cancellationToken);

            if (modified)
            {
                workload.Statistics.FilesProcessed = 1;
            }
            else
            {
                workload.Statistics.FilesProcessed = 0;
                _logger.LogInformation("No modifications were needed for {FilePath}", workload.FilePath);
            }

            workload.State = WorkloadState.Completed;
            workload.Statistics.CompletedAt = DateTime.UtcNow;

            result.State = WorkloadState.Completed;
            result.Statistics = workload.Statistics;

            if (workload.Statistics.FilesProcessed > 0)
            {
                result.Summary = $"Documented {workload.Statistics.ItemsModified} items in {Path.GetFileName(workload.FilePath)}, " +
                               $"{workload.Statistics.ErrorCount} errors";
            }
            else
            {
                result.Summary = $"No documentation needed for {Path.GetFileName(workload.FilePath)}";
            }

            // Generate git messages
            result.CommitMessage = GenerateCommitMessage(workload, result);
            result.PullRequestTitle = GeneratePRTitle(workload, result);
            result.PullRequestBody = GeneratePRBody(workload, result);

            _logger.LogInformation("Code documentation workload completed: {Summary}", result.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in code documentation workload");
            workload.State = WorkloadState.Failed;
            workload.Statistics.CompletedAt = DateTime.UtcNow;
            workload.Statistics.ErrorCount++;
            workload.Statistics.Errors.Add(ex.Message);

            result.State = WorkloadState.Failed;
            result.Statistics = workload.Statistics;
            result.Summary = $"Workload failed: {ex.Message}";
        }

        return result;
    }

    private async Task<bool> ProcessFileAsync(string filePath, CodeDocWorkload workload, CancellationToken cancellationToken)
    {
        var fileModified = false;
        var fileAnalysis = await _codeAnalyzer.AnalyzeFileAsync(filePath, cancellationToken);

        if (!fileAnalysis.ParsedSuccessfully)
        {
            _logger.LogWarning("Skipping file with parse errors: {FilePath}", filePath);
            return false;
        }

        // Process classes that match visibility criteria
        foreach (var classInfo in fileAnalysis.Classes.Where(c => ShouldDocument(c.Accessibility, workload.DocumentVisibility)))
        {
            // Process methods
            foreach (var method in classInfo.Methods.Where(m => ShouldDocument(m.Accessibility, workload.DocumentVisibility) && m.XmlDocumentation == null))
            {
                try
                {
                    var modified = await DocumentMethodAsync(filePath, method, workload, cancellationToken);
                    if (modified)
                    {
                        fileModified = true;
                        workload.Statistics.ItemsModified++;

                        // Re-analyze the file after modification
                        fileAnalysis = await _codeAnalyzer.AnalyzeFileAsync(filePath, cancellationToken);
                    }
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("AI server timed out for method {MethodName} in {FilePath}", method.Name, filePath);
                    workload.Statistics.ErrorCount++;
                    workload.Statistics.Errors.Add($"Timeout: {classInfo.Name}.{method.Name}");
                }
            }

            // Process properties
            foreach (var property in classInfo.Properties.Where(p => ShouldDocument(p.Accessibility, workload.DocumentVisibility) && p.XmlDocumentation == null))
            {
                // TODO: Implement property documentation generation
                _logger.LogDebug("Property {PropertyName} needs documentation (not yet implemented)", property.Name);
            }

            // Document class itself if needed
            if (classInfo.XmlDocumentation == null)
            {
                // TODO: Implement class documentation generation
                _logger.LogDebug("Class {ClassName} needs documentation (not yet implemented)", classInfo.Name);
            }
        }

        return fileModified;
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

    private async Task<bool> DocumentMethodAsync(string filePath, Models.Analysis.MethodInfo method, CodeDocWorkload workload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating documentation for method {MethodName} at line {LineNumber}", method.Name, method.FirstLineNumber);

        var originalContentLines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        // Extract method code
        var methodCode = originalContentLines
            .Skip(method.FirstLineNumber - 1)
            .Take(method.LastLineNumber - method.FirstLineNumber + 1);
        var methodSource = string.Join(Environment.NewLine, methodCode);

        // Generate documentation
        var startTime = DateTime.UtcNow;
        var aiResponse = await _aiServer.GenerateMethodXmlDocumentationAsync(methodSource, cancellationToken);
        var aiDuration = DateTime.UtcNow - startTime;

        workload.Statistics.AIApiCalls++;
        workload.Statistics.TotalAIProcessingTime += aiDuration;

        if (!aiResponse.Done)
        {
            _logger.LogWarning("AI response not complete for method {MethodName}", method.Name);
            return false;
        }

        _logger.LogDebug("Documentation generation took {Duration}", aiDuration);

        // Calculate indentation
        var indentation = new string(' ', originalContentLines[method.FirstLineNumber - 1].TakeWhile(c => c == ' ').Count());

        // Parse and clean documentation lines
        var docLines = aiResponse.Response
            .Trim('`')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("///"))
            .Select(l => $"{indentation}{l.TrimEnd('\r', '\n')}")
            .ToList();

        if (docLines.Count == 0)
        {
            _logger.LogWarning("No valid documentation lines generated for method {MethodName}", method.Name);
            return false;
        }

        // Build new file content
        var sb = new StringBuilder();

        // Write everything before the method
        for (var i = 0; i < method.FirstLineNumber - 1; i++)
        {
            sb.AppendLine(originalContentLines[i]);
        }

        // Write the documentation
        foreach (var docLine in docLines)
        {
            sb.AppendLine(docLine);
        }

        // Write the method and everything after
        for (var i = method.FirstLineNumber - 1; i < originalContentLines.Length; i++)
        {
            sb.AppendLine(originalContentLines[i]);
        }

        // Back up original file
        var backupPath = filePath + ".bak";
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
        File.Move(filePath, backupPath);

        // Write modified file
        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

        _logger.LogInformation("Successfully documented method {MethodName}", method.Name);
        return true;
    }

    private string GenerateCommitMessage(CodeDocWorkload workload, WorkloadResult result)
    {
        var fileName = Path.GetFileName(workload.FilePath);
        var message = $"Add XML documentation to {fileName}\n\n" +
                     $"Generated by MoonlightAI code documentation workload\n" +
                     $"- Items documented: {result.Statistics.ItemsModified}\n" +
                     $"- AI API calls: {result.Statistics.AIApiCalls}\n" +
                     $"- Duration: {result.Statistics.Duration?.TotalSeconds:F1} seconds\n\n" +
                     $"🤖 Generated with [MoonlightAI](https://github.com/ctacke/MoonlightAI)\n\n" +
                     $"Co-Authored-By: MoonlightAI <moonlight@example.com>";

        return message;
    }

    private string GeneratePRTitle(CodeDocWorkload workload, WorkloadResult result)
    {
        var fileName = Path.GetFileName(workload.FilePath);
        return $"[MoonlightAI] Add XML documentation to {fileName}";
    }

    private string GeneratePRBody(CodeDocWorkload workload, WorkloadResult result)
    {
        var fileName = Path.GetFileName(workload.FilePath);
        var body = $"## Summary\n\n" +
                  $"This PR adds XML documentation comments to public methods, properties, and classes " +
                  $"in `{workload.FilePath}`.\n\n" +
                  $"## Statistics\n\n" +
                  $"- **File**: `{fileName}`\n" +
                  $"- **Items documented**: {result.Statistics.ItemsModified}\n" +
                  $"- **AI API calls**: {result.Statistics.AIApiCalls}\n" +
                  $"- **Processing time**: {result.Statistics.Duration?.TotalSeconds:F1} seconds\n" +
                  $"- **AI processing time**: {result.Statistics.TotalAIProcessingTime.TotalSeconds:F1} seconds\n" +
                  $"- **Errors**: {result.Statistics.ErrorCount}\n\n";

        if (result.Statistics.Errors.Any())
        {
            body += "## Errors\n\n";
            foreach (var error in result.Statistics.Errors.Take(10))
            {
                body += $"- {error}\n";
            }
            if (result.Statistics.Errors.Count > 10)
            {
                body += $"\n...and {result.Statistics.Errors.Count - 10} more\n";
            }
            body += "\n";
        }

        body += "## Review Notes\n\n" +
               "Please review the generated documentation for:\n" +
               "- Accuracy of descriptions\n" +
               "- Proper parameter documentation\n" +
               "- Return value descriptions\n" +
               "- Any TODO or FIXME comments that may need attention\n\n" +
               "🤖 Generated with [MoonlightAI](https://github.com/ctacke/MoonlightAI)";

        return body;
    }
}
