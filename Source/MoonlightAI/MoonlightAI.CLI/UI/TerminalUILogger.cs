using Microsoft.Extensions.Logging;

namespace MoonlightAI.CLI.UI;

/// <summary>
/// Custom logger that routes log messages to the Terminal UI.
/// </summary>
public class TerminalUILogger : ILogger
{
    private readonly string _categoryName;
    private readonly MoonlightTerminalUI _ui;
    private readonly LogLevel _minLevel;

    public TerminalUILogger(string categoryName, MoonlightTerminalUI ui, LogLevel minLevel = LogLevel.Information)
    {
        _categoryName = categoryName;
        _ui = ui;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var levelPrefix = logLevel switch
        {
            LogLevel.Trace => "[TRACE]",
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Information => "[INFO]",
            LogLevel.Warning => "[WARN]",
            LogLevel.Error => "[ERROR]",
            LogLevel.Critical => "[CRIT]",
            _ => "[LOG]"
        };

        var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
        var formattedMessage = $"{levelPrefix} {categoryShort}: {message}";

        if (exception != null)
        {
            formattedMessage += $"\n  Exception: {exception.Message}";
        }

        _ui.AppendLog(formattedMessage);
    }
}

/// <summary>
/// Logger provider that creates TerminalUILogger instances.
/// </summary>
public class TerminalUILoggerProvider : ILoggerProvider
{
    private readonly MoonlightTerminalUI _ui;
    private readonly LogLevel _minLevel;

    public TerminalUILoggerProvider(MoonlightTerminalUI ui, LogLevel minLevel = LogLevel.Information)
    {
        _ui = ui;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TerminalUILogger(categoryName, _ui, _minLevel);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
