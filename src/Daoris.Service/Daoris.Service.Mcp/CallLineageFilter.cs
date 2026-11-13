using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Every call's lineage, read from its <c>_meta</c> and written before its tool runs, for the driver to bind a native child to its
/// session's part. It decides nothing: every tool runs for every caller, and only the harness's <c>_meta</c> says who called.
/// </summary>
public static class CallLineageFilter
{
    public static IMcpServerBuilder RecordCallLineage(this IMcpServerBuilder builder, LineageFile file) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var tool = request.Params?.Name;
            file.Call(tool, CallLineage.Read(request.Params?.Meta), DateTimeOffset.UtcNow, CallPlace.ActOf(tool));
            return await next(request, cancellationToken).ConfigureAwait(false);
        }));
}
