using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6's records and words, without git: where each session branch started (<c>session-branches.json</c>), where a
/// landing's branch grew from (<c>landings.json</c>'s <c>from</c>), and the sentences each door says a row in.
/// </summary>
public sealed class SyncRecordsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-sync-records-" + Guid.NewGuid().ToString("N")[..8]);

    public SyncRecordsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_session_branch_start_is_recorded_whole_and_replaced_by_the_same_branch()
    {
        var grown = new SessionBranches(_home);
        grown.Record(new GrownBranch("engine", "aurora", "daoris/s-1", "main", "aaaa1111", "daoris/s-0", At));
        grown.Record(new GrownBranch("game", "aurora", "daoris/s-1", null, "bbbb2222", null, At));
        grown.Record(new GrownBranch("Engine", "aurora", "daoris/s-1", "main", "cccc3333", null, At));

        Assert.Equal(2, grown.All().Count);
        var engine = grown.Of("engine", "daoris/s-1")!;
        Assert.Equal("cccc3333", engine.From);
        Assert.Null(engine.GrewFrom);
        var game = grown.Of("game", "daoris/s-1")!;
        Assert.Null(game.Line);
        Assert.Equal(At, game.At);

        var text = File.ReadAllText(Path.Combine(_home, SessionBranches.FileName));
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("\n", text);
    }

    [Fact]
    public void The_record_forgets_branches_in_one_repository_only()
    {
        var grown = new SessionBranches(_home);
        grown.Record(new GrownBranch("engine", "aurora", "daoris/s-1", "main", "aaaa1111", null, At));
        grown.Record(new GrownBranch("game", "aurora", "daoris/s-1", "main", "bbbb2222", null, At));

        grown.Forget("ENGINE", ["daoris/s-1"]);

        Assert.Null(grown.Of("engine", "daoris/s-1"));
        Assert.NotNull(grown.Of("game", "daoris/s-1"));
    }

    /// <summary>A file that does not read is no record — a tree still opens, and the press cuts by content.</summary>
    [Fact]
    public void A_record_that_does_not_read_is_no_record_and_an_entry_without_its_start_is_skipped()
    {
        File.WriteAllText(Path.Combine(_home, SessionBranches.FileName), "{ not json");
        Assert.Empty(new SessionBranches(_home).All());

        File.WriteAllText(Path.Combine(_home, SessionBranches.FileName), """
            { "branches": [ { "repository": "engine", "branch": "daoris/s-1" },
                            { "repository": "engine", "branch": "daoris/s-2", "from": "abcd1234" } ] }
            """);
        var only = Assert.Single(new SessionBranches(_home).All());
        Assert.Equal("daoris/s-2", only.Branch);
        Assert.Equal("default", only.Workspace);
    }

    /// <summary>A landing's branch keeps where its work grew from, and a replay moves its tip and that start together.</summary>
    [Fact]
    public void A_landed_branch_keeps_where_it_grew_from_and_a_replay_moves_both()
    {
        var landings = new LandedBranches(_home);
        landings.Record(new LandedBranch("engine", "aurora", "feature/q2-second", "main", "tip00001", "s2", "q2", "Second", At) { From = "from0001" });
        landings.Record(new LandedBranch("engine", "aurora", "feature/q3-third", "main", "tip00003", "s3", "q3", "Third", At));

        Assert.Equal("from0001", landings.Of("engine", "feature/q2-second")!.From);
        Assert.Null(landings.Of("engine", "feature/q3-third")!.From);

        landings.Moved("engine", "feature/q2-second", "tip00002", "line0002");
        var moved = landings.Of("engine", "feature/q2-second")!;
        Assert.Equal("tip00002", moved.Tip);
        Assert.Equal("line0002", moved.From);
        Assert.Equal("s2", moved.Session);
        Assert.Equal("tip00003", landings.Of("engine", "feature/q3-third")!.Tip);
    }

    [Fact]
    public void Each_pull_says_what_it_would_do_and_why_it_would_not()
    {
        LinePull Pull(string kind, int commits = 0, string? fetch = null) => new("engine", "aurora", "main", kind, "a", "b", commits, fetch, "git said so");

        Assert.Equal("engine  main  fast-forwards 2 commit(s) to `origin/main`", SyncWords.Describe(Pull(PullKind.FastForward, 2)));
        Assert.Contains("up to date", SyncWords.Describe(Pull(PullKind.UpToDate)));
        Assert.Contains("Daoris never pushes", SyncWords.Describe(Pull(PullKind.Ahead, 1)));
        Assert.Contains("only a fast-forward", SyncWords.Describe(Pull(PullKind.Diverged, 1)));
        Assert.Contains("uncommitted work", SyncWords.Describe(Pull(PullKind.Dirty)));
        Assert.Contains("another working tree", SyncWords.Describe(Pull(PullKind.CheckedOut)));
        Assert.Contains("no `origin/main`", SyncWords.Describe(Pull(PullKind.NoRemote)));
        Assert.Contains("no local `main`", SyncWords.Describe(Pull(PullKind.NoLocal)));
        Assert.Contains("git said so", SyncWords.Describe(Pull(PullKind.Unknown)));
        Assert.EndsWith("(not fetched: there is no `origin` remote here)", SyncWords.Describe(Pull(PullKind.UpToDate, fetch: "there is no `origin` remote here")));
        Assert.Contains("no line is set", SyncWords.Describe(new LinePull("engine", "aurora", null, PullKind.NoLine, null, null, 0, null, null)));
    }

    [Fact]
    public void Each_branch_says_what_its_replay_would_do_and_why_it_is_left()
    {
        RebaseItem Item(string kind, string? cutBy = null, int commits = 1, string? grewFrom = null) =>
            new("engine", "aurora", "daoris/s-2", false, kind, "main", "abcd1234", cutBy, grewFrom, commits, "git said so");

        Assert.Equal("engine  daoris/s-2  replays 1 commit(s) of its own onto `main`", SyncWords.Describe(Item(RebaseKind.Replay, CutBy.Line)));
        Assert.EndsWith("after `daoris/s-1`'s work, which reached the line", SyncWords.Describe(Item(RebaseKind.Replay, CutBy.Record, grewFrom: "daoris/s-1")));
        Assert.EndsWith("whose work reads on the line", SyncWords.Describe(Item(RebaseKind.Replay, CutBy.Content)));
        Assert.Contains("nothing of its own", SyncWords.Describe(Item(RebaseKind.Replay, CutBy.Line, commits: 0)));
        Assert.Contains("already on `main`", SyncWords.Describe(Item(RebaseKind.UpToDate)));
        Assert.Contains("still running or waiting", SyncWords.Describe(Item(RebaseKind.InUse)));
        Assert.Contains("uncommitted work", SyncWords.Describe(Item(RebaseKind.Dirty)));
        Assert.Contains("not a session's own", SyncWords.Describe(Item(RebaseKind.CheckedOut)));
        Assert.Contains("force push", SyncWords.Describe(Item(RebaseKind.Pushed)));
        Assert.Contains("it grew from `daoris/s-1`, whose work has not reached the line", SyncWords.Describe(Item(RebaseKind.Waits, grewFrom: "daoris/s-1")));
        Assert.Contains("git said so", SyncWords.Describe(Item(RebaseKind.Unknown)));
    }
}
