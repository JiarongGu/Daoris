using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The probe a client of the protocol's 2026-07-28 revision opens with, <c>server/discover</c> (SEP-2575),
/// answered as a server that predates the revision answers it: method not found, which is how the client
/// learns to fall back to the <c>initialize</c> handshake (MCPDISCOVER1).
/// </summary>
/// <remarks>
/// <para>The SDK this host is built on registers no handler for the probe, so that was already the answer on
/// the wire. It came with two warnings, the missing handler and the handler's failure with its exception,
/// written into the machine log at every connector's start, where a warning is something a person should
/// look at (<c>docs/2026-09-30-machine-log-design.md</c> §4) and this one asks nothing of anyone. Answered
/// here, before the SDK's dispatch, the client gets the same error and the log one line at debug, below
/// every provider's floor. Every other method with no handler still reaches the SDK and still warns.</para>
///
/// <para>A later SDK answers the probe itself and offers the new revision; this filter would then go on
/// refusing it. <c>DiscoverProbeTests</c> holds that the SDK in use does not, so the upgrade that changes
/// that removes this and decides which revision the connector speaks.</para>
/// </remarks>
public static class DiscoverProbe
{
    /// <summary>The probe's method.</summary>
    public const string Method = "server/discover";

    /// <summary>The probe answered before the SDK's dispatch, and every other message passed on.</summary>
    public static IMcpServerBuilder AnswerDiscoverProbe(this IMcpServerBuilder builder) =>
        builder.WithMessageFilters(filters => filters.AddIncomingFilter(Filter));

    private static McpMessageHandler Filter(McpMessageHandler next) => (context, cancellationToken) =>
    {
        if (context.JsonRpcMessage is not JsonRpcRequest { Method: Method }) return next(context, cancellationToken);

        context.Services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(DiscoverProbe).FullName!).LogDebug(
            "A client probed '{Method}', a protocol revision this server predates: answered method not found, so it falls back to initialize.",
            Method);
        // Thrown, not sent: the session answers a request's protocol error with its code and message and writes
        // nothing, the same error and words the SDK gives a method it has no handler for.
        return Task.FromException(new McpProtocolException($"Method '{Method}' is not available.", McpErrorCode.MethodNotFound));
    };
}
