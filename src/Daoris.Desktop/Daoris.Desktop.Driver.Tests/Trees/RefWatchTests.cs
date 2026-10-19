using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ref watch (FABRIC-END3, the design §A4.1, §B4 and link 1 of §C6) on the fake source, raising what git's writes raise
/// (§A5's measured sequences), stepped by hand on a clock that stands still: canaries 1-4 on git, and canary 11's own moves.
/// The judge here counts; <see cref="RefWatchProcessTests"/> runs the real keeper over real git.
/// </summary>
public sealed class RefWatchTests : IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(5);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-refs-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FakeChangeSource _source = new();

    private readonly List<string> _kept = [];

    private IReadOnlyList<RefRepository> _repositories = [];

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private int _writes;

    public RefWatchTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private RefRepository Repository(string name) => new(name, "aurora", Path.Combine(_home, name));

    private string CommonDir(string name) => Path.Combine(_home, name, ".git");

    private string[] Kept()
    {
        lock (_kept) return [.. _kept];
    }

    private Task<int> Keep(RefRepository repository, CancellationToken ct)
    {
        lock (_kept) _kept.Add(repository.Repository);
        return Task.FromResult(_writes);
    }

    /// <summary>A watch over <paramref name="names"/>, opened and its set read, not pumping: a test steps it.</summary>
    private async Task<RefWatch> Watching(MachineLog? log = null, Func<RefRepository, CancellationToken, Task<int>>? keep = null, params string[] names)
    {
        _repositories = [.. names.Select(Repository)];
        var watch = new RefWatch(_home, _ => Task.FromResult(_repositories), keep ?? Keep, Deadline, _source, () => _now) { Log = log };
        watch.Open();
        await watch.RebuildAsync(CancellationToken.None);
        return watch;
    }

    /// <summary>What git's write raises on the repository's common dir, said on the handle open there.</summary>
    private void Say(string name, string moved, ChangeKind kind = ChangeKind.Renamed) => _source.On(CommonDir(name))!.Raise(moved, kind);

    /// <summary>A commit on <c>main</c>, as §A5 measured it: its objects, its lock, its logs, the ref deleted, then the lock renamed onto it.</summary>
    private void Commit(string name, string branch = "main")
    {
        for (var each = 0; each < 17; each++) Say(name, $"objects/{each:x2}/{Guid.NewGuid():N}", ChangeKind.Created);
        Say(name, $"refs/heads/{branch}.lock", ChangeKind.Created);
        Say(name, "logs/HEAD", ChangeKind.Changed);
        Say(name, $"logs/refs/heads/{branch}", ChangeKind.Changed);
        Say(name, $"refs/heads/{branch}", ChangeKind.Deleted);
        Say(name, $"refs/heads/{branch}.lock", ChangeKind.Deleted);
        Say(name, $"refs/heads/{branch}");
        Say(name, "index", ChangeKind.Changed);
    }

    private async Task Step(RefWatch watch, TimeSpan by)
    {
        _now += by;
        watch.Changes.Pump();
        await watch.Changes.Settled();
    }

    private MachineLog Log() => new(_home, "driver", () => _now);

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

    /// <summary>The filter (§A4.1): git's final ref paths are heard; its objects, logs, index and every temporary are not.</summary>
    [Theory]
    [InlineData("HEAD", true)]
    [InlineData("packed-refs", true)]
    [InlineData("refs/heads/main", true)]
    [InlineData("refs/heads/daoris/s-0001", true)]
    [InlineData("refs/remotes/origin/main", true)]
    [InlineData("worktrees/s-0001/HEAD", true)]
    [InlineData("reftable/tables.list", true)]
    [InlineData("reftable/0x000000000001-0x000000000002-1a2b3c4d.ref", true)]
    [InlineData("worktrees/s-0001/reftable/tables.list", true)]
    [InlineData("refs/heads/main.lock", false)]
    [InlineData("packed-refs.lock", false)]
    [InlineData("packed-refs.new", false)]
    [InlineData("worktrees/s-0001/HEAD.lock", false)]
    [InlineData("reftable/tables.list.lock", false)]
    [InlineData("objects/ab/cdef0123", false)]
    [InlineData("objects/pack/pack-1.pack", false)]
    [InlineData("logs/HEAD", false)]
    [InlineData("logs/refs/heads/main", false)]
    [InlineData("index", false)]
    [InlineData("worktrees/s-0001/index", false)]
    [InlineData("FETCH_HEAD", false)]
    [InlineData("ORIG_HEAD", false)]
    [InlineData("config", false)]
    [InlineData("refs/heads", false)]
    [InlineData("refs/tags/v1", false)]
    [InlineData("worktrees/s-0001/logs/HEAD", false)]
    public void The_filter_hears_the_final_ref_paths_alone(string name, bool heard) => Assert.Equal(heard, RefWatch.Hears(name));

    /// <summary>
    /// Canary 3 on git: a commit's burst (37 events, the ref deleted then renamed onto) is one judgement of its repository, handed
    /// the repository and never the event; its <c>change.judged</c> line says the hint, how long it waited and what it wrote, and the
    /// facts it wrote are link 1's <c>chain.fired</c>.
    /// </summary>
    [Fact]
    public async Task A_commit_s_burst_is_one_judgement_of_its_repository_and_its_line_says_so()
    {
        _writes = 1;
        using var log = Log();
        await using var watch = await Watching(log, names: ["engine", "deck"]);

        Commit("engine");
        await Step(watch, ChangeWatch.Quiet);
        Assert.Equal(["engine"], Kept());

        await Step(watch, ChangeWatch.Cap);
        Assert.Equal(["engine"], Kept());

        var judged = Assert.Single(Lines("change.judged"));
        Assert.Equal(
            (ChangeTopics.Refs, "engine", ChangeWhy.Hint, 1),
            (judged.GetProperty("topic").GetString(), judged.GetProperty("subject").GetString(), judged.GetProperty("why").GetString(),
                judged.GetProperty("acted").GetInt32()));
        Assert.Equal(ChangeWatch.Quiet.TotalMilliseconds, judged.GetProperty("waitedMs").GetInt64());
        var fired = Assert.Single(Lines("chain.fired"));
        Assert.Equal(ChainRanks.Judge, fired.GetProperty("link").GetInt32());
    }

    /// <summary>
    /// Canary 3's other halves: a hint arriving while its repository is judged is judged after it, never beside it, and two
    /// repositories are judged at once.
    /// </summary>
    [Fact]
    public async Task A_hint_during_a_judgement_is_judged_after_it_and_never_beside_it()
    {
        var release = new TaskCompletionSource();
        var running = 0;
        var most = 0;
        await using var watch = await Watching(keep: async (repository, ct) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            lock (_kept) _kept.Add(repository.Repository);
            await release.Task.WaitAsync(ct);
            Interlocked.Decrement(ref running);
            return 0;
        }, names: ["engine"]);

        Say("engine", "refs/heads/main");
        _now += ChangeWatch.Quiet;
        watch.Changes.Pump();
        await Poll.Until(() => Kept().Length == 1, () => "the first hint was never judged");

        Say("engine", "worktrees/s-0001/HEAD");
        _now += ChangeWatch.Quiet;
        watch.Changes.Pump();
        release.SetResult();
        await watch.Changes.Settled();

        Assert.Equal(["engine", "engine"], Kept());
        Assert.Equal(1, most);
    }

    /// <summary>
    /// Canary 2 on git: an overflow on a repository's watcher judges that repository whole at once and reopens it; what its old
    /// watcher says after is dropped; a resume judges every repository.
    /// </summary>
    [Fact]
    public async Task An_overflow_judges_its_repository_at_once_and_a_resume_judges_them_all()
    {
        using var log = Log();
        await using var watch = await Watching(log, names: ["engine", "deck"]);
        var old = _source.On(CommonDir("engine"))!;

        old.Lose();
        await Step(watch, TimeSpan.Zero);
        Assert.Equal(["engine"], Kept());
        Assert.Equal(ChangeWhy.Lost, Assert.Single(Lines("change.judged")).GetProperty("why").GetString());

        await Step(watch, TimeSpan.Zero);
        var reopened = _source.On(CommonDir("engine"))!;
        Assert.NotSame(old, reopened);
        old.Raise("refs/heads/main");
        await Step(watch, ChangeWatch.Cap);
        Assert.Equal(1, watch.Changes.Dropped);

        watch.Resumed();
        await Step(watch, TimeSpan.Zero);
        Assert.Equal(["deck", "engine"], Kept().Skip(Kept().Length - 2).Order().ToArray());
    }

    /// <summary>
    /// Canary 1 on git: with the source silenced, nothing is judged by the watch before its deadline, and at the deadline the roots
    /// are reconciled (a repository absent at the open is opened and judged) while each repository's ends stay the tidy's pass's,
    /// the reconciliation (§B4): an idle deadline judges no repository a second time.
    /// </summary>
    [Fact]
    public async Task A_silent_source_leaves_the_ends_to_the_tidy_s_pass_and_reconciles_its_roots_at_the_deadline()
    {
        _source.Absent(CommonDir("deck"));
        await using var watch = await Watching(names: ["engine", "deck"]);
        Assert.Null(_source.On(CommonDir("deck")));

        await Step(watch, Deadline - TimeSpan.FromSeconds(1));
        Assert.Empty(Kept());

        _source.Absent(CommonDir("deck"), absent: false);
        await Step(watch, TimeSpan.FromSeconds(1));
        Assert.NotNull(_source.On(CommonDir("deck")));
        Assert.Equal(["deck"], Kept());

        _source.On(CommonDir("engine"))!.Same = false;
        await Step(watch, Deadline);
        Assert.Contains("engine", Kept());
        Assert.Equal(1, Kept().Count(each => each == "deck"));
    }

    /// <summary>A deletion alone is a hint like any other: the repository is judged, handed no path and no kind, so the keeper reads its refs.</summary>
    [Fact]
    public async Task A_deletion_hint_alone_is_a_judgement_that_reads_the_refs()
    {
        RefRepository? handed = null;
        await using var watch = await Watching(keep: (repository, _) =>
        {
            handed = repository;
            return Task.FromResult(0);
        }, names: ["engine"]);

        Say("engine", "refs/heads/daoris/s-0001", ChangeKind.Deleted);
        await Step(watch, ChangeWatch.Quiet);

        Assert.Equal(Repository("engine"), handed);
    }

    /// <summary>Canary 4 on git: a thousand ref hints are one judgement, and the watch runs nothing but its keeper (no look, no sync).</summary>
    [Fact]
    public async Task A_thousand_ref_hints_are_one_judgement()
    {
        await using var watch = await Watching(names: ["engine"]);

        for (var each = 0; each < 1000; each++) Say("engine", each % 2 == 0 ? "refs/heads/main" : "packed-refs");
        await Step(watch, ChangeWatch.Quiet);
        await Step(watch, ChangeWatch.Cap);

        Assert.Equal(["engine"], Kept());
        Assert.Equal(1, watch.Changes.Judgements);
    }

    /// <summary>
    /// Canary 11: Daoris's own ref move judges its repository at once, without the quiet window, and takes up the watcher's echo
    /// already heard, so the move is judged once; a move in another home is not this watch's.
    /// </summary>
    [Fact]
    public async Task Daoris_s_own_move_is_judged_at_once_and_its_echo_with_it()
    {
        using var log = Log();
        await using var watch = await Watching(log, names: ["engine"]);

        Say("engine", "refs/heads/daoris/s-0001", ChangeKind.Deleted);
        Say("engine", "worktrees/s-0001/HEAD", ChangeKind.Deleted);
        RefMoves.Mine(_home, "engine");
        RefMoves.Mine(Path.Combine(_home, "elsewhere"), "engine");
        await Step(watch, TimeSpan.Zero);
        Assert.Equal(["engine"], Kept());

        await Step(watch, ChangeWatch.Cap);
        Assert.Equal(["engine"], Kept());
        Assert.Equal(ChangeWhy.Own, Assert.Single(Lines("change.judged")).GetProperty("why").GetString());
    }

    /// <summary>
    /// OWNMOVES1: the judgement Daoris's own move asks for is told as its own, the echo taken up with it; another repository's hint
    /// beside it is told as no own move, and so is the moved repository's next hint, which the own move does not outlive.
    /// </summary>
    [Fact]
    public async Task Daoris_s_own_move_is_told_as_its_own_and_a_hint_beside_it_is_not()
    {
        var told = new List<(string Repository, bool Mine)>();
        (string, bool)[] Told()
        {
            lock (told) return [.. told];
        }

        _repositories = [Repository("engine"), Repository("deck")];
        await using var watch = new RefWatch(_home, _ => Task.FromResult(_repositories), Keep, Deadline, _source, () => _now)
        {
            Heard = (repository, mine) =>
            {
                lock (told) told.Add((repository, mine));
            },
        };
        watch.Open();
        await watch.RebuildAsync(CancellationToken.None);

        Say("engine", "refs/heads/daoris/s-0001", ChangeKind.Deleted);
        RefMoves.Mine(_home, "engine");
        Say("deck", "refs/heads/main");
        await Step(watch, TimeSpan.Zero);
        Assert.Equal([("engine", true)], Told());

        await Step(watch, ChangeWatch.Quiet);
        Assert.Equal([("engine", true), ("deck", false)], Told());

        Say("engine", "refs/heads/main");
        await Step(watch, ChangeWatch.Quiet);
        Assert.Equal([("engine", true), ("deck", false), ("engine", false)], Told());
    }

    /// <summary>A tree opened in a repository not yet watched adds its common dir in process, and judges it.</summary>
    [Fact]
    public async Task A_tree_opened_in_a_repository_not_yet_watched_adds_it()
    {
        await using var watch = await Watching(names: ["engine"]);
        Directory.CreateDirectory(CommonDir("deck"));

        RefMoves.Mine(_home, "deck", "aurora", Path.Combine(_home, "deck"));
        await Step(watch, TimeSpan.Zero);

        Assert.Equal(["deck"], Kept());
        Assert.Contains("deck", watch.Watched);
        Say("deck", "refs/heads/main");
        await Step(watch, ChangeWatch.Quiet);
        Assert.Equal(["deck", "deck"], Kept());
    }

    /// <summary>
    /// The set is rebuilt from the repositories holding Daoris's work: one that no longer does is let go of, and a repository the cap
    /// of 32 left on its deadline takes its place, opened and judged.
    /// </summary>
    [Fact]
    public async Task The_set_is_rebuilt_and_a_root_let_go_of_gives_its_place_to_one_the_cap_left_out()
    {
        using var log = Log();
        var names = Enumerable.Range(1, ChangeWatch.MostRoots + 1).Select(each => $"repo{each:00}").ToArray();
        await using var watch = await Watching(log, names: names);
        Assert.Null(_source.On(CommonDir(names[^1])));
        Assert.Single(Lines("change.capped"));

        var first = _source.On(CommonDir(names[0]))!;
        _repositories = [.. names.Skip(1).Select(Repository)];
        await watch.RebuildAsync(CancellationToken.None);
        await Step(watch, TimeSpan.Zero);

        Assert.True(first.Disposed);
        Assert.DoesNotContain(names[0], watch.Watched);
        Assert.NotNull(_source.On(CommonDir(names[^1])));
        Assert.Equal([names[^1]], Kept());

        first.Raise("refs/heads/main");
        await Step(watch, ChangeWatch.Cap);
        Assert.Equal([names[^1]], Kept());
    }

    /// <summary>
    /// The driver's set (§A4.1): the registry's repositories with a checkout here whose common dir is a folder, and that hold
    /// Daoris's work in <c>session-branches.json</c> or <c>landings.json</c>; with no work kept, the registry is not asked.
    /// </summary>
    [Fact]
    public async Task The_driver_s_set_is_the_registered_repositories_holding_daoris_s_work()
    {
        var asked = 0;
        IReadOnlyList<RepoView> registry =
        [
            new("engine", Adopted: true, Path.Combine(_home, "engine"), "aurora"),
            new("deck", Adopted: true, Path.Combine(_home, "deck"), "aurora"),
            new("ghost", Adopted: true, Root: null, "aurora"),
        ];
        Task<IReadOnlyList<RepoView>> Registry(CancellationToken _)
        {
            asked++;
            return Task.FromResult(registry);
        }

        Assert.Empty(await RefWatch.RepositoriesAsync(_home, Registry, CancellationToken.None));
        Assert.Equal(0, asked);

        Directory.CreateDirectory(CommonDir("engine"));
        Directory.CreateDirectory(CommonDir("deck"));
        var grown = new SessionBranches(_home);
        grown.Record(new GrownBranch("engine", "aurora", "daoris/s-0001", "main", new string('a', 40), null, _now));
        grown.Record(new GrownBranch("ghost", "aurora", "daoris/s-0002", "main", new string('b', 40), null, _now));

        var set = await RefWatch.RepositoriesAsync(_home, Registry, CancellationToken.None);
        Assert.Equal([new RefRepository("engine", "aurora", Path.Combine(_home, "engine"))], set);
        Assert.Equal(1, asked);
    }

    private static void InterlockedMax(ref int most, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref most);
            if (value <= seen) return;
        }
        while (Interlocked.CompareExchange(ref most, value, seen) != seen);
    }
}
