using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Every call's lineage read from its <c>_meta</c> and written before its tool runs (FABRIC-C1a, D173): the one new read the
/// C0 audit names, since the connector took its identity once, from the environment it was started with, and a native child
/// calls with its parent's.
/// </summary>
/// <remarks>
/// It records and decides nothing: every call goes on to its tool exactly as before. Deciding what a child may call from this
/// lineage is FABRIC-C2a's. Only the harness's <c>_meta</c> is read, never the arguments, which are the model's.
/// </remarks>
public static class CallLineageFilter
{
    /// <summary>Each <c>tools/call</c>'s lineage appended to <paramref name="file"/>, then the call passed on.</summary>
    public static IMcpServerBuilder RecordCallLineage(this IMcpServerBuilder builder, LineageFile file) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => (request, cancellationToken) =>
        {
            file.Call(request.Params?.Name, CallLineage.Read(request.Params?.Meta), DateTimeOffset.UtcNow);
            return next(request, cancellationToken);
        }));
}
