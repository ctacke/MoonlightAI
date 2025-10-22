using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Analysis;

namespace MoonlightAI.Tests;

/// <summary>
/// Tests for the RoslynCodeAnalyzer.
/// </summary>
public class CodeAnalyzerTests
{
    private readonly RoslynCodeAnalyzer _analyzer;
    private readonly string _testDataPath;

    public CodeAnalyzerTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<RoslynCodeAnalyzer>();

        _analyzer = new RoslynCodeAnalyzer(logger);

        // Get path to test data
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ParsesSuccessfully()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ParsedSuccessfully);
        Assert.Empty(result.ParseErrors);
        Assert.Equal(filePath, result.FilePath);
        Assert.Equal("SampleClass.cs", result.FileName);
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ExtractsClasses()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        Assert.Equal(3, result.Classes.Count);

        var sampleClass = result.Classes.FirstOrDefault(c => c.Name == "SampleClass");
        Assert.NotNull(sampleClass);
        Assert.Equal("public", sampleClass.Accessibility);
        Assert.Equal("MoonlightAI.Tests.TestData", sampleClass.Namespace);
        Assert.False(sampleClass.IsStatic);
        Assert.False(sampleClass.IsAbstract);
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ExtractsXmlDocumentation()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        var sampleClass = result.Classes.FirstOrDefault(c => c.Name == "SampleClass");
        Assert.NotNull(sampleClass);
        Assert.NotNull(sampleClass.XmlDocumentation);
        Assert.Contains("sample class for testing", sampleClass.XmlDocumentation);
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ExtractsProperties()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        var sampleClass = result.Classes.FirstOrDefault(c => c.Name == "SampleClass");
        Assert.NotNull(sampleClass);

        // Should have 2 public properties
        var publicProperties = sampleClass.Properties.Where(p => p.Accessibility == "public").ToList();
        Assert.Equal(2, publicProperties.Count);

        var nameProperty = publicProperties.FirstOrDefault(p => p.Name == "Name");
        Assert.NotNull(nameProperty);
        Assert.Equal("string", nameProperty.PropertyType);
        Assert.True(nameProperty.HasGetter);
        Assert.True(nameProperty.HasSetter);
        Assert.NotNull(nameProperty.XmlDocumentation);
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ExtractsMethods()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        var sampleClass = result.Classes.FirstOrDefault(c => c.Name == "SampleClass");
        Assert.NotNull(sampleClass);

        var publicMethods = sampleClass.Methods.Where(m => m.Accessibility == "public").ToList();
        Assert.Equal(2, publicMethods.Count);

        var getFormattedMethod = publicMethods.FirstOrDefault(m => m.Name == "GetFormattedValue");
        Assert.NotNull(getFormattedMethod);
        Assert.Equal("string", getFormattedMethod.ReturnType);
        Assert.Single(getFormattedMethod.Parameters);
        Assert.Equal("value", getFormattedMethod.Parameters[0].Name);
        Assert.Equal("string", getFormattedMethod.Parameters[0].Type);
        Assert.False(getFormattedMethod.IsAsync);
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ExtractsAsyncMethods()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        var sampleClass = result.Classes.FirstOrDefault(c => c.Name == "SampleClass");
        Assert.NotNull(sampleClass);

        var processMethod = sampleClass.Methods.FirstOrDefault(m => m.Name == "ProcessAsync");
        Assert.NotNull(processMethod);
        Assert.True(processMethod.IsAsync);
        Assert.Equal("Task<bool>", processMethod.ReturnType);
        Assert.Single(processMethod.Parameters);
        Assert.True(processMethod.Parameters[0].HasDefaultValue);
        Assert.Equal("100", processMethod.Parameters[0].DefaultValue);
    }

    [Fact]
    public async Task AnalyzeFileAsync_ValidFile_ExtractsUsings()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        Assert.Contains("System", result.Usings);
        Assert.Contains("System.Collections.Generic", result.Usings);
    }

    [Fact]
    public async Task GetPublicClasses_FiltersByAccessibility()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");
        var codeFile = await _analyzer.AnalyzeFileAsync(filePath);

        // Act
        var publicClasses = _analyzer.GetPublicClasses(codeFile).ToList();

        // Assert
        Assert.Equal(2, publicClasses.Count); // SampleClass and StaticHelper
        Assert.All(publicClasses, c => Assert.Equal("public", c.Accessibility));
        Assert.DoesNotContain(publicClasses, c => c.Name == "InternalClass");
    }

    [Fact]
    public async Task GetPublicMembers_FiltersByAccessibility()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");
        var codeFile = await _analyzer.AnalyzeFileAsync(filePath);
        var sampleClass = codeFile.Classes.First(c => c.Name == "SampleClass");

        // Act
        var publicMembers = _analyzer.GetPublicMembers(sampleClass).ToList();

        // Assert
        Assert.Equal(4, publicMembers.Count); // 2 properties + 2 methods
        Assert.All(publicMembers, m => Assert.Equal("public", m.Accessibility));

        // Verify they're sorted by line number
        Assert.True(publicMembers.SequenceEqual(publicMembers.OrderBy(m => m.FirstLineNumber)));
    }

    [Fact]
    public async Task AnalyzeFileAsync_DetectsStaticClass()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "SampleClass.cs");

        // Act
        var result = await _analyzer.AnalyzeFileAsync(filePath);

        // Assert
        var staticHelper = result.Classes.FirstOrDefault(c => c.Name == "StaticHelper");
        Assert.NotNull(staticHelper);
        Assert.True(staticHelper.IsStatic);
        Assert.Equal("public", staticHelper.Accessibility);
    }

    [Fact]
    public async Task AnalyzeDirectoryAsync_ScansAllCsFiles()
    {
        // Arrange
        var directoryPath = _testDataPath;

        // Act
        var results = await _analyzer.AnalyzeDirectoryAsync(directoryPath);

        // Assert
        var resultsList = results.ToList();
        Assert.NotEmpty(resultsList);
        Assert.All(resultsList, r => Assert.EndsWith(".cs", r.FilePath));
    }

    [Fact]
    public async Task AnalyzeFileAsync_NonExistentFile_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "NonExistent.cs");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _analyzer.AnalyzeFileAsync(filePath));
    }

    [Fact]
    public async Task AnalyzeDirectoryAsync_NonExistentDirectory_ThrowsException()
    {
        // Arrange
        var directoryPath = Path.Combine(_testDataPath, "NonExistent");

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await _analyzer.AnalyzeDirectoryAsync(directoryPath));
    }
}
