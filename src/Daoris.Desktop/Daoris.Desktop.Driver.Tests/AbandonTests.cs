using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Abandon an ask's work, or one quest's, on this machine (PAUSE1d, D132 points 6–10, design §3): the first press lists every
/// piece and what it would keep and why, and the second sends exactly that list with the person's reason, judging each piece
/// again. In §3.4's order it pauses the scope, stops and ends the work's sessions, declines each quest with the reason (an
/// open one only while open), closes the ask, syncs, discards a tree or branch nothing else holds, archives the sessions,
/// tidies <c>driver.json</c> and writes <c>abandoned.json</c>, never a path.
/// </summary>
/// <remarks>
/// In-process: the service is a stand-in reached through the real client; the trees' proof and discard are a stand-in too
/// (<see cref="IWorkTrees"/>), since git is <see cref="SessionTreeOnlyHereTests"/>' (the <c>Process</c> half); the sync is
/// the door's seam. The registry is the home's markers, as a terminal holds it, so a working session nothing here runs is
/// ended as an orphan, the person's stop.
/// </remarks>
public sealed class AbandonTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-abandon-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 3, 16, 20, 0, TimeSpan.Zero);

    private const string Reason = "This went the wrong way; the bridge is v3 and the report is the common one.";

    public AbandonTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string ConfigPath => Path.Combine(_home, "driver.json");

    private string Tree(string name) => Path.Combine(_home, "trees", "default", "engine", name).Replace('\\', '/');

    private static JsonObject Record(
        string id, string state, string? quest = null, string? tree = null, string? baseCommit = null, bool took = false,
        string repository = "engine", string? ask = null, string kind = "driven", int minutes = 0) => new()
    {
        ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest, ["ask"] = ask,
        ["tree"] = tree, ["baseCommit"] = baseCommit, ["took"] = took, ["adapter"] = "claude-code",
        ["created"] = Now.AddHours(-2).AddMinutes(minutes).ToString("O"), ["updated"] = Now.AddHours(-2).AddMinutes(minutes + 5).ToString("O"),
    };

    private static JsonObject Quest(string id, string status, string from = "ask #a1", string? publishedBy = null) => new()
    {
        ["id"] = id, ["from"] = from, ["to"] = "engine", ["title"] = $"The work of #{id}", ["body"] = "A body.", ["status"] = status,
        ["publishedBy"] = publishedBy, ["workspace"] = "default",
    };

    /// <summary>
    /// Ask <c>a1</c>'s work, every piece an abandon meets (design §3.2): its running intake; <c>q1</c> open; <c>q2</c> taken by
    /// a working session that took it, in a tree only here; <c>q3</c> taken by a session waiting on you, whose tree is gone
    /// and its branch only here; <c>q4</c> done, its session's tree pushed, and an earlier session's tree a landing names;
    /// <c>q5</c> taken on a teammate's machine; <c>q6</c> a question the working session asked; <c>q8</c> taken here outside
    /// Daoris, a failed session's tree git cannot judge; <c>q9</c> declined. Beside it, ask <c>b2</c>'s quest and session.
    /// </summary>
    private Ledger Family() => new(
        [
            Record("1ntake00", "working", repository: "ask #a1", ask: "a1", kind: "chat", minutes: 0),
            Record("land0000", "completed", "q4", Tree("s-dddddddd"), "base-dddd", took: true, minutes: 5),
            Record("w0rk1ng0", "working", "q2", Tree("s-aaaaaaaa"), "base-aaaa", took: true, minutes: 10),
            Record("p4rk3d00", "awaiting-person", "q3", Tree("s-bbbbbbbb"), "base-bbbb", took: true, minutes: 20),
            Record("laptop/s9", "working", "q5", minutes: 30),
            Record("d0ne0000", "completed", "q4", Tree("s-cccccccc"), "base-cccc", took: true, minutes: 40),
            Record("unkn0000", "failed", "q8", Tree("s-eeeeeeee"), minutes: 45),
            Record("s0ther00", "working", "q7", minutes: 50),
        ],
        [
            Quest("q1", "Open"), Quest("q2", "Taken"), Quest("q3", "Taken"), Quest("q4", "Done"), Quest("q5", "Taken"),
            Quest("q6", "Open", from: "engine", publishedBy: "w0rk1ng0"), Quest("q8", "Taken"), Quest("q9", "Declined"),
            Quest("q7", "Open", from: "ask #b2"),
        ],
        new Dictionary<string, string> { ["q2"] = "held", ["q3"] = "held", ["q5"] = "none", ["q8"] = "held" },
        ["a1", "b2"]);

    /// <summary>The proof's answers for the family's trees, and what each kept tree holds for its review.</summary>
    private static FakeTrees Trees(List<string> events) => new(events)
    {
        Judged =
        {
            ["daoris/s-aaaaaaaa"] = new TreeOnlyHere(OnlyHereKind.OnlyHere)
            {
                TreeHere = true, Tip = "9f3e2a1aaaa", Commits = 3, Uncommitted = 2, Files = ["src/a.cs", "README.md"],
            },
            ["daoris/s-bbbbbbbb"] = new TreeOnlyHere(OnlyHereKind.OnlyHere) { Tip = "7c1d0e2bbbb", Commits = 1, Root = "X:/work/engine" },
            ["daoris/s-cccccccc"] = new TreeOnlyHere(OnlyHereKind.Elsewhere) { TreeHere = true, Tip = "c0ffee", Commits = 2, Where = "origin/daoris/s-cccccccc" },
            ["daoris/s-dddddddd"] = new TreeOnlyHere(OnlyHereKind.OnlyHere) { TreeHere = true, Tip = "d00d", Commits = 1 },
            ["daoris/s-eeeeeeee"] = new TreeOnlyHere(OnlyHereKind.Unknown) { TreeHere = true },
        },
        Work =
        {
            ["s-cccccccc"] = new TreeWork(1, 0),
            ["s-dddddddd"] = new TreeWork(0, 0),
            ["s-eeeeeeee"] = new TreeWork(null, null),
        },
    };

    private void Landed() => new LandedBranches(_home).Record(new LandedBranch(
        "engine", "default", "feature/q4", "main", "d00d", "land0000", "q4", "The work of #q4", Now.AddHours(-1)));

    private WorkWorld World(ServiceClient service, FakeTrees trees, List<string> events, MachineLog? log = null,
        Func<string, SyncReport?>? sync = null) =>
        new(service, _home, ConfigPath, new SessionProcesses(Path.Combine(_home, "sessions")))
        {
            Log = log,
            Clock = () => Now,
            Wait = TimeSpan.FromSeconds(5),
            Poll = TimeSpan.FromMilliseconds(20),
            Trees = trees,
            Sync = (workspace, _) =>
            {
                events.Add($"sync:{workspace}");
                return Task.FromResult(sync is null ? SyncReport.Clean : sync(workspace));
            },
        };

    private static readonly string[] Takes =
    [
        "quest:q1", "quest:q2", "quest:q3", "quest:q6", "session:1ntake00", "session:land0000", "session:w0rk1ng0", "session:p4rk3d00",
        "tree:engine:daoris/s-aaaaaaaa", "tree:engine:daoris/s-bbbbbbbb", "ask:a1",
    ];

    /// <summary>The first press names what an abandon does with each piece, and why it keeps what it keeps (design §3.2).</summary>
    [Fact]
    public async Task The_first_press_names_each_piece_and_each_keep()
    {
        Landed();
        var events = new List<string>();
        var trees = Trees(events);
        using var service = Family().Client();

        var plan = (await WorkAbandoning.PlanAsync(World(service, trees, events), WorkScope.Ask, "#a1"))!;

        Assert.Equal(
            ["q1:decline", "q2:decline", "q3:decline", "q4:keep:done", "q5:keep:taken-elsewhere", "q6:decline", "q8:keep:taken-outside", "q9:none"],
            plan.Quests.Select(each => $"{each.Quest.Quest.Id}:{each.Act}{(each.Why is null ? "" : $":{each.Why}")}").Order(StringComparer.Ordinal));
        Assert.Equal(["q1", "q6"], plan.Quests.Where(each => each.WhileOpen).Select(each => each.Quest.Quest.Id).Order(StringComparer.Ordinal));
        Assert.Equal("laptop", Assert.Single(plan.Quests, each => each.Quest.Quest.Id == "q5").Machine);
        Assert.Equal(
            [
                "1ntake00:stop:archive", "d0ne0000:keep:review", "land0000:archive:archive", "laptop/s9:keep:teammate",
                "p4rk3d00:end:archive", "unkn0000:keep:review", "w0rk1ng0:stop:archive",
            ],
            plan.Sessions.Select(each => $"{each.Session.Record.Id}:{each.Act}:{(each.Archive ? "archive" : each.Why)}").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["s-aaaaaaaa:discard", "s-bbbbbbbb:delete", "s-cccccccc:keep:elsewhere", "s-dddddddd:keep:landed", "s-eeeeeeee:keep:unknown"],
            plan.Trees.Select(each => $"{each.Tree.Branch["daoris/".Length..]}:{each.Act}{(each.Why is null ? "" : $":{each.Why}")}").Order(StringComparer.Ordinal));
        Assert.Equal("a1", plan.Closes);
        Assert.True(plan.Abandonable);
        Assert.Equal(Takes.Order(StringComparer.Ordinal), plan.Pieces.Order(StringComparer.Ordinal));
        Assert.Equal(9, plan.Kept.Count);
        // The proof is asked with the tree's first session's base, and a gone tree's branch with the registered checkout.
        Assert.Equal("base-aaaa", trees.Asked["daoris/s-aaaaaaaa"].Base);
        Assert.Equal("X:/work/engine", trees.Asked["daoris/s-bbbbbbbb"].Root);
    }

    /// <summary>
    /// 🔴 The second press in §3.4's order: the pause written before anything is stopped; the sessions stopped and the parked
    /// one ended unanswered; each quest declined with the reason verbatim, an open one only while open; the ask closed with it;
    /// one pass; the trees discarded behind the proof; the sessions archived; <c>driver.json</c> tidied.
    /// </summary>
    [Fact]
    public async Task The_second_press_pauses_stops_declines_closes_syncs_discards_and_archives_in_that_order()
    {
        Landed();
        var events = new List<string>();
        var ledger = Family();
        ledger.Events = events;
        var pausedWhenStopped = true;
        ledger.Moving = _ => pausedWhenStopped &= DriverConfig.Load(ConfigPath).PausedAsk("a1") is not null;
        (DriverConfig.Empty
                .WithReleased("q2", "old00000")
                .WithReleased("q5", "x0000000") with { Forgiven = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["q3"] = 2 } })
            .Save(ConfigPath);
        using var service = ledger.Client();

        var outcome = await WorkAbandoning.AbandonAsync(
            World(service, Trees(events), events), WorkScope.Ask, "a1", Reason, pieces: null, PluginEvents.Terminal);

        Assert.Equal(AbandonVerdict.Abandoned, outcome.Verdict);
        Assert.True(pausedWhenStopped, "the pause was written before the first stop");
        Assert.Equal(
            [
                "state:1ntake00:stopped", "state:w0rk1ng0:stopped", "state:p4rk3d00:stopped",
                "decline:q1", "decline:q2", "decline:q3", "decline:q6", "close:a1", "sync:default",
                "discard:daoris/s-aaaaaaaa", "discard:daoris/s-bbbbbbbb",
            ],
            events);
        Assert.Equal(("stopped", false), (ledger.State("w0rk1ng0"), ledger.Interrupted("w0rk1ng0")));
        Assert.Equal("ask `#a1` abandoned.", ledger.Note("p4rk3d00"));
        Assert.Equal(
            [("q1", Reason, true), ("q2", Reason, false), ("q3", Reason, false), ("q6", Reason, true)],
            ledger.Declines);
        Assert.Equal(("Closed", Reason), ledger.Ask("a1"));
        Assert.Equal(("working", "Done", "Taken", "Taken"), (ledger.State("laptop/s9"), ledger.Status("q4"), ledger.Status("q5"), ledger.Status("q8")));
        Assert.Equal(["1ntake00", "land0000", "p4rk3d00", "w0rk1ng0"], new SessionArchive(_home).Marks().Keys.Order(StringComparer.Ordinal));
        Assert.Equal((11, 11), (outcome.Listed, outcome.Went));
        Assert.Empty(outcome.Changed);
        Assert.Empty(outcome.Failed);
        Assert.False(outcome.StillPaused);
        Assert.Equal(
            ["q1:confirmed", "q2:confirmed", "q3:confirmed", "q6:confirmed"],
            outcome.Declines.Select(each => $"{each.Quest}:{each.Answer}").Order(StringComparer.Ordinal));

        var config = DriverConfig.Load(ConfigPath);
        Assert.Null(config.PausedAsk("a1"));
        Assert.Equal(["q5"], config.Released.Keys);
        Assert.Empty(config.Forgiven);
    }

    /// <summary>
    /// The record of what went (design §4.2): the reason, each quest declined, each tree's repository, branch, tip and counts,
    /// each session stopped and archived, what stayed and why, each decline's answer, and never a path on this machine. The
    /// machine log counts it, naming no id (D94 §5).
    /// </summary>
    [Fact]
    public async Task The_record_keeps_what_went_and_what_stayed_with_each_tip_and_never_a_path()
    {
        Landed();
        var events = new List<string>();
        using var service = Family().Client();
        var log = new MachineLog(_home, "driver");

        await WorkAbandoning.AbandonAsync(World(service, Trees(events), events, log), WorkScope.Ask, "a1", Reason, null, PluginEvents.Screen);
        log.Dispose();

        var entry = Assert.Single(new AbandonRecord(_home).Entries());
        Assert.Equal(("ask", "a1", "screen", Reason, true), (entry.Scope, entry.Id, entry.Door, entry.Reason, entry.Closed));
        Assert.Equal(["q1", "q2", "q3", "q6"], entry.Declined);
        Assert.Equal(
            [
                new AbandonedTree("engine", "daoris/s-aaaaaaaa", "9f3e2a1aaaa", 3, 2, ["w0rk1ng0"]),
                new AbandonedTree("engine", "daoris/s-bbbbbbbb", "7c1d0e2bbbb", 1, null, ["p4rk3d00"], Alone: true),
            ],
            entry.Trees.Select(tree => tree with { Sessions = [.. tree.Sessions] }),
            new TreeComparer());
        Assert.Equal(["1ntake00", "p4rk3d00", "w0rk1ng0"], entry.Stopped.Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "quest:q4:done", "quest:q5:taken-elsewhere", "quest:q8:taken-outside", "session:d0ne0000:review", "session:laptop/s9:teammate",
                "session:unkn0000:review", "tree:engine:daoris/s-cccccccc:elsewhere", "tree:engine:daoris/s-dddddddd:landed",
                "tree:engine:daoris/s-eeeeeeee:unknown",
            ],
            entry.Stayed.Select(keep => $"{keep.Piece}:{keep.Why}").Order(StringComparer.Ordinal));
        Assert.Equal("origin/daoris/s-cccccccc", Assert.Single(entry.Stayed, keep => keep.Why == AbandonWhy.Elsewhere).Where);
        Assert.Equal("laptop", Assert.Single(entry.Stayed, keep => keep.Why == AbandonWhy.TakenElsewhere).Machine);
        Assert.Equal(4, entry.Declines.Count);

        var written = File.ReadAllText(new AbandonRecord(_home).FilePath);
        Assert.DoesNotContain(_home.Replace('\\', '/'), written.Replace("\\\\", "/"));
        Assert.DoesNotContain("trees/default", written);
        Assert.DoesNotContain("X:/work", written);

        var line = Assert.Single(Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(File.ReadAllLines),
            each => each.Contains("\"work.abandoned\"", StringComparison.Ordinal));
        foreach (var said in new[]
                 {
                     "\"scope\":\"ask\"", "\"declined\":4", "\"discarded\":1", "\"branches\":1", "\"archived\":4", "\"kept\":9", "\"lost\":0",
                     "\"door\":\"screen\"",
                 })
        {
            Assert.Contains(said, line);
        }

        Assert.DoesNotContain("a1", line);
        Assert.DoesNotContain("w0rk1ng0", line);
    }

    /// <summary>
    /// A piece that changed between the two presses is kept and counted (design §3.1): an open quest another machine took
    /// meanwhile is not declined, and a tree pushed meanwhile is not discarded, its session stopped and left to review.
    /// </summary>
    [Fact]
    public async Task A_piece_that_changed_between_the_presses_is_kept_and_counted()
    {
        Landed();
        var events = new List<string>();
        var ledger = Family();
        var trees = Trees(events);
        using var service = ledger.Client();
        var world = World(service, trees, events);
        var listed = (await WorkAbandoning.PlanAsync(world, WorkScope.Ask, "a1"))!.Pieces;

        ledger.Take("q1", "none");
        trees.Judged["daoris/s-aaaaaaaa"] = new TreeOnlyHere(OnlyHereKind.Elsewhere) { TreeHere = true, Tip = "9f3e2a1aaaa", Commits = 3, Where = "origin/daoris/s-aaaaaaaa" };
        trees.Work["s-aaaaaaaa"] = new TreeWork(0, 2);
        var outcome = await WorkAbandoning.AbandonAsync(world, WorkScope.Ask, "a1", Reason, listed, PluginEvents.Screen);

        Assert.Equal((11, 9), (outcome.Listed, outcome.Went));
        Assert.Equal(
            ["quest:q1:taken-elsewhere", "tree:engine:daoris/s-aaaaaaaa:elsewhere"],
            outcome.Changed.Select(keep => $"{keep.Piece}:{keep.Why}").Order(StringComparer.Ordinal));
        Assert.Equal("Taken", ledger.Status("q1"));
        Assert.DoesNotContain("decline:q1", events);
        Assert.DoesNotContain("discard:daoris/s-aaaaaaaa", events);
        Assert.Equal("stopped", ledger.State("w0rk1ng0"));
        Assert.DoesNotContain("w0rk1ng0", new SessionArchive(_home).Marks().Keys);
        Assert.False(outcome.StillPaused);
        Assert.Equal("abandoned ask #a1: 9 of 11 pieces; 2 changed since the list and were kept.", WorkAbandoning.Lines(outcome)[0]);
    }

    /// <summary>
    /// A question asked after the list opened joins the work as it appears (design §1): it is not taken, since the person did
    /// not see it, and the scope stays paused, so nothing of it starts until they abandon again or resume.
    /// </summary>
    [Fact]
    public async Task Work_that_joined_after_the_list_is_not_taken_and_the_scope_stays_paused()
    {
        Landed();
        var events = new List<string>();
        var ledger = Family();
        using var service = ledger.Client();
        var world = World(service, Trees(events), events);
        var listed = (await WorkAbandoning.PlanAsync(world, WorkScope.Ask, "a1"))!.Pieces;

        ledger.Publish(Quest("q10", "Open", from: "engine", publishedBy: "w0rk1ng0"));
        var outcome = await WorkAbandoning.AbandonAsync(world, WorkScope.Ask, "a1", Reason, listed, PluginEvents.Screen);

        Assert.Equal(["quest:q10"], outcome.Joined);
        Assert.Equal("Open", ledger.Status("q10"));
        Assert.True(outcome.StillPaused);
        Assert.NotNull(DriverConfig.Load(ConfigPath).PausedAsk("a1"));
        Assert.Contains(WorkAbandoning.Lines(outcome), line => line.Contains("stays paused", StringComparison.Ordinal));
    }

    /// <summary>
    /// The pass answers each shared decline (design §5.2): a decline another machine's take beat to the remote is lost, the
    /// quest staying theirs; one the pass left behind is unconfirmed; the rest are confirmed.
    /// </summary>
    [Fact]
    public async Task The_pass_says_which_shared_declines_were_confirmed_lost_or_unconfirmed()
    {
        Landed();
        var events = new List<string>();
        var ledger = Family();
        using var service = ledger.Client();
        ledger.Behind = ["q6"];
        var world = World(service, Trees(events), events, sync: _ =>
        {
            // The rebase: another machine's take of q1 reached the remote first, so the decline is a conflict and the take stands.
            ledger.Take("q1", "none");
            return SyncReport.Clean;
        });

        var outcome = await WorkAbandoning.AbandonAsync(world, WorkScope.Ask, "a1", Reason, null, PluginEvents.Terminal);

        Assert.Equal(
            ["q1:lost", "q2:confirmed", "q3:confirmed", "q6:unconfirmed"],
            outcome.Declines.Select(each => $"{each.Quest}:{each.Answer}").Order(StringComparer.Ordinal));
        var said = string.Join('\n', WorkAbandoning.Lines(outcome));
        Assert.Contains("#q1 was taken on another machine before your decline reached the remote; it stays theirs, and the conflict is on the quest.", said);
        Assert.Contains("#q6's decline is unconfirmed: it travels on the next sync, where the same rule applies.", said);
    }

    /// <summary>A pass that does not reach its remote leaves every shared decline unconfirmed; a workspace with no remote answers none.</summary>
    [Fact]
    public async Task A_pass_that_failed_leaves_each_decline_unconfirmed_and_no_remote_answers_none()
    {
        Landed();
        var events = new List<string>();
        using var service = Family().Client();

        var failed = await WorkAbandoning.AbandonAsync(
            World(service, Trees(events), events, sync: _ => new SyncReport("the remote did not answer")),
            WorkScope.Ask, "a1", Reason, null, PluginEvents.Terminal);

        Assert.All(failed.Declines, each => Assert.Equal(DeclineAnswer.Unconfirmed, each.Answer));
        Assert.Equal(4, failed.Declines.Count);

        using var unwired = Family().Client();
        var local = await WorkAbandoning.AbandonAsync(
            World(unwired, Trees(events), events, sync: _ => null), WorkScope.Ask, "a1", Reason, null, PluginEvents.Terminal);
        Assert.Empty(local.Declines);
    }

    /// <summary>A second press with no reason is refused (<c>WORK_REASON</c>), and nothing is written or moved.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_second_press_without_a_reason_is_refused_and_nothing_moves(string? reason)
    {
        var events = new List<string>();
        var ledger = Family();
        using var service = ledger.Client();

        var outcome = await WorkAbandoning.AbandonAsync(World(service, Trees(events), events), WorkScope.Ask, "a1", reason, null, PluginEvents.Screen);

        Assert.Equal(AbandonVerdict.Reason, outcome.Verdict);
        Assert.Empty(events);
        Assert.Empty(ledger.Declines);
        Assert.False(File.Exists(ConfigPath));
        Assert.False(File.Exists(new AbandonRecord(_home).FilePath));
    }

    /// <summary>An ask this machine has no record of, or a quest not here, is <c>WORK_UNKNOWN</c> in both doors' words.</summary>
    [Theory]
    [InlineData(WorkScope.Ask, "zz", "no ask #zz on this machine.")]
    [InlineData(WorkScope.Quest, "q99", "no quest #q99 here.")]
    public async Task An_unknown_ask_or_quest_is_refused_at_both_presses(WorkScope scope, string id, string said)
    {
        var events = new List<string>();
        using var service = Family().Client();
        var output = new StringWriter();
        var world = World(service, Trees(events), events);

        var listed = await WorkCommand.RunAsync(new WorkAsk(scope, "abandon", id), world, output);
        var pressed = await WorkCommand.RunAsync(new WorkAsk(scope, "abandon", id) { Reason = Reason, Yes = true }, world, output);

        Assert.Equal((1, 1), (listed, pressed));
        Assert.Contains($"daoris-driver: {said}", output.ToString());
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>
    /// A work with nothing left to take is said, never refused (D48 §6): the first press offers only closing the ask, and the
    /// second abandons nothing and writes nothing.
    /// </summary>
    [Fact]
    public async Task A_work_with_nothing_left_to_take_says_so_and_writes_nothing()
    {
        var events = new List<string>();
        var ledger = new Ledger([], [Quest("q4c", "Done", from: "ask #c3")], new Dictionary<string, string>(), ["c3"]);
        using var service = ledger.Client();
        var output = new StringWriter();
        var world = World(service, Trees(events), events);

        var listed = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "abandon", "c3"), world, output);
        var outcome = await WorkAbandoning.AbandonAsync(world, WorkScope.Ask, "c3", Reason, null, PluginEvents.Terminal);

        Assert.Equal(0, listed);
        Assert.Contains("nothing of ask #c3 is left to abandon on this machine", output.ToString());
        Assert.Equal(AbandonVerdict.Nothing, outcome.Verdict);
        Assert.False(File.Exists(ConfigPath));
        Assert.False(File.Exists(new AbandonRecord(_home).FilePath));
        Assert.Empty(ledger.Declines);
    }

    /// <summary>
    /// A step that fails keeps what is left and says so (design §3.4): a session another process runs took no request in
    /// time, so its quest is not declined, its tree is not discarded, and the scope stays paused; the terminal exits 2.
    /// </summary>
    [Fact]
    public async Task A_session_it_could_not_stop_keeps_its_quest_and_tree_and_the_scope_paused()
    {
        Landed();
        var events = new List<string>();
        var ledger = Family();
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(_home, "sessions", "w0rk1ng0.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
        using var service = ledger.Client();
        var output = new StringWriter();
        var world = World(service, Trees(events), events) with { Wait = TimeSpan.FromMilliseconds(200) };

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "abandon", "a1") { Reason = Reason, Yes = true }, world, output);

        Assert.Equal(2, exit);
        Assert.Equal(("working", "Taken"), (ledger.State("w0rk1ng0"), ledger.Status("q2")));
        Assert.DoesNotContain("discard:daoris/s-aaaaaaaa", events);
        Assert.Equal("Declined", ledger.Status("q1"));
        Assert.NotNull(DriverConfig.Load(ConfigPath).PausedAsk("a1"));
        var said = output.ToString();
        Assert.Contains("could not stop w0rk1ng0", said);
        Assert.Contains("ask #a1 stays paused", said);
        Assert.Contains("daoris-driver ask --abandon a1", said);
        Assert.Contains("daoris-driver ask --resume a1", said);
    }

    /// <summary>One quest's work: the quest and the question its session asked, its session and its tree, and no ask to close.</summary>
    [Fact]
    public async Task A_quests_work_is_the_quest_and_what_its_sessions_asked_and_closes_no_ask()
    {
        var events = new List<string>();
        var ledger = Family();
        using var service = ledger.Client();

        var plan = (await WorkAbandoning.PlanAsync(World(service, Trees(events), events), WorkScope.Quest, "q2"))!;
        var outcome = await WorkAbandoning.AbandonAsync(World(service, Trees(events), events), WorkScope.Quest, "q2", Reason, null, PluginEvents.Terminal);

        Assert.Null(plan.Closes);
        Assert.Equal(
            ["quest:q2", "quest:q6", "session:w0rk1ng0", "tree:engine:daoris/s-aaaaaaaa"],
            plan.Pieces.Order(StringComparer.Ordinal));
        Assert.Equal(["q2", "q6"], outcome.Declined.Order(StringComparer.Ordinal));
        Assert.False(outcome.Closed);
        Assert.Equal(("Published", (string?)null), ledger.Ask("a1"));
        Assert.Equal("Open", ledger.Status("q1"));
        Assert.Null(DriverConfig.Load(ConfigPath).PausedQuest("q2"));
    }

    /// <summary>
    /// The terminal's first press prints every piece and each keep with its reason, in English, and changes nothing: no path,
    /// a tree by its repository, branch and counts, up to five uncommitted files named relative to the repository.
    /// </summary>
    [Fact]
    public async Task The_terminals_first_press_lists_each_piece_and_changes_nothing()
    {
        Landed();
        var events = new List<string>();
        var ledger = Family();
        using var service = ledger.Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "abandon", "a1"), World(service, Trees(events), events), output);

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, exit);
        Assert.StartsWith(
            "daoris-driver: abandoning ask #a1 would take 11 pieces and keep 9; "
            + "`daoris-driver ask --abandon a1 --reason \"…\" --yes` abandons what this list holds.\n",
            said);
        Assert.Contains("  declines #q1: it applies only while it is open.", said);
        Assert.Contains("  stops w0rk1ng0 on #q2, then archives it.", said);
        Assert.Contains("  ends p4rk3d00 on #q3 unanswered, then archives it.", said);
        Assert.Contains("  discards engine daoris/s-aaaaaaaa with its tree: 3 commit(s) and 2 uncommitted file(s) (src/a.cs, README.md).", said);
        Assert.Contains("  deletes engine daoris/s-bbbbbbbb, whose tree is gone: 1 commit(s).", said);
        Assert.Contains("  closes ask #a1 with your reason.", said);
        Assert.Contains("  keeps #q5: taken on laptop: its work is theirs; decline it on its page if you mean to stop it there.", said);
        Assert.Contains("  keeps #q8: taken here outside Daoris: its work is wherever its taker works, not in a tree Daoris made.", said);
        Assert.Contains("  keeps #q4: done: finished work keeps its record.", said);
        Assert.Contains("  keeps engine daoris/s-cccccccc: its commits are on `origin/daoris/s-cccccccc`.", said);
        Assert.Contains("  keeps engine daoris/s-dddddddd: a landing took its work.", said);
        Assert.Contains("  keeps engine daoris/s-eeeeeeee: git could not say whether its work is anywhere else.", said);
        Assert.Contains("  keeps d0ne0000 to review: its tree keeps work.", said);
        Assert.DoesNotContain(_home.Replace('\\', '/'), said.Replace('\\', '/'));
        Assert.Empty(events);
        Assert.Empty(ledger.Declines);
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>
    /// The terminal's second press says what went, how a discarded branch comes back while git keeps its commits, and each
    /// shared decline's answer.
    /// </summary>
    [Fact]
    public async Task The_terminals_second_press_says_what_went_and_how_a_branch_comes_back()
    {
        Landed();
        var events = new List<string>();
        using var service = Family().Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(
            new WorkAsk(WorkScope.Ask, "abandon", "a1") { Reason = Reason, Yes = true }, World(service, Trees(events), events), output);

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, exit);
        Assert.StartsWith("daoris-driver: abandoned ask #a1: 11 of 11 pieces.\n", said);
        Assert.Contains("  declined #q1, #q2, #q3, #q6 with your reason.", said);
        Assert.Contains("  closed ask #a1 with your reason.", said);
        Assert.Contains(
            "  discarded engine daoris/s-aaaaaaaa at 9f3e2a1aaaa: `git branch daoris/s-aaaaaaaa 9f3e2a1aaaa`, in engine, brings it back while git keeps its commits.",
            said);
        Assert.Contains("  #q1's decline was confirmed by the remote.", said);
        Assert.DoesNotContain("stays paused", said);
    }

    /// <summary>`--yes` without `--reason` is refused before anything is asked: the reason is what each declined quest keeps.</summary>
    [Fact]
    public async Task Yes_without_a_reason_is_refused_and_nothing_moves()
    {
        var events = new List<string>();
        var ledger = Family();
        using var service = ledger.Client();
        var output = new StringWriter();

        var exit = await WorkCommand.RunAsync(new WorkAsk(WorkScope.Ask, "abandon", "a1") { Yes = true }, World(service, Trees(events), events), output);

        Assert.Equal(1, exit);
        Assert.Contains($"daoris-driver: {WorkAbandoning.NeedsReason}", output.ToString());
        Assert.Empty(ledger.Declines);
        Assert.False(File.Exists(ConfigPath));
    }

    public static TheoryData<WorkScope, string[]> Problems => new()
    {
        { WorkScope.Ask, ["--abandon"] },
        { WorkScope.Ask, ["--abandon", "a1", "--reason"] },
        { WorkScope.Ask, ["--abandon", "a1", "--now"] },
        { WorkScope.Ask, ["--abandon", "a1", "a2"] },
        { WorkScope.Quest, ["abandon"] },
        { WorkScope.Quest, ["abandon", "--yes"] },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_an_abandon_does_not_take_are_a_problem(WorkScope scope, string[] args)
    {
        Assert.True(WorkCommand.Asks(scope, args));
        Assert.Null(WorkCommand.Read(scope, args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void The_words_name_an_abandon_its_reason_and_its_yes()
    {
        Assert.Equal(new WorkAsk(WorkScope.Ask, "abandon", "a1"), WorkCommand.Read(WorkScope.Ask, ["--abandon", "#a1"], out _));
        var pressed = WorkCommand.Read(WorkScope.Ask, ["--abandon", "a1", "--reason", Reason, "--yes"], out _)!;
        Assert.Equal(("abandon", "a1", Reason, true), (pressed.Verb, pressed.Id, pressed.Reason, pressed.Yes));
        var quest = WorkCommand.Read(WorkScope.Quest, ["abandon", "q1", "--yes", "--reason", "no longer wanted"], out _)!;
        Assert.Equal((WorkScope.Quest, "q1", "no longer wanted", true), (quest.Scope, quest.Id, quest.Reason, quest.Yes));
    }

    /// <summary>The proof and the discard, as a test hands them in: answers by branch, and each discard heard in order.</summary>
    private sealed class FakeTrees(List<string> events) : IWorkTrees
    {
        public Dictionary<string, TreeOnlyHere> Judged { get; } = new(StringComparer.Ordinal);

        /// <summary>What each tree holds for its review, by its folder's name.</summary>
        public Dictionary<string, TreeWork?> Work { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, (string? Base, string? Root)> Asked { get; } = new(StringComparer.Ordinal);

        public Task<TreeOnlyHere> OnlyHereAsync(string tree, string branch, string? baseCommit, string? root, CancellationToken ct = default)
        {
            Asked[branch] = (baseCommit, root);
            return Task.FromResult(Judged.GetValueOrDefault(branch) ?? new TreeOnlyHere(OnlyHereKind.Gone));
        }

        public Task<TreeRemoval> DiscardAsync(string tree, string branch, TreeOnlyHere judged, CancellationToken ct = default)
        {
            events.Add($"discard:{branch}");
            Judged[branch] = new TreeOnlyHere(OnlyHereKind.Gone);
            return Task.FromResult(new TreeRemoval(true, $"removed `{branch}`."));
        }

        public Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default) =>
            Task.FromResult(Work.GetValueOrDefault(tree.Replace('\\', '/').Split('/')[^1]));
    }

    private sealed class TreeComparer : IEqualityComparer<AbandonedTree>
    {
        public bool Equals(AbandonedTree? x, AbandonedTree? y) =>
            x is not null && y is not null
            && (x.Repository, x.Branch, x.Tip, x.Commits, x.Uncommitted, x.Alone) == (y.Repository, y.Branch, y.Tip, y.Commits, y.Uncommitted, y.Alone)
            && x.Sessions.SequenceEqual(y.Sessions);

        public int GetHashCode(AbandonedTree obj) => obj.Branch.GetHashCode(StringComparison.Ordinal);
    }

    /// <summary>
    /// The service's doors an abandon reads and moves: the quests, closed ones too; the records, live or all; the registry;
    /// the asks; this machine's claim on a quest; a record's move; a decline; an ask's close; a workspace's standing.
    /// </summary>
    private sealed class Ledger(
        IEnumerable<JsonObject> records, IEnumerable<JsonObject> quests, IReadOnlyDictionary<string, string> claims, IEnumerable<string> asks)
        : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _records = [.. records];
        private readonly List<JsonObject> _quests = [.. quests];
        private readonly Dictionary<string, string> _claims = new(claims, StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (string State, string? Note)> _asks = asks.ToDictionary(ask => ask, _ => ("Published", (string?)null));

        /// <summary>Every move, in order: a record's, a decline, a close.</summary>
        public List<string> Events { get; set; } = [];

        public List<(string Quest, string Reason, bool WhileOpen)> Declines { get; } = [];

        /// <summary>Called with a session's id as its record is moved, before the move lands.</summary>
        public Action<string>? Moving { get; set; }

        /// <summary>The quests a workspace's standing names as behind.</summary>
        public IReadOnlyList<string> Behind { get; set; } = [];

        public ServiceClient Client() => new("http://ledger.test", null, new HttpClient(this, disposeHandler: false));

        private JsonObject Of(string id) => _records.Single(each => (string?)each["id"] == id);

        private JsonObject QuestOf(string id) => _quests.Single(each => (string?)each["id"] == id);

        public string State(string id) { lock (_gate) return (string)Of(id)["state"]!; }

        public string? Note(string id) { lock (_gate) return (string?)Of(id)["note"]; }

        public bool Interrupted(string id) { lock (_gate) return (bool?)Of(id)["interrupted"] ?? false; }

        public string Status(string id) { lock (_gate) return (string)QuestOf(id)["status"]!; }

        public (string State, string? Note) Ask(string id) { lock (_gate) return _asks[id]; }

        /// <summary>Another machine's take, or this machine's: the quest taken, and this machine's claim on it as given.</summary>
        public void Take(string id, string claim)
        {
            lock (_gate)
            {
                QuestOf(id)["status"] = "Taken";
                _claims[id] = claim;
            }
        }

        public void Publish(JsonObject quest) { lock (_gate) _quests.Add(quest); }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;
            var all = query.Contains("includeClosed=true", StringComparison.Ordinal);
            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))?.AsObject();
            if (request.Method == HttpMethod.Post && path.EndsWith("/state", StringComparison.Ordinal))
            {
                Moving?.Invoke(Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]));
            }

            lock (_gate)
            {
                static bool Live(JsonObject record) => (string)record["state"]! is "queued" or "starting" or "working" or "awaiting-person";
                JsonObject AskJson(string id) => new()
                {
                    ["id"] = id, ["workspace"] = "default", ["sentence"] = "Add the note field", ["state"] = _asks[id].State,
                    ["tier"] = "named", ["note"] = _asks[id].Note,
                };
                switch (request.Method.Method, path)
                {
                    case ("GET", "/api/sessions"):
                        return Answer(HttpStatusCode.OK, new JsonArray([.. _records.Where(each => all || Live(each)).Select(each => each.DeepClone())]));
                    case ("GET", "/api/quests"):
                        return Answer(HttpStatusCode.OK, new JsonArray([.. _quests
                            .Where(each => all || (string)each["status"]! is "Open" or "Taken").Select(each => each.DeepClone())]));
                    case ("GET", "/api/registry"):
                        return Answer(HttpStatusCode.OK, new JsonArray(
                            new JsonObject { ["repository"] = "engine", ["adopted"] = true, ["root"] = "X:/work/engine", ["workspace"] = "default" }));
                    case ("GET", "/api/asks"):
                        return Answer(HttpStatusCode.OK, new JsonArray([.. _asks.Keys.Select(id => (JsonNode)AskJson(id))]));
                    case ("GET", "/api/sync"):
                        return Answer(HttpStatusCode.OK, new JsonObject
                        {
                            ["workspace"] = "default", ["wired"] = true, ["ahead"] = 0,
                            ["behind"] = new JsonArray([.. Behind.Select(id => (JsonNode)id)]), ["conflicts"] = new JsonArray(),
                        });
                    case ("GET", _) when path.StartsWith("/api/quests/", StringComparison.Ordinal) && path.EndsWith("/claim", StringComparison.Ordinal):
                        var claimed = Uri.UnescapeDataString(path["/api/quests/".Length..^"/claim".Length]);
                        return Answer(HttpStatusCode.OK, new JsonObject { ["quest"] = claimed, ["claim"] = _claims.GetValueOrDefault(claimed, "none") });
                    case ("GET", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal):
                        var ask = Uri.UnescapeDataString(path["/api/asks/".Length..]);
                        return _asks.ContainsKey(ask)
                            ? Answer(HttpStatusCode.OK, AskJson(ask))
                            : Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"No ask `#{ask}`." });
                    case ("POST", _) when path.EndsWith("/state", StringComparison.Ordinal):
                        var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]);
                        var moved = Of(id);
                        moved["state"] = body!["state"]!.GetValue<string>();
                        if (body["note"] is { } note) moved["note"] = note.GetValue<string>();
                        moved["interrupted"] = body["interrupted"]?.GetValue<bool>() ?? false;
                        Events.Add($"state:{id}:{moved["state"]}");
                        return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = moved.DeepClone(), ["message"] = $"Session is now {moved["state"]}." });
                    case ("POST", _) when path.EndsWith("/respond", StringComparison.Ordinal):
                        var questId = Uri.UnescapeDataString(path["/api/quests/".Length..^"/respond".Length]);
                        var quest = QuestOf(questId);
                        var whileOpen = body!["whileOpen"]?.GetValue<bool>() ?? false;
                        if ((string?)body["action"] != "decline")
                        {
                            return Answer(HttpStatusCode.BadRequest, new JsonObject { ["error"] = "the stand-in declines only" });
                        }

                        if (whileOpen && (string)quest["status"]! != "Open")
                        {
                            return Answer(HttpStatusCode.Conflict, new JsonObject { ["error"] = $"Quest `#{questId}` is taken: this decline applies only while it is open." });
                        }

                        quest["status"] = "Declined";
                        quest["note"] = (string?)body["reason"];
                        Declines.Add((questId, (string)body["reason"]!, whileOpen));
                        Events.Add($"decline:{questId}");
                        return Answer(HttpStatusCode.OK, new JsonObject { ["quest"] = quest.DeepClone(), ["message"] = $"Quest `#{questId}` is now Declined." });
                    case ("POST", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal) && path.EndsWith("/close", StringComparison.Ordinal):
                        var closing = Uri.UnescapeDataString(path["/api/asks/".Length..^"/close".Length]);
                        _asks[closing] = ("Closed", (string?)body!["reason"]);
                        Events.Add($"close:{closing}");
                        return Answer(HttpStatusCode.OK, new JsonObject { ["ask"] = AskJson(closing), ["message"] = $"Ask `#{closing}` is closed: {body["reason"]}" });
                    default:
                        return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"the stand-in has no {request.Method} {path}" });
                }
            }
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) => new(status)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
