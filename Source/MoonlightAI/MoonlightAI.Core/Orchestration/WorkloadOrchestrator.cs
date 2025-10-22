using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Analysis;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Workloads;
using System.Text;

namespace MoonlightAI.Core.Orchestration;

public class WorkloadOrchestrator
{
    private readonly ILogger<WorkloadOrchestrator> _logger;
    private readonly IAIServer _aiServer;
    private readonly GitManager _gitManager;
    private readonly RepositoryManager _repoManager;
    private readonly RoslynCodeAnalyzer _codeAnalyzer;

    public WorkloadOrchestrator(
        ILogger<WorkloadOrchestrator> logger,
        IAIServer aiServer,
        GitManager gitManager,
        RepositoryManager repoManager,
        RoslynCodeAnalyzer codeAnalyzer
        )
    {
        _logger = logger;
        _aiServer = aiServer;
        _gitManager = gitManager;
        _codeAnalyzer = codeAnalyzer;
    }

    public async Task EnqueueWorkload(CodeDocWorkload workload)
    {
        // todo: pull the code repository

        // todo: check to see what branches/PRs already exist

        // todo: pick a file in the project
        // note: for now I'm using a hard-cded file path for testing
        var file = @"F:\repos\sf\solution-family\src\Engine\Modules\MQTT\SolutionEngine.MQTT.Module\Services\MqttPublisherService.cs";

        // get it's classes, methods, and properties? (do we need to split it, or just do whole file? depends on LLM speed, token limits, etc)
        var fileAnalysis = await _codeAnalyzer.AnalyzeFileAsync(file);

        var nextUndocumentedMethod = fileAnalysis.Classes
            .SelectMany(c => c.Methods)
            .FirstOrDefault(m => m.XmlDocumentation == null);

        while (nextUndocumentedMethod != null)
        {
            _logger.LogInformation("Next undocumented method: {MethodName}", nextUndocumentedMethod.Name);
            var originalContentLines = await File.ReadAllLinesAsync(file);

            _logger.LogInformation("  creating method docs for {MethodName}...", nextUndocumentedMethod.Name);

            var methodCode = originalContentLines
                .Skip(nextUndocumentedMethod.FirstLineNumber - 1)
                .Take(nextUndocumentedMethod.LastLineNumber - nextUndocumentedMethod.FirstLineNumber + 1);
            var lines = string.Join(Environment.NewLine, methodCode);

            // generate XML docs for the method using LLM
            try
            {
                var result = await _aiServer.GenerateMethodXmlDocumentationAsync(lines);

                if (result.Done)
                {
                    _logger.LogInformation("Doc took {duration}", TimeSpan.FromMicroseconds((double)(result.TotalDuration! / 1000)));

                    var docSpacing = string.Empty;

                    var sb = new StringBuilder();
                    // write everything before the method
                    for (var l = 0; l < nextUndocumentedMethod.FirstLineNumber - 1; l++)
                    {
                        if (l == 0)
                        {
                            // count spaces before method name so we can insert the same spacing in the doc lines
                            docSpacing = new string(' ', originalContentLines[nextUndocumentedMethod.FirstLineNumber - 1].TakeWhile(c => c == ' ').Count());
                        }
                        sb.AppendLine(originalContentLines[l]);
                    }

                    // write the docs
                    var docLines = result.Response
                        .Trim('`')
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Where(l => l.StartsWith("///"));

                    foreach (var doc in docLines)
                    {
                        sb.AppendLine($"{docSpacing}{doc.TrimEnd('\r', '\n')}");
                    }

                    // write the method code and everything after
                    for (var l = nextUndocumentedMethod.FirstLineNumber - 1; l < originalContentLines.Length; l++)
                    {
                        sb.AppendLine(originalContentLines[l]);
                    }

                    // back up the original
                    File.Move(file, file + ".bak", true);
                    // write the new file
                    await File.WriteAllTextAsync(file, sb.ToString());
                }
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("AI server timed out generating docs for method {MethodName}", nextUndocumentedMethod.Name);
                break;
            }

            // TODO: if we timed out, maybe skip that method?

            fileAnalysis = await _codeAnalyzer.AnalyzeFileAsync(file);
            nextUndocumentedMethod = fileAnalysis.Classes
                .SelectMany(c => c.Methods)
                .FirstOrDefault(m => m.XmlDocumentation == null);
        }

        foreach (var c in fileAnalysis.Classes)
        {
            _logger.LogInformation("Class: {ClassName}", c.Name);

            if (c.XmlDocumentation == null)
            {
                _logger.LogInformation(" creating class docs...");
                // TODO: generate XML docs for the class
            }

            foreach (var m in c.Methods)
            {
                _logger.LogInformation("  Method: {MethodName}", m.Name);
                if (m.XmlDocumentation == null)
                {
                    _logger.LogInformation("  creating method docs for {MethodName}...", m.Name);
                }
            }
            foreach (var p in c.Properties)
            {
                _logger.LogInformation("  Property: {PropertyName}", p.Name);
                if (p.XmlDocumentation == null)
                {
                    _logger.LogInformation("  creating property docs for {PropertyName}...", p.Name);
                }
            }
        }

        // send the code to the LLM for analysis

        // insert the results back to the original file

        // make sure the project still builds

        // create a PR with the changes

    }
}
