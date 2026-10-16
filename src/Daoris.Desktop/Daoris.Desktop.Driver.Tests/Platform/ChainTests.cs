using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The chains (FABRIC-CHAIN2, the design §C5 and §C6) on the change watch's fake source, stepped by hand on a clock that stands
/// still. The link table is ranked, and a link pointing to a lower rank is refused; a fact link emits only on a change; an effect
/// link stays pending until done, is tried on its unblocking wakes with its retries bounded, and is recovered at start from the
/// persisted facts; each topic's deadline is a <c>timer</c> event. The design's canaries 4, 11, 12, 16, 17 and 18.
/// </summary>
/// <remarks>
/// Links 1, 2, 4 and 5 are fakes here, over a world of persisted facts that a restart keeps: their facts are FABRIC-END2b's,
/// their ref source FABRIC-END3's, the tidy FABRIC-END4a's and the look's split FABRIC-EVENT3a's. Link 7 is wired on the requests
/// folder today (<see cref="SessionRequestChangeTests"/>).
/// </remarks>
public sealed class ChainTests : IDisposable
{
    private const string CommonDir = "common-dir";

    private const string RequestsFolder = "requests-folder";

    private static readonly TimeSpan RefsDeadline = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan LookDeadline = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RequestsDeadline = TimeSpan.FromSeconds(3);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chain-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    /// <summary>What a restart keeps: the facts recorded, and the branches and trees as git and the ledger have them.</summary>
    private readonly World _world = new();

    private DateTimeOffset _now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    public ChainTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private MachineLog Log() => new(_home, "driver", () => _now);

    /// <summary>The data of each machine-log line named <paramref name="event"/>, oldest first.</summary>
    private JsonElement[] Lines(string @event)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl")
                .SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event)
                .Select(line => line.GetProperty("data")),
        ];
    }

    private static int[] LinksOf(IEnumerable<JsonElement> lines) => [.. lines.Select(line => line.GetProperty("link").GetInt32())];

    /// <summary>Time passes, then one step of the watch, and every judgement it began is waited for.</summary>
    private async Task Step(Rig rig, TimeSpan by)
    {
        _now += by;
        rig.Watch.Pump();
        await rig.Watch.Settled();
    }

    /// <summary>
    /// A loop's chain over the shared world, as a process that started would build it: the refs topic with the common dir as its
    /// root, the requests folder, and the look's topic, which the ledger feeds; links 1, 2, 4, 5 and 7 bound to fakes. Opened, not
    /// pumping. <paramref name="wait"/> is a cycle's wait for the tidy, counted from <c>Held.Since</c> (the owner's answer to §Q1).
    /// </summary>
    private Rig Start(TimeSpan? wait = null)
    {
        var rig = new Rig(this, wait);
        rig.Watch.Open();
        return rig;
    }

    private static ChainEvent RefsMoved() => new(ChainEvents.Refs, "repo", ChangeTopics.Refs);

    // The fixed table and its ranks.

    /// <summary>§C6's seven links, ranked: each event a link may emit is heard only by links ranked above it, so none wakes itself.</summary>
    [Fact]
    public void The_fixed_table_is_ranked_so_that_no_link_hears_what_it_or_a_link_above_it_emits()
    {
        var table = ChainTable.Fixed;

        Assert.Empty(ChainTable.Check(table.Links));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], table.Links.Select(link => link.Rank));
        Assert.Equal(
            [ChainKind.Fact, ChainKind.Effect, ChainKind.Fact, ChainKind.Fact, ChainKind.Reader, ChainKind.Reader, ChainKind.Reader],
            table.Links.Select(link => link.Kind));
        foreach (var link in table.Links)
        {
            foreach (var emitted in link.Emits)
            {
                Assert.All(table.Links.Where(other => other.Hears(emitted)), hearer => Assert.True(hearer.Rank > link.Rank));
            }
        }

        // The tidy is the only effect: woken by its facts' change, its unblocking wakes, its deadline and the start.
        var tidy = Assert.Single(table.Links, link => link.Kind == ChainKind.Effect);
        Assert.Equal(ChainRanks.Tidy, tidy.Rank);
        Assert.Equal([ChainEvents.WorkHeld, ChainEvents.WorkEnded], tidy.On);
        Assert.Equal(
            [ChainEvents.Ledger, ChainEvents.Press, ChainEvents.Home, ChainEvents.TimerOf(ChangeTopics.Refs), ChainEvents.Start],
            tidy.Wakes);
        Assert.Equal([ChainEvents.WorkTidied], tidy.Emits);
    }

    /// <summary>A link that points to a lower rank, or to itself, is refused: the table is not built, and the check names it.</summary>
    [Fact]
    public void A_link_to_a_lower_rank_fails()
    {
        // The plugins' observation (3) made to emit `work.held`, which the tidy (2) hears below it, and the publish (4) above.
        ChainLink[] lower =
        [
            .. ChainTable.Fixed.Links.Select(link => link.Rank == ChainRanks.Ended ? link with { Emits = [ChainEvents.WorkHeld] } : link),
        ];
        var problem = Assert.Single(ChainTable.Check(lower));
        Assert.Contains("link 3", problem, StringComparison.Ordinal);
        Assert.Contains("link 2", problem, StringComparison.Ordinal);
        Assert.Contains(ChainEvents.WorkHeld, problem, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new ChainTable(lower));

        // The tidy made to emit `refs`, which the judgement (1) hears: the loop through the world, drawn into the table.
        ChainLink[] echo =
        [
            .. ChainTable.Fixed.Links.Select(link =>
                link.Rank == ChainRanks.Tidy ? link with { Emits = [ChainEvents.WorkTidied, ChainEvents.Refs] } : link),
        ];
        Assert.Throws<ArgumentException>(() => new ChainTable(echo));

        // A link that hears what it emits wakes itself.
        ChainLink[] itself =
        [
            .. ChainTable.Fixed.Links.Select(link => link.Rank == ChainRanks.Judge ? link with { Emits = [.. link.Emits, ChainEvents.Refs] } : link),
        ];
        Assert.Throws<ArgumentException>(() => new ChainTable(itself));
    }

    /// <summary>The table's other rules: ranks are distinct, a reader emits nothing, and an effect has a deadline and the start.</summary>
    [Fact]
    public void A_table_whose_effect_has_no_deadline_or_start_or_whose_reader_emits_is_refused()
    {
        var links = ChainTable.Fixed.Links;
        Assert.Throws<ArgumentException>(() => new ChainTable(
            [.. links.Select(link => link.Rank == ChainRanks.Tidy ? link with { Wakes = [ChainEvents.Ledger, ChainEvents.Start] } : link)]));
        Assert.Throws<ArgumentException>(() => new ChainTable(
            [.. links.Select(link => link.Rank == ChainRanks.Tidy ? link with { Wakes = [ChainEvents.TimerOf(ChangeTopics.Refs)] } : link)]));
        Assert.Throws<ArgumentException>(() => new ChainTable(
            [.. links.Select(link => link.Rank == ChainRanks.Read ? link with { Emits = [ChainEvents.WorkStep] } : link)]));
        Assert.Throws<ArgumentException>(() => new ChainTable([.. links, links[0] with { Id = "again" }]));
    }

    // Canary 12: no configuration makes a loop.

    /// <summary>
    /// Canary 12: every value a cycle's end may take (§C2) is tried against the link table. The event that reaches each end runs
    /// each link at most once and ends, and its echo, judged again, emits nothing.
    /// </summary>
    [Theory]
    [InlineData("handed", ChainEvents.Refs)]
    [InlineData("pushed", ChainEvents.Refs)]
    [InlineData("pull-request", ChainEvents.PluginStep)]
    [InlineData("line", ChainEvents.Refs)]
    [InlineData("deploy-plugin/deployed", ChainEvents.PluginStep)]
    public async Task No_value_a_cycle_may_take_makes_a_loop(string end, string reachedBy)
    {
        Assert.Empty(ChainTable.Check(ChainTable.Fixed.Links));
        await using var rig = Start();
        _world.Add("u1", onLine: true, reached: end);

        var reached = reachedBy == ChainEvents.Refs ? RefsMoved() : new ChainEvent(reachedBy, "u1");
        await rig.Chain.FireAsync([reached], CancellationToken.None);

        // One wake: the judgement once, the tidy once, the publish once with what both said.
        Assert.Equal(1, rig.Judged);
        Assert.Equal(1, rig.Tidy.Tries);
        Assert.Equal(1, rig.Publishes);
        Assert.Equal(
            new[] { ChainEvents.WorkHeld, ChainEvents.WorkEnded, ChainEvents.WorkTidied }.Order(StringComparer.Ordinal),
            rig.Published.Select(heard => heard.Type).Order(StringComparer.Ordinal));

        // The tidy's ref move heard back: judged once, and nothing follows.
        await rig.Chain.FireAsync([RefsMoved()], CancellationToken.None);
        Assert.Equal(2, rig.Judged);
        Assert.Equal(1, rig.Tidy.Tries);
        Assert.Equal(1, rig.Publishes);
    }

    // Canary 11: a chain that changes nothing wakes nothing.

    /// <summary>
    /// Canary 11 through the queue: a judgement that finds what it found emits nothing; a change runs the tidy once; and the tidy's
    /// own ref move, heard back by the source, is judged once and fires nothing, since the removed unit is a trace (§C6).
    /// </summary>
    [Fact]
    public async Task A_chain_that_changes_nothing_wakes_nothing_and_the_tidys_echo_is_judged_once()
    {
        await using var rig = Start();
        rig.Chain.Log = Log();
        _world.Add("u1", onLine: false);
        var refs = _source.On(CommonDir)!;

        refs.Raise("refs/heads/main", ChangeKind.Changed);
        await Step(rig, ChangeWatch.Quiet);
        Assert.Equal(1, rig.Judged);
        Assert.Empty(Lines("chain.fired"));

        // The work lands on the line: one change, one tidy, one publish.
        _world.Units["u1"].OnLine = true;
        refs.Raise("refs/heads/main", ChangeKind.Changed);
        await Step(rig, ChangeWatch.Quiet);
        Assert.Equal(2, rig.Judged);
        Assert.Equal(1, rig.Tidy.Done);
        Assert.False(_world.Units["u1"].Branch);
        Assert.NotNull(_world.Units["u1"].GoneAt);
        Assert.Equal([ChainRanks.Judge, ChainRanks.Tidy, ChainRanks.Publish], LinksOf(Lines("chain.fired")));

        // The tidy deleted the branch and the source heard it: judged once, and nothing fired.
        await Step(rig, ChangeWatch.Quiet);
        Assert.Equal(3, rig.Judged);
        Assert.Equal(1, rig.Tidy.Tries);
        Assert.Equal(1, rig.Publishes);
        Assert.Equal([ChainRanks.Judge, ChainRanks.Tidy, ChainRanks.Publish], LinksOf(Lines("chain.fired")));

        // And the chain is quiet: nothing more is due.
        await Step(rig, ChangeWatch.Cap);
        Assert.Equal(3, rig.Judged);
        Assert.False(rig.Watch.Judging);
    }

    /// <summary>An emission outside its link's row is dropped, never heard, and said as <c>chain.dropped</c>.</summary>
    [Fact]
    public async Task An_emission_outside_its_row_is_dropped()
    {
        await using var rig = Start();
        rig.Chain.Log = Log();
        rig.Undeclared = new ChainEvent(ChainEvents.WorkTidied, "u1");
        _world.Add("u1", onLine: false);

        await rig.Chain.FireAsync([RefsMoved()], CancellationToken.None);

        Assert.Equal(1, rig.Judged);
        Assert.Equal(0, rig.Publishes);
        var dropped = Assert.Single(Lines("chain.dropped"));
        Assert.Equal("undeclared", dropped.GetProperty("code").GetString());
        Assert.Equal(ChainRanks.Judge, dropped.GetProperty("link").GetInt32());
        Assert.Equal(ChainEvents.WorkTidied, dropped.GetProperty("event").GetString());
    }

    // Canary 4: more wakes are not more full looks.

    /// <summary>
    /// Canary 4: a thousand hints on the refs and the requests run their own links, coalesced, and never the look (link 5) nor its
    /// remote sync; a ledger wake runs the look once.
    /// </summary>
    [Fact]
    public async Task A_thousand_hints_run_no_look_and_no_remote_sync()
    {
        await using var rig = Start();
        var refs = _source.On(CommonDir)!;
        var requests = _source.On(RequestsFolder)!;

        for (var i = 0; i < 1000; i++)
        {
            if (i % 2 == 0) refs.Raise($"refs/heads/b{i}");
            else requests.Raise($"r{i}.json");
            await Step(rig, TimeSpan.FromMilliseconds(10));
        }

        await Step(rig, ChangeWatch.Cap);
        Assert.Equal(0, rig.Looks);
        Assert.Equal(0, rig.Syncs);
        Assert.InRange(rig.Judged, 1, 10);
        Assert.InRange(rig.Reads, 1, 10);

        rig.Chain.Hint(ChangeTopics.Look, "sessions");
        await Step(rig, ChangeWatch.Quiet);
        Assert.Equal(1, rig.Looks);
    }

    // Canary 17: busy, then free.

    /// <summary>
    /// Canary 17: the tidy refused for a busy tree stays pending, said as <c>chain.pending</c>; a wake that cannot unblock it and a
    /// fact that stands do not try it again, a deadline tries it at most once, and the session leaving the tree runs it, with no
    /// fact having changed.
    /// </summary>
    [Fact]
    public async Task A_tidy_refused_for_a_busy_tree_runs_when_the_session_leaves_it()
    {
        await using var rig = Start();
        rig.Chain.Log = Log();
        _world.Add("u1", onLine: true, busy: true);

        await rig.Chain.FireAsync([RefsMoved()], CancellationToken.None);
        Assert.Equal(1, rig.Tidy.Tries);
        Assert.True(_world.Units["u1"].Branch);
        var pending = Assert.Single(Lines("chain.pending"));
        Assert.Equal(ChainRanks.Tidy, pending.GetProperty("link").GetInt32());
        Assert.Equal("refused", pending.GetProperty("code").GetString());
        Assert.Equal(1, pending.GetProperty("count").GetInt32());

        // The window's focus unblocks nothing, and the refs judged again find what they found.
        await rig.Chain.FireAsync([new ChainEvent(ChainEvents.Focus, "window")], CancellationToken.None);
        await rig.Chain.FireAsync([RefsMoved()], CancellationToken.None);
        Assert.Equal(1, rig.Tidy.Tries);

        // A deadline within the one it was tried in does not try it; the next tries it once, and a second of the same topic
        // (another subject's) before the one after does not.
        await rig.Chain.FireAsync([ChainEvent.Timer(ChangeTopics.Refs)], CancellationToken.None);
        Assert.Equal(1, rig.Tidy.Tries);
        _now += RefsDeadline;
        await rig.Chain.FireAsync([ChainEvent.Timer(ChangeTopics.Refs, "repo")], CancellationToken.None);
        await rig.Chain.FireAsync([ChainEvent.Timer(ChangeTopics.Refs)], CancellationToken.None);
        Assert.Equal(2, rig.Tidy.Tries);

        // The session leaves the tree: the ledger moves, no fact the judgement reads changed, and the tidy runs.
        _world.Units["u1"].Busy = false;
        var judged = rig.Judged;
        await rig.Chain.FireAsync([new ChainEvent(ChainEvents.Ledger, "sessions", ChangeTopics.Look)], CancellationToken.None);
        Assert.Equal(judged, rig.Judged);
        Assert.Equal(3, rig.Tidy.Tries);
        Assert.Equal(1, rig.Tidy.Done);
        Assert.False(_world.Units["u1"].Branch);
        Assert.Contains(rig.Published, heard => heard == new ChainEvent(ChainEvents.WorkTidied, "u1"));
    }

    // Canary 16: a restart between a fact and its effect.

    /// <summary>
    /// Canary 16: the fact recorded, the process killed before the tidy, and the next start's reconciliation finds the pending tidy
    /// from the persisted facts and performs it once; a later start finds nothing pending.
    /// </summary>
    [Fact]
    public async Task A_restart_between_a_fact_and_its_effect_performs_the_pending_effect_once()
    {
        _world.Add("u1", onLine: true);
        await using (var before = Start())
        {
            using var kill = new CancellationTokenSource();
            before.KillAfterFact = kill;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => before.Chain.FireAsync([RefsMoved()], kill.Token));
            Assert.Equal(0, before.Tidy.Tries);
        }

        Assert.True(_world.Units["u1"].Held);
        Assert.True(_world.Units["u1"].Branch);

        await using (var after = Start())
        {
            after.Chain.Log = Log();
            await after.Chain.RecoverAsync(CancellationToken.None);
            Assert.Equal(1, after.Tidy.Tries);
            Assert.Equal(1, after.Tidy.Done);
            Assert.False(_world.Units["u1"].Branch);
            var recovered = Assert.Single(Lines("chain.pending"));
            Assert.Equal("recovered", recovered.GetProperty("code").GetString());
            Assert.Equal(1, recovered.GetProperty("count").GetInt32());

            // Its echo and its deadline find a trace: nothing is tried again.
            await after.Chain.FireAsync([RefsMoved()], CancellationToken.None);
            await after.Chain.FireAsync([ChainEvent.Timer(ChangeTopics.Refs)], CancellationToken.None);
            Assert.Equal(1, after.Tidy.Tries);
        }

        await using var later = Start();
        await later.Chain.RecoverAsync(CancellationToken.None);
        Assert.Equal(0, later.Tidy.Tries);
    }

    // Canary 18: a deadline expiring.

    /// <summary>Canary 18: with every source silent, each topic is judged whole at its deadline, as a <c>timer</c> event naming it.</summary>
    [Fact]
    public async Task With_every_source_silent_each_topic_is_judged_whole_at_its_deadline()
    {
        await using var rig = Start();

        await Step(rig, RequestsDeadline);
        Assert.Equal(1, rig.Reads);
        Assert.Equal(0, rig.Looks);
        Assert.Equal(0, rig.Judged);
        Assert.Equal(ChainEvent.Timer(ChangeTopics.Requests), Assert.Single(rig.ReadBy));

        await Step(rig, LookDeadline - RequestsDeadline);
        Assert.Equal(1, rig.Looks);
        Assert.Equal(ChainEvent.Timer(ChangeTopics.Look), Assert.Single(rig.LookedBy));
        Assert.Equal(0, rig.Judged);

        await Step(rig, RefsDeadline - LookDeadline);
        Assert.InRange(rig.Judged, 1, 2);
        Assert.All(rig.JudgedBy, cause => Assert.Equal(ChainEvents.TimerOf(ChangeTopics.Refs), cause.Key));
        Assert.Contains(ChainEvent.Timer(ChangeTopics.Refs), rig.JudgedBy);
    }

    /// <summary>
    /// Canary 18's second half: a wait counted from <c>Held.Since</c> survives a restart unchanged. The chain keeps no origin of
    /// its own: the effect says from the persisted facts when its unit is first due, so a restart neither resets nor shortens it.
    /// </summary>
    [Fact]
    public async Task A_wait_counted_from_held_since_survives_a_restart_unchanged()
    {
        var wait = TimeSpan.FromMinutes(10);
        var since = _now;
        _world.Add("u1", onLine: true);
        await using (var before = Start(wait))
        {
            await before.Chain.FireAsync([RefsMoved()], CancellationToken.None);
            Assert.Equal(since, _world.Units["u1"].HeldSince);
            Assert.Equal(0, before.Tidy.Tries);
        }

        _now += TimeSpan.FromMinutes(6);
        await using var after = Start(wait);
        await after.Chain.RecoverAsync(CancellationToken.None);
        Assert.Equal(0, after.Tidy.Tries);
        Assert.Equal(since, _world.Units["u1"].HeldSince);

        // The refs topic's deadline, five minutes after this start, is past the wait counted from the first holding: tried once.
        await Step(after, RefsDeadline);
        Assert.Equal(1, after.Tidy.Tries);
        Assert.Equal(1, after.Tidy.Done);
        Assert.Equal(since, _world.Units["u1"].HeldSince);
    }

    /// <summary>The facts a restart keeps, and the truth git and the ledger would say.</summary>
    private sealed class World
    {
        public Dictionary<string, Unit> Units { get; } = new(StringComparer.Ordinal);

        public void Add(string name, bool onLine, bool busy = false, string? reached = null) =>
            Units[name] = new Unit { OnLine = onLine, Busy = busy, Reached = reached is not null };
    }

    private sealed class Unit
    {
        /// <summary>Git's truth: the line holds the unit's tip.</summary>
        public bool OnLine { get; set; }

        /// <summary>The cycle's end reached, on its evidence.</summary>
        public bool Reached { get; set; }

        /// <summary>A session works in the unit's tree.</summary>
        public bool Busy { get; set; }

        public bool Branch { get; set; } = true;

        /// <summary>The kept fact: <c>Held</c>, as END2b keeps it.</summary>
        public bool Held { get; set; }

        /// <summary>The kept fact: the end judged reached.</summary>
        public bool Ended { get; set; }

        public DateTimeOffset? HeldSince { get; set; }

        /// <summary>Set at the removal: the unit is a trace, never judged again.</summary>
        public DateTimeOffset? GoneAt { get; set; }
    }

    /// <summary>A loop's chain and its fakes, counting what each link did.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly ChainTests _test;

        public Rig(ChainTests test, TimeSpan? wait)
        {
            _test = test;
            Watch = new ChangeWatch(test._source, () => test._now);
            Tidy = new FakeTidy(test, wait);
            Chain = new Chain(Watch, clock: () => test._now)
                .Bind(ChainRanks.Judge, JudgeAsync)
                .Bind(ChainRanks.Tidy, Tidy)
                .Bind(ChainRanks.Publish, PublishAsync)
                .Bind(ChainRanks.Plan, LookAsync)
                .Bind(ChainRanks.Read, ReadAsync);
            Chain.Topic(ChangeTopics.Refs, ChainEvents.Refs, RefsDeadline);
            Chain.Topic(ChangeTopics.Requests, ChainEvents.Home, RequestsDeadline);
            Chain.Topic(ChangeTopics.Look, ChainEvents.Ledger, LookDeadline);
            Watch.Watch(ChangeTopics.Refs, "repo", CommonDir, (_, _) => true, subfolders: true);
            Watch.Watch(
                ChangeTopics.Requests, ChangeTopics.Requests, RequestsFolder,
                (name, kind) => kind != ChangeKind.Deleted && name.EndsWith(".json", StringComparison.Ordinal));
        }

        public ChangeWatch Watch { get; }

        public Chain Chain { get; }

        public FakeTidy Tidy { get; }

        public int Judged { get; private set; }

        public List<ChainEvent> JudgedBy { get; } = [];

        public int Publishes { get; private set; }

        public List<ChainEvent> Published { get; } = [];

        public int Looks { get; private set; }

        public List<ChainEvent> LookedBy { get; } = [];

        /// <summary>Today's look syncs with the remote before it plans (Driver.cs:332): one per look.</summary>
        public int Syncs { get; private set; }

        public int Reads { get; private set; }

        public List<ChainEvent> ReadBy { get; } = [];

        /// <summary>An event the judgement adds to what it emits, outside its row.</summary>
        public ChainEvent? Undeclared { get; set; }

        /// <summary>Cancelled once the judgement has recorded its facts: the process killed before the effect.</summary>
        public CancellationTokenSource? KillAfterFact { get; set; }

        /// <summary>Link 1 on fakes: a standing unit's facts judged against the kept ones, emitting only what changed.</summary>
        private Task<ChainFired> JudgeAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
        {
            var emitted = new List<ChainEvent>();
            lock (_test._world)
            {
                Judged++;
                JudgedBy.AddRange(heard);
                foreach (var (name, unit) in _test._world.Units)
                {
                    // A trace is never judged again (§C6): its branch's going never reads as its end lost.
                    if (unit.GoneAt is not null) continue;
                    if (unit.OnLine != unit.Held)
                    {
                        unit.Held = unit.OnLine;
                        unit.HeldSince = unit.Held ? unit.HeldSince ?? _test._now : null;
                        emitted.Add(new ChainEvent(ChainEvents.WorkHeld, name));
                    }

                    if (unit.Reached != unit.Ended)
                    {
                        unit.Ended = unit.Reached;
                        emitted.Add(new ChainEvent(ChainEvents.WorkEnded, name));
                    }
                }
            }

            KillAfterFact?.Cancel();
            if (Undeclared is not null) emitted.Add(Undeclared);
            return Task.FromResult(ChainFired.Of(emitted));
        }

        private Task<ChainFired> PublishAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
        {
            lock (Published)
            {
                Publishes++;
                Published.AddRange(heard);
            }

            return Task.FromResult(new ChainFired(heard.Count, []));
        }

        private Task<ChainFired> LookAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
        {
            lock (LookedBy)
            {
                Looks++;
                Syncs++;
                LookedBy.AddRange(heard);
            }

            return Task.FromResult(new ChainFired(1, []));
        }

        private Task<ChainFired> ReadAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
        {
            lock (ReadBy)
            {
                Reads++;
                ReadBy.AddRange(heard);
            }

            return Task.FromResult(ChainFired.Nothing);
        }

        public ValueTask DisposeAsync() => Watch.DisposeAsync();
    }

    /// <summary>
    /// Link 2 on fakes: pending while <c>Held</c> stands and the branch is still there, refused while a session works in the tree;
    /// done, it deletes the branch, leaves a trace, and the source hears the deletion.
    /// </summary>
    private sealed class FakeTidy(ChainTests test, TimeSpan? wait) : IChainEffect
    {
        public int Tries { get; private set; }

        public int Done { get; private set; }

        public Task<IReadOnlyList<ChainPending>> PendingAsync(CancellationToken ct)
        {
            lock (test._world)
            {
                IReadOnlyList<ChainPending> pending =
                [
                    .. test._world.Units
                        .Where(pair => pair.Value is { Held: true, Branch: true, GoneAt: null })
                        .Select(pair => new ChainPending(pair.Key, wait is { } after ? pair.Value.HeldSince + after : null)),
                ];
                return Task.FromResult(pending);
            }
        }

        public Task<ChainTry> TryAsync(string unit, CancellationToken ct)
        {
            lock (test._world)
            {
                Tries++;
                var kept = test._world.Units[unit];
                if (kept.Busy) return Task.FromResult(ChainTry.Refused);
                kept.Branch = false;
                kept.GoneAt = test._now;
                Done++;
            }

            test._source.On(CommonDir)?.Raise($"refs/heads/{unit}", ChangeKind.Deleted);
            return Task.FromResult(ChainTry.Done(new ChainEvent(ChainEvents.WorkTidied, unit)));
        }
    }
}
