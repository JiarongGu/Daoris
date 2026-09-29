using Microsoft.Extensions.Logging;

namespace Daoris.Knowledge.Hosting;

/// <summary>
/// The framework's warnings and errors, from every category, into a host's machine log as the
/// <c>log</c> event (LOG1a, D94) — linked into both hosts, and the service's own copy of the desktop's
/// provider, since the two artefacts share no code.
/// </summary>
/// <remarks>
/// The HTTP host runs windowless under the shell, which keeps none of its output, and the MCP host's
/// standard error belongs to the agent that started it: a failing endpoint's or tool's exception,
/// logged by the framework at Error, went nowhere a person could read. Information and below stay out.
/// </remarks>
public sealed class MachineLogProvider(MachineLog log) : ILoggerProvider
{
    /// <summary>A request slower than this is written as <c>request.failed</c> though it succeeded.</summary>
    public const long SlowRequestMs = 2000;

    public ILogger CreateLogger(string categoryName) => new Writer(log, categoryName);

    public void Dispose()
    {
        // The log belongs to the host's entry point, which disposes it.
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
