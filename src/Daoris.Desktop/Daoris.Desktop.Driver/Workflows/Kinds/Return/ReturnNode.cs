namespace Daoris.Driver;

/// <summary>
/// A folder's return (design §4.5, <i>Bring back</i>): performed through its door once the work is accepted, which brings the copy its
/// session worked in back into the folder file by file (<see cref="FolderReturn"/>). Reached once every file is back, or where the
/// session worked in the folder itself with nothing to bring back; it waits <c>place-busy</c> while a turn runs in the folder, and
/// <c>both-changed</c> while a file changed on both sides waits for the person's choice, offered per path as its own acts.
/// </summary>
[NodeKind(NodeKinds.Return, NodeGroup.Others, NodeKinds.Apply)]
internal sealed class ReturnNode : BuiltInNode, IStarts, IPressed
{
    /// <summary>The wait while a session's turn runs in the folder.</summary>
    public const string PlaceBusy = "place-busy";

    /// <summary>The wait while a file changed on both sides waits for the person's choice.</summary>
    public const string BothChanged = "both-changed";

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

    /// <summary>The wait an active node's status names: its paths changed on both sides as <c>paths</c>, those refused as <c>refused</c>.</summary>
    public override WorkflowRunWait? WaitOf(NodeStatus status)
    {
        if (status.State != NodeStates.Active || status.Code is not (PlaceBusy or BothChanged)) return null;
        var lines = (status.Holds ?? "").Split('\n');
        var values = new Dictionary<string, string> { ["paths"] = string.Join("\n", lines.Where(line => !line.Contains('\t'))) };
        if (lines.Any(line => line.Contains('\t'))) values["refused"] = string.Join("\n", lines.Where(line => line.Contains('\t')));
        return new WorkflowRunWait(status.Code, values, status.Since);
    }

    /// <summary>The words a return waiting on the person holds: a line for each path, a refused one followed by a tab and its <see cref="ReturnRefusal"/> code.</summary>
    public static string HoldsOf(FolderReturned returned) =>
        string.Join("\n", returned.Waiting.Concat(returned.Refused.Select(each => $"{each.Key}\t{each.Value}")));

    public INodeDoor? Door(NodeDoorWiring wiring) => new ReturnDoor(wiring.Home, said => wiring.Say("return.run", said), wiring.Turning, wiring.MayStart);

    /// <summary>Begun in the pass; its door brings the copy back after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>A run's active <c>return</c>, handed to its door with the choices the person marked on its attempt.</summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IReturnDoor>(Row.Kind) is not { } door) return;
        var start = ReturnStart.Of(kept.Governed.Unit, kept.Node, kept.Status, kept.Record, kept.Authority);
        // Waiting on the person, the folders are read again only once they chose for a path it waits on, not at every pass.
        if (kept.Status.Code == BothChanged && !(kept.Status.Holds ?? "").Split('\n').Any(line => start.Choices.ContainsKey(line.Split('\t')[0]))) return;
        door.Start(start);
    }

    public IReadOnlyList<string> Takes => NodeWordActs.Choices;

    public string Refusal => RunNodeWords.NotReturn;

    /// <summary>A return waiting on a file changed on both sides offers the three choices, each of one path.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && status.Code == BothChanged ? NodeWordActs.Choices : [];

    /// <summary>A choice moves nothing: it is kept as a mark of the attempt, which the door reads.</summary>
    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) => null;

    /// <summary>The record's mark of the person's <paramref name="act"/> for <paramref name="path"/> on <paramref name="node"/>'s attempt.</summary>
    public static string ChoiceKey(string node, int attempt, string act, string path) => $"choice:{node}:{attempt}:{act}:{path}";

    /// <summary>The choices marked on <paramref name="node"/>'s attempt, by path, the newest for each.</summary>
    public static IReadOnlyDictionary<string, string> Choices(NodeRecord record, string node, int attempt)
    {
        var prefix = $"choice:{node}:{attempt}:";
        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mark in record.Fired.Where(mark => mark.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var rest = mark[prefix.Length..];
            var colon = rest.IndexOf(':');
            if (colon > 0 && NodeWordActs.Choices.Contains(rest[..colon], StringComparer.Ordinal)) chosen[rest[(colon + 1)..]] = rest[..colon];
        }

        return chosen;
    }
}
