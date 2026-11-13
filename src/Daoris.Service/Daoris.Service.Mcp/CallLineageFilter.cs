using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Every call's lineage read from its <c>_meta</c> and written before its tool runs: the connector takes its identity once, from the
/// environment it is started with, and a native child calls with its parent's, so the driver binds the child's calls to the session's
/// part from this record.
/// </summary>
/// <remarks>
/// The record only says who called; it decides nothing. A subagent's call counts as its session's, and every tool runs for every
/// caller (ACCESS1 row h). Only the harness's <c>_meta</c> says who called, never the arguments, which are the model's.
/// </remarks>
public static class CallLineageFilter
{
    /// <summary>Each <c>tools/call</c>'s lineage appended to <paramref name="file"/>, then the call passed on.</summary>
    /// <param name="builder">The server being built.</param>
    /// <param name="file">The session's lineage file.</param>
    public static IMcpServerBuilder RecordCallLineage(this IMcpServerBuilder builder, LineageFile file) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var tool = request.Params?.Name;
            file.Call(tool, CallLineage.Read(request.Params?.Meta), DateTimeOffset.UtcNow, CallPlace.ActOf(tool));
            return await next(request, cancellationToken).ConfigureAwait(false);
        }));
}
