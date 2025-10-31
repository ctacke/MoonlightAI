using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Configuration;
using System.Text;
using System.Text.RegularExpressions;

namespace MoonlightAI.Core.Prompts;

/// <summary>
/// Service for managing AI prompts, loading from files with fallback to defaults.
/// </summary>
public class PromptService
{
    private readonly ILogger<PromptService> _logger;
    private readonly PromptConfiguration _config;
    private readonly string _promptsDirectory;

    public PromptService(ILogger<PromptService> logger, PromptConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Resolve prompts directory to absolute path
        _promptsDirectory = Path.IsPathRooted(_config.Directory)
            ? _config.Directory
            : Path.Combine(AppContext.BaseDirectory, _config.Directory);

        _logger.LogInformation("PromptService initialized with directory: {Directory}", _promptsDirectory);
    }

    /// <summary>
    /// Gets a prompt template for the specified workload, operation, and model.
    /// </summary>
    /// <param name="workload">Workload type (e.g., "codedoc", "cleanup")</param>
    /// <param name="operation">Operation type (e.g., "method", "field", "unused-using")</param>
    /// <param name="modelName">Full model name (e.g., "codellama:13b-instruct")</param>
    /// <param name="variables">Dictionary of variables to replace in the template</param>
    /// <returns>Prompt with variables replaced</returns>
    public string GetPrompt(
        string workload,
        string operation,
        string modelName,
        Dictionary<string, string>? variables = null)
    {
        var template = LoadPromptTemplate(workload, operation, modelName);
        return ReplaceVariables(template, variables ?? new Dictionary<string, string>());
    }

    /// <summary>
    /// Loads a prompt template using fallback chain:
    /// 1. prompts/{workload}/{model}/{operation}.txt
    /// 2. prompts/{workload}/default/{operation}.txt
    /// 3. Hardcoded default
    /// </summary>
    private string LoadPromptTemplate(string workload, string operation, string modelName)
    {
        if (!_config.EnableCustomPrompts)
        {
            _logger.LogDebug("Custom prompts disabled, using hardcoded defaults");
            return GetHardcodedDefault(workload, operation);
        }

        // Normalize model name (e.g., "codellama:13b-instruct" → "codellama")
        var normalizedModel = NormalizeModelName(modelName);

        // Try model-specific prompt
        var modelPromptPath = Path.Combine(_promptsDirectory, workload, normalizedModel, $"{operation}.txt");
        if (File.Exists(modelPromptPath))
        {
            _logger.LogDebug("Loading prompt from: {Path}", modelPromptPath);
            try
            {
                return File.ReadAllText(modelPromptPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load prompt from {Path}, falling back", modelPromptPath);
            }
        }

        // Try default prompt
        var defaultPromptPath = Path.Combine(_promptsDirectory, workload, "default", $"{operation}.txt");
        if (File.Exists(defaultPromptPath))
        {
            _logger.LogDebug("Loading default prompt from: {Path}", defaultPromptPath);
            try
            {
                return File.ReadAllText(defaultPromptPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load default prompt from {Path}, using hardcoded", defaultPromptPath);
            }
        }

        // Fall back to hardcoded default
        _logger.LogDebug("Using hardcoded default prompt for {Workload}/{Operation}", workload, operation);
        return GetHardcodedDefault(workload, operation);
    }

    /// <summary>
    /// Normalizes model name to extract model family.
    /// Examples:
    /// - "codellama:13b-instruct" → "codellama"
    /// - "llama3:8b-instruct" → "llama3"
    /// - "gpt-4" → "gpt4"
    /// </summary>
    private string NormalizeModelName(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return "default";
        }

        // Take everything before the first colon or hyphen
        var normalized = modelName.Split(':', '-')[0].ToLowerInvariant();

        // Remove common suffixes
        normalized = normalized
            .Replace("-instruct", "")
            .Replace("-chat", "")
            .Replace("_", "");

        return normalized;
    }

    /// <summary>
    /// Replaces template variables in the format {variableName} with their values.
    /// </summary>
    private string ReplaceVariables(string template, Dictionary<string, string> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return template;
        }

        var result = template;

        foreach (var kvp in variables)
        {
            var placeholder = $"{{{kvp.Key}}}";
            result = result.Replace(placeholder, kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Gets hardcoded default prompts as a last resort fallback.
    /// These are the original prompts extracted from the code.
    /// </summary>
    private string GetHardcodedDefault(string workload, string operation)
    {
        return (workload.ToLowerInvariant(), operation.ToLowerInvariant()) switch
        {
            ("codedoc", "method") => GetDefaultCodeDocMethodPrompt(),
            ("codedoc", "field") => GetDefaultCodeDocFieldPrompt(),
            ("codedoc", "property") => GetDefaultCodeDocPropertyPrompt(),
            ("codedoc", "event") => GetDefaultCodeDocEventPrompt(),
            ("cleanup", "unused-using") => GetDefaultCleanupUnusedUsingPrompt(),
            ("cleanup", "field-to-property") => GetDefaultCleanupFieldToPropertyPrompt(),
            ("cleanup", "reorder-fields") => GetDefaultCleanupReorderFieldsPrompt(),
            _ => $"No default prompt available for {workload}/{operation}"
        };
    }

    // Default prompts extracted from original code
    // These serve as fallbacks if no prompt files are found

    private string GetDefaultCodeDocMethodPrompt()
    {
        return """
            You are a C# XML documentation generator. Your task is to generate ONLY the XML documentation comments for the method below.

            C# Method to document:
            ```csharp
            {method}
            ```

            CRITICAL REQUIREMENTS:
            1. Output ONLY the XML documentation comment lines (starting with "///")
            2. DO NOT include the method code itself
            3. DO NOT add XML tags that don't match the method signature
            4. Use ONLY these XML tags: <summary>, <param>, <returns>, <remarks>, <exception>
            5. Do NOT use <returns> for void methods or methods returning Task
            6. Include <param> tags for ALL method parameters
            7. Keep descriptions concise and accurate
            8. The first line MUST be: /// <summary>

            Example output format:
            /// <summary>
            /// Description of what the method does.
            /// </summary>
            /// <param name="paramName">Description of parameter.</param>
            /// <returns>Description of return value.</returns>

            OUTPUT:
            """;
    }

    private string GetDefaultCodeDocFieldPrompt()
    {
        return """
            Generate XML documentation comment for the following C# constant/field:

            {field}

            Requirements:
            - Output ONLY the XML documentation lines (starting with "///" )
            - Use <summary> tag only
            - Keep description concise (1-2 sentences)
            - The first line MUST be: /// <summary>
            - Do NOT include the field declaration itself

            Example:
            /// <summary>
            /// Description of the field.
            /// </summary>

            OUTPUT:
            """;
    }

    private string GetDefaultCodeDocPropertyPrompt()
    {
        return """
            Generate XML documentation comment for the following C# property:

            {property}

            Requirements:
            - Output ONLY the XML documentation lines (starting with "///")
            - Use <summary> tag only
            - Keep description concise (1-2 sentences)
            - The first line MUST be: /// <summary>
            - Do NOT include the property declaration itself

            Example:
            /// <summary>
            /// Description of the property.
            /// </summary>

            OUTPUT:
            """;
    }

    private string GetDefaultCodeDocEventPrompt()
    {
        return """
            Generate XML documentation comment for the following C# event:

            {event}

            Requirements:
            - Output ONLY the XML documentation lines (starting with "///")
            - Use <summary> tag only
            - Describe when the event is raised
            - Keep description concise (1-2 sentences)
            - The first line MUST be: /// <summary>
            - Do NOT include the event declaration itself

            Example:
            /// <summary>
            /// Raised when something happens.
            /// </summary>

            OUTPUT:
            """;
    }

    private string GetDefaultCleanupUnusedUsingPrompt()
    {
        return """
            You are a C# code cleanup assistant. Your task is to perform a specific refactoring operation.

            CLEANUP OPERATION: Remove unused using statement
            LINE NUMBER: {lineNumber}

            Remove the unused using statement: {namespace}

            INSTRUCTIONS:
            1. Remove only the specified using statement
            2. Preserve all other using statements
            3. Preserve all code

            CURRENT FILE CONTENT:
            ```csharp
            {fileContent}
            ```

            GENERAL INSTRUCTIONS:
            1. Return the COMPLETE modified file
            2. Do NOT add comments or explanations
            3. Do NOT include markdown code block markers in your response
            4. The response should be valid C# code that can be directly written to the file
            5. Preserve all formatting, comments, and structure except for the specific cleanup
            """;
    }

    private string GetDefaultCleanupFieldToPropertyPrompt()
    {
        return """
            You are a C# code cleanup assistant. Your task is to perform a specific refactoring operation.

            CLEANUP OPERATION: Convert public field to PascalCase property
            LINE NUMBER: {lineNumber}

            Convert the public field '{fieldName}' to a PascalCase property '{propertyName}'.

            INSTRUCTIONS:
            1. Replace the field declaration with: public {fieldType} {propertyName} {{ get; set; }}
            2. The property MUST be named '{propertyName}' (PascalCase)
            3. Update all references to '{fieldName}' to use '{propertyName}'
            4. Preserve all other code

            CURRENT FILE CONTENT:
            ```csharp
            {fileContent}
            ```

            GENERAL INSTRUCTIONS:
            1. Return the COMPLETE modified file
            2. Do NOT add comments or explanations
            3. Do NOT include markdown code block markers in your response
            4. The response should be valid C# code that can be directly written to the file
            5. Preserve all formatting, comments, and structure except for the specific cleanup
            """;
    }

    private string GetDefaultCleanupReorderFieldsPrompt()
    {
        return """
            You are a C# code cleanup assistant. Your task is to perform a specific refactoring operation.

            CLEANUP OPERATION: Reorder private fields to top of class
            LINE NUMBER: {lineNumber}

            Reorder private fields in class '{className}' to the top of the class.

            INSTRUCTIONS:
            1. Move all private fields to the top of the class (after class declaration)
            2. Keep private fields in their current order relative to each other
            3. Private fields should come before properties, constructors, and methods
            4. Preserve all other code and formatting

            CURRENT FILE CONTENT:
            ```csharp
            {fileContent}
            ```

            GENERAL INSTRUCTIONS:
            1. Return the COMPLETE modified file
            2. Do NOT add comments or explanations
            3. Do NOT include markdown code block markers in your response
            4. The response should be valid C# code that can be directly written to the file
            5. Preserve all formatting, comments, and structure except for the specific cleanup
            """;
    }
}
