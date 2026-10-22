using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A graph node's option read as a step's field (FABRIC-CUTOVER1e): a node keeps each option as its JSON text, and a step reads text, a
/// switch or a list of names, so the person's part (<see cref="Involvement.StepsOf(NodeGraph)"/>) and a graph run's drawing read an option
/// as its step would.
/// </summary>
public static class NodeOptions
{
    /// <summary>An option's text read as a field: text, a switch, a list of names, null; anything else (a number, an object) its JSON text.</summary>
    public static object? Value(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var value = document.RootElement;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array when value.EnumerateArray().All(each => each.ValueKind == JsonValueKind.String) =>
                    (IReadOnlyList<string>)[.. value.EnumerateArray().Select(each => each.GetString()!)],
                _ => value.GetRawText(),
            };
        }
        catch (JsonException)
        {
            return text;
        }
    }

    /// <summary>A node's option as text, or null where it names none or names no text.</summary>
    public static string? Text(NodeDeclaration node, string name) =>
        node.Options.TryGetValue(name, out var text) ? Value(text) as string : null;
}

/// <summary>
/// Which row a graph run's node stands at (FABRIC-CUTOVER1e; D190 points 2 and 3, the cutover design §2, §3): its state on the run's record
/// by the one lifecycle (<see cref="NodeStates"/>), and one more. Read with <see cref="WorkflowRunStep.Node"/> set, never with a step's kind.
/// </summary>
public static class WorkflowGraphDetails
{
    public const string Blocked = NodeStates.Blocked;
    public const string Ready = NodeStates.Ready;

    /// <summary>
    /// A second opinion, a look or a plugin's performed step its wires let start, which this build does not begin on a graph run: the runtime
    /// begins them once the landing doors read the run's record (FABRIC-CUTOVER1c's guards, CUTOVER1d, which drops this detail).
    /// </summary>
    public const string NotBegun = "not-begun";

    public const string Active = NodeStates.Active;
    public const string Reached = NodeStates.Reached;
    public const string Failed = NodeStates.Failed;
    public const string Rejected = NodeStates.Rejected;
    public const string Unknown = NodeStates.Unknown;
    public const string Skipped = NodeStates.Skipped;
    public const string Stopped = NodeStates.Stopped;
    public const string Invalidated = NodeStates.Invalidated;

    public static IReadOnlyList<string> All { get; } =
        [Blocked, Ready, NotBegun, Active, Reached, Failed, Rejected, Unknown, Skipped, Stopped, Invalidated];
}

/// <summary>
/// What a graph run's node is said to observe (FABRIC-CUTOVER1e; the cutover design §2.2's table, <see cref="RunSubjects"/>): its newest work
/// quest here, its branch at a tip, or that branch at its landing's tip. The run itself is no subject named.
/// </summary>
public static class WorkflowGraphSubjects
{
    public const string Quest = "quest";
    public const string Branch = "branch";
    public const string Landing = "landing";
}

/// <summary>One wire of a graph run's graph, as the page draws it: from a node to a node, by its kind (<c>after</c>, <c>failed</c>, …).</summary>
public sealed record WorkflowGraphWire(string From, string To, string Kind);

/// <summary>
/// A run bound to a graph version, drawn from its record (FABRIC-CUTOVER1e; D190 points 2, 3 and 7, the cutover design §2, §3, §6): its
/// graph's entries, end and wires, the subjects its nodes observe, and what its record says of it. Its nodes are the run's steps
/// (<see cref="WorkflowRun.Steps"/>), each with <see cref="WorkflowRunStep.Node"/>.
/// </summary>
/// <param name="Run">The run's id: its chain's first quest in its repository, whose record is <c>run:&lt;run&gt;</c>.</param>
public sealed record WorkflowGraphRun(string Run, IReadOnlyList<string> Entries, string End, IReadOnlyList<WorkflowGraphWire> Wires)
{
    /// <summary><c>claimed</c>'s subject: its newest work quest here; null where none was read.</summary>
    public string? Quest { get; init; }

    /// <summary>The branch its newest session here worked, the one its gates, its landing and a plugin's node before it observe; null where none.</summary>
    public string? Branch { get; init; }

    /// <summary>Whether Daoris has judged its graph: its record holds a node's status.</summary>
    public bool Judged { get; init; }

    /// <summary>Its record does not read, so nothing on it is judged or started until a person mends it.</summary>
    public bool Unread { get; init; }

    /// <summary>The quest it ended at, where its record keeps its end (FABRIC-CUTOVER1c); null while it goes on.</summary>
    public string? Ended { get; init; }

    /// <summary>
    /// Where it stands: the first step that is neither not reached nor settled (live, or failed, refused or stopped there); with none, nowhere
    /// where it ended or its graph's end is settled, its first entry where Daoris has not judged it, else its first step not settled.
    /// </summary>
    public string? At { get; init; }
}

/// <summary>What a run bound to a graph version is drawn from (FABRIC-CUTOVER1e): the run's records as a steps run's, its graph and its record.</summary>
/// <param name="Run">The records a steps run is read from: its quests here, its sessions, its review's gate, its landings, its process.</param>
/// <param name="RunId">The run: its chain's first quest in its repository.</param>
/// <param name="Graph">The graph version it is bound to, as its file holds it (<see cref="WorkflowProcess.Graph"/>).</param>
/// <param name="Record">Its node record, <c>run:&lt;run&gt;</c>: empty where Daoris has not judged it yet, null where it does not read.</param>
public sealed record WorkflowGraphRunFacts(WorkflowRunFacts Run, string RunId, NodeGraph Graph, NodeRecord? Record)
{
    /// <summary>This machine's session branches (<see cref="SessionBranches"/>), which name a branch unit its nodes observe by its branch.</summary>
    public IReadOnlyList<GrownBranch> Branches { get; init; } = [];
}

/// <summary>
/// A run bound to a graph version, drawn from its record (FABRIC-CUTOVER1e; D190 points 2, 3 and 7, the cutover design §2.2, §2.3, §6, §11.1).
/// Pure: its reader gathers the records, and this reads them alone, so <c>WorkflowGraphRunTests</c> holds each row without a process.
/// </summary>
/// <remarks>
/// <para><b>Each node is a step</b>, in its graph's order, its state its record's status read by one table (<see cref="StateOf"/>): blocked is
/// not reached, a reach done, a skip skipped, a failure failed, the person's refusal and stop stopped, unknown not known; an active node the
/// person answers (a go-ahead, a person's task, the look) waits on them, any other works. Its subject is the one its binding names, the
/// cutover design §2.2's (<see cref="RunSubjects"/>), named: a quest by its id, a branch by its name as this machine's records hold it, a
/// landing by the landed branch at its tip.</para>
/// <para><b>Not begun until CUTOVER1d</b>: the runtime begins no second opinion and no look and starts no plugin's performed node on a graph
/// run (FABRIC-CUTOVER1c), so one its wires let start is said not begun, never as waiting on an agent or on the person.</para>
/// <para><b>A look the work's choice adds</b> (D190 point 7, §6) is D154's hold at every landing door, drawn beside the graph as a steps run
/// draws one (<see cref="WorkflowRuns.AddedLook"/>), never as one of its nodes, and only where the graph holds no look.</para>
/// </remarks>
public static class WorkflowGraphRuns
{
    /// <summary>The run of <paramref name="facts"/>: its graph's nodes, each where its record says it stands, and a look its work added.</summary>
    public static WorkflowRun Derive(WorkflowGraphRunFacts facts, string workspace, string? ask = null)
    {
        var run = facts.Run;
        var graph = facts.Graph;
        var self = RunUnits.IdOf(facts.RunId);
        var statuses = facts.Record?.Statuses ?? new Dictionary<string, NodeStatus>();
        var afterHanded = RunSubjects.AfterHanded(graph);

        var work = run.Quests.Where(quest => quest.SetUpIn is null).ToList();
        var workIds = new HashSet<string>(work.Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        var sessions = run.Sessions
            .Where(session => session.Quest is { } quest && workIds.Contains(quest))
            .OrderBy(session => session.Created ?? DateTimeOffset.MinValue)
            .ToList();
        var newestHere = sessions.LastOrDefault(session => !session.Teammate);
        var handed = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Handed);
        NodeStatus StatusOf(string node) => statuses.GetValueOrDefault(node) ?? NodeLifecycle.Start(self, node);

        // The run's branch: the one its newest session here worked, as its tree is named and this machine's records follow it (TREEFOLLOW1);
        // else the one its record last bound its landing to.
        var runBranch = newestHere?.Tree is { Length: > 0 } tree ? BranchOfTree(tree, facts.Branches) : null;
        runBranch ??= handed is not null && Bound(StatusOf(handed.Id)) is { } landingBound && landingBound.Observes != self
            ? BranchName(landingBound.Observes, facts.Branches)
            : null;

        var cells = graph.Nodes.Select(node => Cell(node, run.Process)).ToList();
        var steps = new List<WorkflowRunStep>();
        // FABRIC-CUTOVER1f: a run that ended, or whose record does not read, takes no word; a node holding one waiting offers nothing more.
        var open = facts.Record is { Ended: null };
        foreach (var (node, cell) in graph.Nodes.Zip(cells))
        {
            var status = StatusOf(node.Id);
            var (state, detail) = StateOf(node, status.State);
            var asked = facts.Record?.Pending.LastOrDefault(word => word.Node == node.Id);
            var acts = open && asked is null ? RunNodeWords.Acts(node, status) : [];
            var step = new WorkflowRunStep(cell, state, detail)
            {
                Node = node.Kind,
                Code = status.State == NodeStates.Skipped ? status.By : status.Code,
                Words = status.State is NodeStates.Skipped or NodeStates.Rejected or NodeStates.Stopped ? status.Words
                    : node.Kind == NodeKinds.GoAhead ? NodeOptions.Text(node, "act") : null,
                Acts = acts.Count > 0 ? acts : null,
                Asked = asked,
            };
            steps.Add(Observing(step, node, Bound(status), RunSubjects.Of(node, afterHanded)));
        }

        // §6: a look the chain's, the ask's or a set-up step's choice adds stays D154's hold, drawn beside the graph, before its landing.
        if (!graph.Nodes.Any(node => node.Kind == NodeKinds.Look))
        {
            var claimed = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Claimed);
            var workDone = claimed is not null
                ? WorkflowRunStates.Settled(StateOf(claimed, StatusOf(claimed.Id).State).State)
                : work.Count > 0 && work.All(quest => quest.Status == "Done");
            var landed = handed is not null && StatusOf(handed.Id).State == NodeStates.Reached;
            var current = new CurrentWorkflow(run.Current.StartHolds, cells, run.Current.Version);
            if (WorkflowRuns.AddedLook(run with { Current = current }, workDone, landed) is { } added)
            {
                var at = handed is null ? -1 : steps.FindIndex(step => step.Step.Id == handed.Id);
                steps.Insert(at < 0 ? steps.Count : at, added);
            }
        }

        var record = facts.Record;
        var part = new WorkflowGraphRun(
            facts.RunId, graph.Entries, graph.End,
            [.. graph.Wires.Where(wire => wire.From is { Unit: null } && wire.To.Unit is null).Select(wire => new WorkflowGraphWire(wire.From!.Node, wire.To.Node, wire.Kind))])
        {
            Quest = work.LastOrDefault()?.Id,
            Branch = runBranch,
            Judged = record is { Statuses.Count: > 0 },
            Unread = record is null,
            Ended = record?.Ended?.Quest,
        };

        return new WorkflowRun(run.Repository, workspace, new CurrentWorkflow(run.Current.StartHolds, cells, run.Current.Version),
            [.. run.Quests.Select(quest => quest.Id)], steps)
        {
            Process = run.Process,
            Ask = ask,
            Session = run.Sessions.MaxBy(session => session.Created ?? DateTimeOffset.MinValue)?.Id,
            Graph = part with { At = At(part, steps, StatusOf(graph.End).State) },
        };

        // A node's subject named on its step: the one its binding names, else the one it will observe this look.
        WorkflowRunStep Observing(WorkflowRunStep step, NodeDeclaration node, NodeAcceptance? bound, RunSubject subject)
        {
            var observes = bound?.Observes is { } unit && unit != self ? unit : null;
            switch (subject)
            {
                case RunSubject.Quest:
                    var quest = observes is not null && observes.StartsWith("quest:", StringComparison.Ordinal) ? observes["quest:".Length..] : work.LastOrDefault()?.Id;
                    return quest is null ? step : step with { On = WorkflowGraphSubjects.Quest, Quest = quest };
                case RunSubject.Branch:
                    var branch = observes is null ? runBranch : BranchName(observes, facts.Branches);
                    return branch is null ? step : step with
                    {
                        On = WorkflowGraphSubjects.Branch, Branch = branch, Commit = observes is null ? null : bound!.SubjectRevision, Session = newestHere?.Id,
                    };
                case RunSubject.Landing:
                    // What follows `handed` binds the landing it reached on, at that landing's tip, as its record keeps it (§2.2).
                    var reached = handed is null ? null : StatusOf(handed.Id) is { State: NodeStates.Reached, Acceptance: { } acceptance } ? acceptance : null;
                    var on = observes ?? (reached is { Subject: { } landedOn } ? landedOn : null);
                    var tip = observes is not null ? bound!.SubjectRevision : reached?.Evidence?.Revision;
                    if (on is null) return step;
                    var landing = run.Landings.FirstOrDefault(entry => entry.Units.Contains(on, StringComparer.Ordinal) && (tip is null || entry.Tip == tip));
                    return step with { On = WorkflowGraphSubjects.Landing, Branch = landing?.Branch ?? BranchName(on, facts.Branches), Commit = tip };
                default:
                    return step;
            }
        }
    }

    /// <summary>
    /// What the run's view says of one node standing at <paramref name="status"/>, its subject unnamed (FABRIC-CUTOVER1d): the words a landing door
    /// holds with where <c>handed</c> waits on it (<see cref="GraphGate"/>). Not reached says so.
    /// </summary>
    public static string Said(NodeDeclaration node, NodeStatus status)
    {
        var (state, detail) = StateOf(node, status.State);
        var step = new WorkflowRunStep(Cell(node, null), state, detail)
        {
            Node = node.Kind,
            Code = status.State == NodeStates.Skipped ? status.By : status.Code,
            Words = status.State is NodeStates.Skipped or NodeStates.Rejected or NodeStates.Stopped ? status.Words
                : node.Kind == NodeKinds.GoAhead ? NodeOptions.Text(node, "act") : null,
        };
        return WorkflowRunWords.Said(step) ?? "Not reached yet.";
    }

    /// <summary>
    /// A node's state on the record as a run's step says it, and its detail (<see cref="WorkflowGraphDetails"/>): the one table the drawing
    /// reads, held by <c>WorkflowGraphRunTests</c>. A state this build does not know is said as recorded.
    /// </summary>
    public static (string State, string Detail) StateOf(NodeDeclaration node, string state) => state switch
    {
        NodeStates.Blocked => (WorkflowRunStates.NotReached, WorkflowGraphDetails.Blocked),
        // FABRIC-CUTOVER1c's guards (CUTOVER1d drops them, and this row): a run's opinion and look are not begun and its performed node not
        // started until its landing doors read its record.
        NodeStates.Ready when node.Kind is NodeKinds.Opinion or NodeKinds.Look || (node.Reach == NodeReach.Performed && NodeKinds.IsPlugin(node.Kind)) =>
            (WorkflowRunStates.CannotStart, WorkflowGraphDetails.NotBegun),
        NodeStates.Ready => (WorkflowRunStates.Working, WorkflowGraphDetails.Ready),
        NodeStates.Active when node.Kind is NodeKinds.GoAhead or NodeKinds.Human or NodeKinds.Look =>
            (WorkflowRunStates.WaitingOnYou, WorkflowGraphDetails.Active),
        NodeStates.Active => (WorkflowRunStates.Working, WorkflowGraphDetails.Active),
        NodeStates.Reached => (WorkflowRunStates.Done, WorkflowGraphDetails.Reached),
        NodeStates.Skipped => (WorkflowRunStates.Skipped, WorkflowGraphDetails.Skipped),
        NodeStates.Failed => (WorkflowRunStates.Failed, WorkflowGraphDetails.Failed),
        NodeStates.Rejected => (WorkflowRunStates.Stopped, WorkflowGraphDetails.Rejected),
        NodeStates.Stopped => (WorkflowRunStates.Stopped, WorkflowGraphDetails.Stopped),
        NodeStates.Unknown => (WorkflowRunStates.NotKnown, WorkflowGraphDetails.Unknown),
        NodeStates.Invalidated => (WorkflowRunStates.Working, WorkflowGraphDetails.Invalidated),
        _ => (state, state),
    };

    /// <summary>
    /// A node as a step's cell (FABRIC-CUTOVER1e), so the page and the terminal draw it as they draw a step: a built-in with a step kind as that
    /// kind (<c>claimed</c> the work, <c>handed</c> the landing), any other as its own kind; the person's part, who acts and their press by its
    /// kind; its options as its settings, the landing's form and press where the graph leaves them to the repository's rule, as that rule says
    /// them.
    /// </summary>
    private static WorkflowStep Cell(NodeDeclaration node, WorkflowProcess? process)
    {
        var settings = node.Options.Select(option => new WorkflowSetting(option.Key, NodeOptions.Value(option.Value))).ToList();
        void Default(string name, object? value)
        {
            if (value is not null && settings.All(setting => setting.Name != name)) settings.Add(new WorkflowSetting(name, value));
        }

        var rule = process?.Landing.Rule;
        if (node.Kind == NodeKinds.Handed)
        {
            Default("form", rule?.Form);
            Default("accept", rule is null ? null : rule.AutoAccept ? "automatic" : "you");
        }

        if (node.Kind == NodeKinds.PullRequest) Default("plugin", rule?.Plugin);
        if (NodeKinds.IsPlugin(node.Kind)) Default("plugin", node.Kind[..node.Kind.IndexOf('/')]);

        var automatic = settings.FirstOrDefault(setting => setting.Name == "accept")?.Value is "automatic";
        var (kind, participation, executor, press) = node.Kind switch
        {
            NodeKinds.Claimed => (WorkflowKinds.Work, WorkflowParticipation.Agent, WorkflowExecutor.Agent, (string?)null),
            NodeKinds.Opinion => (WorkflowKinds.Opinion, WorkflowParticipation.Agent, WorkflowExecutor.Agent, null),
            NodeKinds.Look => (WorkflowKinds.Look, WorkflowParticipation.AgentAndYou, WorkflowExecutor.Agent, WorkflowPress.Reviewed),
            NodeKinds.Handed => (WorkflowKinds.Landing, automatic ? WorkflowParticipation.Automatic : WorkflowParticipation.You, WorkflowExecutor.Daoris,
                automatic ? null : WorkflowPress.Accept),
            NodeKinds.PullRequest => (WorkflowKinds.PullRequest, WorkflowParticipation.You, WorkflowExecutor.Plugin, WorkflowPress.Merge),
            NodeKinds.GoAhead => (WorkflowKinds.GoAhead, WorkflowParticipation.You, WorkflowExecutor.Daoris, null),
            NodeKinds.Human => (NodeKinds.Human, WorkflowParticipation.You, WorkflowExecutor.Daoris, null),
            _ when NodeKinds.IsPlugin(node.Kind) => (node.Kind, WorkflowParticipation.Automatic, WorkflowExecutor.Plugin, null),
            _ => (node.Kind, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris, null),
        };
        return new WorkflowStep(node.Id, kind, participation, executor, press,
            new WorkflowSource("workflow", process?.Binding?.Level ?? WorkflowLevels.Repository), settings, WorkflowRuntime.Built, null);
    }

    /// <summary>What a node's status is bound to: its reach's acceptance where it is reached, else its attempt's binding, kept as history.</summary>
    private static NodeAcceptance? Bound(NodeStatus status) =>
        status.State == NodeStates.Reached ? status.Acceptance ?? status.Binding : status.Binding ?? status.Acceptance;

    /// <summary>A branch unit by its branch's name, as this machine's session branches record it; else its id's own spelling; else its id.</summary>
    private static string BranchName(string unit, IReadOnlyList<GrownBranch> branches)
    {
        if (branches.LastOrDefault(entry => entry.Unit == unit) is { } entry) return entry.Now ?? entry.Branch;
        const string Prefix = "branch:";
        if (unit.StartsWith(Prefix, StringComparison.Ordinal) && unit.IndexOf('/', Prefix.Length) is var slash and > 0) return unit[(slash + 1)..];
        return unit;
    }

    /// <summary>A session's branch by its tree's name (the inventory's join, <c>WorkInventory.AttachTrees</c>), where its agent moved it since.</summary>
    private static string BranchOfTree(string tree, IReadOnlyList<GrownBranch> branches)
    {
        var name = TreeFollows.SessionPrefix + Path.GetFileName(Path.TrimEndingDirectorySeparator(tree));
        return branches.LastOrDefault(entry => entry.Branch == name)?.Now ?? name;
    }

    /// <summary>Where a graph run stands (<see cref="WorkflowGraphRun.At"/>).</summary>
    private static string? At(WorkflowGraphRun part, IReadOnlyList<WorkflowRunStep> steps, string end)
    {
        if (part.Ended is not null) return null;
        if (steps.FirstOrDefault(step => step.State != WorkflowRunStates.NotReached && !WorkflowRunStates.Settled(step.State)) is { } moving) return moving.Step.Id;
        if (end is NodeStates.Reached or NodeStates.Skipped) return null;
        if (!part.Judged) return part.Entries.FirstOrDefault();
        return steps.FirstOrDefault(step => !WorkflowRunStates.Settled(step.State))?.Step.Id;
    }
}
