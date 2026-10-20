using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A change to the person's part judged (D157 point 10): what it lowers, and whether it applies now. An agent's choice that lowers
/// anything is kept as a proposal, which only the person's press applies; the person's own applies, said.
/// </summary>
public sealed record InvolvementJudged(IReadOnlyList<string> Lowered, bool Applies);

/// <summary>
/// The lowering table's rows for routes and verification pipelines (FABRIC-PIPELINE1a, D192 point 2, the pipeline design §1.3): a landed
/// route removed, set to <c>current</c> or pointed at a pipeline whose checks run fewer of the declaration's gates; a verification
/// version that drops a <c>check</c>, narrows a check's <c>gates</c> (from <c>heavy</c> to names, or by a name), or removes the wire a
/// node waits on a check by; and a check's <c>failed</c> moved off <c>wait</c>. A table compared node by node, never a reading. The
/// fourth row, <c>light</c> with nothing running the heavy gates, ships with FABRIC-PIPELINE1b.
/// </summary>
public static partial class Involvement
{
    /// <summary>A check's <c>gates</c> when it names none: the declaration's heavy gates (the pipeline design §4.1).</summary>
    public const string HeavyGates = "heavy";

    /// <summary>A check's <c>failed</c> when it names none: the failure waits for the person's <i>Ask for a fix…</i>.</summary>
    public const string FailedWait = "wait";

    /// <summary>
    /// How <paramref name="chosen"/> verifies the work that lands less than <paramref name="otherwise"/> (the pipeline design §1.3), each
    /// said as what it does; empty where it lowers nothing. A route is null or <c>current</c> where it names no verification.
    /// </summary>
    /// <param name="newest">A verification pipeline's newest graph by its id, or null where it cannot be read here.</param>
    public static IReadOnlyList<string> LowersRoute(WorkflowLanded? chosen, WorkflowLanded? otherwise, Func<string, NodeGraph?> newest)
    {
        static string? Named(WorkflowLanded? route) => route?.Workflow is { } id && id != WorkflowSelection.Current ? id : null;
        if (Named(otherwise) is not { } before) return [];
        if (Named(chosen) is not { } after) return ["it leaves the work that lands unverified"];
        if (newest(after) is not { } graph) return [$"it verifies the work that lands with `{after}`, whose checks cannot be read here"];
        if (newest(before) is not { } was) return [];

        var fewer = Fewer(GatesOf(was), GatesOf(graph)).Select(gate => gate == HeavyGates ? "the heavy gates" : $"`{gate}`").ToList();
        if (fewer.Count == 0) return [];
        var named = fewer.Count == 1 ? fewer[0] : $"{string.Join(", ", fewer.Take(fewer.Count - 1))} and {fewer[^1]}";
        return [$"it verifies the work that lands with fewer of its gates: {named}"];
    }

    /// <summary>
    /// How a verification pipeline's <paramref name="chosen"/> version asks less of its checks than <paramref name="otherwise"/> (the
    /// pipeline design §1.3), check by check in <paramref name="otherwise"/>'s order, each said as what it does; empty where it lowers
    /// nothing.
    /// </summary>
    public static IReadOnlyList<string> LowersVerification(NodeGraph chosen, NodeGraph otherwise)
    {
        var lowered = new List<string>();
        var kept = chosen.Nodes.Where(node => node.Kind == NodeKinds.Check).ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (var check in otherwise.Nodes.Where(node => node.Kind == NodeKinds.Check))
        {
            if (!kept.TryGetValue(check.Id, out var now))
            {
                lowered.Add($"it drops the check `{check.Id}`");
                continue;
            }

            if (Fewer(GatesOf(check), GatesOf(now)).Count > 0) lowered.Add($"it narrows the check `{check.Id}`'s gates");
            if (OptionText(check, "failed") is FailedWait or null && OptionText(now, "failed") is { } failed and not FailedWait)
            {
                lowered.Add($"it moves the check `{check.Id}`'s failure off your press, to `{failed}`");
            }
        }

        var nodes = chosen.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var wire in otherwise.Wires.Where(wire => wire.From is { Unit: null } from && otherwise.Nodes.Any(node => node.Id == from.Node && node.Kind == NodeKinds.Check)))
        {
            var (from, to) = (wire.From!.Node, wire.To.Node);
            if (kept.ContainsKey(from) && nodes.Contains(to) && !chosen.Wires.Any(each => each.From?.Node == from && each.From.Unit is null && each.To.Node == to))
            {
                lowered.Add($"it no longer holds `{to}` on the check `{from}`");
            }
        }

        return [.. lowered.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// A change judged (D157 point 10, the workflow design §4.3): the person's press applies whatever it lowers, said; an agent's choice
    /// applies only where it lowers nothing, and is otherwise kept as a proposal for the person's press.
    /// </summary>
    public static InvolvementJudged Judge(IReadOnlyList<string> lowered, bool byThePerson) => new(lowered, byThePerson || lowered.Count == 0);

    /// <summary>
    /// The gates <paramref name="was"/> runs that <paramref name="now"/> does not, in order: the heavy gates where <paramref name="now"/>
    /// runs them no longer, and a name where it lists names and not that one. Names given up for the heavy gates are none (the design's
    /// table counts a narrowing from <c>heavy</c> to names, or by a name), since which gates are heavy is the declaration's, read when
    /// the check runs.
    /// </summary>
    private static List<string> Fewer(List<string> was, List<string> now) =>
        now.Contains(HeavyGates) ? [] : [.. was.Where(gate => !now.Contains(gate))];

    /// <summary>The gates a graph's checks run, by name in node order, <see cref="HeavyGates"/> standing for the declaration's heavy gates.</summary>
    private static List<string> GatesOf(NodeGraph graph) =>
        [.. graph.Nodes.Where(node => node.Kind == NodeKinds.Check).SelectMany(GatesOf).Distinct(StringComparer.Ordinal)];

    /// <summary>A check's <c>gates</c>: <see cref="HeavyGates"/> where it names none or names it, else the names it lists.</summary>
    private static List<string> GatesOf(NodeDeclaration check)
    {
        if (!check.Options.TryGetValue("gates", out var text)) return [HeavyGates];
        using var value = JsonDocument.Parse(text);
        return value.RootElement.ValueKind switch
        {
            JsonValueKind.Array => [.. value.RootElement.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)],
            JsonValueKind.String => [value.RootElement.GetString()!],
            _ => [HeavyGates],
        };
    }

    /// <summary>An option's text value, or null where it names none or names no text.</summary>
    private static string? OptionText(NodeDeclaration node, string name)
    {
        if (!node.Options.TryGetValue(name, out var text)) return null;
        using var value = JsonDocument.Parse(text);
        return value.RootElement.ValueKind == JsonValueKind.String ? value.RootElement.GetString() : null;
    }
}
