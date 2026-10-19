using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END6 (the design §A4.3, link 6 of §C6): a plugin is asked about an open pull request at origin's line heard moving, right
/// after Daoris pushes, and at the window's focus; the line is fetched only where the moment did not bring it; both within D148's
/// minute; never at a removing press. A fake world counts each ask, fetch, line read and registry read, on a clock that stands still;
/// <see cref="PullRequestMomentsGitTests"/> runs the real asks over real git to an end no press made.
/// </summary>
public sealed class PullRequestMomentsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-moments-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly World _world;

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    public PullRequestMomentsTests()
    {
        Directory.CreateDirectory(_home);
        _world = new World(() => _now);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static RefRepository Repository(string name) => new(name, "aurora", Path.Combine("D:", "fam", name));

    private static LandedBranch Landing(string repository, string branch, string? state = null, DateTimeOffset? asked = null) =>
        new(repository, "aurora", branch, "main", new string('b', 40), "s-" + branch, "q1", "Fix", new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero))
        {
            Plugin = "acme.asks",
            Pushed = true,
            PullRequest = $"https://example.test/pr/{branch}",
            PullRequestState = state is null ? null : new PullRequestState(state) { AskedAt = asked ?? DateTimeOffset.MinValue },
        };

    /// <summary>The moments of this home, listening and not pumping: a test steps them.</summary>
    private Task<PullRequestMoments> Opened()
    {
        var moments = new PullRequestMoments(_home, _world, new FakeChangeSource(), () => _now) { Log = Log() };
        moments.Open();
        return Task.FromResult(moments);
    }

    private static async Task Step(PullRequestMoments moments)
    {
        moments.Changes.Pump();
        await moments.Changes.Settled();
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

    /// <summary>Which landings a moment concerns: standing, pushed by a plugin, neither completed nor abandoned, and not yet ended.</summary>
    [Fact]
    public void A_moment_concerns_a_standing_pushed_landing_whose_pull_request_is_open_and_whose_end_is_not_held()
    {
        var open = Landing("engine", "feature/a");
        Assert.True(PullRequestMoments.Concerns(open));
        Assert.True(PullRequestMoments.Concerns(Landing("engine", "feature/b", PullRequestStates.Open)));
        Assert.True(PullRequestMoments.Concerns(Landing("engine", "feature/c", PullRequestStates.Unknown)));

        Assert.False(PullRequestMoments.Concerns(Landing("engine", "feature/d", PullRequestStates.Completed)));
        Assert.False(PullRequestMoments.Concerns(Landing("engine", "feature/e", PullRequestStates.Abandoned)));
        Assert.False(PullRequestMoments.Concerns(open with { Pushed = false }));
        Assert.False(PullRequestMoments.Concerns(open with { GoneAt = _now }));
        Assert.False(PullRequestMoments.Concerns(open with
        {
            Held = new HeldFact(_now, _now, "line", new string('b', 40), "main", "main", new string('c', 40), "repo"),
        }));
    }

    /// <summary>
    /// Origin's line heard moving (the person's fetch, through the ref watch) asks each open pull request of that repository once and
    /// fetches nothing: the fetch was the moment. The first reading is the line's baseline, and a ref move that leaves origin's line
    /// where it was is no moment.
    /// </summary>
    [Fact]
    public async Task Origin_s_line_moving_asks_each_open_pull_request_once_and_fetches_nothing()
    {
        _world.Entries.AddRange([Landing("engine", "feature/a"), Landing("engine", "feature/b", PullRequestStates.Open, _now.AddHours(-1))]);
        _world.Origin["engine"] = "a1";
        await using var moments = await Opened();

        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Empty(_world.Asked);

        _world.Origin["engine"] = "a2";
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Equal(["feature/a", "feature/b"], _world.Asked.Order(StringComparer.Ordinal));
        Assert.Empty(_world.Fetches);
        // The person's fetch is counted: the window's focus inside its minute fetches nothing more.
        Assert.Equal(_now, _world.FetchedAt["engine"]);

        // A commit in a tree a minute later: refs moved, origin's line did not.
        _now += TimeSpan.FromMinutes(2);
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Equal(2, _world.Asked.Count);

        var fired = Assert.Single(Lines("chain.fired"));
        Assert.Equal((ChainRanks.Ask, ChangeTopics.Refs, 2), (fired.GetProperty("link").GetInt32(), fired.GetProperty("topic").GetString(), fired.GetProperty("count").GetInt32()));
    }

    /// <summary>
    /// D148's minute bounds every moment: a second inside it asks nothing and fetches nothing, whichever moment it is, and the first
    /// after it asks again. The record's last ask counts, and so does this process's own.
    /// </summary>
    [Fact]
    public async Task A_second_moment_inside_the_minute_asks_nothing()
    {
        _world.Entries.Add(Landing("engine", "feature/a"));
        _world.Registry.Add(Repository("engine"));
        _world.Origin["engine"] = "a1";
        await using var moments = await Opened();

        moments.Focused();
        await Step(moments);
        Assert.Equal(["feature/a"], _world.Asked);
        Assert.Equal(["engine"], _world.Fetches);

        _now += TimeSpan.FromSeconds(20);
        _world.Origin["engine"] = "a2";
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        PullRequestMoments.Pushed(_home, "engine");
        await Step(moments);
        moments.Focused();
        await Step(moments);
        Assert.Single(_world.Asked);
        Assert.Single(_world.Fetches);

        // A plugin that was not ready kept nothing on the record: this process's own ask still bounds the minute.
        _world.Keeps = false;
        _now += TimeSpan.FromSeconds(41);
        moments.Focused();
        await Step(moments);
        Assert.Equal(2, _world.Asked.Count);
        _now += TimeSpan.FromSeconds(30);
        moments.Focused();
        await Step(moments);
        Assert.Equal(2, _world.Asked.Count);
    }

    /// <summary>
    /// FABRIC-END6b: origin's line moving inside a pull request's minute stays a moment. Nothing is asked inside the minute, at the move
    /// or at a wake after it; the first wake past the minute asks once, fetching nothing the person's fetch brought within it; a wake after
    /// that asks nothing more, the move having been asked about.
    /// </summary>
    [Fact]
    public async Task A_line_moved_inside_the_minute_is_asked_at_the_first_wake_past_it_and_never_twice()
    {
        _world.Entries.Add(Landing("engine", "feature/a"));
        _world.Registry.Add(Repository("engine"));
        _world.Origin["engine"] = "a1";
        await using var moments = await Opened();

        // Daoris's push: asked, its line fetched, and origin's line read as its baseline.
        PullRequestMoments.Pushed(_home, "engine");
        await Step(moments);
        Assert.Equal(["feature/a"], _world.Asked);

        // The person's fetch moves origin's line at +20 s: inside the minute, nothing is asked.
        _now += TimeSpan.FromSeconds(20);
        _world.Origin["engine"] = "a2";
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Single(_world.Asked);

        // A ref move at +50 s that leaves origin's line where it was: still inside the minute.
        _now += TimeSpan.FromSeconds(30);
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Single(_world.Asked);

        // The first wake past the minute, at +65 s: the move is asked about, once, and nothing is fetched (the person's was at +20 s).
        _now += TimeSpan.FromSeconds(15);
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Equal(["feature/a", "feature/a"], _world.Asked);
        Assert.Single(_world.Fetches);

        // Never twice: later wakes, inside the next minute and past it, ask nothing more for that move.
        _now += TimeSpan.FromSeconds(10);
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        _now += TimeSpan.FromMinutes(2);
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);
        Assert.Equal(2, _world.Asked.Count);
    }

    /// <summary>
    /// Right after Daoris pushes: asked once, and the line not fetched where the landing's press just fetched it. An automatic landing
    /// fetched nothing, so its push fetches the line once, before it asks, so a completed answer's merge commit is here to confirm.
    /// </summary>
    [Fact]
    public async Task Daoris_s_push_asks_once_and_fetches_only_where_its_landing_did_not()
    {
        _world.Entries.AddRange([Landing("engine", "feature/a"), Landing("tools", "feature/t")]);
        _world.Registry.AddRange([Repository("engine"), Repository("tools")]);
        _world.FetchedAt["engine"] = _now.AddSeconds(-20);
        await using var moments = await Opened();

        PullRequestMoments.Pushed(_home, "engine");
        await Step(moments);
        Assert.Equal(["feature/a"], _world.Asked);
        Assert.Empty(_world.Fetches);

        PullRequestMoments.Pushed(_home, "tools");
        await Step(moments);
        Assert.Equal(["feature/a", "feature/t"], _world.Asked);
        Assert.Equal(["tools"], _world.Fetches);
        // The push's fetch comes before its ask.
        Assert.Equal(["ask feature/a", "fetch tools", "ask feature/t"], _world.Order);
    }

    /// <summary>
    /// The window's focus while a pull request is open: each repository with one is fetched once and asked, a repository with none is
    /// neither, and the registry is read once.
    /// </summary>
    [Fact]
    public async Task The_window_s_focus_fetches_and_asks_each_repository_with_an_open_pull_request_once()
    {
        _world.Entries.AddRange([Landing("engine", "feature/a"), Landing("engine", "feature/b"), Landing("site", "feature/s", PullRequestStates.Completed)]);
        _world.Registry.AddRange([Repository("engine"), Repository("site"), Repository("tools")]);
        await using var moments = await Opened();

        moments.Focused();
        await Step(moments);

        Assert.Equal(["feature/a", "feature/b"], _world.Asked.Order(StringComparer.Ordinal));
        Assert.Equal(["engine"], _world.Fetches);
        Assert.Equal(1, _world.RegistryReads);
        Assert.True(_world.Order.IndexOf("fetch engine") < _world.Order.IndexOf("ask feature/a"), "the focus's fetch comes before its ask");
    }

    /// <summary>A wake that concerns no open pull request reads no line and no registry, fetches nothing and asks nothing.</summary>
    [Fact]
    public async Task A_wake_that_concerns_no_open_pull_request_asks_and_fetches_nothing()
    {
        _world.Entries.AddRange([Landing("engine", "feature/d", PullRequestStates.Completed), Landing("engine", "feature/a") with { Pushed = false }]);
        _world.Registry.Add(Repository("engine"));
        await using var moments = await Opened();

        moments.Focused();
        PullRequestMoments.Moved(_home, Repository("engine"));
        PullRequestMoments.Pushed(_home, "engine");
        await Step(moments);

        Assert.Empty(_world.Asked);
        Assert.Empty(_world.Fetches);
        Assert.Equal((0, 0), (_world.RegistryReads, _world.LineReads));
        Assert.Empty(Lines("chain.fired"));
    }

    /// <summary>
    /// Never at a removing press (D148 point 2, the owner's answer): a press is no moment, and the sweep's own ref moves, a branch
    /// deleted, leave origin's line where it was, so nothing is asked and nothing fetched.
    /// </summary>
    [Fact]
    public async Task A_removing_press_asks_and_fetches_nothing()
    {
        _world.Entries.Add(Landing("engine", "feature/a"));
        _world.Registry.Add(Repository("engine"));
        _world.Origin["engine"] = "a1";
        await using var moments = await Opened();
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);

        await moments.Chain.FireAsync([new ChainEvent(ChainEvents.Press, "lines", ChainEvents.Press)], CancellationToken.None);
        PullRequestMoments.Moved(_home, Repository("engine"));
        await Step(moments);

        Assert.Empty(_world.Asked);
        Assert.Empty(_world.Fetches);
    }

    /// <summary>
    /// FABRIC-END6c, finding 1: a removing press, bringing up to date or a landing holding the repository, a moment skips it, fetching
    /// and asking nothing, rather than run beside it; it is owed, and asked once the hold is let go, whatever moment it was.
    /// </summary>
    [Fact]
    public async Task A_moment_skips_a_held_repository_and_asks_once_it_is_let_go()
    {
        _world.Entries.AddRange([Landing("engine", "feature/a"), Landing("tools", "feature/t")]);
        _world.Registry.AddRange([Repository("engine"), Repository("tools")]);
        await using var moments = await Opened();

        using (RepositoryHolds.Hold(_home, "engine"))
        {
            PullRequestMoments.Pushed(_home, "engine");
            await Step(moments);
            moments.Focused();
            await Step(moments);
            Assert.Equal(["feature/t"], _world.Asked);
            Assert.Equal(["tools"], _world.Fetches);
        }

        await Step(moments);
        Assert.Equal(["feature/t", "feature/a"], _world.Asked);
        Assert.Equal(["tools", "engine"], _world.Fetches);

        // Owed once: the next wake inside the minute asks nothing more.
        PullRequestMoments.Pushed(_home, "engine");
        await Step(moments);
        Assert.Equal(2, _world.Asked.Count);
    }

    /// <summary>
    /// FABRIC-END6c, finding 2: D148's minute is shared with every other occasion in this process. A look asking the pull request now
    /// leaves a moment nothing to ask, and a moment's ask leaves the look's minute taken.
    /// </summary>
    [Fact]
    public async Task Another_occasion_s_minute_bounds_a_moment_and_a_moment_s_bounds_another_occasion()
    {
        var a = Landing("engine", "feature/a");
        var b = Landing("engine", "feature/b");
        _world.Entries.AddRange([a, b]);
        _world.Registry.Add(Repository("engine"));
        _world.Keeps = false;
        await using var moments = await Opened();

        Assert.True(PullRequestMinute.TryTake(_home, a, _now));
        moments.Focused();
        await Step(moments);

        Assert.Equal(["feature/b"], _world.Asked);
        Assert.False(PullRequestMinute.TryTake(_home, b, _now.AddSeconds(30)));
    }

    /// <summary>
    /// A completed answer judges the repository's ends again at once, as Daoris's own move does, so its end is reached with no press;
    /// an answer that is not completed judges nothing.
    /// </summary>
    [Fact]
    public async Task A_completed_answer_judges_the_repository_again_at_once()
    {
        _world.Entries.AddRange([Landing("engine", "feature/a"), Landing("tools", "feature/t")]);
        _world.Registry.AddRange([Repository("engine"), Repository("tools")]);
        _world.Answers["feature/a"] = PullRequestStates.Completed;
        await using var moments = await Opened();

        moments.Focused();
        await Step(moments);

        Assert.Equal(["engine"], _world.Judged);
    }

    /// <summary>Daoris's push in a repository the ref watch has not told yet: its checkout is found in the registry.</summary>
    [Fact]
    public async Task A_push_in_a_repository_not_yet_heard_finds_its_checkout_in_the_registry()
    {
        _world.Entries.Add(Landing("engine", "feature/a"));
        _world.Registry.Add(Repository("engine"));
        await using var moments = await Opened();

        PullRequestMoments.Pushed(_home, "engine");
        await Step(moments);

        Assert.Equal(["feature/a"], _world.Asked);
        Assert.Equal(1, _world.RegistryReads);
    }

    /// <summary>The ref watch and the landing tell the moments of their own home alone, and nothing once they are disposed.</summary>
    [Fact]
    public async Task Only_the_home_s_own_moments_hear_and_none_once_disposed()
    {
        _world.Entries.Add(Landing("engine", "feature/a"));
        _world.Registry.Add(Repository("engine"));
        var moments = await Opened();

        PullRequestMoments.Pushed(Path.Combine(_home, "other"), "engine");
        await Step(moments);
        Assert.Empty(_world.Asked);

        await moments.DisposeAsync();
        PullRequestMoments.Pushed(_home, "engine");
        Assert.Empty(_world.Asked);
    }

    /// <summary>The fake world: the record, the registry, origin's lines and the fetch ledger in memory, each read and act counted.</summary>
    private sealed class World(Func<DateTimeOffset> clock) : IPullRequestMomentWorld
    {
        public List<LandedBranch> Entries { get; } = [];

        public List<RefRepository> Registry { get; } = [];

        public Dictionary<string, string?> Origin { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, DateTimeOffset> FetchedAt { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Answers { get; } = new(StringComparer.Ordinal);

        public List<string> Asked { get; } = [];

        public List<string> Fetches { get; } = [];

        public List<string> Judged { get; } = [];

        public List<string> Order { get; } = [];

        public int RegistryReads { get; private set; }

        public int LineReads { get; private set; }

        /// <summary>Whether an answer is kept on the record, as a plugin that answered is; false is one that was not ready.</summary>
        public bool Keeps { get; set; } = true;

        public IReadOnlyList<LandedBranch> Open() => [.. Entries.Where(PullRequestMoments.Concerns)];

        public Task<IReadOnlyList<RefRepository>> RepositoriesAsync(CancellationToken ct)
        {
            RegistryReads++;
            return Task.FromResult<IReadOnlyList<RefRepository>>([.. Registry]);
        }

        public Task<string?> LineAsync(RefRepository repository, CancellationToken ct)
        {
            LineReads++;
            return Task.FromResult<string?>("main");
        }

        public string? OriginTip(RefRepository repository, string line) => Origin.GetValueOrDefault(repository.Repository);

        public DateTimeOffset? Fetched(RefRepository repository, string line) =>
            FetchedAt.TryGetValue(repository.Repository, out var at) ? at : null;

        public void HeardFetched(RefRepository repository, string line, DateTimeOffset at) => FetchedAt[repository.Repository] = at;

        public Task FetchAsync(RefRepository repository, string line, CancellationToken ct)
        {
            Fetches.Add(repository.Repository);
            Order.Add($"fetch {repository.Repository}");
            FetchedAt[repository.Repository] = clock();
            return Task.CompletedTask;
        }

        public Task<int> AskAsync(RefRepository repository, IReadOnlyList<LandedBranch> entries, CancellationToken ct)
        {
            var completed = 0;
            foreach (var entry in entries)
            {
                Asked.Add(entry.Branch);
                Order.Add($"ask {entry.Branch}");
                var state = Answers.GetValueOrDefault(entry.Branch, PullRequestStates.Open);
                if (state == PullRequestStates.Completed) completed++;
                if (!Keeps) continue;
                var index = Entries.FindIndex(each => each.Branch == entry.Branch && each.Repository == entry.Repository);
                Entries[index] = Entries[index] with { PullRequestState = new PullRequestState(state) { AskedAt = clock() } };
            }

            return Task.FromResult(completed);
        }

        public void Judge(RefRepository repository) => Judged.Add(repository.Repository);
    }
}
