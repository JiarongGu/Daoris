using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELOOK1: one repository's part of the trees fact (FACTSPLIT1b) reads a tree again only where something its walk rests on
/// moved, and asks every other one <c>git status</c>, a few trees at once; a tree that changed is still read. Real git under a
/// scratch home, at as many kept trees as the install's machine log found (143-145).
/// </summary>
[Trait(Category.Name, Category.Process)]
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
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// The row's proof: the part's step at 145 trees, the home's read first, again with nothing moved, which asks one
    /// <c>git status</c> a tree, and once the line moved on; each says what the fixture holds of every tree. Before TREELOOK1 the
    /// same fixture's step walked each tree one after another: 1409 git starts a read (D181's TREELOOK1 note has the times).
    /// </summary>
    [Fact]
    public async Task A_part_of_a_hundred_and_forty_five_trees_reads_each_unchanged_tree_with_one_git_start()
    {
        var (root, part, trees) = await KeptAsync(Kept);

        var (first, firstMs, firstStarts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));
        var (again, againMs, againStarts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));
        // The line moves on, as a landing moves it: a tree that counted no commit keeps none by the refs' forward move alone.
        await CommitAsync(root, "landed.txt", "another session's work");
        var (moved, movedMs, movedStarts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));

        _output.WriteLine($"{Kept} trees, the part's step: the home's first read {firstMs} ms ({firstStarts} git starts); "
                          + $"again, nothing moved {againMs} ms ({againStarts}); the line moved on {movedMs} ms ({movedStarts})");
        Assert.Equal(Kept, first.Asked.Count);
        Assert.Equal(Expected(trees), Said(first));
        Assert.Equal(Expected(trees), Said(again));
        Assert.Equal(Expected(trees), Said(moved));
        Assert.Equal(Kept, againStarts);
        // The trees with commits are walked again (about nineteen starts each, the content proof's); the rest are asked their
        // status, after one ask of the line's move: 930 where the first read made 1409, on 2026-10-22.
        Assert.True(movedStarts < firstStarts, $"{movedStarts} git starts where the first read made {firstStarts}");
    }

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
        WorkingTree.Started.Value = arguments =>
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
            WorkingTree.Started.Value = null;
        }

        Assert.Equal(await TruthAsync(all), after);
        Assert.Equal((1, 0), after[N(committed)]);
        Assert.Equal((0, 1), after[N(edited)]);
        Assert.Equal((0, 0), after[N(landed.Path)]);
        Assert.Equal((1, 0), after[N(held.Path)]);
        Assert.Equal((0, 0), after[N(quiet)]);
        // A branch went, so no tree is held by the refs' forward move: each is walked again.
        Assert.Contains(starts, start => start.EndsWith("rev-parse --show-toplevel", StringComparison.Ordinal));
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
        await File.WriteAllTextAsync(Path.Combine(tree, "draft.txt"), "half done\n");

        var (read, _, starts) = await TimedAsync(ct => HelpFacts.ReadTreesAsync(_home, part, ct));

        Assert.Equal((1, 1), Said(read)[N(tree)]);
        Assert.Equal(1, starts);
    }

    /// <summary>
    /// READSHARE1: the home's stamp reads the landings while the move that landed them still holds them, so a tree nothing moved in
    /// is still asked its status alone; a stamp that could not read them is a stamp of its own, and walked every tree again.
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

        Assert.Equal(1, starts);
    }

    /// <summary>The fixture's trees: every third with a commit of its own, every fifth with a file uncommitted.</summary>
    private async Task<(string Root, HelpTreesPart Part, IReadOnlyList<(string Tree, int Commits, int Uncommitted)> Trees)> KeptAsync(int count)
    {
        var root = await RepositoryAsync("engine");
        var sessionTrees = new SessionTrees(_home);
        var trees = new List<(string, int, int)>();
        for (var i = 0; i < count; i++)
        {
            var tree = (await sessionTrees.OpenAsync(root, "engine", "default")).Path;
            if (i % 3 == 0) await CommitAsync(tree, $"work-{i}.txt", $"work {i}");
            if (i % 5 == 0) await File.WriteAllTextAsync(Path.Combine(tree, "draft.txt"), "half done\n");
            trees.Add((tree, i % 3 == 0 ? 1 : 0, i % 5 == 0 ? 1 : 0));
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

    private static async Task<(HelpTrees Trees, long Ms, int Starts)> TimedAsync(Func<CancellationToken, Task<HelpTrees>> read)
    {
        var starts = 0;
        WorkingTree.Started.Value = _ => Interlocked.Increment(ref starts);
        try
        {
            var watch = Stopwatch.StartNew();
            var trees = await read(CancellationToken.None);
            return (trees, watch.ElapsedMilliseconds, Volatile.Read(ref starts));
        }
        finally
        {
            WorkingTree.Started.Value = null;
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

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", file);
    }
}
