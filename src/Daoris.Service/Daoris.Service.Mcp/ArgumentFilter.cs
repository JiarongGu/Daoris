using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A call that leaves out an argument its tool requires, answered in plain words (CONNARGS1): which parameter is missing, which the call
/// gave that the tool does not take, and every one it takes.
/// </summary>
/// <remarks>
/// Read from the tool's own advertised schema, so a tool added later is covered by its signature and no list names it.
/// </remarks>
public static class ConnectorArguments
{
    /// <summary>The event the machine log keeps a refusal under.</summary>
    public const string Event = "tool.refused";

    /// <summary>
    /// The sentence that refuses a call of <paramref name="tool"/> that gave <paramref name="given"/>, or null where every required
    /// parameter is there (or the tool declares no schema to hold the call to). The names are the parameters', never a value.
    /// </summary>
    public static Refusal? Judge(string tool, JsonElement schema, IReadOnlyCollection<string> given)
    {
        if (schema.ValueKind != JsonValueKind.Object) return null;

        var takes = new List<string>();
        if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            takes.AddRange(properties.EnumerateObject().Select(property => property.Name));
        }

        var required = new List<string>();
        if (schema.TryGetProperty("required", out var names) && names.ValueKind == JsonValueKind.Array)
        {
            required.AddRange(names.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!));
        }

        var missing = required.Where(name => !given.Contains(name, StringComparer.Ordinal)).ToList();
        if (missing.Count == 0) return null;

        var unknown = given.Where(name => !takes.Contains(name, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToList();
        var text = $"`{tool}` was called without {Names(missing)}, which it needs.";
        if (unknown.Count > 0) text += $" It was given {Names(unknown)}, which it does not take.";
        var parts = takes.Select(name => required.Contains(name, StringComparer.Ordinal) ? $"`{name}` (required)" : $"`{name}`");
        text += $" It takes {string.Join(", ", parts)}. Call it again with those names.";
        return new Refusal(text, missing, unknown);
    }

    private static string Names(IReadOnlyList<string> names) =>
        names.Count == 1 ? $"`{names[0]}`" : string.Join(", ", names.Select(name => $"`{name}`"));

    /// <summary>A refused call: what is said to the caller, and the parameter names the log keeps.</summary>
    public sealed record Refusal(string Text, IReadOnlyList<string> Missing, IReadOnlyList<string> Unknown);
}

/// <summary>
/// Every <c>tools/call</c> held to its tool's required parameters before the tool is built (CONNARGS1): a call that leaves one out is the
/// caller's mistake, answered as the tool's error result naming what is missing and what the tool takes, and written to the machine log as
/// a refusal (<c>tool.refused</c>, information), never as the unhandled exception the SDK's argument marshaller made of it.
/// </summary>
/// <remarks>
/// <para>It judges the arguments against the schema the tool advertises, before the tool runs, rather than catching what the marshaller
/// throws: an <see cref="ArgumentException"/> a tool's own code throws is the tool's, still logged as the error it is, and nothing is
/// caught to tell the two apart. A call that names every required parameter goes on as it was.</para>
/// <para>Only the names of the arguments are read, never their values, which are the model's words and stay out of the log.</para>
/// </remarks>
public static class ArgumentFilter
{
    /// <summary>Each call that leaves out a required argument refused with the sentence naming it, else passed on.</summary>
    /// <param name="builder">The server.</param>
    /// <param name="log">Where a refusal is kept; none writes nothing.</param>
    public static IMcpServerBuilder RefuseMissingArguments(this IMcpServerBuilder builder, MachineLog? log) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => (request, cancellationToken) =>
        {
            var name = request.Params?.Name;
            var tool = name is null ? null : request.Server.ServerOptions.ToolCollection?.FirstOrDefault(found => found.ProtocolTool.Name == name);
            if (tool is null) return next(request, cancellationToken);

            var given = request.Params!.Arguments?.Keys.ToList() ?? [];
            if (ConnectorArguments.Judge(name!, tool.ProtocolTool.InputSchema, given) is not { } refusal) return next(request, cancellationToken);

            log?.Info(
                ConnectorArguments.Event,
                ("tool", name),
                ("why", "arguments"),
                ("missing", string.Join(",", refusal.Missing)),
                ("unknown", string.Join(",", refusal.Unknown)));
            return ValueTask.FromResult(new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = refusal.Text }] });
        }));
}
