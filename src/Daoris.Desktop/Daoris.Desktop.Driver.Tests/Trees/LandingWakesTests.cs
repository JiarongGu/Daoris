using System.Collections.Concurrent;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Auto-landing on its wakes (FABRIC-END7, the fabric events design §A3, §B4; D173's END3, EVENT3a, EVENT3b and EVENT4 notes): a
/// wake looks only at the due entries its topic can release, found without git, and the whole look stays their deadline. Each
/// release path's wake is here on files and fakes; <see cref="AutoLandingWakesTests"/> counts the git a wake starts over real git.
/// </summary>
public sealed class LandingWakesTests : IDisposable
{
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Moved = "2222222222222222222222222222222222222222";

    private static readonly DateTimeOffset At = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-landing-wakes-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    public LandingWakesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static AutoLanding Entry(string? code, bool tipKept, string repository = "engine", string tree = "tree") =>
        new("s1", "q1", repository, "aurora", tree, At)
        {
            Tries = code is null ? [] : [new AutoTry(At, code) { Tip = tipKept ? Tip : null, Status = tipKept ? "clean" : null }],
        };

    /// <summary>
    /// The table: what each due entry waits on, read from its last try, and which wakes can release it. A tree's tip that has not
    /// moved is no release, whatever the wake.
    /// </summary>
    [Theory]
    // No try yet: every wake looks at it.
    [InlineData(null, false, "ledger", "sessions", true)]
    [InlineData(null, false, "refs", "deck", true)]
    // FABRIC-CLOSE1h6: a closed try on an entry open again was the earlier due's; due again by a new conclusion, every wake looks at it.
    [InlineData("landed", false, "ledger", "sessions", true)]
    [InlineData("undone", false, "refs", "deck", true)]
    // Held for the person's yes or evidence's verdict, and the review's gate: the quests family, and the configuration.
    [InlineData("held", false, "ledger", "quests", true)]
    [InlineData("held", false, "ledger", "sessions,quests", true)]
    [InlineData("held", false, "ledger", "sessions", false)]
    [InlineData("held", false, "refs", "engine", false)]
    [InlineData("held", false, "gates", "gates", false)]
    [InlineData("held", false, "requests", "s9", false)]
    [InlineData("held", false, "config", "driver.json", true)]
    [InlineData("unreviewed", false, "ledger", "quests", true)]
    [InlineData("unreviewed", false, "ledger", "sessions", false)]
    [InlineData("unreviewed", false, "refs", "engine", false)]
    // FABRIC-CLOSE1h7: a push held back after its branch was made keeps its tip, but waits on its quest standing again (the hold's
    // release, a reopen, the next submission), so the quests family and the configuration look at it, and the tree's refs do not.
    // FABRIC-CLOSE1h12: and on its landing's gate letting go, as an opinion's and a workflow's wait: any ledger family (the reviewer's
    // session), the gate's files (the person's answer, a run's say-so). FABRIC-CLOSE1h15: the opinion owed on it is asked at the look,
    // which kept the ask in the gate's files, and settles as its reviewer's session ends (`sessions`): both rows below wake it.
    [InlineData("push-held", true, "ledger", "quests", true)]
    [InlineData("push-held", true, "ledger", "sessions,quests", true)]
    [InlineData("push-held", true, "config", "driver.json", true)]
    [InlineData("push-held", true, "ledger", "sessions", true)]
    [InlineData("push-held", true, "gates", "gates", true)]
    [InlineData("push-held", true, "refs", "engine", false)]
    [InlineData("push-held", true, "requests", "s9", false)]
    [InlineData("push-held", true, "words", "held-words.json", false)]
    // A second opinion: any ledger family (the reviewer's session and its end), the gate's files (the person's answer), the configuration.
    [InlineData("opinion", false, "ledger", "sessions", true)]
    [InlineData("opinion", false, "ledger", "quests", true)]
    [InlineData("opinion", false, "gates", "gates", true)]
    [InlineData("opinion", false, "refs", "engine", false)]
    [InlineData("opinion", false, "words", "held-words.json", false)]
    // A workflow's hold, kept with no tip: the quests family (the run's chain), the gate's files (a version, a run's say-so), the configuration.
    [InlineData("refused", false, "ledger", "quests", true)]
    [InlineData("refused", false, "gates", "gates", true)]
    [InlineData("refused", false, "config", "driver.json", true)]
    [InlineData("refused", false, "ledger", "sessions", false)]
    [InlineData("refused", false, "refs", "engine", false)]
    // A refusal kept with its tree's tip: only a moved tip releases it, and here the tip stands.
    [InlineData("uncommitted", true, "refs", "engine", false)]
    [InlineData("uncommitted", true, "ledger", "quests", false)]
    [InlineData("exists", true, "refs", "engine", false)]
    [InlineData("completed", true, "gates", "gates", false)]
    [InlineData("refused", true, "refs", "engine", false)]
    [InlineData("uncommitted", true, "config", "driver.json", true)]
    public void A_wake_concerns_the_entries_its_topic_can_release(string? code, bool tipKept, string topic, string subject, bool concerned) =>
        Assert.Equal(concerned, LandingConcerns.Concerns(Entry(code, tipKept), [new LandingWake(topic, subject)], _ => Tip));

    /// <summary>
    /// FABRIC-END7b: a due entry a look left because a live session held its tree keeps no try, so its last try still says what it waits
    /// on; a session's end (the ledger's <c>sessions</c>, the loop's own end among them) concerns it as well, whatever that is. Another
    /// wake concerns it only as its last try says, and an entry no look left so is not a session end's.
    /// </summary>
    [Theory]
    [InlineData("held", false, "ledger", "sessions", true)]
    [InlineData("held", false, "ledger", "quests,sessions", true)]
    [InlineData("unreviewed", false, "ledger", "sessions", true)]
    [InlineData("refused", false, "ledger", "sessions", true)]
    [InlineData("uncommitted", true, "ledger", "sessions", true)]
    [InlineData("held", false, "refs", "engine", false)]
    [InlineData("held", false, "requests", "s9", false)]
    [InlineData("held", false, "gates", "gates", false)]
    [InlineData("held", false, "ledger", "quests", true)]
    public void A_session_s_end_concerns_an_entry_a_look_left_for_a_live_session_on_its_tree(
        string code, bool tipKept, string topic, string subject, bool concerned)
    {
        var entry = Entry(code, tipKept);
        LandingWake[] woke = [new(topic, subject)];

        Assert.Equal(concerned, LandingConcerns.Concerns(entry, woke, _ => Tip, leftLive: _ => true));
        Assert.Equal(LandingConcerns.Concerns(entry, woke, _ => Tip), LandingConcerns.Concerns(entry, woke, _ => Tip, leftLive: _ => false));
    }

    /// <summary>
    /// FABRIC-END7b: what a look left for a live session is kept per home and per entry until a look judges it past the session, or it is no
    /// longer due; another home's is not this one's.
    /// </summary>
    [Fact]
    public void An_entry_left_for_a_live_session_is_kept_until_judged_past_it_or_no_longer_due()
    {
        var entry = Entry(AutoLandingCode.Held, tipKept: false);
        var other = entry with { Session = "s2" };
        Assert.False(LandingJudged.LeftLive(_home, entry));

        LandingJudged.Live(_home, entry, live: true);
        LandingJudged.Live(_home, other, live: true);
        Assert.True(LandingJudged.LeftLive(_home, entry));
        Assert.False(LandingJudged.LeftLive(Path.Combine(_home, "elsewhere"), entry));

        LandingJudged.Live(_home, entry, live: false);
        Assert.False(LandingJudged.LeftLive(_home, entry));
        LandingJudged.Keep(_home, [entry]);
        Assert.False(LandingJudged.LeftLive(_home, other));
    }

    /// <summary>
    /// A refusal kept with its tree's tip is released by its tree moving: any wake that finds the tip read from git's files moved
    /// looks at it, and a tip those files cannot say is git's to read.
    /// </summary>
    [Fact]
    public void A_tree_whose_tip_moved_is_looked_at_by_any_wake()
    {
        var waiting = Entry(AutoLandingCode.Uncommitted, tipKept: true);
        LandingWake[] refs = [new(ChangeTopics.Refs, "engine")];

        Assert.False(LandingConcerns.Concerns(waiting, refs, _ => Tip));
        Assert.False(LandingConcerns.Concerns(waiting, refs, _ => Tip.ToUpperInvariant()));
        Assert.True(LandingConcerns.Concerns(waiting, refs, _ => Moved));
        Assert.True(LandingConcerns.Concerns(waiting, refs, _ => null));
        Assert.True(LandingConcerns.Concerns(waiting, [new(ChangeTopics.Ledger, "sessions")], _ => Moved));
        Assert.Equal(LandingWaits.Tree, LandingWaits.Of(waiting));
    }

    /// <summary>The whole look (no wakes) is every entry's deadline: it looks at each, whatever it waits on.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("held", false)]
    [InlineData("push-held", true)]
    [InlineData("opinion", false)]
    [InlineData("refused", false)]
    [InlineData("uncommitted", true)]
    public void The_whole_look_looks_at_every_entry(string? code, bool tipKept) =>
        Assert.True(LandingConcerns.Concerns(Entry(code, tipKept), woke: null, _ => Tip));

    // ——— the tree's tip, read from git's files with no process

    private string Git(string root) => Path.Combine(root, ".git");

    private void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>A main checkout on <c>main</c>, and a linked tree on <c>daoris/s1</c>, as git lays them out.</summary>
    private (string Root, string Tree) Layout(string name = "engine")
    {
        var root = Path.Combine(_home, name);
        Write(Path.Combine(Git(root), "HEAD"), "ref: refs/heads/main\n");
        Write(Path.Combine(Git(root), "refs", "heads", "main"), Tip + "\n");
        var tree = DaorisHome.Entry(_home, HomeEntry.Trees, name + "-t1");
        var own = Path.Combine(Git(root), "worktrees", "t1");
        Write(Path.Combine(own, "HEAD"), "ref: refs/heads/daoris/s1\n");
        Write(Path.Combine(own, "commondir"), "../..\n");
        Write(Path.Combine(tree, ".git"), $"gitdir: {own}\n");
        Write(Path.Combine(Git(root), "refs", "heads", "daoris", "s1"), Tip + "\n");
        return (root, tree);
    }

    /// <summary>The tip is git's own files read: a loose ref, a packed one, a detached HEAD; reftable and anything unreadable say nothing.</summary>
    [Fact]
    public void A_tree_s_tip_is_read_from_git_s_files_without_a_process()
    {
        var (root, tree) = Layout();
        Assert.Equal(Tip, TreeTips.Read(root));
        Assert.Equal(Tip, TreeTips.Read(tree));

        // Packed: the loose ref gone, the line in packed-refs says it.
        File.Delete(Path.Combine(Git(root), "refs", "heads", "daoris", "s1"));
        Write(Path.Combine(Git(root), "packed-refs"), $"# pack-refs with: peeled fully-peeled sorted \n{Moved} refs/heads/daoris/s1\n^{Tip}\n");
        Assert.Equal(Moved, TreeTips.Read(tree));

        // Detached: HEAD holds the commit itself.
        Write(Path.Combine(Git(root), "worktrees", "t1", "HEAD"), Tip.ToUpperInvariant() + "\n");
        Assert.Equal(Tip, TreeTips.Read(tree));

        // An unborn branch, reftable's format, and no repository at all: nothing is said, so git is asked.
        Write(Path.Combine(Git(root), "worktrees", "t1", "HEAD"), "ref: refs/heads/daoris/none\n");
        Assert.Null(TreeTips.Read(tree));
        Directory.CreateDirectory(Path.Combine(Git(root), "reftable"));
        Assert.Null(TreeTips.Read(root));
        Assert.Null(TreeTips.Read(Path.Combine(_home, "nowhere")));
    }

    // ——— the loop's wakes, kept between looks

    /// <summary>
    /// The loop keeps each wake until the next look takes them: a wake's plan looks at those alone, a plan no wake asked for is a
    /// session's end (the ledger's sessions), and the whole look takes them and looks at every entry.
    /// </summary>
    [Fact]
    public void The_loop_keeps_its_wakes_until_a_look_takes_them()
    {
        var wakes = new LandingWakes();
        wakes.Hear(ChangeTopics.Ledger, "quests");
        wakes.Hear(ChangeTopics.Refs, "engine");
        wakes.Hear(ChangeTopics.Ledger, "quests");

        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "quests"), new LandingWake(ChangeTopics.Refs, "engine")], wakes.For(whole: false)!);
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "sessions")], wakes.For(whole: false)!);

        wakes.Hear(ChangeTopics.Config, "driver.json");
        Assert.Null(wakes.For(whole: true));
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "sessions")], wakes.For(whole: false)!);
    }

    /// <summary>
    /// FABRIC-END7's review: a wake is spent only where a landing judged with it. What a look took goes back ahead of what was heard
    /// meanwhile, a plan's session end with it; held back for a running landing, that landing's end offers it again, once; and a whole
    /// look's goes back as every entry, which the next plan judges.
    /// </summary>
    [Fact]
    public void Wakes_a_landing_did_not_judge_with_go_back_and_a_running_landing_s_end_offers_them_again()
    {
        var wakes = new LandingWakes();
        wakes.Hear(ChangeTopics.Ledger, "quests");
        var failed = wakes.Take(whole: false);
        wakes.Hear(ChangeTopics.Refs, "engine");
        wakes.Hear(ChangeTopics.Ledger, "quests");
        wakes.Unspent(failed, heldBack: false);
        Assert.False(wakes.Reoffer());
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "quests"), new LandingWake(ChangeTopics.Refs, "engine")], wakes.For(whole: false)!);

        var skipped = wakes.Take(whole: false);
        wakes.Unspent(skipped, heldBack: true);
        Assert.True(wakes.Reoffer());
        Assert.False(wakes.Reoffer());
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "sessions")], wakes.For(whole: false)!);

        wakes.Hear(ChangeTopics.Gates, "opinions");
        wakes.Unspent(wakes.Take(whole: true), heldBack: false);
        Assert.Null(wakes.For(whole: false));
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "sessions")], wakes.For(whole: false)!);
    }

    /// <summary>
    /// FABRIC-END7's review: a look that fails before its landing judged anything puts the wakes it took back, so the hold a quest's
    /// release woke is not left to the whole look. The stand-in's quest list does not answer, so the plan fails after it took them; the
    /// start's whole look failed first, so the next plan owes every entry its judgement too.
    /// </summary>
    [Fact]
    public async Task A_look_that_fails_before_its_landing_puts_its_wakes_back()
    {
        using var ledger = new StandInLedger { Down = true };
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": ["engine"], "adapter": "stub", "pollSeconds": 600 }""");
        using var service = ledger.Client();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        LandingTaken? left = null;
        var failures = 0;
        DriverWatch? watch = null;
        watch = new DriverWatch(service, config, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.SessionPids)), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        await watch.RunAsync(
            (_, _) => Task.CompletedTask,
            _ =>
            {
                if (++failures == 1) watch.Nudge(ChangeTopics.Ledger, "quests");
                else
                {
                    left = watch.Landing.Take(whole: false);
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            stop.Token);

        Assert.Equal(2, failures);
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "quests")], left!.Heard);
        Assert.True(left.Whole);
    }

    // ——— the refs' wake, found without git

    private void Due(AutoLanding entry)
    {
        var due = new AutoLandings(_home);
        due.Due(entry with { Tries = [] });
        foreach (var tried in entry.Tries) due.Tried(entry.Session, tried, close: false);
    }

    /// <summary>
    /// A ref move wakes the landing only where a due entry of that repository is waiting on its tree and the tree's tip moved: a
    /// landing's own branch, another repository's entry, a hold, and an empty due list wake nothing, and no process starts.
    /// </summary>
    [Fact]
    public void A_ref_move_wakes_the_landing_only_for_a_tree_that_moved()
    {
        var (root, tree) = Layout();
        var woken = new List<(string Topic, string Subject)>();
        void Wake(string topic, string subject) => woken.Add((topic, subject));
        var starts = 0;
        using var within = ProcessStarts.Within(_ =>
        {
            Interlocked.Increment(ref starts);
            return null;
        });

        LandingWakes.Moved(_home, "engine", Wake);
        Assert.Empty(woken);

        Due(Entry(AutoLandingCode.Uncommitted, tipKept: true, tree: tree));
        Due(Entry(AutoLandingCode.Held, tipKept: false, tree: tree) with { Session = "s2", Quest = "q2" });
        LandingWakes.Moved(_home, "engine", Wake);
        Assert.Empty(woken);

        // The landing's own branch moves no tree's tip.
        Write(Path.Combine(Git(root), "refs", "heads", "feature", "q1"), Moved + "\n");
        LandingWakes.Moved(_home, "engine", Wake);
        Assert.Empty(woken);

        // The tree's own commit does; another repository's move is not its.
        Write(Path.Combine(Git(root), "refs", "heads", "daoris", "s1"), Moved + "\n");
        LandingWakes.Moved(_home, "deck", Wake);
        Assert.Empty(woken);
        LandingWakes.Moved(_home, "engine", Wake);
        Assert.Equal([(ChangeTopics.Refs, "engine")], woken);
        Assert.Equal(0, starts);
    }

    /// <summary>
    /// A wake that concerns no due entry reads nothing of the world and starts no git: the entries it cannot release are not looked
    /// at, and keep their tries as they were.
    /// </summary>
    [Fact]
    public async Task A_wake_that_concerns_nothing_reads_no_world_and_starts_no_git()
    {
        var (_, tree) = Layout();
        Due(Entry(AutoLandingCode.Held, tipKept: false, tree: tree));
        Due(Entry(AutoLandingCode.Uncommitted, tipKept: true, tree: tree) with { Session = "s2", Quest = "q2" });
        var starts = 0;
        using var within = ProcessStarts.Within(_ =>
        {
            Interlocked.Increment(ref starts);
            return null;
        });
        var lander = new AutoLander(_home, new Unasked(), new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));

        var chosen = await lander.ChooseAsync(DriverConfig.Empty, CancellationToken.None, [new(ChangeTopics.Refs, "engine"), new(ChangeTopics.Ledger, "sessions")]);

        Assert.Empty(chosen.Chosen);
        Assert.Empty(chosen.Said);
        Assert.Equal(0, starts);
        Assert.All(new AutoLandings(_home).Open(), entry => Assert.Single(entry.Tries));
    }

    /// <summary>A world that fails the test if it is asked: a wake that concerns nothing never reads it.</summary>
    private sealed class Unasked : IAutoLandingWorld
    {
        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => throw new InvalidOperationException("the quest was read");
        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => throw new InvalidOperationException("the records were read");
        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => throw new InvalidOperationException("the trees in use were read");
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => throw new InvalidOperationException("the quests were read");
        public Task<AskView?> AskAsync(string id, CancellationToken ct) => throw new InvalidOperationException("an ask was read");
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => throw new InvalidOperationException("an opinion was read");
    }

    // ——— the ref watch tells the landing what it judged

    /// <summary>
    /// The ref watch tells the landing each repository a hint or Daoris's own move judges, before its ends are kept; its deadlines,
    /// which judge nothing a second time, tell nothing.
    /// </summary>
    [Fact]
    public async Task The_ref_watch_tells_the_landing_each_repository_it_judges_and_no_deadline()
    {
        var now = At;
        var heard = new ConcurrentQueue<string>();
        RefRepository engine = new("engine", "aurora", Path.Combine(_home, "engine"));
        Directory.CreateDirectory(engine.CommonDir);
        await using var watch = new RefWatch(_home, _ => Task.FromResult<IReadOnlyList<RefRepository>>([engine]), (_, _) => Task.FromResult(0),
            TimeSpan.FromMinutes(5), _source, () => now) { Heard = (repository, _) => heard.Enqueue(repository) };
        watch.Open();
        await watch.RebuildAsync(CancellationToken.None);

        async Task Step(TimeSpan by)
        {
            now += by;
            watch.Changes.Pump();
            await watch.Changes.Settled();
        }

        _source.On(engine.CommonDir)!.Raise("refs/heads/main", ChangeKind.Renamed);
        await Step(ChangeWatch.Quiet);
        Assert.Equal(["engine"], heard);

        RefMoves.Mine(_home, "engine");
        await Step(TimeSpan.Zero);
        Assert.Equal(["engine", "engine"], heard);

        await Step(TimeSpan.FromMinutes(6));
        Assert.Equal(["engine", "engine"], heard);
    }

    // ——— the gate's files

    private string Gates => DaorisHome.Entry(_home, HomeEntry.OpinionGates);

    private string Workflows => WorkflowStore.FolderOf(_home);

    /// <summary>
    /// The gate's files are the <c>gates</c> topic: a second opinion's record (the person's answer kept there) and the workflows (a
    /// version, a run's binding with the person's say-so), each heard when another process writes it; this process's own writes,
    /// marked, wake nothing.
    /// </summary>
    [Fact]
    public async Task The_gate_s_files_wake_the_landing_and_the_loop_s_own_writes_do_not()
    {
        Directory.CreateDirectory(Gates);
        Directory.CreateDirectory(Path.Combine(Workflows, "runs"));
        // FABRIC-END7b: the run a due entry's gate read, the one the topic lists of the runs.
        LandingJudged.Read(_home, Entry(AutoLandingCode.Refused, tipKept: false), "run1");
        var woken = new ConcurrentQueue<(string Topic, string Subject)>();
        await using var watch = HomeWatch.ForDriver(_home, DaorisHome.Entry(_home, HomeEntry.DriverConfig), (topic, subject) => woken.Enqueue((topic, subject)),
            TimeSpan.FromHours(1), _source);
        watch.Start();

        void Say(string folder, string name)
        {
            foreach (var handle in _source.Opened.Where(handle => handle.Folder == Path.GetFullPath(folder) && !handle.Disposed)) handle.Raise(name, ChangeKind.Renamed);
        }

        // This process's own: the gate kept at the look, a run bound at its start, a version saved.
        new OpinionGates(_home).Requested("q1", "engine", new OpinionRequest(At, "s1", OpinionRules.Landing));
        Say(Gates, "q1.json");
        new WorkflowRunFiles(_home).Write("run1", Binding("run1"));
        Say(Workflows, "runs/run1.json");
        await Poll.Until(() => watch.Changes.Judgements >= 1, () => $"{watch.Changes.Judgements} judgements");
        await watch.Changes.Settled();
        Assert.Empty(woken);

        // Another process's: the person's answer from a terminal, and a run's binding written there.
        File.AppendAllText(Path.Combine(Gates, "q1.json"), " ");
        Say(Gates, "q1.json");
        await Poll.Until(() => woken.Contains((ChangeTopics.Gates, ChangeTopics.Gates)), () => "the gate's record was never heard");
        woken.Clear();
        File.AppendAllText(WorkflowRunBindings.PathOf(_home, "run1"), " ");
        Say(Workflows, "runs/run1.json");
        await Poll.Until(() => woken.Contains((ChangeTopics.Gates, ChangeTopics.Gates)), () => "the run's binding was never heard");
    }

    /// <summary>With the source silent, the gate's files are found by their topic's deadline, as every home topic is (link 7's timer).</summary>
    [Fact]
    public async Task The_gate_s_files_are_found_by_their_deadline_when_the_source_is_silent()
    {
        Directory.CreateDirectory(Gates);
        var woken = new ConcurrentQueue<(string Topic, string Subject)>();
        await using var watch = HomeWatch.ForDriver(_home, DaorisHome.Entry(_home, HomeEntry.DriverConfig), (topic, subject) => woken.Enqueue((topic, subject)),
            TimeSpan.FromMilliseconds(300), _source);
        watch.Start();

        File.WriteAllText(Path.Combine(Gates, "q1.json"), "{}");

        await Poll.Until(() => woken.Contains((ChangeTopics.Gates, ChangeTopics.Gates)), () => "the gate's record was never found at its deadline");
    }

    /// <summary>
    /// FABRIC-END7b: the <c>gates</c> topic lists of <c>workflows/runs/</c> only the runs a due entry's gate read, by name, never the folder,
    /// which keeps one file per run for good. Forty runs bound and two read by due entries' gates are two runs' files listed, beside the
    /// opinion gates' records and the workflows' versions; a run read but not bound is listed as absent, so its binding arriving moves it.
    /// </summary>
    [Fact]
    public void The_gates_deadline_lists_only_the_runs_a_due_entry_s_gate_read()
    {
        var runs = new WorkflowRunFiles(_home);
        for (var each = 0; each < 40; each++) runs.Bind(Binding($"run{each}"));
        Directory.CreateDirectory(Gates);
        File.WriteAllText(Path.Combine(Gates, "q1.json"), "{}");
        File.WriteAllText(Path.Combine(Workflows, "careful.json"), "{}");
        LandingJudged.Read(_home, Entry(AutoLandingCode.Refused, tipKept: false), "run7");
        LandingJudged.Read(_home, Entry(AutoLandingCode.PushHeld, tipKept: true) with { Session = "s2" }, "run99");
        LandingJudged.Read(_home, Entry(AutoLandingCode.Held, tipKept: false) with { Session = "s3" }, run: null);

        var listed = HomeWatch.Listed(HomeWatch.GateParts(_home));

        var folder = WorkflowRunBindings.FolderOf(_home) + Path.DirectorySeparatorChar;
        Assert.Equal(
            [WorkflowRunBindings.PathOf(_home, "run7"), WorkflowRunBindings.PathOf(_home, "run99")],
            listed.Keys.Where(key => key.StartsWith(folder, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("-", listed[WorkflowRunBindings.PathOf(_home, "run99")]);
        Assert.Equal(4, listed.Count);

        // No longer due: its run is no longer listed.
        LandingJudged.Keep(_home, [Entry(AutoLandingCode.Refused, tipKept: false)]);
        Assert.Equal(3, HomeWatch.Listed(HomeWatch.GateParts(_home)).Count);
    }

    /// <summary>
    /// FABRIC-END7b: a run's name entering or leaving the list is the list's change, never the file's, so it wakes nothing; a listed run's
    /// file written by another process does, and one this process wrote does not.
    /// </summary>
    [Fact]
    public async Task A_run_entering_or_leaving_the_list_wakes_nothing_and_its_file_written_elsewhere_does()
    {
        var runs = new WorkflowRunFiles(_home);
        runs.Bind(Binding("run1"));
        runs.Bind(Binding("run2"));
        var woken = new ConcurrentQueue<(string Topic, string Subject)>();
        await using var watch = HomeWatch.ForDriver(_home, DaorisHome.Entry(_home, HomeEntry.DriverConfig), (topic, subject) => woken.Enqueue((topic, subject)),
            TimeSpan.FromHours(1), _source);
        watch.Start();

        void Say(string name)
        {
            foreach (var handle in _source.Opened.Where(handle => handle.Folder == Path.GetFullPath(Workflows) && !handle.Disposed)) handle.Raise(name, ChangeKind.Renamed);
        }

        async Task Judged(int judgements)
        {
            await Poll.Until(() => watch.Changes.Judgements >= judgements, () => $"{watch.Changes.Judgements} judgements");
            await watch.Changes.Settled();
        }

        // run1 read by a due entry's gate: listed from now on, which moves nothing.
        LandingJudged.Read(_home, Entry(AutoLandingCode.Refused, tipKept: false), "run1");
        Say("runs/run1.json");
        await Judged(1);
        Assert.Empty(woken);

        // run2, which no gate read, written by another process: not listed, so nothing.
        File.AppendAllText(WorkflowRunBindings.PathOf(_home, "run2"), " ");
        Say("runs/run2.json");
        await Judged(2);
        Assert.Empty(woken);

        // The entry no longer due: run1 leaves the list, which moves nothing either.
        LandingJudged.Keep(_home, []);
        Say("runs/run1.json");
        await Judged(3);
        Assert.Empty(woken);

        // Read again, and written by another process: heard.
        LandingJudged.Read(_home, Entry(AutoLandingCode.Refused, tipKept: false), "run1");
        Say("runs/run1.json");
        await Judged(4);
        File.AppendAllText(WorkflowRunBindings.PathOf(_home, "run1"), " ");
        Say("runs/run1.json");
        await Poll.Until(() => woken.Contains((ChangeTopics.Gates, ChangeTopics.Gates)), () => "the run's binding was never heard");
    }

    private static WorkflowRunBinding Binding(string run) => new(run, "engine", "aurora", "careful", WorkflowLevels.Task, At);
}
