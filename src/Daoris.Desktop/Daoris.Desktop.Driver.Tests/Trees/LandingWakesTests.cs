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
        var tree = Path.Combine(_home, "trees", name + "-t1");
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

        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "quests"), new LandingWake(ChangeTopics.Refs, "engine")], wakes.For(LookPart.Plan)!);
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "sessions")], wakes.For(LookPart.Plan)!);

        wakes.Hear(ChangeTopics.Config, "driver.json");
        Assert.Null(wakes.For(LookPart.Look));
        Assert.Equal([new LandingWake(ChangeTopics.Ledger, "sessions")], wakes.For(LookPart.Plan)!);
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
            TimeSpan.FromMinutes(5), _source, () => now) { Heard = heard.Enqueue };
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

    private static WorkflowRunBinding Binding(string run) => new(run, "engine", "aurora", "careful", WorkflowLevels.Task, At);
}
