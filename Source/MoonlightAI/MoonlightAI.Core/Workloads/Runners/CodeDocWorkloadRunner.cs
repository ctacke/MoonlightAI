using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Build;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Prompts;
using System.Text;

namespace MoonlightAI.Core.Workloads.Runners;

/// <summary>
/// Workload runner for generating XML documentation for C# code.
/// </summary>
public class CodeDocWorkloadRunner : IWorkloadRunner<CodeDocWorkload>
{
    private readonly ILogger<CodeDocWorkloadRunner> _logger;
    private readonly IAIServer _aiServer;
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly IBuildValidator _buildValidator;
    private readonly IGitManager _gitManager;
    private readonly WorkloadConfiguration _workloadConfig;
    private readonly PromptService _promptService;
    private readonly AIServerConfiguration _aiServerConfig;
    private readonly CodeDocSanitizer _sanitizer;

    public CodeDocWorkloadRunner(
        ILogger<CodeDocWorkloadRunner> logger,
        IAIServer aiServer,
        ICodeAnalyzer codeAnalyzer,
        IBuildValidator buildValidator,
        IGitManager gitManager,
        WorkloadConfiguration workloadConfig,
        PromptService promptService,
        AIServerConfiguration aiServerConfig,
        CodeDocSanitizer sanitizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiServer = aiServer ?? throw new ArgumentNullException(nameof(aiServer));
        _codeAnalyzer = codeAnalyzer ?? throw new ArgumentNullException(nameof(codeAnalyzer));
        _buildValidator = buildValidator ?? throw new ArgumentNullException(nameof(buildValidator));
        _gitManager = gitManager ?? throw new ArgumentNullException(nameof(gitManager));
        _workloadConfig = workloadConfig ?? throw new ArgumentNullException(nameof(workloadConfig));
        _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
        _aiServerConfig = aiServerConfig ?? throw new ArgumentNullException(nameof(aiServerConfig));
        _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
    }

    /// <inheritdoc/>
    public async Task<WorkloadResult> ExecuteAsync(CodeDocWorkload workload, string repositoryPath, CancellationToken cancellationToken = default)
    {
        var result = new WorkloadResult
        {
            Workload = workload
        };

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

            // Set default visibility if not specified
            if (workload.DocumentVisibility == 0)
            {
                workload.DocumentVisibility = MemberVisibility.Public | MemberVisibility.Internal;
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
                // Validate build if enabled
                bool buildPassed = true;

                if (_workloadConfig.ValidateBuilds && !string.IsNullOrWhiteSpace(workload.SolutionPath))
                {
                    buildPassed = await ValidateAndFixBuildAsync(
                        repositoryPath,
                        workload.SolutionPath,
                        workload.FilePath,
                        workload,
                        cancellationToken);
                }
                else if (_workloadConfig.ValidateBuilds && string.IsNullOrWhiteSpace(workload.SolutionPath))
                {
                    _logger.LogWarning("Build validation is enabled but no solution path provided, skipping validation");
                }

                if (buildPassed)
                {
                    workload.Statistics.FilesProcessed = 1;
                    // Add the modified file path (relative to repository root)
                    result.ModifiedFiles.Add(workload.FilePath);
                }
                else
                {
                    // File was reverted due to build failure
                    workload.Statistics.FilesProcessed = 0;
                    _logger.LogWarning("File {FilePath} was reverted due to build validation failure", workload.FilePath);
                }
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

            // Diagnostic logging for statistics
            _logger.LogInformation("Workload stats for {File}: Sanitization={Sanit}, PromptTokens={Prompt}, ResponseTokens={Response}",
                Path.GetFileName(workload.FilePath),
                workload.Statistics.TotalSanitizationFixes,
                workload.Statistics.TotalPromptTokens,
                workload.Statistics.TotalResponseTokens);

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
        var processedMembers = new HashSet<string>(); // Track members that have been processed (success or failure)

        // Keep processing until no more undocumented members are found
        while (true)
        {
            var fileAnalysis = await _codeAnalyzer.AnalyzeFileAsync(filePath, cancellationToken);

            if (!fileAnalysis.ParsedSuccessfully)
            {
                _logger.LogWarning("Skipping file with parse errors: {FilePath}", filePath);
                return fileModified;
            }

            var documentedSomething = false;

            // Process classes that match visibility criteria
            foreach (var classInfo in fileAnalysis.Classes.Where(c => ShouldDocument(c.Accessibility, workload.DocumentVisibility)))
            {
                // Process methods - document one at a time
                var undocumentedMethod = classInfo.Methods
                    .Where(m => ShouldDocument(m.Accessibility, workload.DocumentVisibility) && m.XmlDocumentation == null)
                    .FirstOrDefault(m => !processedMembers.Contains($"{classInfo.Name}.{m.Name}"));

                if (undocumentedMethod != null)
                {
                    var memberKey = $"{classInfo.Name}.{undocumentedMethod.Name}";
                    processedMembers.Add(memberKey); // Mark as processed immediately to prevent re-processing

                    try
                    {
                        var modified = await DocumentMethodAsync(filePath, undocumentedMethod, workload, cancellationToken);
                        if (modified)
                        {
                            fileModified = true;
                            documentedSomething = true;
                            workload.Statistics.ItemsModified++;
                            break; // Break out to re-analyze with fresh line numbers
                        }
                        else
                        {
                            // AI failed to generate valid documentation
                            workload.Statistics.ErrorCount++;
                            workload.Statistics.Errors.Add($"Failed to generate documentation: {memberKey}");
                            documentedSomething = true; // Continue processing other members
                            break; // Break to re-analyze
                        }
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("AI server timed out for method {MethodName} in {FilePath}", undocumentedMethod.Name, filePath);
                        workload.Statistics.ErrorCount++;
                        workload.Statistics.Errors.Add($"Timeout: {memberKey}");
                        documentedSomething = true; // Continue processing other members
                        break; // Break to re-analyze
                    }
                }

                // Process fields (constants and enums) - document one at a time
                var undocumentedField = classInfo.Fields
                    .Where(f => ShouldDocument(f.Accessibility, workload.DocumentVisibility) &&
                                f.XmlDocumentation == null &&
                                (f.IsConst || f.IsReadOnly)) // Only document constants and readonly fields
                    .FirstOrDefault(f => !processedMembers.Contains($"{classInfo.Name}.{f.Name}"));

                if (undocumentedField != null)
                {
                    var memberKey = $"{classInfo.Name}.{undocumentedField.Name}";
                    processedMembers.Add(memberKey); // Mark as processed immediately to prevent re-processing

                    try
                    {
                        var modified = await DocumentFieldAsync(filePath, undocumentedField, workload, cancellationToken);
                        if (modified)
                        {
                            fileModified = true;
                            documentedSomething = true;
                            workload.Statistics.ItemsModified++;
                            break; // Break out to re-analyze with fresh line numbers
                        }
                        else
                        {
                            // AI failed to generate valid documentation
                            workload.Statistics.ErrorCount++;
                            workload.Statistics.Errors.Add($"Failed to generate documentation: {memberKey}");
                            documentedSomething = true;
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("AI server timed out for field {FieldName} in {FilePath}", undocumentedField.Name, filePath);
                        workload.Statistics.ErrorCount++;
                        workload.Statistics.Errors.Add($"Timeout: {memberKey}");
                        documentedSomething = true;
                        break;
                    }
                }

                // Process properties - document one at a time
                var undocumentedProperty = classInfo.Properties
                    .Where(p => ShouldDocument(p.Accessibility, workload.DocumentVisibility) && p.XmlDocumentation == null)
                    .FirstOrDefault(p => !processedMembers.Contains($"{classInfo.Name}.{p.Name}"));

                if (undocumentedProperty != null)
                {
                    var memberKey = $"{classInfo.Name}.{undocumentedProperty.Name}";
                    processedMembers.Add(memberKey); // Mark as processed immediately to prevent re-processing

                    try
                    {
                        var modified = await DocumentPropertyAsync(filePath, undocumentedProperty, workload, cancellationToken);
                        if (modified)
                        {
                            fileModified = true;
                            documentedSomething = true;
                            workload.Statistics.ItemsModified++;
                            break; // Break out to re-analyze with fresh line numbers
                        }
                        else
                        {
                            // AI failed to generate valid documentation
                            workload.Statistics.ErrorCount++;
                            workload.Statistics.Errors.Add($"Failed to generate documentation: {memberKey}");
                            documentedSomething = true;
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("AI server timed out for property {PropertyName} in {FilePath}", undocumentedProperty.Name, filePath);
                        workload.Statistics.ErrorCount++;
                        workload.Statistics.Errors.Add($"Timeout: {memberKey}");
                        documentedSomething = true;
                        break;
                    }
                }

                // Process events - document one at a time
                var undocumentedEvent = classInfo.Events
                    .Where(e => ShouldDocument(e.Accessibility, workload.DocumentVisibility) && e.XmlDocumentation == null)
                    .FirstOrDefault(e => !processedMembers.Contains($"{classInfo.Name}.{e.Name}"));

                if (undocumentedEvent != null)
                {
                    var memberKey = $"{classInfo.Name}.{undocumentedEvent.Name}";
                    processedMembers.Add(memberKey); // Mark as processed immediately to prevent re-processing

                    try
                    {
                        var modified = await DocumentEventAsync(filePath, undocumentedEvent, workload, cancellationToken);
                        if (modified)
                        {
                            fileModified = true;
                            documentedSomething = true;
                            workload.Statistics.ItemsModified++;
                            break; // Break out to re-analyze with fresh line numbers
                        }
                        else
                        {
                            // AI failed to generate valid documentation
                            workload.Statistics.ErrorCount++;
                            workload.Statistics.Errors.Add($"Failed to generate documentation: {memberKey}");
                            documentedSomething = true;
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("AI server timed out for event {EventName} in {FilePath}", undocumentedEvent.Name, filePath);
                        workload.Statistics.ErrorCount++;
                        workload.Statistics.Errors.Add($"Timeout: {memberKey}");
                        documentedSomething = true;
                        break;
                    }
                }

                // Document class itself if needed
                if (classInfo.XmlDocumentation == null)
                {
                    // TODO: Implement class documentation generation
                    _logger.LogDebug("Class {ClassName} needs documentation (not yet implemented)", classInfo.Name);
                }
            }

            // If we didn't document anything this iteration, we're done
            if (!documentedSomething)
            {
                break;
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
        workload.Statistics.TotalPromptTokens += aiResponse.PromptEvalCount ?? 0;
        workload.Statistics.TotalResponseTokens += aiResponse.EvalCount ?? 0;

        if (!aiResponse.Done)
        {
            _logger.LogWarning("AI response not complete for method {MethodName}", method.Name);
            return false;
        }

        _logger.LogDebug("Documentation generation took {Duration}", aiDuration);

        // Calculate indentation
        var indentation = new string(' ', originalContentLines[method.FirstLineNumber - 1].TakeWhile(c => c == ' ').Count());

        // Perform common sanitization and validation checks
        var (isValid, docLines) = _sanitizer.SanitizeAndValidateDocumentation(
            aiResponse.Response,
            indentation,
            method.Name);

        if (!isValid)
        {
            return false;
        }

        // Perform method-specific validation and sanitization
        var (cleanedDocLines, sanitizationFixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);
        if (cleanedDocLines.Count == 0)
        {
            _logger.LogWarning("No valid documentation lines remain after sanitization for method {MethodName}",
                method.Name);

            // Save prompt and response for debugging
            await SaveFailedDocumentationAttemptAsync(filePath, method.Name, methodSource,
                $"All documentation removed during sanitization\n\nORIGINAL DOCUMENTATION:\n{string.Join("\n", docLines)}\n\nAI RESPONSE:\n{aiResponse.Response}",
                cancellationToken);

            return false;
        }

        // Use the sanitized documentation
        docLines = cleanedDocLines;

        // Log and track sanitization fixes for quality tracking
        if (sanitizationFixCount > 0)
        {
            _logger.LogInformation("Applied {FixCount} sanitization fix(es) to method {MethodName} documentation",
                sanitizationFixCount, method.Name);
            workload.Statistics.TotalSanitizationFixes += sanitizationFixCount;
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

        // Delete backup file after successful write
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        _logger.LogInformation("Successfully documented method {MethodName}", method.Name);
        return true;
    }

    private async Task<bool> DocumentFieldAsync(string filePath, Models.Analysis.FieldInfo field, CodeDocWorkload workload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating documentation for field {FieldName} at line {LineNumber}", field.Name, field.FirstLineNumber);

        var originalContentLines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        // Extract field declaration (usually just one line)
        var fieldLine = originalContentLines[field.FirstLineNumber - 1];

        // Generate prompt using PromptService
        var variables = new Dictionary<string, string>
        {
            ["field"] = fieldLine
        };
        var prompt = _promptService.GetPrompt("codedoc", "field", _aiServerConfig.ModelName, variables);

        // Generate documentation
        var startTime = DateTime.UtcNow;
        var aiResponse = await _aiServer.SendPromptAsync(prompt, cancellationToken);
        var aiDuration = DateTime.UtcNow - startTime;

        workload.Statistics.AIApiCalls++;
        workload.Statistics.TotalAIProcessingTime += aiDuration;
        workload.Statistics.TotalPromptTokens += aiResponse.PromptEvalCount ?? 0;
        workload.Statistics.TotalResponseTokens += aiResponse.EvalCount ?? 0;

        if (!aiResponse.Done)
        {
            _logger.LogWarning("AI response not complete for field {FieldName}", field.Name);
            return false;
        }

        _logger.LogDebug("Documentation generation took {Duration}", aiDuration);

        // Calculate indentation
        var indentation = new string(' ', originalContentLines[field.FirstLineNumber - 1].TakeWhile(c => c == ' ').Count());

        // Perform common sanitization and validation checks
        var (isValid, docLines) = _sanitizer.SanitizeAndValidateDocumentation(
            aiResponse.Response,
            indentation,
            field.Name);

        if (!isValid)
        {
            return false;
        }

        // Build new file content
        var sb = new StringBuilder();

        // Write everything before the field
        for (var i = 0; i < field.FirstLineNumber - 1; i++)
        {
            sb.AppendLine(originalContentLines[i]);
        }

        // Write the documentation
        foreach (var docLine in docLines)
        {
            sb.AppendLine(docLine);
        }

        // Write the field and everything after
        for (var i = field.FirstLineNumber - 1; i < originalContentLines.Length; i++)
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

        // Delete backup file after successful write
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        _logger.LogInformation("Successfully documented field {FieldName}", field.Name);
        return true;
    }

    private async Task<bool> DocumentPropertyAsync(string filePath, Models.Analysis.PropertyInfo property, CodeDocWorkload workload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating documentation for property {PropertyName} at line {LineNumber}", property.Name, property.FirstLineNumber);

        var originalContentLines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        // Extract property declaration (usually just one line)
        var propertyLine = originalContentLines[property.FirstLineNumber - 1];

        // Generate prompt using PromptService
        var variables = new Dictionary<string, string>
        {
            ["property"] = propertyLine
        };
        var prompt = _promptService.GetPrompt("codedoc", "property", _aiServerConfig.ModelName, variables);

        // Generate documentation
        var startTime = DateTime.UtcNow;
        var aiResponse = await _aiServer.SendPromptAsync(prompt, cancellationToken);
        var aiDuration = DateTime.UtcNow - startTime;

        workload.Statistics.AIApiCalls++;
        workload.Statistics.TotalAIProcessingTime += aiDuration;
        workload.Statistics.TotalPromptTokens += aiResponse.PromptEvalCount ?? 0;
        workload.Statistics.TotalResponseTokens += aiResponse.EvalCount ?? 0;

        if (!aiResponse.Done)
        {
            _logger.LogWarning("AI response not complete for property {PropertyName}", property.Name);
            return false;
        }

        _logger.LogDebug("Documentation generation took {Duration}", aiDuration);

        // Calculate indentation
        var indentation = new string(' ', originalContentLines[property.FirstLineNumber - 1].TakeWhile(c => c == ' ').Count());

        // Perform common sanitization and validation checks
        var (isValid, docLines) = _sanitizer.SanitizeAndValidateDocumentation(
            aiResponse.Response,
            indentation,
            property.Name);

        if (!isValid)
        {
            return false;
        }

        // Build new file content
        var sb = new StringBuilder();

        // Write everything before the property
        for (var i = 0; i < property.FirstLineNumber - 1; i++)
        {
            sb.AppendLine(originalContentLines[i]);
        }

        // Write the documentation
        foreach (var docLine in docLines)
        {
            sb.AppendLine(docLine);
        }

        // Write the property and everything after
        for (var i = property.FirstLineNumber - 1; i < originalContentLines.Length; i++)
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

        // Delete backup file after successful write
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        _logger.LogInformation("Successfully documented property {PropertyName}", property.Name);
        return true;
    }

    private async Task<bool> DocumentEventAsync(string filePath, Models.Analysis.EventInfo evt, CodeDocWorkload workload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating documentation for event {EventName} at line {LineNumber}", evt.Name, evt.FirstLineNumber);

        var originalContentLines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        // Extract event declaration (usually just one line)
        var eventLine = originalContentLines[evt.FirstLineNumber - 1];

        // Generate prompt using PromptService
        var variables = new Dictionary<string, string>
        {
            ["event"] = eventLine
        };
        var prompt = _promptService.GetPrompt("codedoc", "event", _aiServerConfig.ModelName, variables);

        // Generate documentation
        var startTime = DateTime.UtcNow;
        var aiResponse = await _aiServer.SendPromptAsync(prompt, cancellationToken);
        var aiDuration = DateTime.UtcNow - startTime;

        workload.Statistics.AIApiCalls++;
        workload.Statistics.TotalAIProcessingTime += aiDuration;
        workload.Statistics.TotalPromptTokens += aiResponse.PromptEvalCount ?? 0;
        workload.Statistics.TotalResponseTokens += aiResponse.EvalCount ?? 0;

        if (!aiResponse.Done)
        {
            _logger.LogWarning("AI response not complete for event {EventName}", evt.Name);
            return false;
        }

        _logger.LogDebug("Documentation generation took {Duration}", aiDuration);

        // Calculate indentation
        var indentation = new string(' ', originalContentLines[evt.FirstLineNumber - 1].TakeWhile(c => c == ' ').Count());

        // Perform common sanitization and validation checks
        var (isValid, docLines) = _sanitizer.SanitizeAndValidateDocumentation(
            aiResponse.Response,
            indentation,
            evt.Name);

        if (!isValid)
        {
            return false;
        }

        // Build new file content
        var sb = new StringBuilder();

        // Write everything before the event
        for (var i = 0; i < evt.FirstLineNumber - 1; i++)
        {
            sb.AppendLine(originalContentLines[i]);
        }

        // Write the documentation
        foreach (var docLine in docLines)
        {
            sb.AppendLine(docLine);
        }

        // Write the event and everything after
        for (var i = evt.FirstLineNumber - 1; i < originalContentLines.Length; i++)
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

        // Delete backup file after successful write
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        _logger.LogInformation("Successfully documented event {EventName}", evt.Name);
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
                  $"- **Total tokens**: {result.Statistics.TotalTokens:N0} ({result.Statistics.TotalPromptTokens:N0} prompt + {result.Statistics.TotalResponseTokens:N0} response)\n" +
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

    private async Task SaveFailedDocumentationAttemptAsync(
        string filePath,
        string methodName,
        string prompt,
        string response,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a debug directory next to the file being processed
            var debugDir = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", ".moonlight-debug");
            Directory.CreateDirectory(debugDir);

            // Create a timestamp-based filename
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var safeMethodName = string.Join("_", methodName.Split(Path.GetInvalidFileNameChars()));
            var baseFileName = $"{timestamp}-{safeMethodName}";

            // Save the prompt
            var promptFile = Path.Combine(debugDir, $"{baseFileName}-prompt.txt");
            await File.WriteAllTextAsync(promptFile, prompt, cancellationToken);

            // Save the response
            var responseFile = Path.Combine(debugDir, $"{baseFileName}-response.txt");
            await File.WriteAllTextAsync(responseFile, response, cancellationToken);

            _logger.LogInformation("Saved failed documentation attempt to {DebugDir}", debugDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save debug information for method {MethodName}", methodName);
        }
    }

    /// <summary>
    /// Validates the build after modifying a file and attempts to fix any errors with AI.
    /// </summary>
    /// <returns>True if build passes or is fixed successfully, false if file was reverted.</returns>
    private async Task<bool> ValidateAndFixBuildAsync(
        string repositoryPath,
        string solutionPath,
        string filePath,
        CodeDocWorkload workload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating build after modifying {FilePath}", filePath);

        // Build the solution
        var buildResult = await _buildValidator.BuildAsync(repositoryPath, solutionPath, cancellationToken);

        if (buildResult.Success)
        {
            _logger.LogInformation("Build validation passed");
            return true;
        }

        // Build failed - try to fix with AI
        _logger.LogWarning("Build failed with {ErrorCount} error(s), attempting AI fix...", buildResult.Errors.Count);

        // Filter errors related to the current file
        var fileErrors = buildResult.Errors
            .Where(e => e.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) ||
                       Path.GetFullPath(Path.Combine(repositoryPath, e.FilePath))
                           .Equals(Path.GetFullPath(Path.Combine(repositoryPath, filePath)), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!fileErrors.Any())
        {
            _logger.LogWarning("Build failed but no errors attributed to {FilePath}. Errors in other files may have been introduced.", filePath);
            // Still consider this a failure for this file since the build is broken
            fileErrors = buildResult.Errors.ToList();
        }

        // Try to fix with AI (with retries)
        for (int attempt = 1; attempt <= _workloadConfig.MaxBuildRetries; attempt++)
        {
            workload.Statistics.BuildRetries++;

            _logger.LogInformation("AI fix attempt {Attempt}/{Max} for {FilePath}",
                attempt, _workloadConfig.MaxBuildRetries, filePath);

            var fixApplied = await TryFixBuildErrorsAsync(
                repositoryPath,
                filePath,
                fileErrors,
                attempt,
                workload,
                cancellationToken);

            if (fixApplied)
            {
                // Build again to verify
                var retryBuildResult = await _buildValidator.BuildAsync(repositoryPath, solutionPath, cancellationToken);

                if (retryBuildResult.Success)
                {
                    _logger.LogInformation("AI successfully fixed build errors on attempt {Attempt}", attempt);
                    return true;
                }

                _logger.LogWarning("AI fix applied but build still fails ({ErrorCount} errors)", retryBuildResult.Errors.Count);

                // Update errors for next attempt
                fileErrors = retryBuildResult.Errors
                    .Where(e => e.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!fileErrors.Any())
                {
                    fileErrors = retryBuildResult.Errors.ToList();
                }
            }
            else
            {
                _logger.LogWarning("AI failed to provide a fix on attempt {Attempt}", attempt);
            }
        }

        // All fix attempts exhausted
        workload.Statistics.BuildFailures++;
        workload.Statistics.SkippedFiles.Add(filePath);
        workload.Statistics.Errors.Add($"Build failed for {filePath} after {_workloadConfig.MaxBuildRetries} fix attempts");

        if (_workloadConfig.RevertOnBuildFailure)
        {
            _logger.LogError("Unable to fix build errors after {MaxRetries} attempts, reverting {FilePath}",
                _workloadConfig.MaxBuildRetries, filePath);

            await _gitManager.RevertFileAsync(repositoryPath, filePath, cancellationToken);

            _logger.LogInformation("File reverted: {FilePath}", filePath);
            return false;
        }
        else
        {
            _logger.LogWarning("Build validation failed but RevertOnBuildFailure is false, keeping changes for manual review");
            return true; // Don't revert, but mark it as an issue
        }
    }

    /// <summary>
    /// Attempts to fix build errors by sending them to AI for correction.
    /// </summary>
    private async Task<bool> TryFixBuildErrorsAsync(
        string repositoryPath,
        string filePath,
        List<Models.BuildError> errors,
        int attemptNumber,
        CodeDocWorkload workload,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullFilePath = Path.Combine(repositoryPath, filePath);
            var currentCode = await File.ReadAllTextAsync(fullFilePath, cancellationToken);

            // Create prompt with build errors
            var prompt = CreateBuildErrorFixPrompt(filePath, currentCode, errors, attemptNumber);

            _logger.LogDebug("Sending build error fix request to AI (attempt {Attempt})", attemptNumber);

            // Send to AI
            var startTime = DateTime.UtcNow;
            var aiResponse = await _aiServer.SendPromptAsync(prompt, cancellationToken);
            var aiDuration = DateTime.UtcNow - startTime;

            workload.Statistics.AIApiCalls++;
            workload.Statistics.TotalAIProcessingTime += aiDuration;
            workload.Statistics.TotalPromptTokens += aiResponse.PromptEvalCount ?? 0;
            workload.Statistics.TotalResponseTokens += aiResponse.EvalCount ?? 0;

            if (string.IsNullOrWhiteSpace(aiResponse.Response))
            {
                _logger.LogWarning("AI returned empty response for build error fix");
                return false;
            }

            // Extract code from response (AI might wrap it in markdown code blocks)
            var fixedCode = ExtractCodeFromResponse(aiResponse.Response);

            // Apply the fix
            await File.WriteAllTextAsync(fullFilePath, fixedCode, cancellationToken);

            _logger.LogInformation("Applied AI fix to {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI fix attempt for {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Creates a prompt for the AI to fix build errors.
    /// </summary>
    private string CreateBuildErrorFixPrompt(string filePath, string currentCode, List<Models.BuildError> errors, int attemptNumber)
    {
        var sb = new StringBuilder();

        sb.AppendLine("The following C# file was modified to add XML documentation but now has build errors.");
        sb.AppendLine();
        sb.AppendLine($"File: {filePath}");
        sb.AppendLine($"Fix Attempt: {attemptNumber}");
        sb.AppendLine();
        sb.AppendLine("Build Errors:");
        sb.AppendLine("----------------------------------------");

        foreach (var error in errors)
        {
            sb.AppendLine($"Line {error.LineNumber}: {error.ErrorCode} - {error.Message}");
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine();
        sb.AppendLine("Current File Content:");
        sb.AppendLine("```csharp");

        // Add line numbers to help AI locate errors
        var lines = currentCode.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            sb.AppendLine($"{i + 1,4}: {lines[i].TrimEnd('\r')}");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Instructions:");
        sb.AppendLine("1. Fix the build errors while preserving all XML documentation that was added");
        sb.AppendLine("2. Do not remove or modify XML documentation comments unless they are causing the errors");
        sb.AppendLine("3. Return ONLY the complete corrected C# code");
        sb.AppendLine("4. Do not include any explanations, markdown formatting, or code block markers in your response");
        sb.AppendLine("5. The response should be valid C# code that can be directly written to the file");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts code from AI response, handling markdown code blocks if present.
    /// </summary>
    private string ExtractCodeFromResponse(string response)
    {
        // Check if response is wrapped in markdown code blocks
        var trimmed = response.Trim();

        if (trimmed.StartsWith("```"))
        {
            // Extract code from markdown code block
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
}
