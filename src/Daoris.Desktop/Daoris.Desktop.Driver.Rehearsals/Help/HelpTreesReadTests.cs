using System.Diagnostics;
using Daoris.Driver;
using Daoris.Ports.Git;
using Daoris.Testing;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The room's tree reads over real git, including a large unchanged part and invalidation of its kept reads.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class HelpTreesReadTests : IDisposable
{
    /// <summary>The kept trees the machine log found on one repository's part.</summary>
    private const int Kept = 145;

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private readonly string _scratch;
    private readonly string _home;
    private readonly ITestOutputHelper _output;

    public HelpTreesReadTests(ITestOutputHelper output)
    {
        _output = output;
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "help-trees-read", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        TreeReadMemo.Release(_home);
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// Real folders, stamps and watches at the install's tree count. Git's cold answers are the fixture's known state;
    /// the other cases prove those answers over real git, without hundreds of cold starts before this warm measurement.
    /// </summary>
    [Fact]
    public async Task A_part_of_a_hundred_and_forty_five_unchanged_trees_reads_without_git_inside_the_room_bound()
    {
        var (root, part, trees) = await KeptAsync(Kept);
        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var git = new ScriptedGit().On(ask =>
        {
            var name = Path.GetFileName(ask.Folder);
            if (ask.Arguments.SequenceEqual(GitReadOnce.Layout))
                return new GitAnswer(0, $"{Path.Combine(root, ".git")}\n{ask.Folder}\ndaoris/{name}\n", "");
            if (ask.Arguments.Contains("status"))
            {
                var dirty = File.Exists(Path.Combine(ask.Folder, "draft.txt"));
                return new GitAnswer(0, ask.Arguments.Contains("--branch")
                    ? $"# branch.oid {head}\n# branch.head daoris/{name}\n" + (dirty ? "? draft.txt\n" : "")
                    : dirty ? "?? draft.txt\n" : "", "");
            }
            return ask.Arguments[0] == "log" ? new GitAnswer(0, "", "") : null;
        });
        using var chosen = FlowGit.Use(git);

        var (first, firstMs, firstStarts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));
        var asked = git.Asked.Count;
        var (again, againMs, againStarts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));
        _output.WriteLine($"{Kept} trees, the part's step: the home's first read {firstMs} ms ({firstStarts} git starts); "
                          + $"again, nothing moved {againMs} ms ({againStarts})");
        Assert.Equal(Kept, first.Asked.Count);
        Assert.Equal(Expected(trees), Said(first));
        Assert.Equal(Expected(trees), Said(again));
        Assert.True(asked > 0);
        Assert.Equal(asked, git.Asked.Count);
        Assert.Equal(0, againStarts);
        Assert.True(againMs < 500, $"the unchanged trees took {againMs} ms, past the room's 500 ms bound");
    }

    /// <summary>A tree's walk, as its first start shows it: the tree's layout, asked in one question for the walk.</summary>
    private static bool IsWalk(string start) => start.EndsWith(string.Join(' ', GitReadOnce.Layout), StringComparison.Ordinal);

    /// <summary>
    /// 🔴 A tree that changed is still read: a commit made in it, a file left uncommitted in it, its commits landed on the line, a
    /// branch that held its commits gone, each said as git says it now, where the trees nothing moved in are asked their status alone.
    /// </summary>
    [Fact]
    public async Task A_tree_that_changed_since_the_last_read_is_read_again()
    {
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var committed = (await sessionTrees.OpenAsync(root, "engine", "default")).Path;
        var editedTree = await sessionTrees.OpenAsync(root, "engine", "default");
        var edited = editedTree.Path;
        var landed = await sessionTrees.OpenAsync(root, "engine", "default");
        var held = await sessionTrees.OpenAsync(root, "engine", "default");
        var quiet = (await sessionTrees.OpenAsync(root, "engine", "default")).Path;
        await CommitAsync(edited, "tracked.txt", "as committed");
        await GitAsync(root, "branch", "feature/edited", editedTree.Branch);
        await CommitAsync(landed.Path, "landed.txt", "to land");
        await CommitAsync(held.Path, "held.txt", "held by a branch");
        await GitAsync(root, "branch", "feature/held", held.Branch);
        var all = (string[])[committed, edited, landed.Path, held.Path, quiet];
        var part = Part(all);

        var before = Said(await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None));
        Assert.Equal(await TruthAsync(all), before);
        Assert.Equal((0, 0, 1, 0, 0), (before[N(committed)].Item1, before[N(edited)].Item1, before[N(landed.Path)].Item1, before[N(held.Path)].Item1, before[N(quiet)].Item1));

        // Each changed as a person or a session changes a tree, with no file of git's moving for the edit.
        await CommitAsync(committed, "new.txt", "a commit made since");
        await File.WriteAllTextAsync(Path.Combine(edited, "tracked.txt"), "edited since, not committed\n");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", landed.Branch);
        await GitAsync(root, "branch", "-D", "feature/held");

        var starts = new List<string>();
        GitFlow.Started.Value = arguments =>
        {
            lock (starts) starts.Add(string.Join(' ', arguments));
        };
        IReadOnlyDictionary<string, (int?, int?)> after;
        try
        {
            after = Said(await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None));
        }
        finally
        {
            GitFlow.Started.Value = null;
        }

        Assert.Equal(await TruthAsync(all), after);
        Assert.Equal((1, 0), after[N(committed)]);
        Assert.Equal((0, 1), after[N(edited)]);
        Assert.Equal((0, 0), after[N(landed.Path)]);
        Assert.Equal((1, 0), after[N(held.Path)]);
        Assert.Equal((0, 0), after[N(quiet)]);
        // A branch went, so no tree is held by the refs' forward move: each is walked again.
        Assert.Contains(starts, IsWalk);
    }

    /// <summary>A tree whose read nothing moved is asked its status alone, and still says what it holds uncommitted now.</summary>
    [Fact]
    public async Task A_tree_nothing_moved_in_is_asked_its_status_alone_and_says_its_edits_now()
    {
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var tree = (await sessionTrees.OpenAsync(root, "engine", "default")).Path;
        await CommitAsync(tree, "work.txt", "work");
        var part = Part([tree]);
        Assert.Equal((1, 0), Said(await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None))[N(tree)]);
        await EditedAsync(tree, "draft.txt", "half done\n");

        var (read, _, starts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));

        Assert.Equal((1, 1), Said(read)[N(tree)]);
        Assert.Equal(1, starts);
    }

    /// <summary>
    /// The install's part asked 245 trees, 7 of them there, the rest gone or moved
    /// aside under <c>.tidied</c>, and its line said 245 at 7 to 15 s. A gone tree or one set aside is asked, holding nothing, and never
    /// walked: the line counts the tree that is there, the set-aside one starts no git, and the rest cost no more than the bound.
    /// </summary>
    [Fact]
    public async Task A_part_walks_only_the_trees_still_here_never_a_gone_or_set_aside_one()
    {
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var here = (await sessionTrees.OpenAsync(root, "engine", "default")).Path;
        await CommitAsync(here, "work.txt", "work");
        // A folder the look's tidy moved aside: a tree's files and its `.git` link, which names a tree git let go of.
        var aside = Path.Combine(sessionTrees.SetAsideRoot, "default", "engine", "s-a51de000-20261023T230519801Z");
        Directory.CreateDirectory(aside);
        await File.WriteAllTextAsync(Path.Combine(aside, ".git"), $"gitdir: {Path.Combine(root, ".git", "worktrees", "s-a51de000")}\n");
        await File.WriteAllTextAsync(Path.Combine(aside, "README.md"), "# engine\n");
        var gone = Enumerable.Range(0, 243).Select(i => Path.Combine(sessionTrees.TreesRoot, "default", "engine", $"s-90{i:d6}")).ToList();
        var part = Part([.. gone, aside, here]);
        // The tree that is here, read once and again: what a read of it alone starts once its read is kept.
        await HelpFacts.ReadTreesAsync(_home, Part([here]), CancellationToken.None);
        var walkedHere = (await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, Part([here]), ct))).Starts;
        var times = new LookTimes();

        var (read, ms, starts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct, times));

        Assert.Equal(245, read.Asked.Count);
        Assert.Equal(1, times.Trees);
        Assert.Equal([N(here)], read.Trees.Keys);
        Assert.Equal(walkedHere, starts);
        // The gone and set-aside ones alone, with nothing to walk: within the room look's bound (HelpRoomLookLog.SlowMs, 500 ms).
        var (none, noneMs, noneStarts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, Part([.. gone, aside]), ct));
        _output.WriteLine($"245 trees, one here: {ms} ms ({starts} git starts); the 244 gone or set aside alone: {noneMs} ms");
        Assert.Empty(none.Trees);
        Assert.Equal(0, noneStarts);
        Assert.True(noneMs < 500, $"the gone and set-aside trees took {noneMs} ms");
    }

    /// <summary>
    /// A tree whose commits the line holds by content (a content proof holds them) has its read kept, so a
    /// second read starts no git, says what it holds as the walk does, and still sees an edit made since.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_commits_the_line_holds_by_content_is_not_walked_again_on_the_second_read()
    {
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var opened = await sessionTrees.OpenAsync(root, "engine", "default");
        var tree = opened.Path;
        await CommitAsync(tree, "work.txt", "work");
        // The line gets the commit by a cherry-pick, which holds it by content: the tree's commit is no ancestor.
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", $"main..{opened.Branch}");
        var part = Part([tree]);
        var walked = (await sessionTrees.WorkAsync(tree))!;
        Assert.NotNull(walked.Held);
        await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None);

        var starts = new List<string>();
        GitFlow.Started.Value = arguments =>
        {
            lock (starts) starts.Add(string.Join(' ', arguments));
        };
        HelpTrees second;
        try
        {
            second = await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None);
        }
        finally
        {
            GitFlow.Started.Value = null;
        }

        Assert.Empty(starts);
        var read = second.Trees[N(tree)];
        Assert.Equal(walked.Held, read.Held);
        Assert.Equal((walked.Commits, walked.Uncommitted), (read.Commits, read.Uncommitted));

        // An edit made since is still said: the tree stays for it, as the walk says.
        await EditedAsync(tree, "draft.txt", "half done\n");
        var edited = (await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None)).Trees[N(tree)];
        var truth = (await new SessionTrees(_home).WorkAsync(tree))!;
        Assert.Equal(truth.Held, edited.Held);
        Assert.Equal(1, edited.Uncommitted);
        Assert.NotNull(edited.Held!.Stays);
    }

    /// <summary>
    /// The home's stamp reads the landings while the move that landed them still holds them, so a tree nothing moved in
    /// is still asked its status alone; a stamp that could not read them is a stamp of its own, and every tree is walked again.
    /// </summary>
    [Fact]
    public async Task The_home_s_stamp_reads_the_landings_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var tree = (await sessionTrees.OpenAsync(root, "engine", "default")).Path;
        var part = Part([tree]);
        var landings = sessionTrees.LandingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(landings)!);
        AtomicFile.WriteText(landings, "{}\n");
        await HelpFacts.ReadTreesAsync(_home, part, CancellationToken.None);

        int starts;
        using (MoveHold.Take(landings))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(landings));
            (_, _, starts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));
        }

        Assert.Equal(0, starts);
    }

    /// <summary>Copies at the line's tip, every fifth with an uncommitted file.</summary>
    private async Task<(string Root, HelpTreesPart Part, IReadOnlyList<(string Tree, int Commits, int Uncommitted)> Trees)> KeptAsync(int count)
    {
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var trees = new List<(string, int, int)>();
        var opened = await TreeFarm.OpenAsync(sessionTrees, _home, root, "engine", "default", count);
        for (var i = 0; i < count; i++)
        {
            var tree = opened[i];
            if (i % 5 == 0) await File.WriteAllTextAsync(Path.Combine(tree, "draft.txt"), "half done\n");
            trees.Add((tree, 0, i % 5 == 0 ? 1 : 0));
        }

        return (root, Part([.. trees.Select(each => each.Item1)]), trees);
    }

    /// <summary>The engine's part, its sessions ended on <paramref name="trees"/>, one each.</summary>
    private static HelpTreesPart Part(IReadOnlyList<string> trees)
    {
        var records = trees.Select((tree, i) => new SessionRecord($"s{i}", "engine", "completed")
        {
            Tree = tree, Created = At.AddMinutes(i - trees.Count), Updated = At.AddMinutes(i - trees.Count + 1),
        }).ToList();
        return new HelpTreesPart("engine", new HashSet<string>(["engine"], StringComparer.OrdinalIgnoreCase), new SessionLook(records, [], []));
    }

    private static async Task<(HelpTrees Trees, long Ms, int Starts)> TimedAsync(
        Func<CancellationToken, Task<HelpTrees>> read, List<string>? asked = null)
    {
        var starts = 0;
        GitFlow.Started.Value = arguments =>
        {
            Interlocked.Increment(ref starts);
            if (asked is not null) lock (asked) asked.Add(string.Join(' ', arguments));
        };
        try
        {
            var watch = Stopwatch.StartNew();
            var trees = await read(CancellationToken.None);
            return (trees, watch.ElapsedMilliseconds, Volatile.Read(ref starts));
        }
        finally
        {
            GitFlow.Started.Value = null;
        }
    }

    /// <summary>What a walk of each tree says now, with no read kept: what the part's read must say.</summary>
    private async Task<IReadOnlyDictionary<string, (int?, int?)>> TruthAsync(IEnumerable<string> trees)
    {
        var walk = new SessionTrees(_home);
        var truth = new Dictionary<string, (int?, int?)>(StringComparer.OrdinalIgnoreCase);
        foreach (var tree in trees)
        {
            var work = (await walk.WorkAsync(tree))!;
            truth[N(tree)] = (work.Commits, work.Uncommitted);
        }

        return truth;
    }

    private static string N(string tree) => SessionGroups.Normal(tree);

    private static IReadOnlyDictionary<string, (int?, int?)> Expected(IEnumerable<(string Tree, int Commits, int Uncommitted)> trees) =>
        trees.ToDictionary(each => N(each.Tree), each => ((int?)each.Commits, (int?)each.Uncommitted), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, (int?, int?)> Said(HelpTrees read) =>
        read.Trees.ToDictionary(each => each.Key, each => (each.Value.Commits, each.Value.Uncommitted), StringComparer.OrdinalIgnoreCase);

    private Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

    private async Task EditedAsync(string tree, string file, string content)
    {
        var git = Path.GetFullPath(Path.Combine(tree, File.ReadAllText(Path.Combine(tree, ".git"))["gitdir:".Length..].Trim()));
        var changes = TreeReadMemo.Of(_home).Changes;
        var before = changes.Read(tree, git);
        await File.WriteAllTextAsync(Path.Combine(tree, file), content);
        await Poll.Until(() => changes.Read(tree, git) != before, () => "the file watch has not heard the edit");
    }

    private static async Task CommitAsync(string tree, string file, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", file);
    }
}
