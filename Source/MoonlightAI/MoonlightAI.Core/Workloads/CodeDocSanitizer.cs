using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Models.Analysis;

namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Handles sanitization and validation of AI-generated documentation comments.
/// </summary>
public class CodeDocSanitizer
{
    private readonly ILogger<CodeDocSanitizer> _logger;

    // XML documentation tags that should only appear once per documentation block
    private static readonly HashSet<string> SingleOccurrenceTags = new()
    {
        "summary", "remarks", "returns", "value", "example", "inheritdoc"
    };

    public CodeDocSanitizer(ILogger<CodeDocSanitizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Documentation Sanitization Methods

    /// <summary>
    /// Extracts documentation content from &lt;doc&gt; tags if present.
    /// </summary>
    public string ExtractFromDocTags(string response)
    {
        var docMatch = System.Text.RegularExpressions.Regex.Match(response, @"<doc>\s*(.*?)\s*</doc>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return docMatch.Success ? docMatch.Groups[1].Value : response;
    }

    /// <summary>
    /// Parses AI response into documentation lines with proper indentation.
    /// </summary>
    public List<string> ParseDocumentationLines(string response, string indentation)
    {
        return response
            .Trim('`')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("///"))
            .Select(l => $"{indentation}{l.TrimEnd('\r', '\n')}")
            .ToList();
    }

    /// <summary>
    /// Fixes literal escape sequences (like \n) that AI sometimes outputs as strings instead of actual newlines.
    /// </summary>
    public List<string> FixLiteralEscapeSequences(List<string> docLines, string indentation)
    {
        var fixedLines = new List<string>();
        int fixCount = 0;

        foreach (var line in docLines)
        {
            // Check if line contains literal escape sequences
            if (line.Contains("\\n") || line.Contains("\\r\\n") || line.Contains("\\t"))
            {
                var processedLine = line;

                // Replace literal \r\n first (Windows newlines)
                if (processedLine.Contains("\\r\\n"))
                {
                    processedLine = processedLine.Replace("\\r\\n", "\\n");
                    fixCount++;
                }

                // Handle literal \n newlines
                if (processedLine.Contains("\\n"))
                {
                    fixCount++;

                    // Split on literal \n and process each part
                    var parts = processedLine.Split(new[] { "\\n" }, StringSplitOptions.None);

                    foreach (var part in parts)
                    {
                        var trimmedPart = part.Trim();

                        // Skip empty parts
                        if (string.IsNullOrWhiteSpace(trimmedPart))
                            continue;

                        // Ensure part starts with ///
                        if (!trimmedPart.StartsWith("///"))
                        {
                            trimmedPart = "/// " + trimmedPart;
                        }

                        // Add proper indentation
                        if (!trimmedPart.StartsWith(indentation))
                        {
                            trimmedPart = indentation + trimmedPart.TrimStart();
                        }

                        fixedLines.Add(trimmedPart);
                    }
                }
                else
                {
                    // Just had \t or other escape sequences, keep the line
                    fixedLines.Add(processedLine.Replace("\\t", "    ")); // Replace tabs with spaces
                }
            }
            else
            {
                fixedLines.Add(line);
            }
        }

        if (fixCount > 0)
        {
            _logger.LogWarning("Fixed {Count} literal escape sequence(s) in documentation (AI outputted escape sequences as text instead of actual formatting)", fixCount);
        }

        return fixedLines;
    }

    /// <summary>
    /// Removes documentation lines that contain only empty XML tags (e.g., /// &lt;remarks&gt;&lt;/remarks&gt;).
    /// </summary>
    public List<string> RemoveEmptyXmlTags(List<string> docLines)
    {
        // Pattern matches lines like: /// <tagname></tagname> or /// <tagname />
        // with optional whitespace between tags
        var emptyTagPattern = new System.Text.RegularExpressions.Regex(
            @"^(\s*)///\s*<(\w+)>\s*</\2>\s*$|^(\s*)///\s*<(\w+)\s*/>\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var emptyLines = docLines.Where(line => emptyTagPattern.IsMatch(line)).ToList();
        if (emptyLines.Any())
        {
            _logger.LogDebug("Removing {Count} empty XML tag(s) from documentation", emptyLines.Count);
        }

        return docLines
            .Where(line => !emptyTagPattern.IsMatch(line))
            .ToList();
    }

    /// <summary>
    /// Validates that documentation lines contain a &lt;summary&gt; tag on the first line.
    /// </summary>
    public bool ValidateSummaryTag(List<string> docLines, string memberName)
    {
        if (docLines.Count == 0)
        {
            _logger.LogDebug("No documentation lines to validate for {MemberName}", memberName);
            return false;
        }

        if (!docLines[0].Trim().Contains("<summary>"))
        {
            _logger.LogWarning("Documentation for {MemberName} does not start with <summary> tag. First line: {FirstLine}",
                memberName, docLines[0]);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that single-occurrence tags (summary, remarks, returns, value, example, inheritdoc) appear at most once.
    /// </summary>
    public bool ValidateSingleOccurrenceTags(List<string> docLines, string memberName)
    {
        foreach (var tagName in SingleOccurrenceTags)
        {
            var count = docLines.Count(line => line.Contains($"<{tagName}>"));

            if (count > 1)
            {
                _logger.LogWarning(
                    "Documentation for {MemberName} contains {Count} <{TagName}> tags. " +
                    "Only one is allowed. AI likely hallucinated documentation.",
                    memberName, count, tagName);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Performs all common sanitization and validation checks on generated documentation.
    /// </summary>
    /// <returns>Tuple of (isValid, sanitizedLines)</returns>
    public (bool isValid, List<string> lines) SanitizeAndValidateDocumentation(
        string aiResponse,
        string indentation,
        string memberName)
    {
        // Step 1: Extract from <doc> tags if present
        var response = ExtractFromDocTags(aiResponse.Trim());

        // Step 2: Parse into documentation lines
        var docLines = ParseDocumentationLines(response, indentation);

        // Step 3: Fix literal escape sequences (e.g., \n as text instead of newline)
        docLines = FixLiteralEscapeSequences(docLines, indentation);

        // Step 4: Remove empty XML tags
        docLines = RemoveEmptyXmlTags(docLines);

        // Step 5: Check if any valid lines remain
        if (docLines.Count == 0)
        {
            _logger.LogWarning("No valid documentation lines generated for {MemberName}. AI Response: {Response}",
                memberName, aiResponse.Substring(0, Math.Min(500, aiResponse.Length)));
            return (false, docLines);
        }

        // Step 6: Validate summary tag
        if (!ValidateSummaryTag(docLines, memberName))
        {
            return (false, docLines);
        }

        // Step 7: Validate single-occurrence tags (prevent duplicate summary, remarks, returns, etc.)
        if (!ValidateSingleOccurrenceTags(docLines, memberName))
        {
            return (false, docLines);
        }

        return (true, docLines);
    }

    /// <summary>
    /// Performs method-specific sanitization: validates parameters and return type.
    /// </summary>
    public (List<string> sanitizedLines, int fixCount) SanitizeMethodDocumentation(MethodInfo method, List<string> docLines)
    {
        var sanitizedLines = new List<string>();
        var actualParamNames = method.Parameters.Select(p => p.Name).ToHashSet();
        var isVoidMethod = method.ReturnType.Equals("void", StringComparison.OrdinalIgnoreCase) ||
                           method.ReturnType.Equals("Task", StringComparison.OrdinalIgnoreCase);

        var warningsLogged = new List<string>();
        int fixCount = 0;

        // Track open tags to detect orphaned closing tags
        var openTags = new Stack<string>();
        var validXmlTags = new HashSet<string> { "summary", "remarks", "param", "returns", "exception", "example", "see", "seealso", "value", "typeparam" };

        foreach (var line in docLines)
        {
            // Remove trailing backslashes that AI sometimes adds
            var cleanedLine = line.TrimEnd('\\');
            var trimmedLine = cleanedLine.Trim();
            bool skipLine = false;

            // Check for <returns> tag on void methods - strip it out
            if (isVoidMethod && (trimmedLine.Contains("<returns>") || trimmedLine.Contains("</returns>")))
            {
                var warning = $"Stripped <returns> tag from void method {method.ReturnType}";
                if (!warningsLogged.Contains(warning))
                {
                    _logger.LogWarning(warning);
                    warningsLogged.Add(warning);
                }
                fixCount++;
                skipLine = true;
            }

            // Check for hallucinated parameters - strip them out
            var paramMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"<param name=""([^""]+)"">");
            if (paramMatch.Success)
            {
                var paramName = paramMatch.Groups[1].Value;
                if (!actualParamNames.Contains(paramName))
                {
                    var warning = $"Stripped invalid parameter '{paramName}' from documentation";
                    if (!warningsLogged.Contains(warning))
                    {
                        _logger.LogWarning(warning);
                        warningsLogged.Add(warning);
                    }
                    fixCount++;
                    skipLine = true;
                }
            }

            // Check for orphaned closing tags (closing tag without matching opening tag)
            if (!skipLine)
            {
                // Find all opening and closing tags in this line
                var openTagMatches = System.Text.RegularExpressions.Regex.Matches(trimmedLine, @"<(\w+)(?:\s|>)");
                var closeTagMatches = System.Text.RegularExpressions.Regex.Matches(trimmedLine, @"</(\w+)>");

                // Process opening tags
                foreach (System.Text.RegularExpressions.Match match in openTagMatches)
                {
                    var tagName = match.Groups[1].Value;
                    if (validXmlTags.Contains(tagName))
                    {
                        openTags.Push(tagName);
                    }
                }

                // Process closing tags
                foreach (System.Text.RegularExpressions.Match match in closeTagMatches)
                {
                    var tagName = match.Groups[1].Value;
                    if (validXmlTags.Contains(tagName))
                    {
                        if (openTags.Count > 0 && openTags.Peek() == tagName)
                        {
                            openTags.Pop();
                        }
                        else
                        {
                            // Orphaned closing tag!
                            var warning = $"Stripped orphaned closing tag </{tagName}> with no matching opening tag";
                            if (!warningsLogged.Contains(warning))
                            {
                                _logger.LogWarning(warning);
                                warningsLogged.Add(warning);
                            }
                            fixCount++;
                            skipLine = true;
                            break;
                        }
                    }
                }
            }

            // Keep valid lines
            if (!skipLine)
            {
                sanitizedLines.Add(cleanedLine);
            }
        }

        // Log warnings for missing documentation (but don't strip anything)
        if (!isVoidMethod && !string.Join("\n", sanitizedLines).Contains("<returns>"))
        {
            _logger.LogWarning("Method returns {ReturnType} but documentation is missing <returns> tag (will be kept anyway)", method.ReturnType);
        }

        var documentedParams = System.Text.RegularExpressions.Regex.Matches(string.Join("\n", sanitizedLines), @"<param name=""([^""]+)"">")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        foreach (var param in method.Parameters)
        {
            if (!documentedParams.Contains(param.Name))
            {
                _logger.LogWarning("Parameter '{ParamName}' is not documented (will be kept anyway)", param.Name);
            }
        }

        return (sanitizedLines, fixCount);
    }

    #endregion
}
