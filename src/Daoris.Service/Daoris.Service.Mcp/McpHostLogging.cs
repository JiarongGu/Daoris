using Daoris.Knowledge.Hosting;
using Microsoft.Extensions.Logging;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Where this host's framework lines go: standard error and the machine log (LOG1, D94), from warnings up.
/// </summary>
/// <remarks>
/// One method, so the host and the tests that run its server (<c>DiscoverProbeTests</c>) log the same way.
/// </remarks>
public static class McpHostLogging
{
    public static void Use(ILoggingBuilder logging, MachineLog log)
    {
        // stdio IS the protocol channel, so anything written to stdout corrupts it. Logs go to stderr —
        // the single most common way to break a stdio MCP server, and silently, since the transport just
        // stops parsing.
        logging.ClearProviders();
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        // Quiet by default: a stdio server's stderr is the operator's only channel, and per-request info
        // logs bury the one line that matters when something is actually wrong.
        logging.SetMinimumLevel(LogLevel.Warning);
        // The machine log: this host's warnings and errors, in a file of its own beside the desktop's, since
        // this standard error belongs to the agent that started it and nobody keeps it.
        logging.AddProvider(new MachineLogProvider(log));
    }
}
