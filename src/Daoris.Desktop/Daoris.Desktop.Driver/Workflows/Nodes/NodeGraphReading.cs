using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.NodeGraphs;

namespace Daoris.Driver;

/// <summary>A graph's or a fragment's JSON read into its parts, each as written.</summary>
internal static class NodeGraphReading
{
    /// <summary>The parts of <paramref name="json"/>, or null with the problem saying where it is not a graph's shape.</summary>
    internal static GraphParts? Parts(string json, out WireProblem? unread)
    {
        unread = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new Unread("the graph", "an object");
            return new GraphParts(
                Number(root, "schemaVersion", "the graph") ?? SchemaVersion,
                [.. List(root, "nodes", "the graph").Select((node, index) => Node(node, $"nodes[{index}]"))],
                [.. List(root, "wires", "the graph").Select((wire, index) => WireOf(wire, $"wires[{index}]"))],
                [.. List(root, "entries", "the graph").Select((entry, index) => entry.ValueKind == JsonValueKind.String ? entry.GetString()! : throw new Unread($"entries[{index}]", "a node's id"))],
                Text(root, "end", "the graph"),
                root.TryGetProperty("cleanup", out var cleanup) ? CleanupOf(cleanup) : null);
        }
        catch (JsonException error)
        {
            unread = new(GraphRefusal.Unreadable, null, null, $"the graph is not JSON: {error.Message}");
        }
        catch (Unread error)
        {
            unread = new(GraphRefusal.Unreadable, null, null, error.Message);
        }

        return null;
    }

    /// <summary>A place in the file that is not what a graph holds there.</summary>
    private sealed class Unread(string where, string what) : Exception($"{where} is not a graph's shape: it holds no {what} there.");

    private static IEnumerable<JsonElement> List(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var list) || list.ValueKind == JsonValueKind.Null ? []
        : list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToList()
        : throw new Unread($"{where}'s `{name}`", "list");

    private static string? Text(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : throw new Unread($"{where}'s `{name}`", "text");

    private static int? Number(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number
        : throw new Unread($"{where}'s `{name}`", "whole number");

    private static bool? Flag(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
        : throw new Unread($"{where}'s `{name}`", "`true` or `false`");

    private static Dictionary<string, string> Names(JsonElement parent, string name, string where, bool anyValue = false)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return map;
        if (value.ValueKind != JsonValueKind.Object) throw new Unread($"{where}'s `{name}`", "object");
        foreach (var property in value.EnumerateObject())
        {
            map[property.Name] = anyValue ? NodeGraphHomes.Compact(property.Value)
                : property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : throw new Unread($"{where}'s `{name}.{property.Name}`", "schema");
        }

        return map;
    }

    private static NodeDeclaration Node(JsonElement node, string where)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new Unread(where, "node");
        // A synthetic node is decoding's alone (§E7): a file declaring one would escape the authored bound.
        if (node.TryGetProperty("synthetic", out _)) throw new Unread($"{where}'s `synthetic`", "field a person's graph declares");
        var retry = node.TryGetProperty("retry", out var declared) && declared.ValueKind != JsonValueKind.Null ? RetryOf(declared, $"{where}'s `retry`") : null;
        return new NodeDeclaration(
            Text(node, "id", where) ?? throw new Unread($"{where}'s `id`", "text"),
            Text(node, "kind", where) ?? throw new Unread($"{where}'s `kind`", "text"),
            Number(node, "version", where) ?? throw new Unread($"{where}'s `version`", "whole number"),
            Text(node, "reach", where) ?? throw new Unread($"{where}'s `reach`", "text"))
        {
            Inputs = Names(node, "inputs", where),
            Outputs = Names(node, "outputs", where),
            Options = Names(node, "options", where, anyValue: true),
            Evidence = Text(node, "evidence", where),
            Retry = retry,
            TimeoutSeconds = Number(node, "timeoutSeconds", where),
            Rejectable = Flag(node, "rejectable", where) ?? false,
            Skippable = Flag(node, "skippable", where) ?? true,
            Outcomes = [.. List(node, "outcomes", where).Select(outcome => outcome.ValueKind == JsonValueKind.String ? outcome.GetString()! : throw new Unread($"{where}'s `outcomes`", "outcome's name"))],
            OutputsOn = CarriedOn(node, where),
        };
    }

    /// <summary>A node's <c>outputsOn</c>, each output with the outcomes that carry it; absent is none.</summary>
    private static Dictionary<string, IReadOnlyList<string>> CarriedOn(JsonElement node, string where)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (!node.TryGetProperty("outputsOn", out var value) || value.ValueKind == JsonValueKind.Null) return map;
        if (value.ValueKind != JsonValueKind.Object) throw new Unread($"{where}'s `outputsOn`", "object");
        foreach (var output in value.EnumerateObject())
        {
            map[output.Name] = output.Value.ValueKind == JsonValueKind.Array && output.Value.EnumerateArray().All(outcome => outcome.ValueKind == JsonValueKind.String)
                ? [.. output.Value.EnumerateArray().Select(outcome => outcome.GetString()!)]
                : throw new Unread($"{where}'s `outputsOn.{output.Name}`", "list of outcomes");
        }

        return map;
    }

    private static NodeRetry RetryOf(JsonElement retry, string where)
    {
        if (retry.ValueKind != JsonValueKind.Object) throw new Unread(where, "object");
        return new NodeRetry(
            Number(retry, "maxAttempts", where) ?? throw new Unread($"{where}'s `maxAttempts`", "whole number"),
            [.. List(retry, "delaysSeconds", where).Select(delay => delay.ValueKind == JsonValueKind.Number && delay.TryGetInt32(out var seconds) ? seconds : throw new Unread($"{where}'s `delaysSeconds`", "whole number"))]);
    }

    private static Wire WireOf(JsonElement wire, string where)
    {
        if (wire.ValueKind != JsonValueKind.Object) throw new Unread(where, "wire");
        var kind = Text(wire, "kind", where) ?? throw new Unread($"{where}'s `kind`", "text");
        var to = Text(wire, "to", where) ?? throw new Unread($"{where}'s `to`", "node");
        WaitTarget? waits = null;
        WireEnd? from = null;
        if (wire.TryGetProperty("from", out var source) && source.ValueKind != JsonValueKind.Null)
        {
            if (source.ValueKind == JsonValueKind.String) from = EndOf(source.GetString()!, kind);
            else if (source.ValueKind == JsonValueKind.Object)
            {
                waits = new WaitTarget(Text(source, "unit", $"{where}'s `from`") ?? "", Text(source, "revision", $"{where}'s `from`") ?? "")
                {
                    Node = Text(source, "node", $"{where}'s `from`"),
                    End = Flag(source, "end", $"{where}'s `from`") ?? false,
                    Commitment = Text(source, "commitment", $"{where}'s `from`"),
                };
            }
            else throw new Unread($"{where}'s `from`", "node, or what a wait waits on");
        }

        WireGroup? group = null;
        if (wire.TryGetProperty("group", out var declared) && declared.ValueKind != JsonValueKind.Null)
        {
            if (declared.ValueKind != JsonValueKind.Object) throw new Unread($"{where}'s `group`", "object");
            group = new WireGroup(Text(declared, "id", $"{where}'s `group`") ?? "", Text(declared, "mode", $"{where}'s `group`") ?? "");
        }

        return new Wire(Text(wire, "id", where) ?? "", kind, from, EndOf(to, kind))
        {
            Waits = waits, Group = group, Outcome = Text(wire, "outcome", where), On = Text(wire, "on", where),
        };
    }

    /// <summary>A wire's end as the file names it: a node, or on a <c>data</c> wire its port after the last dot (<c>qa.evidence</c>).</summary>
    private static WireEnd EndOf(string named, string kind) =>
        kind == WireKinds.Data && named.LastIndexOf('.') is > 0 and var dot && dot < named.Length - 1 ? new(named[..dot], named[(dot + 1)..]) : new(named);

    private static GraphCleanup CleanupOf(JsonElement cleanup)
    {
        if (cleanup.ValueKind != JsonValueKind.Object) throw new Unread("the graph's `cleanup`", "object");
        return new GraphCleanup(Text(cleanup, "until", "the graph's `cleanup`") ?? GraphCleanupUntil.Held, Number(cleanup, "days", "the graph's `cleanup`"));
    }
}
