using Daoris.Driver;
using Microsoft.Extensions.Logging;

namespace Daoris.Desktop;

/// <summary>
/// The logging framework's warnings and errors, from every category, into the machine log as the
/// <c>log</c> event (LOG1a, D94).
/// </summary>
/// <remarks>
/// The kit, the engine and the modules log through <see cref="ILogger"/>, and an installed application
/// has no console, so what they warned about went nowhere a person could read. Information and below
/// stay out: the log records what happened to the machine, not the framework's running commentary.
/// </remarks>
public sealed class MachineLogProvider(MachineLog log) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Writer(log, categoryName);

    public void Dispose()
    {
        // The log belongs to whoever opened it; the provider only writes to it.
    }

    private sealed class Writer(MachineLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            log.Write(
                logLevel >= LogLevel.Error ? "error" : "warn",
                "log",
                [("category", category), ("message", formatter(state, exception)), ("exception", exception?.ToString())]);
        }
    }
}
