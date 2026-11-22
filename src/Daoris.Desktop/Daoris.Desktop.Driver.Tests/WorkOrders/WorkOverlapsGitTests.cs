using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A branch's overlap is what it changed since its own merge base with the line, read from real branches while the line moves: a branch
/// left behind, one that merged the line in and one rebased onto it name only their own files, so the notice two branches changing one
/// file are each told says that file and nothing the line changed.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class WorkOverlapsGitTests : IAsyncLifetime
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "overlap-false", Guid.NewGuid().ToString("N")[..8]);
    private string _root = "";
    private string _base = "";

    public async Task InitializeAsync()
    {
        _root = await GitSeed.PlainAsync(Path.Combine(_scratch, "checkout"), "Game");
        _base = (await GitAsync(_root, "rev-parse", "HEAD")).Trim();
    }

    public Task DisposeAsync()
    {
        GitSeed.Discard(_scratch);
        return Task.CompletedTask;
    }

    private async Task<string> BranchAsync(string name, string from = "main")
    {
        var tree = Path.Combine(_scratch, name);
        await GitAsync(_root, "worktree", "add", "--quiet", "-b", name, tree, from);
        return tree;
    }

    private static async Task CommitAsync(string tree, string file, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), $"{message}\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "commit", "--quiet", "-m", message);
    }

    private static Task<IReadOnlyList<string>?> ChangedAsync(string tree, string before) => WorkOverlaps.DiffAsync(tree, before, CancellationToken.None);

    private static SessionView Running(string id, string quest, string tree, string before) =>
        new(id, "Game", "running") { Quest = quest, Tree = tree, BaseCommit = before };

    [Fact]
    public async Task A_branch_that_merged_the_line_in_or_was_rebased_onto_it_names_only_its_own_files()
    {
        var behind = await BranchAsync("behind");
        var merged = await BranchAsync("merged");
        var rebased = await BranchAsync("rebased");
        await CommitAsync(behind, "own-behind.txt", "behind");
        await CommitAsync(merged, "own-merged.txt", "merged");
        await CommitAsync(rebased, "own-rebased.txt", "rebased");
        await CommitAsync(_root, "line-one.txt", "line one");
        await CommitAsync(_root, "line-two.txt", "line two");
        await GitAsync(merged, "merge", "--quiet", "--no-edit", "main");
        await GitAsync(rebased, "rebase", "--quiet", "main");

        Assert.Equal(["own-behind.txt"], await ChangedAsync(behind, _base));
        Assert.Equal(["own-merged.txt"], await ChangedAsync(merged, _base));
        Assert.Equal(["own-rebased.txt"], await ChangedAsync(rebased, _base));
    }

    [Fact]
    public async Task A_branch_cut_from_another_branch_is_not_charged_with_that_branchs_files()
    {
        var feature = await BranchAsync("feature");
        await CommitAsync(feature, "feature.txt", "feature");
        var cut = (await GitAsync(feature, "rev-parse", "HEAD")).Trim();
        var stacked = await BranchAsync("stacked", "feature");
        await CommitAsync(stacked, "own-stacked.txt", "stacked");
        await CommitAsync(_root, "line-one.txt", "line one");

        Assert.Equal(["own-stacked.txt"], await ChangedAsync(stacked, cut));
    }

    [Fact]
    public async Task Two_branches_changing_one_file_are_each_told_once_and_the_lines_files_are_in_no_notice()
    {
        var left = await BranchAsync("left");
        var other = await BranchAsync("other");
        await CommitAsync(left, "shared.txt", "left");
        await CommitAsync(other, "shared.txt", "other");
        await CommitAsync(_root, "line-one.txt", "line one");
        await GitAsync(left, "merge", "--quiet", "--no-edit", "main");
        await GitAsync(other, "merge", "--quiet", "--no-edit", "main");

        var said = new List<(string Session, string Text)>();
        Task Say(string session, string text, string key, CancellationToken ct)
        {
            said.Add((session, text));
            return Task.CompletedTask;
        }

        var home = Path.Combine(_scratch, "home");
        for (var look = 0; look < 3; look++)
        {
            var snapshot = await WorkOverlaps.LookAsync(
                new Snapshot([], [], [Running("s1", "q1", left, _base), Running("s2", "q2", other, _base)]),
                new HashSet<string>(), WorkOverlaps.DiffAsync);
            await WorkOverlaps.TellAsync(home, snapshot, Say);
        }

        Assert.Equal(2, said.Count);
        Assert.All(said, word =>
        {
            Assert.Contains("`shared.txt`", word.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("line-one.txt", word.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task A_branch_left_behind_while_the_line_moves_is_told_nothing_for_the_lines_files()
    {
        var behind = await BranchAsync("behind");
        var merged = await BranchAsync("merged");
        await CommitAsync(behind, "own-behind.txt", "behind");
        await CommitAsync(merged, "own-merged.txt", "merged");
        await CommitAsync(_root, "line-one.txt", "line one");
        await GitAsync(merged, "merge", "--quiet", "--no-edit", "main");

        var said = 0;
        var snapshot = await WorkOverlaps.LookAsync(
            new Snapshot([], [], [Running("s1", "q1", behind, _base), Running("s2", "q2", merged, _base)]),
            new HashSet<string>(), WorkOverlaps.DiffAsync);
        await WorkOverlaps.TellAsync(Path.Combine(_scratch, "home"), snapshot, (_, _, _, _) =>
        {
            said++;
            return Task.CompletedTask;
        });

        Assert.Equal(0, said);
    }
}
