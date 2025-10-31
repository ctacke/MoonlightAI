using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Build;
using MoonlightAI.Core.Configuration;
using System.Text;

namespace MoonlightAI.Core.Workloads.Runners;

/// <summary>
/// Workload runner for code cleanup operations.
/// </summary>
public class CodeCleanupWorkloadRunner : IWorkloadRunner<CodeCleanupWorkload>
{
    private readonly ILogger<CodeCleanupWorkloadRunner> _logger;
    private readonly IAIServer _aiServer;
    private readonly IBuildValidator _buildValidator;
    private readonly IGitManager _gitManager;
    private readonly WorkloadConfiguration _workloadConfig;

    public CodeCleanupWorkloadRunner(
        ILogger<CodeCleanupWorkloadRunner> logger,
        IAIServer aiServer,
        IBuildValidator buildValidator,
        IGitManager gitManager,
        WorkloadConfiguration workloadConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiServer = aiServer ?? throw new ArgumentNullException(nameof(aiServer));
        _buildValidator = buildValidator ?? throw new ArgumentNullException(nameof(buildValidator));
        _gitManager = gitManager ?? throw new ArgumentNullException(nameof(gitManager));
        _workloadConfig = workloadConfig ?? throw new ArgumentNullException(nameof(workloadConfig));
    }

    /// <inheritdoc/>
    public async Task<WorkloadResult> ExecuteAsync(CodeCleanupWorkload workload, string repositoryPath, CancellationToken cancellationToken = default)
    {
        var result = new WorkloadResult
        {
            Workload = workload
        };

        try
        {
            workload.State = WorkloadState.Running;
            workload.Statistics.StartedAt = DateTime.UtcNow;

            _logger.LogInformation("Starting code cleanup workload for file: {FilePath}", workload.FilePath);

            // Validate the file path
            if (string.IsNullOrWhiteSpace(workload.FilePath))
            {
                throw new InvalidOperationException("FilePath is required for code cleanup workload");
            }

            var fullFilePath = Path.Combine(repositoryPath, workload.FilePath);

            if (!File.Exists(fullFilePath))
            {
                throw new FileNotFoundException($"File not found: {fullFilePath}");
            }

            // Process cleanup operations
            var cleanedUp = await ProcessFileAsync(fullFilePath, workload, repositoryPath, cancellationToken);

            if (cleanedUp)
            {
                workload.Statistics.FilesProcessed = 1;
                result.ModifiedFiles.Add(workload.FilePath);
            }
            else
            {
                workload.Statistics.FilesProcessed = 0;
                _logger.LogInformation("No cleanup operations performed for {FilePath}", workload.FilePath);
            }

            workload.State = WorkloadState.Completed;
            workload.Statistics.CompletedAt = DateTime.UtcNow;

            result.State = WorkloadState.Completed;
            result.Statistics = workload.Statistics;

            if (workload.Statistics.ItemsModified > 0)
            {
                result.Summary = $"Cleaned up {workload.Statistics.ItemsModified} items in {Path.GetFileName(workload.FilePath)}, " +
                               $"{workload.Statistics.ErrorCount} errors";
            }
            else
            {
                result.Summary = $"No cleanup needed for {Path.GetFileName(workload.FilePath)}";
            }

            // Generate git messages
            result.CommitMessage = GenerateCommitMessage(workload, result);
            result.PullRequestTitle = GeneratePRTitle(workload, result);
            result.PullRequestBody = GeneratePRBody(workload, result);

            _logger.LogInformation("Code cleanup workload completed: {Summary}", result.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in code cleanup workload");
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

    private async Task<bool> ProcessFileAsync(
        string filePath,
        CodeCleanupWorkload workload,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var fileModified = false;

        // Keep processing until no more cleanup opportunities found
        while (true)
        {
            // Detect cleanup opportunities
            var opportunities = await DetectCleanupOpportunitiesAsync(filePath, workload.Options, cancellationToken);

            if (!opportunities.Any())
            {
                _logger.LogInformation("No more cleanup opportunities found in {FilePath}", filePath);
                break;
            }

            // Take only the first N opportunities based on MaxOperationsPerRun
            var prioritized = opportunities
                .OrderBy(o => GetCleanupPriority(o.Type))
                .ThenBy(o => o.LineNumber)
                .Take(workload.Options.MaxOperationsPerRun)
                .ToList();

            _logger.LogInformation("Found {Count} cleanup opportunities, processing {Process}",
                opportunities.Count, prioritized.Count);

            // Process the first cleanup opportunity
            var opportunity = prioritized.First();

            try
            {
                var success = await ProcessCleanupOpportunityAsync(
                    filePath,
                    opportunity,
                    workload,
                    repositoryPath,
                    cancellationToken);

                if (success)
                {
                    fileModified = true;
                    workload.Statistics.ItemsModified++;
                    _logger.LogInformation("Successfully performed {Type} cleanup at line {Line}",
                        opportunity.Type, opportunity.LineNumber);

                    // Only process one operation per run when MaxOperationsPerRun = 1
                    if (workload.Options.MaxOperationsPerRun == 1)
                    {
                        break;
                    }
                }
                else
                {
                    workload.Statistics.ErrorCount++;
                    workload.Statistics.Errors.Add($"Failed to perform {opportunity.Type} cleanup at line {opportunity.LineNumber}");
                    _logger.LogWarning("Failed to perform {Type} cleanup at line {Line}",
                        opportunity.Type, opportunity.LineNumber);
                    break; // Don't continue if cleanup failed
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cleanup opportunity {Type} at line {Line}",
                    opportunity.Type, opportunity.LineNumber);
                workload.Statistics.ErrorCount++;
                workload.Statistics.Errors.Add($"Error: {opportunity.Type} at line {opportunity.LineNumber}: {ex.Message}");
                break;
            }
        }

        return fileModified;
    }

    private async Task<List<CleanupOpportunity>> DetectCleanupOpportunitiesAsync(
        string filePath,
        CleanupOptions options,
        CancellationToken cancellationToken)
    {
        var opportunities = new List<CleanupOpportunity>();

        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        // Detect unused usings
        if (options.RemoveUnusedUsings)
        {
            var unusedUsings = DetectUnusedUsings(root, tree);
            opportunities.AddRange(unusedUsings);
        }

        // Detect public fields that should be properties
        if (options.ConvertPublicFieldsToProperties)
        {
            var publicFields = DetectPublicFields(root);
            opportunities.AddRange(publicFields);
        }

        // Detect private fields that need reordering
        if (options.ReorderPrivateFields)
        {
            var reorderOpportunities = DetectPrivateFieldOrdering(root);
            opportunities.AddRange(reorderOpportunities);
        }

        // Note: Unused variables require semantic analysis (compilation)
        // This is more complex and will be added in a future iteration

        return opportunities;
    }

    private List<CleanupOpportunity> DetectUnusedUsings(SyntaxNode root, SyntaxTree tree)
    {
        var opportunities = new List<CleanupOpportunity>();

        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        var allText = root.ToFullString();

        foreach (var usingDirective in usings)
        {
            var namespaceName = usingDirective.Name?.ToString();
            if (string.IsNullOrEmpty(namespaceName))
                continue;

            // Simple heuristic: check if namespace is referenced elsewhere in file
            // This is a simplified approach; full semantic analysis would be more accurate
            var usingLineText = usingDirective.ToFullString();
            var fileWithoutUsing = allText.Replace(usingLineText, "");

            // Check if any types from this namespace are used
            // For now, we'll use a conservative approach and only flag System.* namespaces
            // that are clearly unused
            var isLikelyUnused = IsLikelyUnusedUsing(namespaceName, fileWithoutUsing);

            if (isLikelyUnused)
            {
                var lineSpan = tree.GetLineSpan(usingDirective.Span);
                opportunities.Add(new CleanupOpportunity
                {
                    Type = CleanupType.UnusedUsing,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    OriginalCode = usingDirective.ToFullString().Trim(),
                    Context = GetContextAroundLine(root, lineSpan.StartLinePosition.Line + 1, 5),
                    Metadata = new Dictionary<string, string>
                    {
                        ["Namespace"] = namespaceName
                    }
                });
            }
        }

        return opportunities;
    }

    private bool IsLikelyUnusedUsing(string namespaceName, string fileContent)
    {
        // Very conservative check - only flag obviously unused common namespaces
        // This avoids false positives
        var commonUnused = new[]
        {
            "System.Text",
            "System.Linq",
            "System.Threading.Tasks",
            "System.Collections.Generic"
        };

        if (!commonUnused.Contains(namespaceName))
            return false;

        // Check for common type usage from these namespaces
        var indicators = new Dictionary<string, string[]>
        {
            ["System.Text"] = new[] { "StringBuilder", "Encoding" },
            ["System.Linq"] = new[] { ".Select(", ".Where(", ".OrderBy(", ".Any(", ".First(", ".ToList(", ".Count(" },
            ["System.Threading.Tasks"] = new[] { "Task<", "Task ", "async ", "await " },
            ["System.Collections.Generic"] = new[] { "List<", "Dictionary<", "IEnumerable<", "HashSet<" }
        };

        if (indicators.TryGetValue(namespaceName, out var typeIndicators))
        {
            return !typeIndicators.Any(indicator => fileContent.Contains(indicator));
        }

        return false;
    }

    private List<CleanupOpportunity> DetectPublicFields(SyntaxNode root)
    {
        var opportunities = new List<CleanupOpportunity>();

        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classes)
        {
            var publicFields = classDecl.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                .ToList();

            foreach (var field in publicFields)
            {
                var lineSpan = field.SyntaxTree.GetLineSpan(field.Span);

                opportunities.Add(new CleanupOpportunity
                {
                    Type = CleanupType.PublicFieldToProperty,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    OriginalCode = field.ToFullString().Trim(),
                    Context = GetContextAroundNode(field, 10),
                    Metadata = new Dictionary<string, string>
                    {
                        ["FieldName"] = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
                        ["FieldType"] = field.Declaration.Type.ToString(),
                        ["ClassName"] = classDecl.Identifier.Text
                    }
                });
            }
        }

        return opportunities;
    }

    private List<CleanupOpportunity> DetectPrivateFieldOrdering(SyntaxNode root)
    {
        var opportunities = new List<CleanupOpportunity>();

        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classes)
        {
            var privateFields = classDecl.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) ||
                           !f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) ||
                                                m.IsKind(SyntaxKind.ProtectedKeyword) ||
                                                m.IsKind(SyntaxKind.InternalKeyword)))
                .ToList();

            if (!privateFields.Any())
                continue;

            // Check if there are any properties or methods before the last private field
            var lastPrivateField = privateFields.Last();
            var lastPrivateFieldIndex = classDecl.Members.IndexOf(lastPrivateField);

            var hasPropertiesOrMethodsBeforeFields = classDecl.Members
                .Take(lastPrivateFieldIndex)
                .Any(m => m is PropertyDeclarationSyntax || m is MethodDeclarationSyntax);

            if (hasPropertiesOrMethodsBeforeFields)
            {
                // Need to reorder - private fields should be at the top
                var lineSpan = classDecl.SyntaxTree.GetLineSpan(classDecl.Span);

                opportunities.Add(new CleanupOpportunity
                {
                    Type = CleanupType.ReorderPrivateFields,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    OriginalCode = "Private fields need reordering",
                    Context = classDecl.ToFullString(),
                    Metadata = new Dictionary<string, string>
                    {
                        ["ClassName"] = classDecl.Identifier.Text,
                        ["FieldCount"] = privateFields.Count.ToString()
                    }
                });
            }
        }

        return opportunities;
    }

    private int GetCleanupPriority(CleanupType type)
    {
        // Lower number = higher priority (will be processed first)
        return type switch
        {
            CleanupType.UnusedUsing => 1,                // Safest
            CleanupType.UnusedVariable => 2,
            CleanupType.PublicFieldToProperty => 3,
            CleanupType.ReorderPrivateFields => 4,
            CleanupType.SimplifyBooleanExpression => 5,
            CleanupType.SimplifyStringOperation => 6,
            CleanupType.RemoveRedundantCode => 7,
            CleanupType.ExtractMagicNumber => 8,
            CleanupType.UseExpressionBodiedMember => 9,
            _ => 10
        };
    }

    private async Task<bool> ProcessCleanupOpportunityAsync(
        string filePath,
        CleanupOpportunity opportunity,
        CodeCleanupWorkload workload,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing {Type} cleanup at line {Line}",
            opportunity.Type, opportunity.LineNumber);

        var originalContent = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Generate AI prompt
        var prompt = GenerateCleanupPrompt(opportunity, originalContent);

        // Send to AI
        var startTime = DateTime.UtcNow;
        var aiResponse = await _aiServer.SendPromptAsync(prompt, cancellationToken);
        var aiDuration = DateTime.UtcNow - startTime;

        workload.Statistics.AIApiCalls++;
        workload.Statistics.TotalAIProcessingTime += aiDuration;
        workload.Statistics.TotalPromptTokens += aiResponse.PromptEvalCount ?? 0;
        workload.Statistics.TotalResponseTokens += aiResponse.EvalCount ?? 0;

        if (!aiResponse.Done || string.IsNullOrWhiteSpace(aiResponse.Response))
        {
            _logger.LogWarning("AI response not complete or empty for {Type} cleanup", opportunity.Type);
            return false;
        }

        // Extract cleaned code from response
        var cleanedCode = ExtractCodeFromResponse(aiResponse.Response);

        if (string.IsNullOrWhiteSpace(cleanedCode))
        {
            _logger.LogWarning("Failed to extract cleaned code from AI response");
            return false;
        }

        // Validate syntax
        if (!ValidateSyntax(cleanedCode))
        {
            _logger.LogWarning("Cleaned code has syntax errors");
            return false;
        }

        // Backup and apply changes
        var backupPath = filePath + ".bak";
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
        File.Copy(filePath, backupPath);

        await File.WriteAllTextAsync(filePath, cleanedCode, cancellationToken);

        // Validate build if enabled
        if (_workloadConfig.ValidateBuilds && !string.IsNullOrWhiteSpace(workload.SolutionPath))
        {
            var buildPassed = await ValidateBuildAsync(repositoryPath, workload.SolutionPath, cancellationToken);

            if (!buildPassed)
            {
                _logger.LogWarning("Build failed after cleanup, reverting changes");
                File.Copy(backupPath, filePath, true);
                File.Delete(backupPath);
                return false;
            }
        }

        // Delete backup after successful validation
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        return true;
    }

    private string GenerateCleanupPrompt(CleanupOpportunity opportunity, string fileContent)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("You are a C# code cleanup assistant. Your task is to perform a specific refactoring operation.");
        prompt.AppendLine();
        prompt.AppendLine($"CLEANUP OPERATION: {opportunity.Type}");
        prompt.AppendLine($"LINE NUMBER: {opportunity.LineNumber}");
        prompt.AppendLine();

        switch (opportunity.Type)
        {
            case CleanupType.UnusedUsing:
                prompt.AppendLine($"Remove the unused using statement: {opportunity.Metadata["Namespace"]}");
                prompt.AppendLine();
                prompt.AppendLine("INSTRUCTIONS:");
                prompt.AppendLine("1. Remove only the specified using statement");
                prompt.AppendLine("2. Preserve all other using statements");
                prompt.AppendLine("3. Preserve all code");
                break;

            case CleanupType.PublicFieldToProperty:
                var fieldName = opportunity.Metadata["FieldName"];
                var propertyName = ToPascalCase(fieldName);
                prompt.AppendLine($"Convert the public field '{fieldName}' to a PascalCase property '{propertyName}'.");
                prompt.AppendLine();
                prompt.AppendLine("INSTRUCTIONS:");
                prompt.AppendLine($"1. Replace the field declaration with: public {opportunity.Metadata["FieldType"]} {propertyName} {{ get; set; }}");
                prompt.AppendLine($"2. The property MUST be named '{propertyName}' (PascalCase)");
                prompt.AppendLine($"3. Update all references to '{fieldName}' to use '{propertyName}'");
                prompt.AppendLine("4. Preserve all other code");
                break;

            case CleanupType.ReorderPrivateFields:
                prompt.AppendLine($"Reorder private fields in class '{opportunity.Metadata["ClassName"]}' to the top of the class.");
                prompt.AppendLine();
                prompt.AppendLine("INSTRUCTIONS:");
                prompt.AppendLine("1. Move all private fields to the top of the class (after class declaration)");
                prompt.AppendLine("2. Keep private fields in their current order relative to each other");
                prompt.AppendLine("3. Private fields should come before properties, constructors, and methods");
                prompt.AppendLine("4. Preserve all other code and formatting");
                break;

            default:
                prompt.AppendLine("INSTRUCTIONS:");
                prompt.AppendLine("1. Perform the requested cleanup operation");
                prompt.AppendLine("2. Make minimal changes");
                prompt.AppendLine("3. Preserve all functionality");
                break;
        }

        prompt.AppendLine();
        prompt.AppendLine("CURRENT FILE CONTENT:");
        prompt.AppendLine("```csharp");
        prompt.AppendLine(fileContent);
        prompt.AppendLine("```");
        prompt.AppendLine();
        prompt.AppendLine("GENERAL INSTRUCTIONS:");
        prompt.AppendLine("1. Return the COMPLETE modified file");
        prompt.AppendLine("2. Do NOT add comments or explanations");
        prompt.AppendLine("3. Do NOT include markdown code block markers in your response");
        prompt.AppendLine("4. The response should be valid C# code that can be directly written to the file");
        prompt.AppendLine("5. Preserve all formatting, comments, and structure except for the specific cleanup");

        return prompt.ToString();
    }

    private string ToPascalCase(string fieldName)
    {
        // Remove leading underscores
        var name = fieldName.TrimStart('_');

        // Capitalize first letter
        if (name.Length > 0)
        {
            name = char.ToUpper(name[0]) + name.Substring(1);
        }

        return name;
    }

    private bool ValidateSyntax(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var diagnostics = tree.GetDiagnostics();
        return !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    private async Task<bool> ValidateBuildAsync(
        string repositoryPath,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating build after cleanup");

        var buildResult = await _buildValidator.BuildAsync(repositoryPath, solutionPath, cancellationToken);

        if (buildResult.Success)
        {
            _logger.LogInformation("Build validation passed");
            return true;
        }

        _logger.LogWarning("Build failed with {ErrorCount} error(s)", buildResult.Errors.Count);
        return false;
    }

    private string ExtractCodeFromResponse(string response)
    {
        var trimmed = response.Trim();

        // Check if response is wrapped in markdown code blocks
        if (trimmed.StartsWith("```"))
        {
            var lines = trimmed.Split('\n');
            var codeLines = new List<string>();
            bool inCodeBlock = false;

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                    }
                    else
                    {
                        break; // End of code block
                    }
                }
                else if (inCodeBlock)
                {
                    codeLines.Add(line);
                }
            }

            return string.Join('\n', codeLines);
        }

        // No markdown formatting, return as-is
        return response;
    }

    private string GetContextAroundLine(SyntaxNode root, int lineNumber, int contextLines)
    {
        var lines = root.ToFullString().Split('\n');
        var start = Math.Max(0, lineNumber - contextLines - 1);
        var end = Math.Min(lines.Length, lineNumber + contextLines);

        return string.Join('\n', lines.Skip(start).Take(end - start));
    }

    private string GetContextAroundNode(SyntaxNode node, int contextLines)
    {
        var tree = node.SyntaxTree;
        var lineSpan = tree.GetLineSpan(node.Span);
        var root = tree.GetRoot();

        return GetContextAroundLine(root, lineSpan.StartLinePosition.Line + 1, contextLines);
    }

    private string GenerateCommitMessage(CodeCleanupWorkload workload, WorkloadResult result)
    {
        var fileName = Path.GetFileName(workload.FilePath);
        var message = $"Clean up {fileName}\n\n" +
                     $"Generated by MoonlightAI code cleanup workload\n" +
                     $"- Items cleaned: {result.Statistics.ItemsModified}\n" +
                     $"- AI API calls: {result.Statistics.AIApiCalls}\n" +
                     $"- Duration: {result.Statistics.Duration?.TotalSeconds:F1} seconds\n\n" +
                     $"🤖 Generated with [MoonlightAI](https://github.com/ctacke/MoonlightAI)\n\n" +
                     $"Co-Authored-By: MoonlightAI <moonlight@example.com>";

        return message;
    }

    private string GeneratePRTitle(CodeCleanupWorkload workload, WorkloadResult result)
    {
        var fileName = Path.GetFileName(workload.FilePath);
        return $"[MoonlightAI] Clean up {fileName}";
    }

    private string GeneratePRBody(CodeCleanupWorkload workload, WorkloadResult result)
    {
        var fileName = Path.GetFileName(workload.FilePath);
        var body = $"## Summary\n\n" +
                  $"This PR performs code cleanup operations on `{workload.FilePath}`.\n\n" +
                  $"## Statistics\n\n" +
                  $"- **File**: `{fileName}`\n" +
                  $"- **Items cleaned**: {result.Statistics.ItemsModified}\n" +
                  $"- **AI API calls**: {result.Statistics.AIApiCalls}\n" +
                  $"- **Processing time**: {result.Statistics.Duration?.TotalSeconds:F1} seconds\n" +
                  $"- **AI processing time**: {result.Statistics.TotalAIProcessingTime.TotalSeconds:F1} seconds\n" +
                  $"- **Total tokens**: {result.Statistics.TotalTokens:N0}\n" +
                  $"- **Errors**: {result.Statistics.ErrorCount}\n\n";

        if (result.Statistics.Errors.Any())
        {
            body += "## Errors\n\n";
            foreach (var error in result.Statistics.Errors.Take(10))
            {
                body += $"- {error}\n";
            }
            body += "\n";
        }

        body += "## Review Notes\n\n" +
               "Please review the cleanup operations for:\n" +
               "- Correctness of refactoring\n" +
               "- No functionality changes\n" +
               "- Proper naming conventions\n\n" +
               "🤖 Generated with [MoonlightAI](https://github.com/ctacke/MoonlightAI)";

        return body;
    }
}
