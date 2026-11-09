using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>resolve</c>'s hand-off over real git (§2.4 of the process steps' design), its resolver scripted: a code conflict asked once with its
/// files and hunks; a four-stop replay; a records stop settled with no ask; the tries counted per stop across attempts and the bound spent;
/// a timeout, a lapse and a restart each stopping the resolver first and putting the tree back with its edits kept and applied at the next
/// ask; a resolver's own commit and <c>rebase --continue</c>; an unnamed file refused; and the rebase's <c>back</c> way reading the resolution.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ResolveDoorGitTests : LandedFixture
{
    private TreeHandOffs Offs => new(Home, () => StoreSettled.Yes);

    [Fact]
    public async Task A_code_conflict_is_asked_once_with_its_files_and_hunks_and_finishes_its_rebase()
    {
        var (root, tree, start) = await ConflictAsync(stops: 1);
        var resolvers = new Scripted(Settle);

        await Door(resolvers).PerformAsync(start, CancellationToken.None);

        Assert.Empty(Logged);
        var words = Assert.Single(resolvers.Words);
        Assert.Contains("`f0.txt` (1 hunk)", words, StringComparison.Ordinal);
        Assert.Contains("<<<<<<<", words, StringComparison.Ordinal);
        Assert.Contains("(try 1 of 3)", words, StringComparison.Ordinal);
        Assert.Contains("do not run `git rebase --continue`", words, StringComparison.Ordinal);
        var made = new ResolveRecords(Home).Of("r1", "resolve", 1)!;
        Assert.Equal(ResolveNode.Resolved, made.Outcome);
        var rebase = new RebaseRecords(Home).Of("r1", "rebase", 1)!;
        Assert.Equal(made.Commit, rebase.Resolution);
        Assert.Equal(made.Commit, (await GitAsync(root, "rev-parse", StepRefs.Of("r1", "rebase", 1))).Trim());
        Assert.Equal(await HeadAsync(root), (await GitAsync(root, "rev-parse", $"{made.Commit}^")).Trim());
        Assert.Equal("settled\n", await GitAsync(root, "show", $"{made.Commit}:f0.txt"));
        await AsHandedAsync(tree, start.Work.Tip);
        Assert.Single(resolvers.StoppedMidRebase);
    }

    [Fact]
    public async Task A_four_stop_replay_asks_each_stop_as_its_own_first_try()
    {
        var (root, tree, start) = await ConflictAsync(stops: 4);
        var resolvers = new Scripted(Settle);

        await Door(resolvers).PerformAsync(start, CancellationToken.None);

        Assert.Equal(4, resolvers.Words.Count);
        Assert.All(resolvers.Words, words => Assert.Contains("(try 1 of 3)", words, StringComparison.Ordinal));
        var made = new ResolveRecords(Home).Of("r1", "resolve", 1)!;
        Assert.Equal(ResolveNode.Resolved, made.Outcome);
        Assert.Equal("settled\n", await GitAsync(root, "show", $"{made.Commit}:f3.txt"));
        await AsHandedAsync(tree, start.Work.Tip);
    }

    [Fact]
    public async Task A_records_stop_after_the_conflict_is_settled_with_no_ask()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitattributes", "CHANGELOG.md merge=union\n", "union records");
        await CommitAsync(root, "CHANGELOG.md", "# Changes\n", "the changelog");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "f0.txt", "from the work\n", "Add f0");
        await CommitAsync(tree.Path, "CHANGELOG.md", "# Changes\n- the work's\n", "the work's change");
        await CommitAsync(root, "f0.txt", "from the line\n", "line f0");
        await CommitAsync(root, "CHANGELOG.md", "# Changes\n- the line's\n", "the line's change");
        var start = await RebasedAsync(root, tree);
        var resolvers = new Scripted(Settle);

        await Door(resolvers).PerformAsync(start, CancellationToken.None);

        Assert.Single(resolvers.Words);
        var made = new ResolveRecords(Home).Of("r1", "resolve", 1)!;
        Assert.Equal(ResolveNode.Resolved, made.Outcome);
        var log = await GitAsync(root, "show", $"{made.Commit}:CHANGELOG.md");
        Assert.Contains("- the work's", log, StringComparison.Ordinal);
        Assert.Contains("- the line's", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_timeout_mid_edit_stops_the_resolver_first_and_its_edits_are_kept_and_applied_at_the_next_ask()
    {
        var (_, tree, start) = await ConflictAsync(stops: 1);
        var seen = "";
        var resolvers = new Scripted(
            async row =>
            {
                await File.WriteAllTextAsync(Path.Combine(row.Tree, "f0.txt"), "half\n");
                return ResolverEnds.TimedOut;
            },
            async row =>
            {
                seen = await File.ReadAllTextAsync(Path.Combine(row.Tree, "f0.txt"));
                return await Settle(row);
            });
        var door = Door(resolvers);

        await door.PerformAsync(start, CancellationToken.None);

        Assert.Equal([true], resolvers.StoppedMidRebase);
        await AsHandedAsync(tree, start.Work.Tip);
        Assert.Null(new ResolveRecords(Home).Of("r1", "resolve", 1));
        Assert.Equal("half\n", await GitAsync(tree.Path, "show", $"{StepRefs.EditsOf("r1", "resolve")}:f0.txt"));
        Assert.Null(Offs.Of(tree.Path));

        await door.PerformAsync(start, CancellationToken.None);

        Assert.Equal("half\n", seen.ReplaceLineEndings("\n"));
        Assert.Contains("(try 2 of 3)", resolvers.Words[^1], StringComparison.Ordinal);
        Assert.Equal(ResolveNode.Resolved, new ResolveRecords(Home).Of("r1", "resolve", 1)!.Outcome);
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("continue")]
    public async Task A_resolver_s_own_commit_or_continue_is_read_as_its_edits(string how)
    {
        var (root, tree, start) = await ConflictAsync(stops: 1);
        var resolvers = new Scripted(async row =>
        {
            await Settle(row);
            await GitAsync(row.Tree, how == "commit" ? ["commit", "--quiet", "-m", "settled it"] : ["-c", "core.editor=:", "rebase", "--continue"]);
            return ResolverEnds.Ended;
        });

        await Door(resolvers).PerformAsync(start, CancellationToken.None);

        var made = new ResolveRecords(Home).Of("r1", "resolve", 1)!;
        Assert.Equal(ResolveNode.Resolved, made.Outcome);
        Assert.Equal("settled\n", await GitAsync(root, "show", $"{made.Commit}:f0.txt"));
        await AsHandedAsync(tree, start.Work.Tip);
    }

    [Fact]
    public async Task Tries_are_counted_per_stop_across_attempts_and_the_spent_bound_fails_said()
    {
        var (_, tree, start) = await ConflictAsync(stops: 1);
        var resolvers = new Scripted(_ => Task.FromResult(ResolverEnds.TimedOut));
        var door = Door(resolvers);

        await door.PerformAsync(start, CancellationToken.None);
        await door.PerformAsync(start with { Attempt = 2 }, CancellationToken.None);
        await door.PerformAsync(start with { Attempt = 2 }, CancellationToken.None);
        await door.PerformAsync(start with { Attempt = 2 }, CancellationToken.None);

        Assert.Equal(["(try 1 of 3)", "(try 2 of 3)", "(try 3 of 3)"], resolvers.Words.Select(words => words[(words.IndexOf("(try", StringComparison.Ordinal))..][..12]));
        var failed = new ResolveRecords(Home).Of("r1", "resolve", 2)!;
        Assert.Equal(NodeStates.Failed, failed.Outcome);
        Assert.StartsWith("Waits on you: `f0.txt`", failed.Said, StringComparison.Ordinal);
        await AsHandedAsync(tree, start.Work.Tip);
    }

    [Fact]
    public async Task A_resolution_that_changes_an_unnamed_file_is_refused()
    {
        var (_, tree, start) = await ConflictAsync(stops: 1);
        var resolvers = new Scripted(async row =>
        {
            await File.WriteAllTextAsync(Path.Combine(row.Tree, "other.txt"), "not asked for\n");
            return await Settle(row);
        });

        await Door(resolvers).PerformAsync(start, CancellationToken.None);

        var made = new ResolveRecords(Home).Of("r1", "resolve", 1)!;
        Assert.Equal(NodeStates.Failed, made.Outcome);
        Assert.Contains("`other.txt`, which the problem did not name", made.Said, StringComparison.Ordinal);
        await AsHandedAsync(tree, start.Work.Tip);
    }

    [Fact]
    public async Task A_lapse_stops_the_resolver_and_puts_the_tree_back_with_nothing_written()
    {
        var (_, tree, start) = await ConflictAsync(stops: 1);
        var resolvers = new Scripted(Settle);
        var door = new ResolveDoor(Home, resolvers, Reserve) { Lapsed = _ => true, HandOffs = Offs };

        await door.PerformAsync(start, CancellationToken.None);

        Assert.Equal([true], resolvers.StoppedMidRebase);
        Assert.Null(new ResolveRecords(Home).Of("r1", "resolve", 1));
        await AsHandedAsync(tree, start.Work.Tip);
        Assert.Null(Offs.Of(tree.Path));
    }

    [Fact]
    public async Task A_restart_with_the_hand_off_open_stops_the_resolver_and_restores_the_tree_keeping_its_edits()
    {
        var (root, tree, start) = await ConflictAsync(stops: 1);
        var rebase = new RebaseRecords(Home).Of("r1", "rebase", 1)!;
        var row = Offs.Write(new TreeHandOff(tree.Path, "engine", tree.Branch, start.Work.Tip, "r1", "resolve", 1, "stop", "session",
            TreeHandOff.AskKey("r1", "resolve", 1, 1), TreeHandOffPhases.Reserved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30)))!;
        var hand = new ResolveHand(root, tree.Path, "engine", tree.Branch, start.Work.Tip, rebase.Onto, rebase.LeftOut, StepRefs.EditsOf("r1", "resolve"));
        Assert.Equal(ResolveSteps.Stopped, (await new ResolveTree(new SessionTrees(Home).Settles).PrepareAsync(hand, CancellationToken.None)).Kind);
        Offs.Advance(row, TreeHandOffPhases.Asked, "s-41");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "f0.txt"), "mid-edit\n");
        var resolvers = new Scripted(Settle);

        await ResolveDoor.RestoreOpenAsync(Home, Offs, resolvers, CancellationToken.None);

        Assert.Equal([true], resolvers.StoppedMidRebase);
        await AsHandedAsync(tree, start.Work.Tip);
        Assert.Equal("mid-edit\n", await GitAsync(tree.Path, "show", $"{StepRefs.EditsOf("r1", "resolve")}:f0.txt"));
        Assert.Null(Offs.Of(tree.Path));
    }

    [Fact]
    public async Task The_line_moving_50_times_while_it_waits_makes_one_ask_and_the_rebase_s_back_way_reads_the_resolution()
    {
        var (root, _, start) = await ConflictAsync(stops: 1);
        var resolvers = new Scripted(async row =>
        {
            for (var at = 0; at < 50; at++) await GitAsync(root, "commit", "--quiet", "--allow-empty", "-m", $"the line moves {at}");
            return await Settle(row);
        });

        await Door(resolvers).PerformAsync(start, CancellationToken.None);

        Assert.Single(resolvers.Words);
        var resolution = new ResolveRecords(Home).Of("r1", "resolve", 1)!.Commit;
        var rebases = new RebaseDoor(Home);
        var unmoved = await rebases.PerformAsync(Rebase(2, start.Work, new RebaseRecords(Home).Of("r1", "rebase", 1)!.Onto), CancellationToken.None);
        Assert.Equal((RebaseOutcomes.Ready, resolution), (unmoved.Outcome, unmoved.Commit));

        var moved = await rebases.PerformAsync(Rebase(3, start.Work, await HeadAsync(root)), CancellationToken.None);
        Assert.Equal(RebaseOutcomes.Ready, moved.Outcome);
        Assert.Equal(await HeadAsync(root), (await GitAsync(root, "rev-parse", $"{moved.Commit}^")).Trim());
        Assert.Equal("settled\n", await GitAsync(root, "show", $"{moved.Commit}:f0.txt"));
    }

    private List<string> Logged { get; } = [];

    private ResolveDoor Door(Scripted resolvers) => new(Home, resolvers, Reserve, Logged.Add) { Lapsed = _ => false, HandOffs = Offs };

    private Task<TreeReserved> Reserve(TreeHandOff row, CancellationToken ct) => Task.FromResult(new TreeReserved(Offs.Write(row), null, null));

    /// <summary>A work of <paramref name="stops"/> commits, each adding a file the line added otherwise, rebased to its first stop.</summary>
    private async Task<(string Root, TreeOpened Tree, ResolveStart Start)> ConflictAsync(int stops)
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        for (var at = 0; at < stops; at++) await CommitAsync(tree.Path, $"f{at}.txt", "from the work\n", $"Add f{at}");
        for (var at = 0; at < stops; at++) await CommitAsync(root, $"f{at}.txt", "from the line\n", $"line f{at}");
        return (root, tree, await RebasedAsync(root, tree));
    }

    private async Task<ResolveStart> RebasedAsync(string root, TreeOpened tree)
    {
        var workspace = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(tree.Path)))!;
        var work = new RebaseWork(root, "engine", workspace, tree.Branch, await HeadAsync(tree.Path));
        var made = await new RebaseDoor(Home).PerformAsync(Rebase(1, work, await HeadAsync(root)), CancellationToken.None);
        Assert.Equal(RebaseOutcomes.Conflicted, made.Outcome);
        return new ResolveStart("r1", "resolve", 1, work, "rebase", 1, "main") { Authority = StepAuthority.OfDoor("r1", "resolve", 1) };
    }

    private static RebaseStart Rebase(int attempt, RebaseWork work, string onto) =>
        new("r1", "rebase", attempt, work, new RebaseTarget("refs/heads/main", onto)) { Authority = StepAuthority.OfDoor("r1", "rebase", attempt) };

    /// <summary>The resolver settling every conflicted file of its stop and staging it.</summary>
    private static async Task<string> Settle(TreeHandOff row)
    {
        foreach (var path in (await GitAsync(row.Tree, "diff", "--name-only", "--diff-filter=U")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await File.WriteAllTextAsync(Path.Combine(row.Tree, path), "settled\n");
            await GitAsync(row.Tree, "add", "--", path);
        }

        return ResolverEnds.Ended;
    }

    /// <summary>The tree as handed: on its branch at its tip, clean, no rebase standing.</summary>
    private static async Task AsHandedAsync(TreeOpened tree, string tip)
    {
        Assert.Equal($"refs/heads/{tree.Branch}", (await GitAsync(tree.Path, "symbolic-ref", "HEAD")).Trim());
        Assert.Equal(tip, await HeadAsync(tree.Path));
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    private static async Task<bool> RebasingAsync(string tree) =>
        Directory.Exists((await GitAsync(tree, "rev-parse", "--path-format=absolute", "--git-path", "rebase-merge")).Trim());

    /// <summary>A resolver whose asks run in order, the last repeated; each stop of it noted with whether the tree stood mid-rebase then.</summary>
    private sealed class Scripted(params Func<TreeHandOff, Task<string>>[] asks) : IResolvers
    {
        private int _asked;

        public List<string> Words { get; } = [];

        public List<bool> StoppedMidRebase { get; } = [];

        public Task<(string? Session, string? Why)> SessionAsync(TreeHandOff row, CancellationToken ct) => Task.FromResult<(string?, string?)>(("s-41", null));

        public async Task<string> AskAsync(TreeHandOff row, string session, string words, Func<bool> lapsed, CancellationToken ct)
        {
            Words.Add(words);
            if (lapsed()) return ResolverEnds.Lapsed;
            return await asks[Math.Min(_asked++, asks.Length - 1)](row);
        }

        public async Task StopAsync(TreeHandOff row, CancellationToken ct) => StoppedMidRebase.Add(await RebasingAsync(row.Tree));
    }
}
