using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Every call's lineage read from its <c>_meta</c> and written before its tool runs (FABRIC-C1a, D173): the one new read the
/// C0 audit names, since the connector took its identity once, from the environment it was started with, and a native child
/// calls with its parent's. Then, for a connector that names a session, who made the call decides whether its tool runs
/// (FABRIC-C2a, D173's FABRIC-C2a note).
/// </summary>
/// <remarks>
/// <para>Only the harness's <c>_meta</c> says who called, never the arguments, which are the model's; they are read only to refuse
/// a child's quest that is not plain, never to allow anything.</para>
///
/// <para>A read (<see cref="ToolClasses"/>) is served to every caller. Any other tool runs only for a caller
/// <see cref="CallerProof"/> proves a root; a child, and a caller whose lineage cannot be read, get an error result before the
/// tool runs. <c>quest_publish</c> alone passes a child, in its plain shape. A connector naming no session, a person's own
/// agent's, is not guarded. This is enforcement on calls a harness mediates, not authentication against code running as the
/// same user.</para>
/// </remarks>
public static class CallLineageFilter
{
    /// <summary>Each <c>tools/call</c>'s lineage appended to <paramref name="file"/>, then the call judged and passed on.</summary>
    /// <param name="builder">The server being built.</param>
    /// <param name="file">The session's lineage file, which also says whether the connector names a session.</param>
    /// <param name="joinBudget">How long a Claude call waits for its hook line; <see cref="HookJoin.Budget"/> when null.</param>
    public static IMcpServerBuilder RecordCallLineage(this IMcpServerBuilder builder, LineageFile file, TimeSpan? joinBudget = null) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var tool = request.Params?.Name;
            var lineage = CallLineage.Read(request.Params?.Meta);
            file.Call(tool, lineage, DateTimeOffset.UtcNow);
            if (file.NamesSession)
            {
                var refusal = await RefusalAsync(request, tool, lineage, file, joinBudget ?? HookJoin.Budget, cancellationToken).ConfigureAwait(false);
                if (refusal is not null) return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = refusal }] };
            }

            return await next(request, cancellationToken).ConfigureAwait(false);
        }));

    /// <summary>The refusal for this call, or null where it may run.</summary>
    private static async Task<string?> RefusalAsync(
        RequestContext<CallToolRequestParams> request, string? tool, CallLineage? lineage, LineageFile file, TimeSpan budget,
        CancellationToken cancellationToken)
    {
        var kind = ToolClasses.Of(tool);
        if (kind == ToolClass.Read) return null;

        var caller = await CallerProof.OfAsync(lineage, toolUse => HookJoin.FindAsync(file.Path, toolUse, budget, cancellationToken))
            .ConfigureAwait(false);
        if (caller.Kind == CallerKind.Root) return null;
        if (kind != ToolClass.Publish) return Text(tool, caller, shape: null);

        var asAsk = request.Services?.GetService<IntakeScope>()?.Active == true;
        return NotPlain(request.Params?.Arguments, asAsk) is { } shape ? Text(tool, caller, shape) : null;
    }

    /// <summary>What a child's quest carries beyond a plain one, or null where it is plain.</summary>
    private static string? NotPlain(IDictionary<string, JsonElement>? arguments, bool asAsk)
    {
        if (asAsk) return "it would be published as the session's ask";
        if (Given(arguments, "from").Any(from => from.ValueKind == JsonValueKind.String && AskDesk.AskOf(from.GetString()) is not null))
        {
            return "it names an ask as its sender";
        }

        foreach (var (field, what) in new[]
        {
            ("attachments", "attachments"), ("then", "a chain of further quests"), ("requirements", "requirements"),
            ("review", "a review choice"), ("reviewProposal", "a review proposal"), ("workflowProposal", "a workflow proposal"),
        })
        {
            if (Given(arguments, field).Any(value => value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 0))
            {
                return $"it carries {what}";
            }
        }

        return null;
    }

    /// <summary>Every argument named <paramref name="name"/> in any casing that says something: a null says nothing.</summary>
    private static IEnumerable<JsonElement> Given(IDictionary<string, JsonElement>? arguments, string name) =>
        (arguments ?? new Dictionary<string, JsonElement>())
        .Where(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)
            && pair.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        .Select(pair => pair.Value);

    /// <summary>
    /// The refusal: that nothing was done, why (a subagent, or lineage that could not be verified, and the second never claims
    /// the first), that the reads remain, and where the request belongs.
    /// </summary>
    private static string Text(string? tool, Caller caller, string? shape)
    {
        var said = tool is null ? "this call" : $"`{tool}`";
        var reads = string.Join(", ", ToolClasses.Reads);
        var who = caller.Kind == CallerKind.Child
            ? "This call came from a subagent"
            : $"Daoris could not verify which agent made this call ({caller.Reason})";
        if (shape is not null)
        {
            return $"Nothing was done: {said} was not run, and no quest was published. {who}, and such a caller may publish only a "
                   + $"plain quest, which this is not: {shape}. Publish it without that, or put the request in your result for your "
                   + $"parent. The reads remain open to you ({reads}).";
        }

        var why = caller.Kind == CallerKind.Child
            ? $"{who}, and a subagent's calls here are read-only."
            : $"{who}, so it ran only the reads. That is no finding that a subagent called.";
        return $"Nothing was done: {said} was not run. {why} The reads remain open to you ({reads}). Put the request in your result "
               + "for your parent, which can make it, or publish a quest to the repository that should.";
    }
}
