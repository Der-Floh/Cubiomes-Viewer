using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Cli.Service.ConsoleServ;

/// <summary>Writes log entries to the error stream as short messages, without categories or stack traces.</summary>
public sealed class CliLoggerProvider : ILoggerProvider
{
    private readonly ICliConsole _console;

    public CliLoggerProvider(ICliConsole console)
    {
        _console = console;
    }

    public ILogger CreateLogger(string categoryName) => new CliLogger(_console);

    public void Dispose()
    {
    }

    private sealed class CliLogger(ICliConsole console) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var reason = exception is null ? string.Empty : $" ({exception.Message})";
            console.WriteMessage($"{Label(logLevel)}: {message}{reason}");
        }

        private static string Label(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warning",
            LogLevel.Error => "error",
            LogLevel.Critical => "critical",
            _ => throw new NotSupportedException($"Unsupported log level: {logLevel}"),
        };
    }
}
