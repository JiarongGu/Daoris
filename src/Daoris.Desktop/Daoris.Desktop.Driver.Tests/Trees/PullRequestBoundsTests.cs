using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END6c (the review of FABRIC-END6, before plugin mode is relied on): one fetch of a repository's line at a time, shared by every
/// fetch path; a repository held by a removing press, bringing it up to date or a landing; D148's minute shared by every occasion; an
/// answer kept only where it was asked after the kept one, a completed one never replaced; each answer stamped with its own request's
/// time; a person's fetch read from git's files; and a focus kept until the moments exist. No process starts here.
/// </summary>
public sealed class PullRequestBoundsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-bounds-" + Guid.NewGuid().ToString("N")[..8]);

    public PullRequestBoundsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static LandedBranch Landing(string branch, string session = "s1") =>
        new("engine", "aurora", branch, "main", new string('b', 40), session, "q1", "Fix", At.AddDays(-1))
        {
            Plugin = "acme.asks",
            Pushed = true,
        };

    /// <summary>
    /// Finding 1: a fetch of the line in flight counts as made, whichever path started it (a landing's press, bringing up to date, a
    /// moment): a second fetch of that line joins it rather than start another, and the next one after it ends fetches again.
    /// </summary>
    [Fact]
    public async Task A_fetch_in_flight_counts_as_made_and_a_second_fetch_of_the_line_joins_it()
    {
        var started = 0;
        var gate = new TaskCompletionSource<(string?, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<(string? Failed, string? Url)> Fetch(CancellationToken ct)
        {
            Interlocked.Increment(ref started);
            return gate.Task;
        }

        var root = Path.Combine(_home, "engine");
        var first = RemoteFetches.OnceAsync(root, "main", Fetch, CancellationToken.None);
        var second = RemoteFetches.OnceAsync(root, "main", Fetch, CancellationToken.None);
        gate.SetResult((null, "https://example.test/engine"));

        Assert.Equal((null, "https://example.test/engine"), await first);
        Assert.Equal((null, "https://example.test/engine"), await second);
        Assert.Equal(1, started);

        await RemoteFetches.OnceAsync(root, "main", Fetch, CancellationToken.None);
        Assert.Equal(2, started);
    }

    /// <summary>A fetch its own caller cancelled is no answer for the one that joined it: that one fetches afresh.</summary>
    [Fact]
    public async Task A_joined_fetch_whose_caller_cancelled_it_is_fetched_afresh()
    {
        var started = 0;
        using var cancelled = new CancellationTokenSource();
        Task<(string? Failed, string? Url)> Fetch(CancellationToken ct)
        {
            return Interlocked.Increment(ref started) == 1
                ? Task.Delay(Timeout.Infinite, ct).ContinueWith<(string?, string?)>(_ => throw new OperationCanceledException(ct), TaskScheduler.Default)
                : Task.FromResult<(string?, string?)>((null, "https://example.test/engine"));
        }

        var root = Path.Combine(_home, "engine");
        var first = RemoteFetches.OnceAsync(root, "main", Fetch, cancelled.Token);
        var second = RemoteFetches.OnceAsync(root, "main", Fetch, CancellationToken.None);
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal((null, "https://example.test/engine"), await second);
        Assert.Equal(2, started);
    }

    /// <summary>
    /// Finding 1: a removing press and bringing up to date hold the repository alone through the replay's lock, and so hold it for the
    /// moments too, in this process; nested holds let go with the last.
    /// </summary>
    [Fact]
    public void The_replays_lock_holds_the_repository_for_the_moments_until_the_last_hold_is_let_go()
    {
        Assert.False(RepositoryHolds.Held(_home, "engine"));
        using (var replay = TreeLock.TryReplaying(_home, "aurora", "engine"))
        {
            Assert.NotNull(replay);
            Assert.True(RepositoryHolds.Held(_home, "Engine"));
            Assert.False(RepositoryHolds.Held(_home, "tools"));
            Assert.False(RepositoryHolds.Held(Path.Combine(_home, "other"), "engine"));
            using (RepositoryHolds.Hold(_home, "engine"))
            {
                Assert.True(RepositoryHolds.Held(_home, "engine"));
            }

            Assert.True(RepositoryHolds.Held(_home, "engine"));
        }

        Assert.False(RepositoryHolds.Held(_home, "engine"));
    }

    /// <summary>
    /// Finding 2: D148's minute is one per pull request in this process, whichever occasion takes it; a later request's own time is
    /// kept, and a minute whose ask was never sent is given back.
    /// </summary>
    [Fact]
    public void The_minute_one_occasion_takes_bounds_every_other()
    {
        var entry = Landing("feature/a");
        Assert.False(PullRequestMinute.Within(_home, entry, At));
        Assert.True(PullRequestMinute.TryTake(_home, entry, At));
        Assert.True(PullRequestMinute.Within(_home, entry, At.AddSeconds(30)));
        Assert.False(PullRequestMinute.TryTake(_home, entry, At.AddSeconds(30)));
        Assert.True(PullRequestMinute.TryTake(_home, Landing("feature/b", "s2"), At.AddSeconds(30)));

        // Its own request went out 40 s after the occasion took it: the minute counts from there.
        PullRequestMinute.Asked(_home, entry, At.AddSeconds(40));
        Assert.False(PullRequestMinute.TryTake(_home, entry, At.AddSeconds(70)));
        Assert.True(PullRequestMinute.TryTake(_home, entry, At.AddSeconds(100)));

        // Taken and never sent (the occasion's bound came first): given back, so the next occasion asks it.
        var c = Landing("feature/c", "s3");
        Assert.True(PullRequestMinute.TryTake(_home, c, At));
        PullRequestMinute.Release(_home, c, At);
        Assert.True(PullRequestMinute.TryTake(_home, c, At.AddSeconds(5)));
        // A give-back for a minute someone else took since is not theirs to give.
        PullRequestMinute.Release(_home, c, At);
        Assert.True(PullRequestMinute.Within(_home, c, At.AddSeconds(10)));
    }

    /// <summary>
    /// Finding 2: an answer is kept only where it was asked no earlier than the kept one, so a moment's slow <c>open</c> never
    /// overwrites a concurrent look's <c>completed</c>; and <c>completed</c>, which is final, is never replaced. A failure older than the
    /// kept answer is not the latest word, and an answer older than the kept failure leaves it.
    /// </summary>
    [Fact]
    public void An_answer_asked_before_the_kept_one_is_not_kept_and_a_completed_one_is_never_replaced()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Landing("feature/a"));
        PullRequestState Said(string state, DateTimeOffset asked) => new(state) { Plugin = "acme.asks", AskedAt = asked };

        landings.Answered(landings.Landing("s1")!, Said(PullRequestStates.Open, At.AddSeconds(10)));
        landings.Answered(landings.Landing("s1")!, Said(PullRequestStates.Unknown, At));
        Assert.Equal((PullRequestStates.Open, At.AddSeconds(10)), (landings.Landing("s1")!.PullRequestState!.State, landings.Landing("s1")!.PullRequestState!.AskedAt));

        landings.Answered(landings.Landing("s1")!, Said(PullRequestStates.Completed, At.AddSeconds(20)) with
        {
            MergeCommit = new string('a', 40),
            SourceCommit = new string('b', 40),
        });
        landings.Answered(landings.Landing("s1")!, Said(PullRequestStates.Open, At.AddSeconds(5)));
        landings.Answered(landings.Landing("s1")!, Said(PullRequestStates.Open, At.AddSeconds(30)));
        Assert.Equal(PullRequestStates.Completed, landings.Landing("s1")!.PullRequestState!.State);

        landings.Record(Landing("feature/b", "s2"));
        landings.AskFailed(landings.Landing("s2")!, new PullRequestAskFailed(PluginEvents.Late, "acme.asks", At.AddSeconds(20)));
        landings.Answered(landings.Landing("s2")!, Said(PullRequestStates.Open, At.AddSeconds(10)));
        Assert.Equal(PluginEvents.Late, landings.Landing("s2")!.PullRequestAskFailed!.Code);
        landings.AskFailed(landings.Landing("s2")!, new PullRequestAskFailed(PluginEvents.Errored, "acme.asks", At));
        Assert.Equal(PluginEvents.Late, landings.Landing("s2")!.PullRequestAskFailed!.Code);
    }

    /// <summary>
    /// Finding 3: each answer carries when its own request was sent, not when the occasion began: frames go one at a time, and a slow
    /// one before it moves the minute's start.
    /// </summary>
    [Fact]
    public async Task Each_answer_carries_when_its_own_request_was_sent()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.asks");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            id = "acme.asks",
            hooks = new { command = new[] { "node", "plugin.mjs" }, points = new[] { HookPoints.Land, HookPoints.State } },
        }));
        var clock = At;
        var plugins = new LandingPlugins(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Slow(() => clock = clock.AddSeconds(25))), clock: () => clock);
        StateAsk Ask(LandedBranch entry) => new(entry, "acme.asks", new StateFrame("engine", "aurora", _home, entry.Branch, "main", null, null));

        var answers = await plugins.AskStatesAsync([Ask(Landing("feature/a")), Ask(Landing("feature/b", "s2"))]);

        Assert.Equal([At, At.AddSeconds(25)], answers.Select(answer => answer.AskedAt));
    }

    /// <summary>
    /// Finding 4: a fetch that changed nothing is still a fetch: git's <c>FETCH_HEAD</c>, where it names the line, says when, across a
    /// restart; so does the newest entry of origin's line's log. An empty one, or one naming other branches alone, says nothing.
    /// </summary>
    [Fact]
    public void Git_s_files_say_when_the_line_was_last_fetched()
    {
        var git = Path.Combine(_home, "engine", ".git");
        Directory.CreateDirectory(git);
        Assert.Null(RemoteFetches.InGit(git, "main"));

        var head = Path.Combine(git, "FETCH_HEAD");
        File.WriteAllText(head, "");
        Assert.Null(RemoteFetches.InGit(git, "main"));

        File.WriteAllText(head, $"{new string('c', 40)}\tnot-for-merge\tbranch 'feature/x' of https://example.test/engine\n");
        Assert.Null(RemoteFetches.InGit(git, "main"));

        File.WriteAllText(head,
            $"{new string('a', 40)}\t\tbranch 'main' of https://example.test/engine\n{new string('c', 40)}\tnot-for-merge\tbranch 'feature/x' of https://example.test/engine\n");
        File.SetLastWriteTimeUtc(head, At.UtcDateTime);
        Assert.Equal(At, RemoteFetches.InGit(git, "main"));

        var log = Path.Combine(git, "logs", "refs", "remotes", "origin", "main");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        var later = At.AddMinutes(5).ToUnixTimeSeconds();
        File.WriteAllText(log,
            $"{new string('0', 40)} {new string('a', 40)} Someone <someone@example.test> {At.AddHours(-1).ToUnixTimeSeconds()} +0000\tfetch: storing head\n"
            + $"{new string('a', 40)} {new string('d', 40)} Someone <someone@example.test> {later} +0200\tfetch origin: fast-forward\n");
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(later), RemoteFetches.InGit(git, "main"));
    }

    /// <summary>Finding 5: a focus before there is anyone to tell is kept, once, and told when they come; one flag, not a queue.</summary>
    [Fact]
    public void A_focus_before_anyone_to_tell_is_told_once_they_come()
    {
        var focus = new PendingFocus();
        var told = 0;

        focus.Focused();
        focus.Focused();
        Assert.True(focus.Pending);

        focus.Ready(() => told++);
        Assert.Equal(1, told);
        Assert.False(focus.Pending);

        focus.Focused();
        Assert.Equal(2, told);

        focus.Ready(null);
        focus.Focused();
        Assert.True(focus.Pending);
        Assert.Equal(2, told);
    }

    /// <summary>A plugin that answers <c>open</c>, its clock moved on by each request it answers.</summary>
    private sealed class Slow(Action took) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land, HookPoints.State];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(new PluginLanding("x", false, null, "x"));

        public Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
        {
            took();
            return Task.FromResult(new PullRequestState(PullRequestStates.Open));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
