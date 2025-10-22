using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Models.Analysis;

namespace MoonlightAI.Core.Analysis;

/// <summary>
/// Analyzes C# source code using Roslyn.
/// </summary>
public class RoslynCodeAnalyzer : ICodeAnalyzer
{
    private readonly ILogger<RoslynCodeAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the RoslynCodeAnalyzer class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RoslynCodeAnalyzer(ILogger<RoslynCodeAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CodeFile>> AnalyzeDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        _logger.LogInformation("Analyzing directory: {DirectoryPath}", directoryPath);

        var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} .cs files", csFiles.Length);

        var results = new List<CodeFile>();

        foreach (var filePath in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var codeFile = await AnalyzeFileAsync(filePath, cancellationToken);
                results.Add(codeFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze file: {FilePath}", filePath);
                results.Add(new CodeFile
                {
                    FilePath = filePath,
                    ParsedSuccessfully = false,
                    ParseErrors = new List<string> { ex.Message }
                });
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<CodeFile> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogDebug("Analyzing file: {FilePath}", filePath);

        var codeFile = new CodeFile
        {
            FilePath = filePath
        };

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Check for parse errors
            var diagnostics = syntaxTree.GetDiagnostics(cancellationToken);
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (errors.Any())
            {
                codeFile.ParsedSuccessfully = false;
                codeFile.ParseErrors = errors.Select(e => e.GetMessage()).ToList();
                _logger.LogWarning("File has parse errors: {FilePath}", filePath);
            }
            else
            {
                codeFile.ParsedSuccessfully = true;
            }

            // Extract usings
            var usings = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.Name?.ToString() ?? string.Empty)
                .Where(u => !string.IsNullOrEmpty(u))
                .ToList();
            codeFile.Usings = usings;

            // Extract classes
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                var classInfo = AnalyzeClass(classDecl, syntaxTree);
                codeFile.Classes.Add(classInfo);
            }

            _logger.LogDebug("Found {Count} classes in {FilePath}", codeFile.Classes.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file: {FilePath}", filePath);
            codeFile.ParsedSuccessfully = false;
            codeFile.ParseErrors.Add(ex.Message);
        }

        return codeFile;
    }

    /// <inheritdoc/>
    public IEnumerable<ClassInfo> GetPublicClasses(CodeFile codeFile)
    {
        return codeFile.Classes.Where(c => c.Accessibility == "public");
    }

    /// <inheritdoc/>
    public IEnumerable<MemberInfo> GetPublicMembers(ClassInfo classInfo)
    {
        var publicMembers = new List<MemberInfo>();

        publicMembers.AddRange(classInfo.Properties.Where(p => p.Accessibility == "public"));
        publicMembers.AddRange(classInfo.Methods.Where(m => m.Accessibility == "public"));

        return publicMembers.OrderBy(m => m.FirstLineNumber);
    }

    private ClassInfo AnalyzeClass(ClassDeclarationSyntax classDecl, SyntaxTree syntaxTree)
    {
        var classInfo = new ClassInfo
        {
            Name = classDecl.Identifier.Text,
            Accessibility = GetAccessibility(classDecl.Modifiers),
            LineNumber = syntaxTree.GetLineSpan(classDecl.Span).StartLinePosition.Line + 1,
            IsStatic = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            IsAbstract = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))
        };

        // Get namespace
        var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        var fileScopedNamespace = classDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();

        if (namespaceDecl != null)
        {
            classInfo.Namespace = namespaceDecl.Name.ToString();
        }
        else if (fileScopedNamespace != null)
        {
            classInfo.Namespace = fileScopedNamespace.Name.ToString();
        }

        // Get XML documentation
        classInfo.XmlDocumentation = GetXmlDocumentation(classDecl);

        // Get base class and interfaces
        if (classDecl.BaseList != null)
        {
            foreach (var baseType in classDecl.BaseList.Types)
            {
                var typeName = baseType.Type.ToString();
                // Simple heuristic: if it starts with 'I' and has uppercase second letter, it's likely an interface
                if (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
                {
                    classInfo.Interfaces.Add(typeName);
                }
                else if (classInfo.BaseClass == null)
                {
                    classInfo.BaseClass = typeName;
                }
                else
                {
                    classInfo.Interfaces.Add(typeName);
                }
            }
        }

        // Analyze properties
        var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            var propertyInfo = AnalyzeProperty(prop, syntaxTree);
            classInfo.Properties.Add(propertyInfo);
        }

        // Analyze methods
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var methodInfo = AnalyzeMethod(method, syntaxTree);
            classInfo.Methods.Add(methodInfo);
        }

        return classInfo;
    }

    private Models.Analysis.PropertyInfo AnalyzeProperty(PropertyDeclarationSyntax propDecl, SyntaxTree syntaxTree)
    {
        return new Models.Analysis.PropertyInfo
        {
            Name = propDecl.Identifier.Text,
            PropertyType = propDecl.Type.ToString(),
            Accessibility = GetAccessibility(propDecl.Modifiers),
            FirstLineNumber = syntaxTree.GetLineSpan(propDecl.Span).StartLinePosition.Line + 1,
            XmlDocumentation = GetXmlDocumentation(propDecl),
            HasGetter = propDecl.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false,
            HasSetter = propDecl.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration)) ?? false
        };
    }

    private Models.Analysis.MethodInfo AnalyzeMethod(MethodDeclarationSyntax methodDecl, SyntaxTree syntaxTree)
    {
        var methodInfo = new Models.Analysis.MethodInfo
        {
            Name = methodDecl.Identifier.Text,
            ReturnType = methodDecl.ReturnType.ToString(),
            Accessibility = GetAccessibility(methodDecl.Modifiers),
            FirstLineNumber = syntaxTree.GetLineSpan(methodDecl.Span).StartLinePosition.Line + 1,
            LastLineNumber = syntaxTree.GetLineSpan(methodDecl.Span).EndLinePosition.Line + 1,
            XmlDocumentation = GetXmlDocumentation(methodDecl),
            IsAsync = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))
        };

        // Analyze parameters
        foreach (var param in methodDecl.ParameterList.Parameters)
        {
            var paramInfo = new ParameterInfo
            {
                Name = param.Identifier.Text,
                Type = param.Type?.ToString() ?? "var",
                HasDefaultValue = param.Default != null,
                DefaultValue = param.Default?.Value.ToString()
            };
            methodInfo.Parameters.Add(paramInfo);
        }

        return methodInfo;
    }

    private string GetAccessibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            return "public";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
            return "private";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return "protected";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
            return "internal";

        // Default accessibility for class members is private
        return "private";
    }

    private string? GetXmlDocumentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                       t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .FirstOrDefault();

        if (trivia == default)
            return null;

        var xml = trivia.GetStructure();
        if (xml == null)
            return null;

        // Extract summary text
        var summaryElement = xml.DescendantNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

        if (summaryElement != null)
        {
            var summaryText = summaryElement.Content.ToString().Trim();
            // Clean up the text
            return string.Join(" ", summaryText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line)));
        }

        return trivia.ToString().Trim();
    }
}
