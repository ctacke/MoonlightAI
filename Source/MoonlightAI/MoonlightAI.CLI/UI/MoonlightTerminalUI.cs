using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Data;
using MoonlightAI.Core.Models;
using Terminal.Gui;

namespace MoonlightAI.CLI.UI;

/// <summary>
/// Terminal UI for MoonlightAI with split-panel layout.
/// Top: Configuration and statistics
/// Middle: Scrolling workload log output
/// Bottom: Command input
/// </summary>
public class MoonlightTerminalUI : IDisposable
{
    private readonly FrameView _statusFrame;
    private readonly Label _statusLabel;
    private readonly FrameView _logFrame;
    private readonly TextView _logView;
    private readonly FrameView _inputFrame;
    private readonly TextField _inputField;
    private readonly Window _mainWindow;

    private readonly AIServerConfiguration _aiConfig;
    private readonly RepositoryConfigurations _repoConfig;
    private readonly WorkloadConfiguration _workloadConfig;
    private readonly ContainerConfiguration _containerConfig;
    private readonly DatabaseConfiguration _databaseConfig;

    // Statistics
    private int _totalRuns = 0;
    private int _filesProcessed = 0;
    private string _currentStatus = "Idle";
    private int _batchCurrent = 0;
    private int _batchTotal = 0;
    private ModelStatistics? _currentModelStats = null;

    public event EventHandler<string>? CommandEntered;

    public MoonlightTerminalUI(
        AIServerConfiguration aiConfig,
        RepositoryConfigurations repoConfig,
        WorkloadConfiguration workloadConfig,
        ContainerConfiguration containerConfig,
        DatabaseConfiguration databaseConfig)
    {
        _aiConfig = aiConfig;
        _repoConfig = repoConfig;
        _workloadConfig = workloadConfig;
        _containerConfig = containerConfig;
        _databaseConfig = databaseConfig;

        // Initialize Terminal.Gui
        Application.Init();

        // Create main window
        _mainWindow = new Window("MoonlightAI - AI-Powered Code Documentation")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Top panel: Status and configuration (fixed height) - split into two columns
        _statusFrame = new FrameView("Configuration & Statistics")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 10
        };

        // Use a single label with formatted text for two-column layout
        _statusLabel = new Label("")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _statusFrame.Add(_statusLabel);

        // Middle panel: Scrolling log output
        _logFrame = new FrameView("Workload Output")
        {
            X = 0,
            Y = Pos.Bottom(_statusFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill() - 4 // Leave room for input at bottom
        };

        _logView = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        _logFrame.Add(_logView);

        // Bottom panel: Command input
        _inputFrame = new FrameView("Command")
        {
            X = 0,
            Y = Pos.Bottom(_logFrame),
            Width = Dim.Fill(),
            Height = 3
        };

        _inputField = new TextField("")
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = 1
        };

        // Handle Enter key for command submission
        _inputField.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Enter)
            {
                var command = _inputField.Text?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(command))
                {
                    CommandEntered?.Invoke(this, command);
                    _inputField.Text = string.Empty;
                }
                e.Handled = true;
            }
        };

        _inputFrame.Add(_inputField);

        // Add all panels to main window
        _mainWindow.Add(_statusFrame, _logFrame, _inputFrame);

        // Add main window to application
        Application.Top.Add(_mainWindow);

        // Initial status update
        UpdateStatus();

        // Set focus to input field
        _inputField.SetFocus();
    }

    /// <summary>
    /// Shortens a file path for display by keeping start and end, replacing middle with "..."
    /// </summary>
    private string ShortenPath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        // Try to show the important parts: drive/start and filename
        var fileName = Path.GetFileName(path);
        var dirPath = Path.GetDirectoryName(path) ?? "";

        // If filename itself is too long, truncate it
        if (fileName.Length > maxLength - 3)
        {
            return "..." + fileName.Substring(fileName.Length - (maxLength - 3));
        }

        // Calculate how much of the directory we can show
        var availableForDir = maxLength - fileName.Length - 4; // 4 for "...\"
        if (availableForDir > 0 && dirPath.Length > availableForDir)
        {
            return dirPath.Substring(0, availableForDir) + "..." + Path.DirectorySeparatorChar + fileName;
        }

        return path;
    }

    /// <summary>
    /// Updates the status panel with current configuration and statistics.
    /// </summary>
    public void UpdateStatus()
    {
        var repo = _repoConfig.Repositories.FirstOrDefault()?.RepositoryUrl ?? "Not configured";

        // Parse repository name from URL for cleaner display
        var repoName = repo;
        if (Uri.TryCreate(repo, UriKind.Absolute, out var uri))
        {
            // Extract owner/repo from github.com/owner/repo
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                repoName = $"{segments[segments.Length - 2]}/{segments[segments.Length - 1]}";
            }
        }

        var containerStatus = _containerConfig.UseLocalContainer
            ? $"{_containerConfig.ContainerName} (Auto)"
            : _aiConfig.ServerUrl;

        // Get shortened paths for display
        var modelsPath = _containerConfig.UseLocalContainer && !string.IsNullOrEmpty(_containerConfig.ModelsPath)
            ? ShortenPath(_containerConfig.ModelsPath, 30)
            : "N/A";
        var dbPath = ShortenPath(_databaseConfig.DatabasePath, 30);

        // Left column: Configuration
        var leftColumn = $@"CONFIGURATION
Repository: {repoName}
Model: {_aiConfig.ModelName}
{(_containerConfig.UseLocalContainer ? "Container" : "Server")}: {containerStatus}
Batch Size: {_workloadConfig.BatchSize}
Models: {modelsPath}
Database: {dbPath}";

        // Right column: Statistics
        var rightLines = new List<string>
        {
            "STATISTICS",
            $"Status: {_currentStatus}"
        };

        if (_batchTotal > 0)
        {
            rightLines.Add($"Batch: {_batchCurrent}/{_batchTotal} files");
        }

        if (_currentModelStats != null)
        {
            rightLines.Add("");
            rightLines.Add("Model Performance:");
            rightLines.Add($"  Success: {_currentModelStats.SuccessRate:F1}%");
            rightLines.Add($"  Sanit Fix: {_currentModelStats.AverageSanitizationFixesPerItem:F2}/item");
            rightLines.Add($"  Build Fail: {_currentModelStats.TotalBuildFailures}");
            rightLines.Add($"  Retries: {_currentModelStats.TotalBuildRetries}");
        }

        // Combine into two columns
        var leftLines = leftColumn.Split('\n');
        var maxLines = Math.Max(leftLines.Length, rightLines.Count);
        var combinedText = new System.Text.StringBuilder();

        for (int i = 0; i < maxLines; i++)
        {
            var left = i < leftLines.Length ? leftLines[i] : "";
            var right = i < rightLines.Count ? rightLines[i] : "";

            // Pad left column to 50 characters
            combinedText.AppendLine($"{left,-50}  {right}");
        }

        Application.MainLoop.Invoke(() =>
        {
            _statusLabel.Text = combinedText.ToString();
        });
    }

    /// <summary>
    /// Appends a log message to the scrolling output.
    /// </summary>
    public void AppendLog(string message)
    {
        Application.MainLoop.Invoke(() =>
        {
            var currentText = _logView.Text?.ToString() ?? string.Empty;
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var newText = currentText + $"[{timestamp}] {message}\n";

            _logView.Text = newText;

            // Auto-scroll to bottom
            _logView.MoveEnd();
        });
    }

    /// <summary>
    /// Updates the current status text.
    /// </summary>
    public void SetStatus(string status)
    {
        _currentStatus = status;
        UpdateStatus();
    }

    /// <summary>
    /// Updates the statistics.
    /// </summary>
    public void UpdateStatistics(int totalRuns, int filesProcessed)
    {
        _totalRuns = totalRuns;
        _filesProcessed = filesProcessed;
        UpdateStatus();
    }

    /// <summary>
    /// Updates the batch progress (current/total files in this batch).
    /// </summary>
    public void UpdateBatchProgress(int current, int total)
    {
        _batchCurrent = current;
        _batchTotal = total;
        UpdateStatus();
    }

    /// <summary>
    /// Clears the batch progress display.
    /// </summary>
    public void ClearBatchProgress()
    {
        _batchCurrent = 0;
        _batchTotal = 0;
        UpdateStatus();
    }

    /// <summary>
    /// Updates the current model statistics for display.
    /// </summary>
    public void UpdateModelStatistics(ModelStatistics? stats)
    {
        _currentModelStats = stats;
        UpdateStatus();
    }

    /// <summary>
    /// Runs the Terminal.Gui application loop.
    /// </summary>
    public void Run()
    {
        Application.Run();
    }

    /// <summary>
    /// Stops the application.
    /// </summary>
    public void Stop()
    {
        Application.RequestStop();
    }

    public void Dispose()
    {
        Application.Shutdown();
    }
}
