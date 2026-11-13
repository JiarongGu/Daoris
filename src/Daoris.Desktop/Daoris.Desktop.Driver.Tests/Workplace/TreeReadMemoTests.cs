using Daoris.Driver;
using Daoris.Ports.Git;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The room's memo over handed stamps and counted git answers: a warm read starts no git.</summary>
public sealed class TreeReadMemoTests : IDisposable
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private readonly string _home = Path.Combine(TestHomes.Root, "tree-read-memo", Guid.NewGuid().ToString("N")[..8]);
    private readonly FakeChangeSource _source = new();
    private readonly TreeReadMemo _memo;
    private readonly SessionTrees _trees;
    private readonly ScriptedGit _git = new();
    private readonly IDisposable _chosen;
    private int _changed;
    private readonly Dictionary<string, string> _heads = new(StringComparer.OrdinalIgnoreCase);
    private string Common => Path.Combine(_home, "repository-store");

    public TreeReadMemoTests()
    {
        _memo = new TreeReadMemo(_source, tree => new TreeReadStamp(
            tree + _heads.GetValueOrDefault(tree, Commit), new CountedRefs(Common, "refs-v1", new Dictionary<string, string>()),
            Path.Combine(Common, "worktrees", Path.GetFileName(tree)), _heads.GetValueOrDefault(tree, Commit)));
        _trees = new SessionTrees(_home);
        Directory.CreateDirectory(Common);
        _git.On(ask =>
        {
            var args = ask.Arguments;
            if (args.SequenceEqual(GitReadOnce.Layout)) return new GitAnswer(0, $"{Common}\n{ask.Folder}\ndaoris/{Path.GetFileName(ask.Folder)}\n", "");
            if (args.Contains("status")) return new GitAnswer(0, args.Contains("--branch")
                ? $"# branch.oid {_heads.GetValueOrDefault(ask.Folder, Commit)}\n# branch.head daoris/{Path.GetFileName(ask.Folder)}\n" + (_changed > 0 ? "? draft.txt\n" : "")
                : _changed > 0 ? "?? draft.txt\n" : "", "");
            if (args[0] == "log") return new GitAnswer(0, "", "");
            return null;
        });
        _chosen = FlowGit.Use(_git);
    }

    public void Dispose()
    {
        _chosen.Dispose();
        _memo.Dispose();
        GitSeed.Discard(_home);
    }

    private string Tree(int number = 0)
    {
        var name = $"s-{number:d8}";
        var tree = Path.Combine(_trees.TreesRoot, "default", "engine", name);
        var admin = Path.Combine(Common, "worktrees", name);
        Directory.CreateDirectory(tree);
        Directory.CreateDirectory(admin);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work\n");
        return tree;
    }

    [Fact]
    public async Task A_warm_look_at_many_trees_starts_no_git()
    {
        var trees = Enumerable.Range(0, 145).Select(Tree).ToArray();
        var cold = _memo.Begin(_trees);
        foreach (var tree in trees) Assert.Equal(new TreeWork(0, 0) { Unlanded = 0 }, await cold.WorkAsync(tree, default));
        var asked = _git.Asked.Count;
        Assert.True(asked > 0);

        var times = new LookTimes();
        var warm = _memo.Begin(_trees, times);
        foreach (var tree in trees) Assert.Equal(new TreeWork(0, 0) { Unlanded = 0 }, await warm.WorkAsync(tree, default));

        Assert.Equal(asked, _git.Asked.Count);
        Assert.Equal(145, times.TreeSteps.Count(step => step.Step == "kept"));
    }

    [Theory]
    [InlineData("file")]
    [InlineData("index")]
    [InlineData("head")]
    [InlineData("loss")]
    public async Task Only_a_tree_that_moved_is_asked_again(string change)
    {
        var tree = Tree();
        var quiet = Tree(1);
        var cold = _memo.Begin(_trees);
        await cold.WorkAsync(tree, default);
        await cold.WorkAsync(quiet, default);
        _changed = 1;
        var admin = Path.Combine(Common, "worktrees", Path.GetFileName(tree));
        if (change == "file") _source.On(tree)!.Raise("draft.txt");
        else if (change == "loss") _source.On(tree)!.Lose();
        else if (change == "head") _heads[tree] = new string('a', 40);
        else File.WriteAllText(Path.Combine(admin, "index"), change);
        var asked = _git.Asked.Count;

        var again = _memo.Begin(_trees);
        Assert.Equal(1, (await again.WorkAsync(tree, default))!.Uncommitted);
        var after = _git.Asked.Count;
        Assert.True(after > asked);
        Assert.Equal(0, (await again.WorkAsync(quiet, default))!.Uncommitted);
        Assert.Equal(after, _git.Asked.Count);
    }

    [Fact]
    public async Task Rejudging_a_proof_keeps_the_status_while_its_own_inputs_stand()
    {
        var tree = Tree();
        await _memo.Begin(_trees).WorkAsync(tree, default);
        var statuses = _git.Asked.Count(ask => ask.Arguments.Contains("status"));
        Directory.CreateDirectory(Path.GetDirectoryName(_trees.ConfigPath)!);
        File.WriteAllText(_trees.ConfigPath, "{}\n");

        Assert.Equal(new TreeWork(0, 0) { Unlanded = 0 }, await _memo.Begin(_trees).WorkAsync(tree, default));

        Assert.Equal(statuses, _git.Asked.Count(ask => ask.Arguments.Contains("status")));
    }

    [Fact]
    public async Task Content_held_work_keeps_its_status_and_ignored_output_judgement_too()
    {
        var tree = Tree();
        await _memo.Begin(_trees).WorkAsync(tree, default);
        Assert.True(_memo.TryLast(tree, out var kept));
        var held = kept.Work with { Held = new HeldWork(new ContentHold("main", false), "refs/kept/work", "an ignored path") };
        _memo.Keep(tree, kept with { Work = held }, _memo.Version(kept.Refs)!);
        var asked = _git.Asked.Count;

        Assert.Equal(held, await _memo.Begin(_trees).WorkAsync(tree, default));
        Assert.Equal(asked, _git.Asked.Count);
    }

    [Fact]
    public async Task A_change_during_a_read_is_not_kept_as_unchanged()
    {
        var tree = Tree();
        _git.Seen(ask =>
        {
            if (ask.Arguments.Contains("status")) _source.On(tree)!.Raise("draft.txt");
        });
        await _memo.Begin(_trees).WorkAsync(tree, default);
        var asked = _git.Asked.Count;
        await _memo.Begin(_trees).WorkAsync(tree, default);
        Assert.True(_git.Asked.Count > asked);
    }
}
