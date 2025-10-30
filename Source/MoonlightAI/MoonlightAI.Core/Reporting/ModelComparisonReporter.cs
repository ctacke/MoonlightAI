using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Data;

namespace MoonlightAI.Core.Reporting;

/// <summary>
/// Generates and displays model comparison reports.
/// </summary>
public class ModelComparisonReporter : IReporter
{
    private readonly IDataService _dataService;
    private readonly ILogger<ModelComparisonReporter> _logger;

    public ModelComparisonReporter(IDataService dataService, ILogger<ModelComparisonReporter> logger)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task DisplayReportAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating model comparison report...");

            var comparison = await _dataService.GetModelComparisonAsync(cancellationToken);

            if (!comparison.Any())
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No workload runs found in database.");
                Console.WriteLine("Run a workload first to see comparison data.");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            // Display header
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                              Model Comparison Report                                             ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // Sort by success rate descending
            var sortedModels = comparison.Values.OrderByDescending(m => m.SuccessRate).ToList();

            // Display table header
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"{"Model",-30} {"Runs",6} {"Success",8} {"Files",7} {"Failures",9} {"Retries",8} {"SanitFix",9} {"Tokens/File",12}");
            Console.WriteLine(new string('-', 110));
            Console.ResetColor();

            // Display each model
            foreach (var model in sortedModels)
            {
                // Color code based on success rate
                if (model.SuccessRate >= 95)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (model.SuccessRate >= 80)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else
                    Console.ForegroundColor = ConsoleColor.Red;

                Console.Write($"{model.ModelName,-30} ");
                Console.ResetColor();

                Console.Write($"{model.TotalRuns,6} ");
                Console.Write($"{model.SuccessRate,7:F1}% ");
                Console.Write($"{model.TotalFilesProcessed,7} ");

                // Highlight build failures in red if > 0
                if (model.TotalBuildFailures > 0)
                    Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{model.TotalBuildFailures,9} ");
                Console.ResetColor();

                // Highlight retries in yellow if > 0
                if (model.TotalBuildRetries > 0)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{model.TotalBuildRetries,8} ");
                Console.ResetColor();

                // Highlight sanitization fixes in magenta if > 0 (indicates hallucinations)
                if (model.TotalSanitizationFixes > 0)
                    Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"{model.AverageSanitizationFixesPerItem,9:F2} ");
                Console.ResetColor();

                Console.WriteLine($"{model.AverageTokensPerFile,12:N0}");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Legend: Success = Files successful / Files processed");
            Console.WriteLine("        SanitFix = Average sanitization fixes per file (hallucinated params, invalid tags, etc.)");
            Console.WriteLine("        Tokens/File = Average total tokens (prompt + response) per file");
            Console.ResetColor();
            Console.WriteLine();

            _logger.LogInformation("Model comparison report displayed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating model comparison report");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error generating report: {ex.Message}");
            Console.ResetColor();
        }
    }
}
