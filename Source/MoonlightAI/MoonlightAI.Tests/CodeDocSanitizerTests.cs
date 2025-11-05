using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Models.Analysis;
using MoonlightAI.Core.Workloads;

namespace MoonlightAI.Tests;

/// <summary>
/// Tests for the CodeDocSanitizer.
/// </summary>
public class CodeDocSanitizerTests
{
    private readonly CodeDocSanitizer _sanitizer;

    public CodeDocSanitizerTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CodeDocSanitizer>();
        _sanitizer = new CodeDocSanitizer(logger);
    }

    #region Helper Methods

    private static MethodInfo CreateTestMethod(string name, string returnType, params (string Name, string Type)[] parameters)
    {
        return new MethodInfo
        {
            Name = name,
            ReturnType = returnType,
            Parameters = parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.Type
            }).ToList()
        };
    }

    private static List<string> CreateDocLines(params string[] lines)
    {
        return lines.ToList();
    }

    #endregion

    #region ExtractFromDocTags Tests

    [Fact]
    public void ExtractFromDocTags_WithDocTags_ExtractsContent()
    {
        // Arrange
        var response = "<doc>/// <summary>Test summary</summary></doc>";

        // Act
        var result = _sanitizer.ExtractFromDocTags(response);

        // Assert
        Assert.Equal("/// <summary>Test summary</summary>", result);
    }

    [Fact]
    public void ExtractFromDocTags_WithoutDocTags_ReturnsOriginal()
    {
        // Arrange
        var response = "/// <summary>Test summary</summary>";

        // Act
        var result = _sanitizer.ExtractFromDocTags(response);

        // Assert
        Assert.Equal(response, result);
    }

    [Fact]
    public void ExtractFromDocTags_WithWhitespace_TrimsContent()
    {
        // Arrange
        var response = "<doc>\n   /// <summary>Test</summary>   \n</doc>";

        // Act
        var result = _sanitizer.ExtractFromDocTags(response);

        // Assert
        Assert.Equal("/// <summary>Test</summary>", result);
    }

    [Fact]
    public void ExtractFromDocTags_WithMultilineContent_PreservesFormatting()
    {
        // Arrange
        var response = @"<doc>
/// <summary>
/// Line 1
/// Line 2
/// </summary>
</doc>";

        // Act
        var result = _sanitizer.ExtractFromDocTags(response);

        // Assert
        Assert.Contains("Line 1", result);
        Assert.Contains("Line 2", result);
    }

    #endregion

    #region ParseDocumentationLines Tests

    [Fact]
    public void ParseDocumentationLines_BasicResponse_ParsesCorrectly()
    {
        // Arrange
        var response = "/// <summary>Test</summary>\n/// <returns>Value</returns>";
        var indentation = "    ";

        // Act
        var result = _sanitizer.ParseDocumentationLines(response, indentation);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, line => Assert.StartsWith(indentation + "///", line));
    }

    [Fact]
    public void ParseDocumentationLines_WithCodeFence_RemovesBackticks()
    {
        // Arrange
        var response = "```\n/// <summary>Test</summary>\n```";
        var indentation = "    ";

        // Act
        var result = _sanitizer.ParseDocumentationLines(response, indentation);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain("`", result[0]);
    }

    [Fact]
    public void ParseDocumentationLines_WithNonDocLines_FiltersThemOut()
    {
        // Arrange
        var response = "/// <summary>Test</summary>\nNot a doc line\n/// <returns>Value</returns>";
        var indentation = "    ";

        // Act
        var result = _sanitizer.ParseDocumentationLines(response, indentation);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, line => Assert.Contains("///", line));
    }

    [Fact]
    public void ParseDocumentationLines_EmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var response = "";
        var indentation = "    ";

        // Act
        var result = _sanitizer.ParseDocumentationLines(response, indentation);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseDocumentationLines_AppliesIndentation_Correctly()
    {
        // Arrange
        var response = "/// <summary>Test</summary>";
        var indentation = "        "; // 8 spaces

        // Act
        var result = _sanitizer.ParseDocumentationLines(response, indentation);

        // Assert
        Assert.Single(result);
        Assert.StartsWith(indentation, result[0]);
    }

    [Fact]
    public void ParseDocumentationLines_TrimsWhitespace_FromEachLine()
    {
        // Arrange
        var response = "  /// <summary>Test</summary>  \n  /// <returns>Value</returns>  ";
        var indentation = "    ";

        // Act
        var result = _sanitizer.ParseDocumentationLines(response, indentation);

        // Assert
        Assert.Equal(2, result.Count);
        // Verify each line starts with exact indentation + /// (not extra spaces)
        Assert.All(result, line => Assert.StartsWith(indentation + "///", line));
        // Verify trailing whitespace was removed
        Assert.All(result, line => Assert.False(line.EndsWith(" ")));
    }

    #endregion

    #region FixLiteralEscapeSequences Tests

    [Fact]
    public void FixLiteralEscapeSequences_WithLiteralNewline_SplitsIntoMultipleLines()
    {
        // Arrange
        var docLines = CreateDocLines("    /// <summary>\\n/// Line 1\\n/// Line 2\\n/// </summary>");
        var indentation = "    ";

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.True(result.Count > 1);
        Assert.All(result, line => Assert.Contains("///", line));
    }

    [Fact]
    public void FixLiteralEscapeSequences_WithLiteralCarriageReturn_ReplacesWithNewline()
    {
        // Arrange
        var docLines = CreateDocLines("    /// <summary>\\r\\n/// Test</summary>");
        var indentation = "    ";

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.DoesNotContain(result, line => line.Contains("\\r\\n"));
    }

    [Fact]
    public void FixLiteralEscapeSequences_WithLiteralTab_ReplacesWithSpaces()
    {
        // Arrange
        var docLines = CreateDocLines("    /// <summary>Test\\tValue</summary>");
        var indentation = "    ";

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.DoesNotContain(result, line => line.Contains("\\t"));
        Assert.Contains(result, line => line.Contains("    ")); // Tab replaced with 4 spaces
    }

    [Fact]
    public void FixLiteralEscapeSequences_EnsuresTripleSlashPrefix_OnSplitLines()
    {
        // Arrange
        var docLines = CreateDocLines("    /// <summary>\\n    Line without prefix");
        var indentation = "    ";

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.All(result, line => Assert.Contains("///", line));
    }

    [Fact]
    public void FixLiteralEscapeSequences_AppliesCorrectIndentation_ToSplitLines()
    {
        // Arrange
        var docLines = CreateDocLines("/// <summary>\\n/// Test</summary>");
        var indentation = "        "; // 8 spaces

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.All(result, line => Assert.StartsWith(indentation, line));
    }

    [Fact]
    public void FixLiteralEscapeSequences_SkipsEmptyParts_AfterSplitting()
    {
        // Arrange
        var docLines = CreateDocLines("    /// <summary>\\n\\n\\n/// Test</summary>");
        var indentation = "    ";

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.All(result, line => Assert.False(string.IsNullOrWhiteSpace(line.Replace("///", "").Trim())));
    }

    [Fact]
    public void FixLiteralEscapeSequences_NoEscapeSequences_ReturnsUnchanged()
    {
        // Arrange
        var docLines = CreateDocLines("    /// <summary>Test</summary>");
        var indentation = "    ";

        // Act
        var result = _sanitizer.FixLiteralEscapeSequences(docLines, indentation);

        // Assert
        Assert.Single(result);
        Assert.Equal(docLines[0], result[0]);
    }

    #endregion

    #region RemoveEmptyXmlTags Tests

    [Fact]
    public void RemoveEmptyXmlTags_WithEmptyPairedTag_RemovesIt()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks></remarks>",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.RemoveEmptyXmlTags(docLines);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, line => line.Contains("<remarks></remarks>"));
    }

    [Fact]
    public void RemoveEmptyXmlTags_WithSelfClosingEmptyTag_RemovesIt()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks />",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.RemoveEmptyXmlTags(docLines);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, line => line.Contains("<remarks />"));
    }

    [Fact]
    public void RemoveEmptyXmlTags_WithContentBetweenTags_KeepsIt()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks>Important note</remarks>",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.RemoveEmptyXmlTags(docLines);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, line => line.Contains("<remarks>Important note</remarks>"));
    }

    [Fact]
    public void RemoveEmptyXmlTags_MultipleEmptyTags_RemovesAll()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks></remarks>",
            "    /// <example></example>",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.RemoveEmptyXmlTags(docLines);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RemoveEmptyXmlTags_CaseInsensitive_RemovesTags()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <REMARKS></REMARKS>",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.RemoveEmptyXmlTags(docLines);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RemoveEmptyXmlTags_WithWhitespace_RemovesEmptyTags()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks>  </remarks>",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.RemoveEmptyXmlTags(docLines);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region ValidateSummaryTag Tests

    [Fact]
    public void ValidateSummaryTag_WithSummaryOnFirstLine_ReturnsTrue()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.ValidateSummaryTag(docLines, "TestMethod");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateSummaryTag_WithoutSummary_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <returns>Value</returns>");

        // Act
        var result = _sanitizer.ValidateSummaryTag(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSummaryTag_EmptyList_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines();

        // Act
        var result = _sanitizer.ValidateSummaryTag(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSummaryTag_SummaryNotFirstLine_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <remarks>Note</remarks>",
            "    /// <summary>Test</summary>");

        // Act
        var result = _sanitizer.ValidateSummaryTag(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ValidateSingleOccurrenceTags Tests

    [Fact]
    public void ValidateSingleOccurrenceTags_AllTagsAppearOnce_ReturnsTrue()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks>Note</remarks>",
            "    /// <returns>Value</returns>",
            "    /// <example>Example code</example>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestMethod");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateSingleOccurrenceTags_DuplicateSummary_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test 1</summary>",
            "    /// <summary>Test 2</summary>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSingleOccurrenceTags_DuplicateRemarks_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <remarks>Note 1</remarks>",
            "    /// <remarks>Note 2</remarks>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSingleOccurrenceTags_DuplicateReturns_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <returns>Value 1</returns>",
            "    /// <returns>Value 2</returns>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSingleOccurrenceTags_DuplicateValue_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <value>Value 1</value>",
            "    /// <value>Value 2</value>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestProperty");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSingleOccurrenceTags_DuplicateExample_ReturnsFalse()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <example>Example 1</example>",
            "    /// <example>Example 2</example>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestMethod");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSingleOccurrenceTags_NoTags_ReturnsTrue()
    {
        // Arrange
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>");

        // Act
        var result = _sanitizer.ValidateSingleOccurrenceTags(docLines, "TestMethod");

        // Assert
        Assert.True(result);
    }

    #endregion

    #region SanitizeAndValidateDocumentation Tests

    [Fact]
    public void SanitizeAndValidateDocumentation_ValidResponse_ReturnsTrue()
    {
        // Arrange
        var response = "<doc>\n/// <summary>Test method</summary>\n</doc>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.True(isValid);
        Assert.NotEmpty(lines);
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_ExtractsFromDocTags_Correctly()
    {
        // Arrange
        var response = "<doc>/// <summary>Inside doc tags</summary></doc>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.True(isValid);
        Assert.Contains("Inside doc tags", string.Join("", lines));
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_FixesEscapeSequences_Correctly()
    {
        // Arrange
        var response = "/// <summary>\\n/// Line 1\\n/// Line 2\\n/// </summary>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.True(isValid);
        Assert.True(lines.Count > 1);
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_RemovesEmptyTags_Correctly()
    {
        // Arrange
        var response = "/// <summary>Test</summary>\n/// <remarks></remarks>\n/// <returns>Value</returns>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.True(isValid);
        Assert.DoesNotContain(lines, line => line.Contains("<remarks></remarks>"));
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_ValidatesSummary_Correctly()
    {
        // Arrange
        var response = "/// <returns>Value</returns>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_ValidatesSingleOccurrence_Correctly()
    {
        // Arrange
        var response = "/// <summary>Test 1</summary>\n/// <summary>Test 2</summary>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_NoValidLines_ReturnsFalse()
    {
        // Arrange
        var response = "Not a doc comment";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.False(isValid);
        Assert.Empty(lines);
    }

    [Fact]
    public void SanitizeAndValidateDocumentation_ComplexScenario_HandlesCorrectly()
    {
        // Arrange
        var response = "<doc>\n/// <summary>\\n/// Test method\\n/// </summary>\n/// <remarks></remarks>\n/// <returns>Value</returns>\n</doc>";
        var indentation = "    ";

        // Act
        var (isValid, lines) = _sanitizer.SanitizeAndValidateDocumentation(response, indentation, "TestMethod");

        // Assert
        Assert.True(isValid);
        Assert.DoesNotContain(lines, line => line.Contains("<remarks></remarks>"));
    }

    #endregion

    #region SanitizeMethodDocumentation Tests

    [Fact]
    public void SanitizeMethodDocumentation_RemovesHallucinatedParameter_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void", ("foo", "string"));
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <param name=\"foo\">Valid param</param>",
            "    /// <param name=\"bar\">Hallucinated param</param>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Equal(2, sanitized.Count);
        Assert.DoesNotContain(sanitized, line => line.Contains("bar"));
        Assert.True(fixCount > 0);
    }

    [Fact]
    public void SanitizeMethodDocumentation_RemovesDuplicateParameter_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void", ("foo", "string"));
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <param name=\"foo\">First description</param>",
            "    /// <param name=\"foo\">Duplicate description</param>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Equal(2, sanitized.Count);
        Assert.Contains(sanitized, line => line.Contains("First description"));
        Assert.DoesNotContain(sanitized, line => line.Contains("Duplicate description"));
        Assert.True(fixCount > 0);
    }

    [Fact]
    public void SanitizeMethodDocumentation_KeepsValidParameters_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void", ("foo", "string"), ("bar", "int"));
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <param name=\"foo\">First param</param>",
            "    /// <param name=\"bar\">Second param</param>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Equal(3, sanitized.Count);
        Assert.Contains(sanitized, line => line.Contains("foo"));
        Assert.Contains(sanitized, line => line.Contains("bar"));
        Assert.Equal(0, fixCount);
    }

    [Fact]
    public void SanitizeMethodDocumentation_RemovesReturnsFromVoidMethod_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <returns>Should be removed</returns>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Single(sanitized);
        Assert.DoesNotContain(sanitized, line => line.Contains("<returns>"));
        Assert.True(fixCount > 0);
    }

    [Fact]
    public void SanitizeMethodDocumentation_RemovesReturnsFromTaskMethod_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethodAsync", "Task");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <returns>Should be removed</returns>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Single(sanitized);
        Assert.DoesNotContain(sanitized, line => line.Contains("<returns>"));
        Assert.True(fixCount > 0);
    }

    [Fact]
    public void SanitizeMethodDocumentation_KeepsReturnsForNonVoidMethod_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "string");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <returns>Return value</returns>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Equal(2, sanitized.Count);
        Assert.Contains(sanitized, line => line.Contains("<returns>"));
        Assert.Equal(0, fixCount);
    }

    [Fact]
    public void SanitizeMethodDocumentation_DetectsOrphanedClosingTag_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// </remarks>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Single(sanitized);
        Assert.DoesNotContain(sanitized, line => line.Contains("</remarks>"));
        Assert.True(fixCount > 0);
    }

    [Fact]
    public void SanitizeMethodDocumentation_RemovesTrailingBackslashes_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>\\",
            "    /// <remarks>Note</remarks>\\");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.All(sanitized, line => Assert.False(line.EndsWith("\\")));
    }

    [Fact]
    public void SanitizeMethodDocumentation_ReturnsCorrectFixCount_ForMultipleIssues()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void", ("foo", "string"));
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <param name=\"foo\">Valid</param>",
            "    /// <param name=\"bar\">Hallucinated</param>",
            "    /// <param name=\"foo\">Duplicate</param>",
            "    /// <returns>Invalid for void</returns>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Equal(3, fixCount); // bar removed, duplicate foo removed, returns removed
    }

    [Fact]
    public void SanitizeMethodDocumentation_ComplexScenario_HandlesCorrectly()
    {
        // Arrange
        var method = CreateTestMethod("ComplexMethod", "int", ("param1", "string"), ("param2", "int"));
        var docLines = CreateDocLines(
            "    /// <summary>Complex test</summary>",
            "    /// <param name=\"param1\">First param</param>",
            "    /// <param name=\"param2\">Second param</param>",
            "    /// <param name=\"param3\">Hallucinated param</param>",
            "    /// <param name=\"param1\">Duplicate first param</param>",
            "    /// <returns>Return value</returns>",
            "    /// </remarks>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Equal(4, sanitized.Count); // summary + 2 params + returns
        Assert.Contains(sanitized, line => line.Contains("param1"));
        Assert.Contains(sanitized, line => line.Contains("param2"));
        Assert.DoesNotContain(sanitized, line => line.Contains("param3"));
        Assert.DoesNotContain(sanitized, line => line.Contains("</remarks>"));
        Assert.True(fixCount >= 3); // param3, duplicate param1, orphaned </remarks>
    }

    [Fact]
    public void SanitizeMethodDocumentation_MethodWithNoParameters_HandlesCorrectly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Single(sanitized);
        Assert.Equal(0, fixCount);
    }

    [Fact]
    public void SanitizeMethodDocumentation_RemovesEmptyParameterName_Correctly()
    {
        // Arrange
        var method = CreateTestMethod("TestMethod", "void");
        var docLines = CreateDocLines(
            "    /// <summary>Test</summary>",
            "    /// <param name=\"\">No parameters for this method.</param>");

        // Act
        var (sanitized, fixCount) = _sanitizer.SanitizeMethodDocumentation(method, docLines);

        // Assert
        Assert.Single(sanitized);
        Assert.DoesNotContain(sanitized, line => line.Contains("<param"));
        Assert.True(fixCount > 0);
    }

    #endregion
}
