namespace Daoris.Driver;

/// <summary>
/// A folder's return (design §4.5, <i>Bring back</i>): performed through its door once the work is accepted, which brings the copy its
/// session worked in back into the folder file by file (<see cref="FolderReturn"/>). Reached once every file is back, or where the
/// session worked in the folder itself with nothing to bring back; it waits <c>place-busy</c> while a turn runs in the folder, and
/// <c>both-changed</c> while a file changed on both sides waits for the person's choice, offered per path as its own acts.
/// </summary>
[NodeKind(NodeKinds.Return, NodeGroup.Others, NodeKinds.Apply)]
internal sealed class ReturnNode : BuiltInNode, IStarts, IPressed, IWritesWork
{
    /// <summary>The wait while a session's turn runs in the folder.</summary>
    public const string PlaceBusy = "place-busy";

    /// <summary>The wait while a file changed on both sides waits for the person's choice.</summary>
    public const string BothChanged = "both-changed";

    /// <summary>The failure of a return whose copy changed after its review read it: nothing is brought back until that work is reviewed.</summary>
    public const string Moved = "moved";

    /// <summary>Reached by Daoris's return alone; never the person's skip, which would read as brought back.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Return, NodeReach.Performed, NodeSource.Return, NodeSource.Return, false, PersonSkips: false, MayEnd: true, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override string NeverEndBecause => "it is reached once the copy is back.";

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Return);

    public override string? Title => "Bring back";

    public override NodeLook Look => NodeLook.Line;

    public override IReadOnlyList<string> Places { get; } = [PlaceKind.Folder];

    public override IReadOnlyList<string> Waits { get; } = [PlaceBusy, BothChanged];

    /// <summary>A graph with no <c>isolate</c> makes no copy, so its <c>return</c> has nothing to bring back.</summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        if (graph.Nodes.Any(each => each.Kind == NodeKinds.Isolate)) yield break;
        yield return new WireProblem(GraphRefusal.ReturnIsolate, null, node.Id,
            $"node `{node.Id}`: a `{NodeKinds.Return}` brings back the copy an `{NodeKinds.Isolate}` made, and this graph sets no work apart.");
    }

    /// <summary>The wait an active node's status names: <c>paths</c> changed on both sides, <c>refused</c> ones (path, tab, code) and the count <c>returned</c>.</summary>
    public override WorkflowRunWait? WaitOf(NodeStatus status)
    {
        if (status.State != NodeStates.Active || status.Code is not (PlaceBusy or BothChanged)) return null;
        if (status.Code == PlaceBusy) return new WorkflowRunWait(status.Code, new Dictionary<string, string> { ["paths"] = status.Holds ?? "" }, status.Since);
        var holds = ReturnHolds.Read(status.Holds);
        var values = new Dictionary<string, string>
        {
            ["paths"] = string.Join("\n", holds.Waiting.Keys.Where(path => !holds.Refused.ContainsKey(path))), ["returned"] = $"{holds.Returned}",
        };
        if (holds.Refused.Count > 0) values["refused"] = string.Join("\n", holds.Refused.Select(each => $"{each.Key}\t{each.Value}"));
        return new WorkflowRunWait(status.Code, values, status.Since);
    }

    public INodeDoor? Door(NodeDoorWiring wiring) => new ReturnDoor(wiring.Home, said => wiring.Say("return.run", said), wiring.Turning, wiring.MayStart);

    /// <summary>Begun in the pass; its door brings the copy back after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>A run's active <c>return</c>, handed to its door with the choices the person marked on its attempt.</summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IReturnDoor>(Row.Kind) is not { } door) return;
        var start = ReturnStart.Of(kept.Governed.Unit, kept.Node, kept.Status, kept.Record, kept.Authority) with { Reviewed = EffectRecord.Of(kept) };
        // Waiting on the person, the folders are read again only once they chose for a path it waits on, not at every pass.
        var holds = ReturnHolds.Read(kept.Status.Holds);
        if (kept.Status.Code == BothChanged && !holds.Waiting.Keys.Concat(holds.Refused.Keys).Any(start.Choices.ContainsKey)) return;
        door.Start(start);
    }

    public IReadOnlyList<string> Takes => NodeWordActs.Choices;

    public string Refusal => RunNodeWords.NotReturn;

    /// <summary>A return waiting on a file changed on both sides offers the three choices, each of one path.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && status.Code == BothChanged ? NodeWordActs.Choices : [];

    /// <summary>A choice moves nothing: it is kept as a mark of the attempt, which the door reads.</summary>
    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) => null;

    /// <summary>The record's mark of the person's <paramref name="act"/> for <paramref name="path"/>, bound to the versions shown (<c>act@copy/folder</c>, none <c>-</c>).</summary>
    public static string ChoiceKey(string node, int attempt, string act, string path, ReturnShown? shown = null) =>
        $"choice:{node}:{attempt}:{act}{(shown is null ? "" : $"@{shown.Copy ?? "-"}/{shown.Folder ?? "-"}")}:{path}";

    /// <summary>The choices marked on <paramref name="node"/>'s attempt, by path, the newest for each.</summary>
    public static IReadOnlyDictionary<string, string> Choices(NodeRecord record, string node, int attempt) =>
        Marked(record, node, attempt).ToDictionary(each => each.Key, each => each.Value.Act, StringComparer.Ordinal);

    /// <summary>The versions each newest choice on <paramref name="node"/>'s attempt names, by path.</summary>
    public static IReadOnlyDictionary<string, ReturnShown> Bound(NodeRecord record, string node, int attempt) =>
        Marked(record, node, attempt).Where(each => each.Value.Shown is not null).ToDictionary(each => each.Key, each => each.Value.Shown!, StringComparer.Ordinal);

    private static Dictionary<string, (string Act, ReturnShown? Shown)> Marked(NodeRecord record, string node, int attempt)
    {
        var prefix = $"choice:{node}:{attempt}:";
        var chosen = new Dictionary<string, (string, ReturnShown?)>(StringComparer.Ordinal);
        foreach (var mark in record.Fired.Where(mark => mark.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var rest = mark[prefix.Length..];
            var colon = rest.IndexOf(':');
            if (colon <= 0) continue;
            var (act, versions) = rest[..colon].Split('@', 2) is [var named, var at] ? (named, at.Split('/', 2)) : (rest[..colon], null);
            if (!NodeWordActs.Choices.Contains(act, StringComparer.Ordinal)) continue;
            chosen[rest[(colon + 1)..]] = (act, versions is [var copy, var folder] ? new ReturnShown(copy == "-" ? null : copy, folder == "-" ? null : folder) : null);
        }

        return chosen;
    }
}
