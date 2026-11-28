using System.Security.Cryptography;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The save step: a tree's head pinned under a kept ref and its staged, unstaged, untracked and named
/// ignored files committed onto it, the record listing each with its hash; a restore that puts the save back into an empty folder and
/// proves every listed file identical; a removal only behind a save that still reads the tree as it is; and the landed test by
/// content, which never calls unlanded work landed. Real git, in a scratch repository under <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSavesTests : LandedFixture
{
    private readonly MovedClock _clock = new();

    /// <summary>A session tree of a repository that converts line endings, holding every kind of work a save takes, and one it leaves.</summary>
    private async Task<(string Root, string Tree, string Branch)> WorkedTreeAsync()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "core.autocrlf", "true");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "committed\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, ".gitignore"), "kept/\nnoise/\n");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "staged\n");
        await GitAsync(tree.Path, "add", "shared.txt");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "staged\nthen changed\n");
        // Bytes git's line-ending filter would rewrite on a checkout: a restore must write them as they were.
        await File.WriteAllBytesAsync(Path.Combine(tree.Path, "work.txt"), "unstaged\nlf only\n"u8.ToArray());
        File.Delete(Path.Combine(tree.Path, "README.md"));
        Directory.CreateDirectory(Path.Combine(tree.Path, "notes"));
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "notes", "new.txt"), "untracked\r\n");
        Directory.CreateDirectory(Path.Combine(tree.Path, "kept"));
        await File.WriteAllBytesAsync(Path.Combine(tree.Path, "kept", "state.db"), [0, 1, 2, 255, 13, 10]);
        Directory.CreateDirectory(Path.Combine(tree.Path, "noise"));
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "noise", "build.log"), "rebuilt at will\n");
        return (root, tree.Path, tree.Branch);
    }

    private static string Sha(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    private static async Task<string> Rev(string cwd, string name) => (await GitAsync(cwd, "rev-parse", "--verify", "--quiet", name)).Trim();

    [Fact]
    public async Task A_save_pins_the_head_lists_every_kind_of_work_with_its_hash_and_leaves_the_tree_as_it_was()
    {
        var (_, tree, branch) = await WorkedTreeAsync();
        var head = await Rev(tree, "HEAD");
        var status = await GitAsync(tree, "status", "--porcelain=v1", "--untracked-files=all");

        var saved = await new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", ["kept"]);

        Assert.True(saved.Save is not null, saved.Said);
        var save = saved.Save!;
        Assert.Equal($"refs/daoris/saves/{branch}/1", save.Id);
        Assert.Equal(head, save.Head);
        Assert.Equal(head, await Rev(tree, save.Id + "^1"));
        Assert.Equal([".gitignore", "README.md", "kept/state.db", "notes/new.txt", "shared.txt", "work.txt"], save.Files.Select(file => file.Path));
        Assert.True(save.Files.Single(file => file.Path == "README.md").Deleted);
        foreach (var file in save.Files.Where(file => !file.Deleted))
        {
            var full = Path.Combine(tree, file.Path);
            Assert.Equal(Sha(full), file.Sha256);
            Assert.Equal(new FileInfo(full).Length, file.Bytes);
        }

        Assert.DoesNotContain(save.Files, file => file.Path.StartsWith("noise/", StringComparison.Ordinal));
        Assert.Equal(status, await GitAsync(tree, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(save.Id, new TreeSaveRecords(Home).LatestOf(tree)!.Id);

        var again = await new TreeSaves(Home, _clock).SaveAsync(tree, "aurora", "engine", ["kept"]);
        Assert.Equal($"refs/daoris/saves/{branch}/2", again.Save!.Id);
    }

    [Fact]
    public async Task A_removal_waits_for_a_save_that_reads_the_tree_as_it_is_and_a_restore_proves_every_file_by_hash()
    {
        var (root, tree, branch) = await WorkedTreeAsync();
        var trees = new SessionTrees(Home);
        var staged = await GitAsync(tree, "diff", "--cached", "--name-status");
        var said = new StringWriter();

        Assert.Equal(1, await TreeSaveDoors.RunAsync(["remove", tree, "--saved"], trees, () => null, _clock, said, said));
        Assert.Contains("no save", said.ToString());
        Assert.True(Directory.Exists(tree));

        Assert.Equal(0, await TreeSaveDoors.RunAsync(["save", tree, "--work", "kept"], trees, () => null, _clock, said, said));
        await File.AppendAllTextAsync(Path.Combine(tree, "notes", "new.txt"), "written after the save\r\n");
        Assert.Equal(1, await TreeSaveDoors.RunAsync(["remove", tree, "--saved"], trees, () => null, _clock, said, said));
        Assert.Contains("changed since its save", said.ToString());
        Assert.True(Directory.Exists(tree));

        Assert.Equal(0, await TreeSaveDoors.RunAsync(["save", tree, "--work", "kept"], trees, () => null, _clock, said, said));
        var save = new TreeSaveRecords(Home).LatestOf(tree)!;
        var hashes = save.Files.Where(file => !file.Deleted).ToDictionary(file => file.Path, file => Sha(Path.Combine(tree, file.Path)));
        Assert.Equal(0, await TreeSaveDoors.RunAsync(["remove", tree, "--saved"], trees, () => null, _clock, said, said));
        Assert.False(Directory.Exists(tree), said.ToString());
        Assert.Equal(save.Head, await Rev(root, $"refs/heads/{branch}"));

        var folder = Path.Combine(Scratch, "restored");
        Directory.CreateDirectory(folder);
        Assert.Equal(0, await TreeSaveDoors.RunAsync(["restore", save.Id, folder], trees, () => null, _clock, said, said));
        Assert.Contains("identical by hash", said.ToString());
        Assert.Equal(branch, (await GitAsync(folder, "symbolic-ref", "--short", "HEAD")).Trim());
        Assert.Equal(save.Head, await Rev(folder, "HEAD"));
        foreach (var (path, sha) in hashes) Assert.Equal(sha, Sha(Path.Combine(folder, path)));
        Assert.False(File.Exists(Path.Combine(folder, "README.md")));
        Assert.Equal(staged, await GitAsync(folder, "diff", "--cached", "--name-status"));
        var day = new TreeLedger(Home).Days(DateOnly.MinValue).Single(row => row.Repository == "engine");
        Assert.Equal((0, 1, 1), (day.Made, day.Removed, day.Restored));
    }

    [Fact]
    public async Task The_landed_test_by_content_never_calls_unlanded_work_landed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var saves = new TreeSaves(Home, _clock);

        async Task<TreeSave> SavedAsync(Func<string, Task> work)
        {
            var tree = await trees.OpenAsync(root, "engine", "aurora");
            await work(tree.Path);
            var saved = await saves.SaveAsync(tree.Path, "aurora", "engine", ["kept"]);
            Assert.True(saved.Save is not null, saved.Said);
            return saved.Save!;
        }

        var committed = await SavedAsync(tree => CommitAsync(tree, "a.txt", "a\n", "a"));
        var uncommitted = await SavedAsync(tree => File.WriteAllTextAsync(Path.Combine(tree, "shared.txt"), "one\nmore\n"));
        var untracked = await SavedAsync(tree => File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "loose\n"));
        var ignored = await SavedAsync(async tree =>
        {
            await CommitAsync(tree, ".gitignore", "kept/\n", "ignore kept");
            Directory.CreateDirectory(Path.Combine(tree, "kept"));
            await File.WriteAllTextAsync(Path.Combine(tree, "kept", "db"), "state\n");
        });
        var squashedThenMore = await SavedAsync(async tree =>
        {
            await CommitAsync(tree, "b.txt", "b\n", "b");
            await File.WriteAllTextAsync(Path.Combine(tree, "c.txt"), "after the squash\n");
        });
        await SquashAsync(root, squashedThenMore.Branch!);
        // The ignore file lands, so only the ignored file the save named as work stays unlanded.
        await CommitAsync(root, ".gitignore", "kept/\n", "the line ignores kept too");

        foreach (var save in new[] { committed, uncommitted, untracked, ignored, squashedThenMore })
        {
            var judged = await TreeSaveLanded.JudgeAsync(save, "main");
            Assert.True(judged.Unread is null, judged.Unread);
            Assert.False(judged.Landed, $"{string.Join(", ", save.Files.Select(file => file.Path))}: {judged.How}");
        }
    }

    [Fact]
    public async Task Work_the_line_holds_by_patch_or_by_content_is_landed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var saves = new TreeSaves(Home, _clock);

        async Task<TreeSave> SavedAsync(Func<string, Task> work)
        {
            var tree = await trees.OpenAsync(root, "engine", "aurora");
            await work(tree.Path);
            return (await saves.SaveAsync(tree.Path, "aurora", "engine", [])).Save!;
        }

        var merged = await SavedAsync(tree => CommitAsync(tree, "m.txt", "m\n", "m"));
        var picked = await SavedAsync(tree => CommitAsync(tree, "p.txt", "p\n", "p"));
        var squashed = await SavedAsync(async tree =>
        {
            await CommitAsync(tree, "s.txt", "s\n", "s");
            await CommitAsync(tree, "s.txt", "s\nt\n", "t");
        });
        var sameFile = await SavedAsync(tree => File.WriteAllTextAsync(Path.Combine(tree, "same.txt"), "same\n"));
        await GitAsync(root, "merge", "--no-ff", "-m", "merge m", merged.Branch!);
        await GitAsync(root, "cherry-pick", picked.Branch!);
        await SquashAsync(root, squashed.Branch!);
        await CommitAsync(root, "same.txt", "same\n", "the same file, written on the line");

        Assert.Equal("patch id", (await TreeSaveLanded.JudgeAsync(merged, "main")).How);
        Assert.Equal("patch id", (await TreeSaveLanded.JudgeAsync(picked, "main")).How);
        foreach (var save in new[] { squashed, sameFile })
        {
            var judged = await TreeSaveLanded.JudgeAsync(save, "main");
            Assert.True(judged.Landed, judged.Unread ?? judged.How);
            Assert.Equal("content", judged.How);
        }
    }

    [Fact]
    public async Task A_save_that_cannot_be_made_is_a_typed_refusal_that_leaves_the_tree_and_no_ref()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "the tree's\n", "tree side");
        await CommitAsync(root, "shared.txt", "the line's\n", "line side");
        await GitAsync(tree.Path, "merge", "main");
        var status = await GitAsync(tree.Path, "status", "--porcelain=v1");

        var refused = await new TreeSaves(Home, _clock).SaveAsync(tree.Path, "aurora", "engine", []);

        Assert.Null(refused.Save);
        Assert.Equal(SaveRefusal.Conflicted, refused.Refused);
        Assert.Contains("shared.txt", refused.Said);
        Assert.Equal(status, await GitAsync(tree.Path, "status", "--porcelain=v1"));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/saves/")).Trim());
        Assert.Null(new TreeSaveRecords(Home).LatestOf(tree.Path));

        var outside = Path.Combine(Scratch, "not-a-tree");
        Directory.CreateDirectory(outside);
        Assert.Equal(SaveRefusal.NoTree, (await new TreeSaves(Home, _clock).SaveAsync(outside, "aurora", "engine", [])).Refused);
    }
}
