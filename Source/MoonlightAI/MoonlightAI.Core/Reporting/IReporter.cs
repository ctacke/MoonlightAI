namespace MoonlightAI.Core.Reporting;

/// <summary>
/// Interface for generating reports.
/// </summary>
public interface IReporter
{
    /// <summary>
    /// Displays a report to the console.
    /// </summary>
    Task DisplayReportAsync(CancellationToken cancellationToken = default);
}
