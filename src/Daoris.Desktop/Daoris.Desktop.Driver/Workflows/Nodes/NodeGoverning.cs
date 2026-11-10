namespace Daoris.Driver;

/// <summary>
/// Which units a graph governs and how: each governed unit with its graph, run and declarations, a run bound to a graph version on its own
/// record, and what a fresh read says of the quests whose closure graph the choices do not hold. Pure: it reads what it is handed.
/// </summary>
internal static class NodeGoverning
{
    /// <summary>
    /// The units a graph governs, in id order, each with its graph, run and declarations. A plugin's performed node is evaluated and started
    /// under its plugin's step (<paramref name="steps"/>): that step's <c>perform</c>, and its retry, timeout and evidence where the graph's
    /// node declares none. A node whose plugin declares no such step at the graph's version and reach is never sent.
    /// </summary>
    /// <param name="kept">The closure graph this home keeps under a digest (<see cref="ClosureGraphs.Read"/>), or null where it keeps none.</param>
    /// <param name="versionOf">
    /// The named version a quest unit's run is bound to (<see cref="NodeRuntimeLook.Bound"/>), or null where none: a quest bound to one is
    /// governed by it alone, a steps version's by its own run, a graph version's on the run's own record (<paramref name="runs"/>), so Current's
    /// graph never governs it beside either. An unreadable binding holds it the same way: its run cannot start.
    /// </param>
    /// <param name="runs">
    /// The runs bound to graph versions: each is governed on its own record, <c>run:&lt;run&gt;</c>, and Current's graphs govern none of its
    /// quests and none of the branches they produced; a branch two runs produced, one of them a graph run, is held; and while the work's
    /// attribution is not read (<see cref="NodeUnitsRead.Unread"/>), no branch of a repository holding an open graph run is governed.
    /// </param>
    /// <param name="said">What the pass says of them: a branch held, an attribution not read, a version that does not read.</param>
    public static IReadOnlyList<GovernedUnit> Govern(
        DriverConfig config, NodeUnitsRead read, IReadOnlyList<NodeDeclaration> steps, Func<string, NodeGraph?>? kept = null,
        Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?>? versionOf = null, IReadOnlyList<GraphRun>? runs = null,
        ICollection<string>? said = null)
    {
        var governed = new List<GovernedUnit>();
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        var quests = Quests(read);
        var owners = new RunOwners(quests, runs ?? [], read.Unread.Count == 0);

        foreach (var each in read.Units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            var unit = each;
            var repository = unit.Owner.Repository ?? "";
            // Only a run's own record and a closure graph a quest is bound to govern a unit: no graph is drawn from the choices.
            NodeGraph? graph = null;

            // A closure graph governs a quest bound to it at its take, and only that quest; any other keeps the plain done. It is the graph the
            // quest is bound to, kept by this home.
            var closes = false;
            if (unit.Kind == WorkUnitKind.Quest && unit.Records.Quest is { Closure: { Length: > 0 } bound } quest)
            {
                if (kept?.Invoke(bound) is { } bin)
                {
                    graph = bin;
                    closes = true;
                }

                // Its revision is its chain's and its branches' (a commit or an ancestor's move lapses it), from the branches' tips as git
                // answered this read, never their records' kept revisions.
                if (closes) unit = unit with { Subject = unit.Subject with { Revision = QuestClosing.Revision(quest, quests, read.Units, read.Tips) } };
            }

            // A quest whose run is bound to a named version is that version's, run by its own engine: Current's graph does not govern it as
            // well. A quest bound to a closure graph at its take is that graph's, as above.
            if (!closes && unit.Kind == WorkUnitKind.Quest && versionOf?.Invoke(unit, quests.Values) is not null) continue;

            // A quest of a run bound to a graph version is that run's, read from the home's own runs, and a branch its quests produced is its
            // graph's, each run's where graph runs alone produced it; one Current's work shares with a graph run's is held, said; and one whose attribution is not read, in a repository holding
            // an open graph run, is governed by nobody, never handed to Current on a guess.
            if (!closes && unit.Records.Quest is { } own && owners.Of(own) is not null) continue;
            if (owners.Keeps(unit) is { } keeps)
            {
                if (keeps == RunOwners.Held) said?.Add(owners.HeldSaid(unit));
                continue;
            }

            if (graph is null || (!Acts(graph) && !(unit.Kind == WorkUnitKind.Branch && Integrates(graph)) && !closes)) continue;
            var (declared, dispatchable, observable) = Declare(graph, steps);

            // The inventory gives an unproven landed branch no revision (only a proven end carries a tip), and a graph is judged on one, so a
            // branch whose graph holds `integrate` is judged on its standing landing's recorded tip, the one `handed` binds.
            if (unit.Kind == WorkUnitKind.Branch && unit.Subject.Revision is null && Integrates(graph)
                && unit.Records.Landings.LastOrDefault(landing => landing.GoneAt is null) is { } standing)
            {
                unit = unit with { Subject = unit.Subject with { Revision = standing.Tip } };
            }

            // The host's half of the frame, the unit it is told for and its repository's identity, read once per checkout.
            var digest = NodeGraphs.Digest(graph);
            var root = read.Roots.GetValueOrDefault(repository) ?? "";
            var identity = IdentityOf(identities, root);
            var watched = new WatchedUnit(repository, unit.Owner.Workspace, root,
                unit.Kind == WorkUnitKind.Branch ? unit.Subject.Artifact ?? "" : "", unit.Subject.Revision ?? "", null, null, [])
            {
                Unit = unit.Id, Identity = identity,
            };
            governed.Add(new GovernedUnit(unit, graph, digest, NodeRuns.Of(unit.Id), declared, dispatchable, watched)
            {
                Closes = closes, Observable = observable, Places = NodeGraphs.Places(graph),
            });
        }

        foreach (var repository in owners.Unattributed.Order(StringComparer.Ordinal))
        {
            said?.Add($"work  `{repository}`: the work's attribution was not read ({string.Join("; ", read.Unread)}), so none of its branches is governed "
                      + "while a run bound to a graph version is open there, and every binding on that run's record is kept.");
        }

        var reads = new RunReads(read, owners);
        foreach (var run in runs ?? [])
        {
            if (Governs(run, read, reads, owners, steps, identities, said) is { } one) governed.Add(one);
        }

        return governed;
    }

    /// <summary>
    /// A graph's nodes as the runtime evaluates and starts them: a plugin's performed or observed node under its plugin's step, where that step is
    /// declared at the node's version and reach, else as the graph declares it and never sent or begun.
    /// </summary>
    private static (Dictionary<string, NodeDeclaration> Declared, HashSet<string> Dispatchable, HashSet<string> Observable) Declare(
        NodeGraph graph, IReadOnlyList<NodeDeclaration> steps)
    {
        var dispatchable = new HashSet<string>(StringComparer.Ordinal);
        var observable = new HashSet<string>(StringComparer.Ordinal);
        var declared = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            declared[node.Id] = node;

            // A kind started on a port through the attempt journal is given its kind's perform once judged, as a plugin's node its step's; its
            // port is Daoris's own.
            if (NodeKit.Of(node.Kind) is IJournalled journalled && node.Reach == NodeReach.Performed)
            {
                declared[node.Id] = journalled.Performed(node);
                dispatchable.Add(node.Id);
                continue;
            }

            if (!(Performs(node) || Observes(node)) || steps.FirstOrDefault(step => step.Kind == node.Kind) is not { } step
                || step.Version != node.Version || step.Reach != node.Reach)
            {
                continue;
            }

            var under = node with
            {
                Perform = step.Perform, Retry = node.Retry ?? step.Retry, TimeoutSeconds = node.TimeoutSeconds ?? step.TimeoutSeconds,
                Evidence = node.Evidence ?? step.Evidence,
            };
            if (NodeDeclarations.Problem(under) is not null) continue;
            declared[node.Id] = under;
            (Performs(node) ? dispatchable : observable).Add(node.Id);
        }

        return (declared, dispatchable, observable);
    }

    /// <summary>A checkout's repository identity, read once per checkout for a whole reading.</summary>
    private static string IdentityOf(Dictionary<string, string> identities, string root)
    {
        if (!identities.TryGetValue(root, out var identity)) identities[root] = identity = RepositoryIdentity.Of(root);
        return identity;
    }

    /// <summary>What every run of a read is judged from, indexed once: asked of each run, a scan of the units for it grew with the square of them.</summary>
    private sealed class RunReads
    {
        public RunReads(NodeUnitsRead read, RunOwners owners)
        {
            Units = read.Units.GroupBy(unit => unit.Id, StringComparer.Ordinal).ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);
            Quests = read.Units.Select(unit => unit.Records.Quest).OfType<QuestView>().ToList();
            Chains = new QuestChains(Quests);
            var branches = read.Units.Where(unit => unit.Kind == WorkUnitKind.Branch).ToList();
            BranchesOf = branches.SelectMany(unit => owners.Governing(unit).Select(run => (run.Run, Unit: unit)))
                .ToLookup(pair => pair.Run, pair => pair.Unit, StringComparer.Ordinal);
            foreach (var branch in branches)
            {
                foreach (var link in branch.Links.Where(link => link.Relation == WorkRelation.ProducedBy).Distinct())
                {
                    if (!ProducedBy.TryGetValue(link.Unit, out var same)) ProducedBy[link.Unit] = same = [];
                    same.Add(branch);
                }
            }
        }

        public Dictionary<string, WorkUnit> Units { get; }

        public List<QuestView> Quests { get; }

        public QuestChains Chains { get; }

        /// <summary>The branch units by the run that owns them.</summary>
        public ILookup<string?, WorkUnit> BranchesOf { get; }

        /// <summary>The branch units by the unit a link says produced them, in the order of the units.</summary>
        public Dictionary<string, List<WorkUnit>> ProducedBy { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// A run bound to a graph version, as the runtime governs it: its own unit under its version's graph, named by its binding's run, and what
    /// its nodes observe this look. Null where it is not judged now: its version does not read (held whole, said), or it ended and its chain
    /// has not come back here, or no start or first look asked a pass for it.
    /// </summary>
    private static GovernedUnit? Governs(
        GraphRun run, NodeUnitsRead read, RunReads reads, RunOwners owners, IReadOnlyList<NodeDeclaration> steps, Dictionary<string, string> identities,
        ICollection<string>? said)
    {
        var binding = run.Binding;
        if (run.Version is not { Graph: { } graph, GraphDigest: { } digest })
        {
            // A run governed here, not ended: its record is held whole. One never governed here has nothing to hold.
            if (run is { Recorded: true, Ended: null })
            {
                said?.Add($"work  {run.Unit}: the graph version its run is bound to does not read ({run.Version.Unreadable}), so its record is held whole, "
                          + "and nothing on it is judged or started until it reads again.");
            }

            return null;
        }

        var repository = binding.Repository;

        // A verification run has no quests and no branch; it observes itself at its commit, and once ended it is never judged again.
        if (run.Line is { } line)
        {
            if (run.Ended is not null) return null;
            var at = new WorkUnit(run.Unit, WorkUnitKind.Run, new WorkOwner(repository, binding.Workspace, VerifyFiles.Lines, WorkRecord.DriverWriter),
                new WorkSubject(binding.Run, line.Commit), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "its verification runs"), []);
            var (checks, sent, seen) = Declare(graph, steps);
            var verifies = new RunScope(run, at) { Root = line.Root, Identity = IdentityOf(identities, line.Root), Tip = line.Commit };
            return new GovernedUnit(at, graph, digest, binding.Run, checks, sent,
                new WatchedUnit(repository, binding.Workspace, line.Root, "", line.Commit, null, null, []) { Unit = run.Unit })
            {
                Observable = seen, Scope = verifies,
            };
        }

        var (units, quests) = (reads.Units, reads.Quests);

        // Its work here is its chain's quests in its repository, never a look's set-up step, which a run's work leaves out. A conversation's
        // run has no quest; its newest candidate, submitted at the person's press, stands in the quest's place, and a run with none yet
        // judges nothing.
        var chat = run.Candidate?.Newest;
        if (ChatRuns.IsChatRun(binding.Run) && chat is null) return null;
        List<QuestView> work = chat is not null ? [] : reads.Chains.Of(binding.Run)
            .Where(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase) && quest.SetUpIn is null).ToList();
        var newest = work.LastOrDefault();

        // An ended run is judged again only once its chain has come back here, at a start's ask or this process's first look. A conversation's
        // comes back at its next candidate, which a press submits, never at a turn's end.
        if (run.Ended is { } ended
            && !(chat is not null ? !string.Equals(chat.IdIn(binding.Run), ended.Quest, StringComparison.Ordinal)
                : run.Asked && newest is not null && !string.Equals(newest.Id, ended.Quest, StringComparison.Ordinal)))
        {
            return null;
        }

        // The branch its newest session worked, the newest opened of those the newest quest produced, whether its tree stands or not, another
        // graph run's work on it too; one Current's work is on is held, and no subject. Every branch of its quests, its former subjects included, is what an answer maps back by.
        var branches = reads.BranchesOf[binding.Run].ToList();
        var produced = newest is null ? null : new WorkLink(WorkRelation.ProducedBy, QuestUnits.IdOf(newest.Id));
        var newestBranch = chat is not null ? branches.FirstOrDefault() : produced is null ? null
            : reads.ProducedBy.GetValueOrDefault(produced.Unit)?.MaxBy(Opened);
        var branch = newestBranch is not null && owners.Keeps(newestBranch) == RunOwners.Run ? newestBranch : null;

        // The go-aheads on its ask that a session serving one of its quests, or a request naming one, asked.
        var askId = binding.Ask ?? (newest is null ? null : AskWords.AskOf(newest.From));
        IReadOnlyList<GoAheadView>? goAheads = null;
        if (askId is not null && units.GetValueOrDefault(AskUnits.IdOf(askId))?.Records.Ask is { } ask)
        {
            var named = work.Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var serving = work.SelectMany(quest => units.GetValueOrDefault(QuestUnits.IdOf(quest.Id))?.Executors ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            goAheads = [.. (ask.GoAheads ?? []).Where(goAhead => goAhead.Asked.Any(asked => serving.Contains(asked.Session) || (asked.Quest is { } quest && named.Contains(quest))))];
        }

        var root = read.Roots.GetValueOrDefault(repository) ?? "";
        var identity = IdentityOf(identities, root);
        var self = new WorkUnit(run.Unit, WorkUnitKind.Run, new WorkOwner(repository, binding.Workspace, WorkflowRunBindings.Folder, WorkRecord.DriverWriter),
            new WorkSubject(binding.Run, null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "its graph runs"), []);
        var scope = new RunScope(run, self)
        {
            Quest = chat is not null ? CandidateUnit(binding, chat, branch) : newest is null ? null : units.GetValueOrDefault(QuestUnits.IdOf(newest.Id)),
            Quests = work,
            Branch = branch,
            Branches = branches,
            AfterHanded = RunSubjects.AfterHanded(graph),
            GoAheads = goAheads,
            // The quests a session serving the run's work published, which its `work.requests` holds the work on.
            Requests = WorkRequestsReading.Published(quests,
                work.SelectMany(quest => units.GetValueOrDefault(QuestUnits.IdOf(quest.Id))?.Executors ?? []).Distinct(StringComparer.Ordinal),
                work.Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)),
            Root = root,
            Identity = identity,
        };

        var (declared, dispatchable, observable) = Declare(graph, steps);
        var watched = new WatchedUnit(repository, binding.Workspace, root, branch is null ? "" : RunBranches.Standing(branch), "", null, null, [])
        {
            Unit = branch?.Id ?? run.Unit, Identity = identity,
        };

        // A named version is never edited, so its run takes no places: its nodes are scoped and bound by the version's digest.
        return new GovernedUnit(self, graph, digest, binding.Run, declared, dispatchable, watched) { Observable = observable, Scope = scope };

        // A conversation's candidate as the quest `claimed` observes: done at the tip the press submitted, one per candidate of the run.
        static WorkUnit CandidateUnit(WorkflowRunBinding binding, ChatCandidate candidate, WorkUnit? branch)
        {
            var id = candidate.IdIn(binding.Run);
            var stood = new QuestView(id, "", binding.Repository, "A conversation's candidate", "", "Done");
            return new WorkUnit(
                QuestUnits.IdOf(id), WorkUnitKind.Quest, new WorkOwner(binding.Repository, binding.Workspace, "chat candidates", WorkRecord.DriverWriter),
                new WorkSubject("#" + id, candidate.Tip),
                new MilestoneEnd(WorkEndState.Ended, "submitted", null, null, candidate.At, "the person's press submitted it"), [])
            {
                Executors = branch?.Executors ?? [], Records = new WorkUnitRecords(null, [], stood),
            };
        }

        // When a branch unit's work is opened: its session branch's opening, else its newest landing.
        static DateTimeOffset Opened(WorkUnit unit) =>
            unit.Records.Branch?.At ?? unit.Records.Landings.Select(landing => landing.LandedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
    }

    /// <summary>
    /// The named version a quest unit's run is bound to in <paramref name="home"/>: the run its chain's first quest in its repository
    /// names (<see cref="WorkflowRunBindings.RunOf"/>), read through the home's workflows and run bindings (<see cref="WorkflowRunNodes.VersionOf"/>)
    /// and never written; null under Current or with no binding. A home that binds no run reads one absent file a quest.
    /// </summary>
    public static Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?> BoundIn(string home)
    {
        var store = new StampedWorkflows(new WorkflowFiles(home), home);
        var runs = new WorkflowRunFiles(home);
        // The chains of one list of quests, indexed once for every quest asked of it.
        var gate = new object();
        IReadOnlyCollection<QuestView>? indexed = null;
        QuestChains? chains = null;
        return (unit, quests) =>
        {
            if (unit.Records.Quest is not { } quest) return null;
            QuestChains known;
            lock (gate)
            {
                if (!ReferenceEquals(indexed, quests)) (indexed, chains) = (quests, new QuestChains([.. quests]));
                known = chains!;
            }

            return WorkflowRunNodes.VersionOf(store, runs, WorkflowRunBindings.RunOf(known.Of(quest.Id), unit.Owner.Repository ?? "") ?? quest.Id);
        };
    }

    /// <summary>Every quest a read holds, by id.</summary>
    public static Dictionary<string, QuestView> Quests(NodeUnitsRead read) =>
        read.Units.Select(unit => unit.Records.Quest).OfType<QuestView>()
            .GroupBy(quest => quest.Id, StringComparer.Ordinal).ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);

    /// <summary>
    /// What a fresh read says of the open quests bound to a closure graph the choices do not hold, one line each, so once a pass: one closed
    /// through the graph this home keeps for it, naming the nodes that graph runs which the choices' graph does not, and those the choices'
    /// graph runs which it does not; one whose graph is kept nowhere here, so nothing here closes it and its close waits. A quest bound to the
    /// choices' own graph, or Done, says nothing.
    /// </summary>
    public static IReadOnlyList<string> Bindings(DriverConfig config, NodeUnitsRead read, IReadOnlyDictionary<string, GovernedUnit> governed)
    {
        var lines = new List<string>();
        foreach (var unit in read.Units.Where(unit => unit.Kind == WorkUnitKind.Quest).OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            if (unit.Records.Quest is not { Closure: { Length: > 0 } bound, Status: not "Done" } quest) continue;
            NodeGraph? held = null;
            if (governed.GetValueOrDefault(unit.Id) is not { Closes: true } own)
            {
                lines.Add($"work  {unit.Id}: #{quest.Id} is bound to the closure graph `{bound}`, which is kept nowhere here and is not the "
                          + "choices' graph, so nothing here closes it: its close waits, and its done is yours to give.");
                continue;
            }

            var head = $"work  {unit.Id}: #{quest.Id} closes through `{own.GraphDigest}`, the graph it was bound to at its take, kept here: ";
            if (held is null)
            {
                lines.Add(head + $"the choices hold no closure graph for it now, and it runs {string.Join(", ", own.Graph.Nodes.Select(node => $"`{node.Id}`"))}.");
                continue;
            }

            if (NodeGraphs.Digest(held) == own.GraphDigest) continue;

            // A node is the choices' where they declare it alike (its declaration's own digest), so an edited option names it as bound.
            // Keyed as Govern keys its declarations, the last of an id standing, so a graph never makes the saying throw.
            static Dictionary<string, string> Declared(NodeGraph graph)
            {
                var each = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var node in graph.Nodes) each[node.Id] = NodeGraphs.Digest(new NodeGraph([node], [], [], node.Id));
                return each;
            }

            var theirs = Declared(held);
            var ours = Declared(own.Graph);
            var runs = own.Graph.Nodes.Where(node => theirs.GetValueOrDefault(node.Id) != ours[node.Id])
                .Select(node => theirs.ContainsKey(node.Id) ? $"`{node.Id}` as it was bound" : $"`{node.Id}`").ToList();
            var misses = held.Nodes.Where(node => !ours.ContainsKey(node.Id)).Select(node => $"`{node.Id}`").ToList();
            var differs = (runs.Count, misses.Count) switch
            {
                (0, 0) => "the same nodes, wired otherwise",
                (_, 0) => $"it runs {string.Join(", ", runs)}, which the choices' graph does not",
                (0, _) => $"it runs none of {string.Join(", ", misses)}, which the choices' graph does",
                _ => $"it runs {string.Join(", ", runs)}, which the choices' graph does not, and none of {string.Join(", ", misses)}, which it does",
            };
            lines.Add(head + $"the choices hold `{NodeGraphs.Digest(held)}` now; {differs}.");
        }

        return lines;
    }

    public static string PluginOf(string kind) => kind[..kind.IndexOf('/')];


    private static bool Performs(NodeDeclaration node) => node.Reach == NodeReach.Performed && NodeKit.IsPlugin(node.Kind);

    private static bool Observes(NodeDeclaration node) => node.Reach == NodeReach.Observed && NodeKit.IsPlugin(node.Kind);

    /// <summary>Whether a graph names a plugin's performed or observed node: what arms the runtime and makes a unit governed.</summary>
    private static bool Acts(NodeGraph? graph) => graph?.Nodes.Any(node => Performs(node) || Observes(node)) == true;

    /// <summary>
    /// Whether a graph holds an <c>integrate</c>: the unit's step in the line's integration. What arms the runtime for a branch rule carrying
    /// <c>integrate</c> and makes its repository's branch units governed.
    /// </summary>
    private static bool Integrates(NodeGraph? graph) => graph?.Nodes.Any(node => node.Kind == NodeKinds.Integrate) == true;
}
