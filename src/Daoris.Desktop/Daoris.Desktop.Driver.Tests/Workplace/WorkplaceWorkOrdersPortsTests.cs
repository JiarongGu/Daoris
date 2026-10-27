using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Work-orders cut: the seams the cut adds. Workplace declares the work-orders operations (the
/// abandons' record, what a unit owes, the inventory read, a done's evidence at a tree's end) and the facet of a session's row; Work orders
/// implements and installs them. The cases that need no new seam are <see cref="WorkplaceWorkOrdersBoundaryTests"/>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkplaceWorkOrdersPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-workorders-ports", Guid.NewGuid().ToString("N"));

    public WorkplaceWorkOrdersPortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Remove(_home);

    private static GrownBranch Grown(string branch, string unit) => new("engine", "default", branch, "main", "f0", null, At) { Unit = unit };

    private static HeldFact Fact() => new(At, At, EndBy.Merged, "t1", "main", "main", "m1", "r1");

    [Fact]
    public void The_installed_operations_are_work_orders_and_do_what_their_callers_did()
    {
        Assert.IsType<WorkplaceWorkOrdersOperations>(WorkplaceWorkOrders.Current);

        var record = new AbandonRecord(_home);
        foreach (var id in new[] { "q1", "q2", "q3" }) record.Write(new AbandonEntry("quest", id, At, "screen", "why"));
        Assert.Equal(2, WorkplaceWorkOrders.Current.ForgetAbandoned(_home, entry => entry.Id == "q2"));
        Assert.Equal(["q2"], record.Entries().Select(entry => entry.Id));
        Assert.Equal(0, WorkplaceWorkOrders.Current.ForgetAbandoned(_home, _ => true));

        Assert.Empty(WorkplaceWorkOrders.Current.Owing(_home));
        var facts = new InventoryFacts([Grown("daoris/s1", "unit-a") with { GoneAt = At, Held = Fact() }], [], [], [], [], new HashSet<string>(), _ => false, At);
        var unit = Assert.Single(WorkplaceWorkOrders.Current.Derive(facts));
        Assert.Equal(("unit-a", "engine", "daoris/s1", "ended"), (unit.Unit, unit.Repository, unit.Branch, unit.End.State));
        Assert.Equal(WorkInventory.Derive(facts).Single().Entries, unit.Entries);

        var submitted = new QuestView("q1", "ask #a1", "reports", "Build", "b", "Taken")
        {
            Closure = "gph-0123", Submission = new QuestSubmissionView("desk", 42, null, "ready", null, "s1"),
        };
        Assert.NotNull(WorkplaceWorkOrders.Current.CommitWanted(submitted, new EvidenceAt("/t", EvidenceCodes.Sweep) { Session = "s1" }));
        Assert.Null(WorkplaceWorkOrders.Current.CommitWanted(submitted, new EvidenceAt("/t", EvidenceCodes.Sweep) { Session = "s2" }));
        Assert.Null(WorkplaceWorkOrders.Current.CommitWanted(null, new EvidenceAt("/t", EvidenceCodes.Sweep) { Session = "s1" }));
    }

    [Fact]
    public async Task A_done_that_waits_on_no_evidence_ends_as_before_and_asks_git_nothing_through_the_installed_operation()
    {
        using var client = Answering(_ => null);
        var asked = 0;
        WorkingTree.GitRead git = (_, _, _, _) => { asked++; return Task.FromResult(-1); };

        var kept = await WorkplaceWorkOrders.Current.AtEndAsync(
            client, null, new EvidenceAt("/t", EvidenceCodes.SessionEnd) { Session = "s1" }, "no commits landed", git, CancellationToken.None);

        Assert.Equal("no commits landed", kept);
        Assert.Equal(0, asked);
    }

    [Fact]
    public void The_archive_judges_a_row_by_the_facet_alone()
    {
        var archive = new SessionArchive(_home);
        IWorkplaceSessionRow[] rows =
        [
            new Row("ended", SessionGroup.Ended), new Row("review", SessionGroup.Review), new Row("working", SessionGroup.Working),
            new Row("you", SessionGroup.You), new Row("later", SessionGroup.Later),
        ];

        var answer = archive.Archive(["ended", "review", "working", "you", "later", "gone"], rows, ["ended", "later"], At);

        Assert.Equal(
            [("ended", ArchiveVerdict.Archived), ("review", ArchiveVerdict.NeedsYou), ("working", ArchiveVerdict.Live),
             ("you", ArchiveVerdict.NeedsYou), ("later", ArchiveVerdict.Archived), ("gone", ArchiveVerdict.Unknown)],
            answer.Outcomes.Select(outcome => (outcome.Session, outcome.Verdict)));
        Assert.Equal([SessionGroup.Review, SessionGroup.You], answer.Outcomes.Select(outcome => outcome.Group).OfType<string>());
        Assert.Equal(["ended", "later"], archive.Marks().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_sessions_row_answers_the_facet_with_its_own_session_group_and_offer()
    {
        var offer = new LandOffer("daoris/a", "a", 1, 0);
        IWorkplaceSessionRow row = new SessionGrouping("s1", SessionGroup.Review, "completed") { Lands = offer };

        Assert.Equal(("s1", "review", offer), (row.Session, row.Group, row.Lands));
        Assert.Null(((IWorkplaceSessionRow)new SessionGrouping("s2", SessionGroup.Ended, "failed")).Lands);
    }

    [Fact]
    public void Forgetting_a_branch_asks_what_units_owe_once_after_the_open_units_and_keeps_the_trace_of_one_that_does()
    {
        var actual = WorkplaceWorkOrders.Current;
        var spy = new Spy { Owes = new HashSet<string>(["unit-owes"], StringComparer.Ordinal) };
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/owes", "unit-owes"));
        grown.Record(Grown("daoris/free", "unit-free"));
        try
        {
            WorkplaceWorkOrders.Current = spy;
            grown.Forget("engine", ["daoris/owes", "daoris/free"]);
        }
        finally { WorkplaceWorkOrders.Current = actual; }

        Assert.Equal([$"owing:{_home}"], spy.Calls);
        var left = Assert.Single(grown.Entries());
        Assert.Equal(("daoris/owes", true), (left.Branch, left.GoneAt is not null));
    }

    [Fact]
    public void The_tidy_reads_its_pending_units_from_the_supplied_inventory_with_the_records_it_gathered()
    {
        var actual = WorkplaceWorkOrders.Current;
        new SessionBranches(_home).Record(Grown("daoris/x", "u1"));
        var spy = new Spy
        {
            Units = [new Milestone("u1", "engine", "daoris/x", new MilestoneEnd("ended", null, null, null, At, ""),
                [new InventoryEntry("u1", InventoryKind.SessionBranch, "daoris/x", InventoryRole.Temporary, InventoryDisposition.Pending, "")])],
        };
        IReadOnlyList<TidyUnit> pending;
        try
        {
            WorkplaceWorkOrders.Current = spy;
            pending = TidyEffect.Pending(_home, At);
        }
        finally { WorkplaceWorkOrders.Current = actual; }

        var unit = Assert.Single(pending);
        Assert.Equal(("u1", "engine", "default", null, At), (unit.Unit, unit.Repository, unit.Workspace, unit.Tree, unit.Since));
        Assert.Equal(["daoris/x"], unit.Branches);
        var facts = Assert.Single(spy.Derived);
        Assert.Equal(["daoris/x"], facts.Branches.Select(branch => branch.Branch));
        Assert.Equal((At, 0, 0), (facts.Now, facts.Landings.Count, facts.Refs.Count));
    }

    [Fact]
    public async Task History_clearing_forgets_abandons_through_the_supplied_operation_once_at_its_tidy_step_with_the_scopes_that_remain()
    {
        var actual = WorkplaceWorkOrders.Current;
        var configPath = DriverConfig.ResolvePath(_home);
        using var ledger = new HistoryStandIn
        {
            Records = [HistoryStandIn.Record("s1", "q1"), HistoryStandIn.Record("o1", "q2")],
            Quests = [HistoryStandIn.Quest("q1"), HistoryStandIn.Quest("q2")],
            Home = _home,
        };
        ledger.Listings["quest=q1"] = [HistoryStandIn.Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
        using var service = ledger.Client();
        var spy = new Spy();
        try
        {
            WorkplaceWorkOrders.Current = spy;
            var world = new HistoryWorld(service, _home, configPath, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
            await HistoryClearing.ClearAsync(world, HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], "screen");
        }
        finally { WorkplaceWorkOrders.Current = actual; }

        Assert.Equal([$"forget:{_home}"], spy.Calls);
        var known = Assert.Single(spy.Known);
        Assert.False(known(new AbandonEntry("quest", "q1", At, "screen", "why")));
        Assert.True(known(new AbandonEntry("quest", "q2", At, "screen", "why")));
        Assert.False(known(new AbandonEntry("ask", "q2", At, "screen", "why")));
    }

    [Fact]
    public async Task The_sweep_reads_a_lost_dones_evidence_through_the_supplied_operation_with_the_sweeps_own_place()
    {
        var actual = WorkplaceWorkOrders.Current;
        var tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(tree);
        var spy = new Spy { Bundle = "evidence bundle" };
        QuestView Done(bool awaits) => new("q1", "ask #a1", "reports", "Build", "b", "Done") { AwaitsEvidence = awaits, Held = awaits };
        JsonObject Lost(string id) => new() { ["id"] = id, ["repository"] = "reports", ["state"] = "working", ["quest"] = "q1", ["tree"] = tree };
        var moves = new List<JsonObject>();
        try
        {
            WorkplaceWorkOrders.Current = spy;
            using (var client = Answering(request => Sessions(request, [Lost("s1")], [Done(awaits: true)], moves)))
            {
                await Orphans.EndAsync(client, new SessionProcesses(Path.Combine(_home, "markers")));
            }

            Assert.Equal(["end:s1:sweep:" + tree + ":"], spy.Calls);
            Assert.Equal("evidence bundle", Assert.Single(moves)["evidence"]!.GetValue<string>());

            spy.Calls.Clear();
            moves.Clear();
            using (var client = Answering(request => Sessions(request, [Lost("s2")], [Done(awaits: false)], moves)))
            {
                spy.Wanted = new QuestSubmissionView("desk", 1, null, "ready", null, "s2");
                await Orphans.EndAsync(client, new SessionProcesses(Path.Combine(_home, "markers")));
            }

            Assert.Equal(["wanted:s2:sweep:" + tree, "end:s2:sweep:" + tree + ":"], spy.Calls);
            Assert.Null(Assert.Single(moves)["evidence"]);
        }
        finally { WorkplaceWorkOrders.Current = actual; }
    }

    [Fact]
    public void A_missing_operation_is_required_only_where_a_caller_reaches_it_and_its_failures_propagate_unchanged()
    {
        var actual = WorkplaceWorkOrders.Current;
        var grown = new SessionBranches(_home);
        grown.Record(Grown("daoris/x", "u1"));
        try
        {
            WorkplaceWorkOrders.Current = null!;
            Assert.Single(grown.Entries());
            Assert.Empty(new SessionArchive(_home).Marks());
            var missing = Assert.Throws<InvalidOperationException>(() => grown.Forget("engine", ["daoris/x"]));
            Assert.Contains("Work orders installs them", missing.Message);
            Assert.Throws<InvalidOperationException>(() => TidyEffect.Pending(_home, At));

            var failure = new IOException("refused");
            WorkplaceWorkOrders.Current = new Spy { Fail = failure };
            Assert.Same(failure, Assert.Throws<IOException>(() => TidyEffect.Pending(_home, At)));
            Assert.Same(failure, Assert.Throws<IOException>(() => grown.Forget("engine", ["daoris/x"])));
        }
        finally { WorkplaceWorkOrders.Current = actual; }
    }

    private static ServiceClient Answering(Func<HttpRequestMessage, (HttpStatusCode, string)?> answer) => QuestStandIn.Client(answer);

    private static (HttpStatusCode, string)? Sessions(HttpRequestMessage request, JsonObject[] sessions, QuestView[] quests, List<JsonObject> moves)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == "/api/sessions") return (HttpStatusCode.OK, new JsonArray([.. sessions.Select(each => each.DeepClone())]).ToJsonString());
        if (request.Method == HttpMethod.Get && path == "/api/quests") return (HttpStatusCode.OK, QuestStandIn.List(quests));
        if (request.Method == HttpMethod.Post && path.EndsWith("/state", StringComparison.Ordinal))
        {
            var body = QuestStandIn.Body(request);
            moves.Add(body);
            return (HttpStatusCode.OK, new JsonObject { ["session"] = new JsonObject { ["id"] = path.Split('/')[3], ["state"] = body["state"]!.DeepClone() }, ["message"] = "moved" }.ToJsonString());
        }

        return null;
    }

    private sealed record Row(string Session, string Group) : IWorkplaceSessionRow
    {
        public LandOffer? Lands => null;
    }

    private sealed record Milestone(string Unit, string Repository, string? Branch, MilestoneEnd End, IReadOnlyList<InventoryEntry> Entries) : IKnowledgeMilestone
    {
        public string? Session => null;
        public IReadOnlyList<InventoryEntry> Result => [];
        public MilestoneCleanup Cleanup => new("pending", [], []);
        public IReadOnlyList<InventoryEntry> Recovery => [];
    }

    private sealed class Spy : IWorkplaceWorkOrders
    {
        public List<string> Calls { get; } = [];
        public List<Func<AbandonEntry, bool>> Known { get; } = [];
        public List<InventoryFacts> Derived { get; } = [];
        public IReadOnlySet<string> Owes { get; init; } = new HashSet<string>();
        public IReadOnlyList<IKnowledgeMilestone> Units { get; init; } = [];
        public QuestSubmissionView? Wanted { get; set; }
        public string? Bundle { get; init; }
        public Exception? Fail { get; init; }

        public int ForgetAbandoned(string home, Func<AbandonEntry, bool> known)
        {
            if (Fail is not null) throw Fail;
            Calls.Add("forget:" + home);
            Known.Add(known);
            return 0;
        }

        public IReadOnlySet<string> Owing(string home)
        {
            if (Fail is not null) throw Fail;
            Calls.Add("owing:" + home);
            return Owes;
        }

        public IReadOnlyList<IKnowledgeMilestone> Derive(InventoryFacts facts)
        {
            if (Fail is not null) throw Fail;
            Derived.Add(facts);
            return Units;
        }

        public QuestSubmissionView? CommitWanted(QuestView? quest, EvidenceAt at)
        {
            Calls.Add($"wanted:{at.Session}:{at.How}:{at.Tree}");
            return Wanted;
        }

        public Task<string?> AtEndAsync(ServiceClient service, QuestView? quest, EvidenceAt at, string? commits, WorkingTree.GitRead git, CancellationToken ct)
        {
            Calls.Add($"end:{at.Session}:{at.How}:{at.Tree}:{commits}");
            return Task.FromResult(Bundle);
        }
    }
}
