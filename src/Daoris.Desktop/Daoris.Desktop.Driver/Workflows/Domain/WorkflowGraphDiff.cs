namespace Daoris.Driver;

/// <summary>What a <see cref="GraphChange"/> is of, and what became of it.</summary>
public static class GraphChanges
{
    public const string Step = "step";
    public const string Way = "way";

    /// <summary>The graph's own fields: its entries, its end, its cleanup.</summary>
    public const string Graph = "graph";

    public const string Added = "added";
    public const string Removed = "removed";
    public const string Changed = "changed";
}

/// <summary>
/// One change a proposed graph makes to its base: a step, a way or a field of the graph, added, removed or changed, the
/// fields that changed, the other changes it needs taken with it, and the driver's English, which the page words.
/// </summary>
/// <param name="Id">
/// The change's own id, unique in its diff: <c>step:</c>, <c>way:</c> or <c>graph:</c>, then <c>+</c>, <c>-</c> or <c>~</c>, then the step's
/// or way's id (the base's for a way removed, the proposal's otherwise) or the graph's field.
/// </param>
/// <param name="What">One of <see cref="GraphChanges.Step"/>, <see cref="GraphChanges.Way"/> and <see cref="GraphChanges.Graph"/>.</param>
/// <param name="Change">One of <see cref="GraphChanges.Added"/>, <see cref="GraphChanges.Removed"/> and <see cref="GraphChanges.Changed"/>.</param>
public sealed record GraphChange(string Id, string What, string Change, string Said)
{
    /// <summary>The step's id, for a step's change.</summary>
    public string? Step { get; init; }

    /// <summary>The way's id, for a way's change: the proposal's, or the base's for a way removed.</summary>
    public string? Way { get; init; }

    /// <summary>The way's id in the base, where a changed way's id moved; null where it kept it.</summary>
    public string? Was { get; init; }

    /// <summary>The step's or the way's kind, as the proposal has it, or the base for one removed.</summary>
    public string? Kind { get; init; }

    /// <summary>The fields that changed: a step's (<c>options.&lt;name&gt;</c> for each option), a way's, the graph's.</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>The changes this one needs taken with it, by their ids.</summary>
    public IReadOnlyList<string> Needs { get; init; } = [];
}

/// <summary>
/// A graph proposed against its base, said as changes. Steps are matched by id. Ways are
/// matched by structure first (kind, ends with their ports and units, outcome, <c>on</c>, what a wait waits on), then by id, so a way the
/// proposal renumbered reads as its id changed, never as removed and added; and nothing that runs differently is folded away, since a way's
/// id orders an <c>anyOf</c>'s witness and keys a trigger. Each change needs the steps its way is added between, the way that held an id it
/// takes, and for a step removed each way of it removed or moved. Removals come first in the base's order, then the rest in the proposal's.
/// </summary>
public static class WorkflowGraphDiff
{
    public static IReadOnlyList<GraphChange> Of(NodeGraph before, NodeGraph after)
    {
        var was = before.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var now = after.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        string StepId(string id, string change) => $"{GraphChanges.Step}:{Mark(change)}{id}";

        // Ways: by structure first, then by id among those left; pairs map a proposal's way to its base's.
        var paired = new Dictionary<Wire, Wire>(ReferenceEqualityComparer.Instance);
        var taken = new HashSet<Wire>(ReferenceEqualityComparer.Instance);
        foreach (var wire in after.Wires)
        {
            var same = before.Wires.Where(old => !taken.Contains(old) && SameStructure(old, wire)).ToList();
            if ((same.FirstOrDefault(old => old.Id == wire.Id) ?? same.FirstOrDefault()) is { } match)
            {
                paired[wire] = match;
                taken.Add(match);
            }
        }

        foreach (var wire in after.Wires.Where(wire => !paired.ContainsKey(wire)))
        {
            if (before.Wires.FirstOrDefault(old => !taken.Contains(old) && old.Id == wire.Id) is { } match)
            {
                paired[wire] = match;
                taken.Add(match);
            }
        }

        var removedWays = before.Wires.Where(old => !taken.Contains(old)).ToList();
        var changedWays = after.Wires.Where(wire => paired.TryGetValue(wire, out var old) && WayFields(old, wire).Count > 0).ToList();
        var addedWays = after.Wires.Where(wire => !paired.ContainsKey(wire)).ToList();

        // The change each base way's id went with: its removal, or its pairing where that moved it or changed it.
        var baseChange = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var old in removedWays) baseChange[old.Id] = WayId(old.Id, GraphChanges.Removed);
        foreach (var wire in changedWays) baseChange[paired[wire].Id] = WayId(wire.Id, GraphChanges.Changed);

        List<string> EndsNeed(Wire wire) =>
        [
            .. new[] { wire.From?.Node, wire.To.Node }.OfType<string>().Distinct(StringComparer.Ordinal)
                .Where(node => !was.ContainsKey(node) && now.ContainsKey(node)).Select(node => StepId(node, GraphChanges.Added)),
        ];

        List<string> IdNeeds(Wire wire, string self) =>
            baseChange.TryGetValue(wire.Id, out var other) && other != self && (!paired.TryGetValue(wire, out var old) || old.Id != wire.Id) ? [other] : [];

        var changes = new List<GraphChange>();
        foreach (var node in before.Nodes.Where(node => !now.ContainsKey(node.Id)))
        {
            var needs = before.Wires.Where(old => Touches(old, node.Id) && baseChange.ContainsKey(old.Id)).Select(old => baseChange[old.Id]);
            changes.Add(new GraphChange(StepId(node.Id, GraphChanges.Removed), GraphChanges.Step, GraphChanges.Removed, $"Remove the step `{node.Id}` (`{node.Kind}`).")
            {
                Step = node.Id, Kind = node.Kind, Needs = [.. needs.Distinct(StringComparer.Ordinal)],
            });
        }

        foreach (var node in after.Nodes)
        {
            if (!was.TryGetValue(node.Id, out var old))
            {
                changes.Add(new GraphChange(StepId(node.Id, GraphChanges.Added), GraphChanges.Step, GraphChanges.Added, $"Add the step `{node.Id}` (`{node.Kind}`).")
                {
                    Step = node.Id, Kind = node.Kind,
                });
                continue;
            }

            var fields = StepFields(old, node);
            if (fields.Count == 0) continue;
            changes.Add(new GraphChange(
                StepId(node.Id, GraphChanges.Changed), GraphChanges.Step, GraphChanges.Changed,
                $"Change the step `{node.Id}` (`{node.Kind}`): its {string.Join(", ", fields.Select(field => $"`{field}`"))}.")
            {
                Step = node.Id, Kind = node.Kind, Fields = fields,
            });
        }

        foreach (var old in removedWays)
        {
            var id = WayId(old.Id, GraphChanges.Removed);
            changes.Add(new GraphChange(id, GraphChanges.Way, GraphChanges.Removed, $"Remove the way `{old.Id}` ({Qualified(old)}): {Ends(old)}.")
            {
                Way = old.Id, Kind = old.Kind,
            });
        }

        foreach (var wire in after.Wires)
        {
            if (!paired.TryGetValue(wire, out var old))
            {
                var id = WayId(wire.Id, GraphChanges.Added);
                changes.Add(new GraphChange(id, GraphChanges.Way, GraphChanges.Added, $"Add the way `{wire.Id}` ({Qualified(wire)}): {Ends(wire)}.")
                {
                    Way = wire.Id, Kind = wire.Kind, Needs = [.. EndsNeed(wire), .. IdNeeds(wire, id)],
                });
                continue;
            }

            var fields = WayFields(old, wire);
            if (fields.Count == 0) continue;
            var changed = WayId(wire.Id, GraphChanges.Changed);
            changes.Add(new GraphChange(changed, GraphChanges.Way, GraphChanges.Changed, $"Change the way `{wire.Id}` ({Qualified(wire)}): {Ends(wire)}; {WasSaid(old, wire, fields)}.")
            {
                Way = wire.Id, Was = old.Id == wire.Id ? null : old.Id, Kind = wire.Kind, Fields = fields, Needs = [.. EndsNeed(wire), .. IdNeeds(wire, changed)],
            });
        }

        if (!before.Entries.SequenceEqual(after.Entries, StringComparer.Ordinal))
        {
            changes.Add(new GraphChange($"{GraphChanges.Graph}:~entries", GraphChanges.Graph, GraphChanges.Changed,
                $"The work starts at {Names(after.Entries)} now, not {Names(before.Entries)}.")
            {
                Fields = ["entries"],
                Needs = [.. after.Entries.Where(entry => !was.ContainsKey(entry) && now.ContainsKey(entry)).Select(entry => StepId(entry, GraphChanges.Added))],
            });
        }

        if (before.End != after.End)
        {
            changes.Add(new GraphChange($"{GraphChanges.Graph}:~end", GraphChanges.Graph, GraphChanges.Changed, $"The work ends at `{after.End}` now, not `{before.End}`.")
            {
                Fields = ["end"],
                Needs = !was.ContainsKey(after.End) && now.ContainsKey(after.End) ? [StepId(after.End, GraphChanges.Added)] : [],
            });
        }

        if (before.Cleanup != after.Cleanup || before.SchemaVersion != after.SchemaVersion)
        {
            changes.Add(new GraphChange($"{GraphChanges.Graph}:~cleanup", GraphChanges.Graph, GraphChanges.Changed,
                $"Its cleanup is `{after.Cleanup.Until}`{(after.Cleanup.Days is { } days ? $" within {days} days" : "")} now.")
            {
                Fields = [.. new[] { before.Cleanup != after.Cleanup ? "cleanup" : null, before.SchemaVersion != after.SchemaVersion ? "schemaVersion" : null }.OfType<string>()],
            });
        }

        return changes;
    }

    private static string Mark(string change) => change switch
    {
        GraphChanges.Added => "+",
        GraphChanges.Removed => "-",
        _ => "~",
    };

    private static string WayId(string id, string change) => $"{GraphChanges.Way}:{Mark(change)}{id}";

    private static bool Touches(Wire wire, string node) => wire.From?.Node == node || wire.To.Node == node;

    /// <summary>Whether two ways run alike, their ids and groups aside.</summary>
    private static bool SameStructure(Wire one, Wire other) =>
        one.Kind == other.Kind && Equals(one.From, other.From) && Equals(one.To, other.To)
        && one.Outcome == other.Outcome && one.On == other.On && Equals(one.Waits, other.Waits);

    /// <summary>A way's fields that differ, in the file's order and names.</summary>
    private static List<string> WayFields(Wire old, Wire wire)
    {
        var fields = new List<string>();
        if (old.Id != wire.Id) fields.Add("id");
        if (old.Kind != wire.Kind) fields.Add("kind");
        if (!Equals(old.From, wire.From)) fields.Add("from");
        if (!Equals(old.To, wire.To)) fields.Add("to");
        if (!Equals(old.Waits, wire.Waits)) fields.Add("waits");
        if (old.Outcome != wire.Outcome) fields.Add("outcome");
        if (old.On != wire.On) fields.Add("on");
        if (!Equals(old.Group, wire.Group)) fields.Add("group");
        return fields;
    }

    /// <summary>A step's fields that differ, each option by its name, and its title and words, which equality leaves out.</summary>
    private static List<string> StepFields(NodeDeclaration old, NodeDeclaration node)
    {
        var fields = new List<string>();
        void When(bool differs, string field)
        {
            if (differs) fields.Add(field);
        }

        When(old.Kind != node.Kind, "kind");
        When(old.Version != node.Version, "version");
        When(old.Reach != node.Reach, "reach");
        When(!Same(old.Inputs, node.Inputs), "inputs");
        When(!Same(old.Outputs, node.Outputs), "outputs");
        When(old.Evidence != node.Evidence, "evidence");
        When(!Equals(old.Retry, node.Retry), "retry");
        When(old.TimeoutSeconds != node.TimeoutSeconds, "timeoutSeconds");
        When(old.Rejectable != node.Rejectable, "rejectable");
        When(old.Skippable != node.Skippable, "skippable");
        When(!Equals(old.Perform, node.Perform), "perform");
        foreach (var name in old.Options.Keys.Union(node.Options.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            When(old.Options.GetValueOrDefault(name) != node.Options.GetValueOrDefault(name), $"options.{name}");
        }

        When(!old.Outcomes.SequenceEqual(node.Outcomes, StringComparer.Ordinal), "outcomes");
        When(old.OutputsOn.Count != node.OutputsOn.Count || old.OutputsOn.Any(each => !node.OutputsOn.TryGetValue(each.Key, out var on) || !on.SequenceEqual(each.Value, StringComparer.Ordinal)), "outputsOn");
        When(!Equals(old.Title, node.Title), "title");
        When(!Equals(old.About, node.About), "about");
        When(old.Synthetic != node.Synthetic, "synthetic");
        return fields;
    }

    private static bool Same(IReadOnlyDictionary<string, string> one, IReadOnlyDictionary<string, string> other) =>
        one.Count == other.Count && one.All(each => other.TryGetValue(each.Key, out var value) && value == each.Value);

    /// <summary>A way's kind, with the outcome or the <c>on</c> it is taken on.</summary>
    private static string Qualified(Wire wire) => wire.Kind switch
    {
        WireKinds.When when wire.Outcome is { } outcome => $"when `{outcome}`",
        WireKinds.Back when wire.On is { } on => $"back on `{on}`",
        _ => wire.Kind,
    };

    private static string End(WireEnd? end) => end is null ? "a wait" : $"`{end.Node}{(end.Port is { } port ? $".{port}" : "")}`";

    private static string Ends(Wire wire) => wire.Waits is { } waits && wire.From is null
        ? $"{(waits.End ? $"`{waits.Unit}`'s end" : $"`{waits.Unit}`'s `{waits.Node}`")} → {End(wire.To)}"
        : $"{End(wire.From)} → {End(wire.To)}";

    /// <summary>A changed way as the base has it: its id where only that moved, else how it ran, then its group where that changed.</summary>
    private static string WasSaid(Wire old, Wire wire, IReadOnlyList<string> fields)
    {
        var said = new List<string>();
        if (fields.Contains("id")) said.Add($"its id was `{old.Id}`");
        if (fields.Any(field => field is "kind" or "from" or "to" or "waits" or "outcome" or "on"))
        {
            said.Add($"it was {Ends(old)}{(fields.Any(field => field is "kind" or "outcome" or "on") ? $" ({Qualified(old)})" : "")}");
        }

        if (fields.Contains("group")) said.Add(old.Group is { } group ? $"its group was `{group.Id}` ({group.Mode})" : "it joined no group");
        return string.Join("; ", said);
    }

    private static string Names(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));
}
