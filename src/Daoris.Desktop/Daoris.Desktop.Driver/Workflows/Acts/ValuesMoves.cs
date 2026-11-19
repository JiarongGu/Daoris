using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A repository's values moved into its delivery knowledge: where the newest version of a workflow it keeps values for
/// (<see cref="WorkflowValuesKey"/>) holds no node some of them are for, and holds a step naming a delivery knowledge entry
/// (<see cref="StepReceipts.Knowledge"/>), those values are proposed into that entry (<see cref="StepLessons"/>, its <see cref="StepLesson.Moved"/>),
/// carried by every hand-off of a step naming it to its holder, the repository's own agent, who writes them in its own change. They stay
/// where they are meanwhile; once the entry in the repository's checkout holds each value they are cleared there with that reason, save a
/// node's a run not ended still reads (one bound to a version holding it) and a node's set anew since. Values the new version still reads
/// (the approval node's) never move. Kept by the pass at the look's deadline alone, whatever door saved the version.
/// </summary>
public static class ValuesMoves
{
    /// <summary>The reason a moved node's values are cleared with, kept beside them (<see cref="WorkflowValuesKey.WhyKey"/>).</summary>
    public static string Reason(string entry) => $"moved into the delivery knowledge `{entry}`, which holds each value (D325)";

    /// <summary>
    /// The entry the values of <paramref name="dropped"/>, a node <paramref name="next"/> holds no more, move into: that of a step of the same role
    /// naming one, else the one entry <paramref name="next"/>'s steps name; null where they name none, or several and none of that role.
    /// </summary>
    public static string? EntryFor(NodeDeclaration? dropped, NodeGraph next)
    {
        var named = next.Nodes.Select(node => (Node: node, Entry: StepReceipts.Knowledge(node))).Where(each => each.Entry is not null).ToList();
        if (dropped is not null && named.FirstOrDefault(each => StepRoles.Of(each.Node) == StepRoles.Of(dropped)).Entry is { } same) return same;
        var entries = named.Select(each => each.Entry!).Distinct(StringComparer.Ordinal).ToList();
        return entries.Count == 1 ? entries[0] : null;
    }

    /// <summary><paramref name="nodes"/>' values that <paramref name="next"/> reads no more, by the entry each moves into; a node of no entry is left out.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ByEntry(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> nodes, Func<string, NodeDeclaration?> declared, NodeGraph next)
    {
        var held = next.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var grouped = new SortedDictionary<string, SortedDictionary<string, IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);
        foreach (var (node, options) in nodes.Where(pair => !held.Contains(pair.Key)))
        {
            if (EntryFor(declared(node), next) is not { } entry) continue;
            if (!grouped.TryGetValue(entry, out var of)) grouped[entry] = of = new(StringComparer.Ordinal);
            of[node] = options;
        }

        return grouped.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>)pair.Value, StringComparer.Ordinal);
    }

    /// <summary>The moves the newest of <paramref name="versions"/> of <paramref name="workflow"/> owes each repository's values, one per entry. Pure.</summary>
    public static IReadOnlyList<StepLesson> Of(DriverConfig config, string workflow, IReadOnlyList<NamedVersion> versions, DateTimeOffset at)
    {
        if (versions.Count == 0 || versions[^1].Graph is not { } next) return [];
        NodeDeclaration? Was(string node) => versions.Reverse().Select(version => version.Graph?.Nodes.FirstOrDefault(each => each.Id == node)).FirstOrDefault(each => each is not null);
        var moves = new List<StepLesson>();
        foreach (var (repository, values) in config.WorkflowValues.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!values.ByWorkflow.TryGetValue(workflow, out var nodes)) continue;
            foreach (var (entry, moved) in ByEntry(nodes, Was, next))
            {
                var carried = new MovedValues(workflow, versions[^1].Version, moved);
                moves.Add(new StepLesson(repository, entry, "", workflow, Words(repository, carried), at) { Moved = carried });
            }
        }

        return moves;
    }

    /// <summary>The proposal's words: each value, the nodes holding it, what the holder writes and what follows its landing.</summary>
    public static string Words(string repository, MovedValues moved)
    {
        var groups = moved.Values.GroupBy(pair => Said(pair.Value), StringComparer.Ordinal).Select(group => $"{group.Key} (for {Nodes(group.Select(pair => pair.Key))})");
        return $"`{moved.Workflow}` v{moved.Version} holds no step reading the values `{repository}` kept for {Nodes(moved.Values.Keys)}, so they move into this "
               + $"entry, each one: {string.Join("; ", groups)}. Write each where the entry names its acts' target; once the entry holds each in your "
               + "repository's checkout, Daoris clears them from `workflowValues`.";
    }

    /// <summary>Nodes named in the notes' way: <c>`a`, `b` and `c`</c>.</summary>
    public static string Nodes(IEnumerable<string> nodes)
    {
        var named = nodes.Order(StringComparer.Ordinal).Select(node => $"`{node}`").ToList();
        return named.Count <= 1 ? string.Concat(named) : $"{string.Join(", ", named[..^1])} and {named[^1]}";
    }

    private static string Said(IReadOnlyDictionary<string, string> options) =>
        string.Join(", ", options.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"`{pair.Key}` {pair.Value}"));

    /// <summary>Whether the entry <paramref name="move"/> names, in the checkout at <paramref name="root"/>, holds each value it moved, each as a word of its own.</summary>
    public static bool Holds(string root, StepLesson move)
    {
        if (move.Moved is not { } moved || root.Length == 0) return false;
        var path = Path.Combine(root, ".claude", "knowledge", move.Entry + ".md");
        string text;
        try
        {
            if (!File.Exists(path)) return false;
            text = AtomicFile.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return moved.Values.Values.SelectMany(options => options.Values).Select(Bare).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal)
            .All(value => Regex.IsMatch(text, $@"(?<![\w-]){Regex.Escape(value)}(?![\w-])", RegexOptions.CultureInvariant));
    }

    /// <summary>A value as an entry writes it: a string's words, anything else its JSON.</summary>
    private static string Bare(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.String ? document.RootElement.GetString()! : document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    /// <summary>
    /// <paramref name="config"/> with each node's values <paramref name="move"/> moved cleared with <see cref="Reason"/>, where they are still the
    /// moved ones and no run of <paramref name="runs"/> not ended is bound to a version holding the node, which keeps them and is said. Pure.
    /// </summary>
    public static DriverConfig Cleared(DriverConfig config, StepLesson move, IReadOnlyList<GraphRun> runs, List<string> said)
    {
        if (move.Moved is not { } moved) return config;
        foreach (var (node, options) in moved.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (WorkflowValuesKey.Of(config.WorkflowValues, move.Repository, moved.Workflow, node) is not { } current || !Same(current, options)) continue;
            var reading = runs.Where(run => run.Ended is null && string.Equals(run.Binding.Repository, move.Repository, StringComparison.OrdinalIgnoreCase)
                && run.Binding.Workflow == moved.Workflow && run.Version.Graph?.Nodes.Any(each => each.Id == node) == true).Select(run => $"#{run.Run}").ToList();
            if (reading.Count > 0)
            {
                said.Add($"values  `{move.Repository}`'s values for `{node}` of `{moved.Workflow}` stay while run {string.Join(", ", reading)} reads them");
                continue;
            }

            config = WorkflowValuesKey.Apply(config, move.Repository, moved.Workflow, node, null, Reason(move.Entry));
            said.Add($"values  `{move.Repository}`'s values for `{node}` of `{moved.Workflow}` cleared: {Reason(move.Entry)}");
        }

        return config;
    }

    private static bool Same(IReadOnlyDictionary<string, string> one, IReadOnlyDictionary<string, string> other) =>
        one.Count == other.Count && one.All(pair => other.TryGetValue(pair.Key, out var value) && value == pair.Value);

    /// <summary>Whether every node <paramref name="move"/> moves is moved already, with the same values, by one of <paramref name="kept"/> into the same entry.</summary>
    private static bool Covered(IReadOnlyList<StepLesson> kept, StepLesson move) =>
        move.Moved!.Values.All(pair => kept.Any(each => each.Moved is { } other && other.Workflow == move.Moved.Workflow && each.Entry == move.Entry
            && string.Equals(each.Repository, move.Repository, StringComparison.OrdinalIgnoreCase)
            && other.Values.TryGetValue(pair.Key, out var options) && Same(pair.Value, options)));

    /// <summary>
    /// The pass's keeping at the look's deadline: each move the workflows' newest versions owe proposed once (one still open for the same
    /// entry and workflow replaced), each open one marked landed once its entry holds its values, and a landed one's values cleared in the
    /// driver's file as <see cref="Cleared"/> says. The lines it says.
    /// </summary>
    /// <param name="roots">Each repository's checkout, where its entry is read.</param>
    /// <param name="runs">Every run bound to a graph version here, ended or not.</param>
    public static IReadOnlyList<string> Keep(string home, DriverConfig config, IReadOnlyDictionary<string, string> roots, IReadOnlyList<GraphRun> runs, DateTimeOffset now)
    {
        if (config.WorkflowValues.Count == 0) return [];
        var kept = StepLessons.Read(home).ToList();
        var said = new List<string>();
        var proposed = false;
        var store = WorkflowPorts.Store(home);
        foreach (var workflow in config.WorkflowValues.Values.SelectMany(values => values.ByWorkflow.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (store.Load(workflow)?.Read is not { Problem: null, Versions.Count: > 0 } read) continue;
            foreach (var move in Of(config, workflow, read.Versions, now).Where(move => !Covered(kept, move)))
            {
                kept.RemoveAll(each => each is { Moved: { } open, Landed: null } && open.Workflow == workflow && each.Entry == move.Entry
                    && string.Equals(each.Repository, move.Repository, StringComparison.OrdinalIgnoreCase));
                kept.Add(move);
                proposed = true;
                said.Add($"values  `{move.Repository}`'s values for {Nodes(move.Moved!.Values.Keys)} of `{workflow}` proposed into its delivery knowledge `{move.Entry}`");
            }
        }

        var path = DaorisHome.Entry(home, HomeEntry.DriverConfig);
        DriverConfig? disk = null;
        var cleared = false;
        for (var at = 0; at < kept.Count; at++)
        {
            if (kept[at] is not { Moved: { } moved } move) continue;
            var landed = false;
            if (move.Landed is null)
            {
                var root = roots.FirstOrDefault(pair => string.Equals(pair.Key, move.Repository, StringComparison.OrdinalIgnoreCase)).Value ?? "";
                if (!Holds(root, move)) continue;
                kept[at] = move = move with { Landed = now };
                proposed = landed = true;
                said.Add($"values  `{move.Repository}`'s delivery knowledge `{move.Entry}` holds each value moved into it");
            }

            if (!moved.Values.Any(pair => WorkflowValuesKey.Of(config.WorkflowValues, move.Repository, moved.Workflow, pair.Key) is { } current && Same(current, pair.Value))) continue;
            try
            {
                var before = disk ??= DriverConfig.Load(path);
                var lines = new List<string>();
                disk = Cleared(before, move, runs, lines);
                cleared |= !ReferenceEquals(before, disk);
                said.AddRange(landed ? lines : lines.Where(line => line.EndsWith(Reason(move.Entry), StringComparison.Ordinal)));
            }
            catch (DriverException ex)
            {
                said.Add($"values  the driver's file was not read, so moved values are cleared at a later look: {ex.Message}");
                break;
            }
        }

        // A failed write leaves both files as they stand: the next look proposes, marks and clears from them again.
        try
        {
            if (proposed) StepLessons.Write(home, kept);
            if (cleared) disk!.Save(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DriverException)
        {
            said.Add($"values  the moved values were not kept this look, and are kept at a later one: {ex.Message}");
        }

        return said;
    }
}
