using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session tree's untracked files read as patches in two git starts whatever their count, each patch the one a file's own
/// <c>diff --no-index /dev/null &lt;path&gt;</c> gave it, byte for byte; the person's index untouched and the private index gone after.
/// The per-file read is kept here, and only here, as the oracle.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class UntrackedPatchesTests : LandedFixture
{
    private async Task<(string Tree, string GitDir)> TreeAsync()
    {
        var root = await RepositoryAsync("engine");
        var tree = (await new SessionTrees(Home).OpenAsync(root, "engine", "aurora")).Path;
        return (tree, (await GitAsync(tree, "rev-parse", "--absolute-git-dir")).Trim());
    }

    [Fact]
    public async Task Each_untracked_files_patch_is_todays_byte_for_byte_in_two_starts()
    {
        var (tree, gitDir) = await TreeAsync();
        // A tracked file a glob of `[br].txt` would match: read literally, only the untracked one is. A clean filter and a textconv run.
        await CommitAsync(tree, "b.txt", "tracked\n", "a file a glob would find");
        await CommitAsync(tree, ".gitattributes", "*.up filter=upper\n*.tc diff=marked\n", "programs for two kinds");
        await GitAsync(tree, "config", "filter.upper.clean", "tr a-z A-Z");
        await GitAsync(tree, "config", "diff.marked.textconv", "sed s/^/marked:/");
        await GitAsync(tree, "config", "core.autocrlf", "true");
        var names = new List<string>
        {
            "text.txt", "a file.txt", "名前.txt", "café.txt", "[br].txt", "-dash.txt", "empty.txt", "nest/deep/n.txt", "crlf.txt",
            "nonl.txt", "bin.dat", "big.txt", "sub/inner.txt", "shout.up", "shown.tc",
        };
        if (!OperatingSystem.IsWindows()) names.AddRange(["*star.txt", "?q.txt", ":colon.txt", "q\"uote.txt", "new\nline.txt"]);
        foreach (var name in names)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(tree, name))!);
            await File.WriteAllTextAsync(Path.Combine(tree, name), $"the file {name}\nits second line\n");
        }

        await File.WriteAllTextAsync(Path.Combine(tree, "empty.txt"), "");
        await File.WriteAllTextAsync(Path.Combine(tree, "crlf.txt"), "one\r\ntwo\r\n");
        await File.WriteAllTextAsync(Path.Combine(tree, "nonl.txt"), "no newline at its end");
        await File.WriteAllBytesAsync(Path.Combine(tree, "bin.dat"), [0x62, 0x00, 0x01, 0x02, 0xff]);
        await File.WriteAllTextAsync(Path.Combine(tree, "big.txt"), new string('x', WorkingTree.UntrackedPatchLimit + 1));
        // A nested repository: status lists its folder, which no read answers.
        await GitAsync(Path.Combine(tree, "sub"), "init", "--quiet");
        var index = Path.Combine(gitDir, "index");
        var (bytes, written) = (await File.ReadAllBytesAsync(index), File.GetLastWriteTimeUtc(index));

        var (read, count) = await CountedAsync(tree);

        Assert.Equal(JsonSerializer.Serialize(await TodayAsync(tree)), JsonSerializer.Serialize(read));
        Assert.Contains(read!.Files, file => file.Path == "text.txt" && file.Patch is not null && file.Added == 2);
        Assert.Contains(read.Files, file => file.Path == "shout.up" && file.Patch!.Contains("+THE FILE SHOUT.UP", StringComparison.Ordinal));
        Assert.Contains(read.Files, file => file.Path == "shown.tc" && file.Patch!.Contains("+marked:the file", StringComparison.Ordinal));
        Assert.DoesNotContain(read.Files, file => file.Path == "b.txt");
        Assert.Equal(2, count.In(GitBudget.Untracked));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(index));
        Assert.Equal(written, File.GetLastWriteTimeUtc(index));
        AssertNothingLeft(gitDir);
    }

    [Fact]
    public async Task Files_past_the_patch_budget_are_folded_as_today()
    {
        var (tree, _) = await TreeAsync();
        // The tracked patch spends its cap; the untracked ones the rest, so the later files are listed with no patch.
        await File.WriteAllTextAsync(Path.Combine(tree, "shared.txt"), string.Concat(Enumerable.Repeat("a long tracked line of the session's work\n", 7_000)));
        for (var at = 0; at < 10; at++)
        {
            await File.WriteAllTextAsync(Path.Combine(tree, $"u{at}.txt"), string.Concat(Enumerable.Repeat($"untracked {at}\n", 5_000)));
        }

        var (read, _) = await CountedAsync(tree);

        Assert.Equal(JsonSerializer.Serialize(await TodayAsync(tree)), JsonSerializer.Serialize(read));
        Assert.NotNull(read!.Truncated);
    }

    /// <summary>A clean filter that fails: git ends mid-read, the files it reached keep their patches, and nothing is left behind.</summary>
    [Fact]
    public async Task A_git_that_fails_mid_read_answers_as_today_and_leaves_no_private_index()
    {
        var (tree, gitDir) = await TreeAsync();
        await CommitAsync(tree, ".gitattributes", "*.fail filter=broken\n", "a filter that fails");
        await GitAsync(tree, "config", "filter.broken.clean", "false");
        await GitAsync(tree, "config", "filter.broken.required", "true");
        await File.WriteAllTextAsync(Path.Combine(tree, "a.txt"), "read\n");
        await File.WriteAllTextAsync(Path.Combine(tree, "x.fail"), "refused\n");
        await File.WriteAllTextAsync(Path.Combine(tree, "y.fail"), "refused\n");

        var (read, _) = await CountedAsync(tree);

        Assert.Equal(JsonSerializer.Serialize(await TodayAsync(tree)), JsonSerializer.Serialize(read));
        Assert.Equal(["a.txt"], read!.Files.Where(file => file.Patch is not null).Select(file => file.Path));
        AssertNothingLeft(gitDir);
    }

    [Fact]
    public async Task A_read_its_caller_ended_leaves_no_private_index()
    {
        var (tree, gitDir) = await TreeAsync();
        await File.WriteAllTextAsync(Path.Combine(tree, "a.txt"), "read\n");
        using var ended = new CancellationTokenSource();
        using (var untracked = new UntrackedPatches(tree, ["a.txt"], new UntrackedFold(new TreeDiff([], null, "HEAD"))))
        {
            Assert.True(await untracked.IntentsAsync(CancellationToken.None));
            Assert.Contains(Directory.EnumerateFiles(gitDir).Select(Path.GetFileName), name => name!.StartsWith("index.daoris-review-", StringComparison.Ordinal));
            await ended.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => untracked.ReadAsync(ended.Token));
        }

        AssertNothingLeft(gitDir);
    }

    private static async Task<(TreeDiff? Read, GitCount Count)> CountedAsync(string tree)
    {
        using var count = GitLaunch.Count();
        using (GitLaunch.Within(GitBudget.Review))
        {
            return (await WorkingTree.UncommittedDiffAsync(tree), count);
        }
    }

    private static void AssertNothingLeft(string gitDir)
    {
        Assert.DoesNotContain(Directory.EnumerateFiles(gitDir).Select(Path.GetFileName), name => name!.StartsWith("index.", StringComparison.Ordinal));
    }

    /// <summary>The oracle: the tracked changes as Review reads them, then each untracked file read on its own, folded in status's order.</summary>
    private static async Task<TreeDiff?> TodayAsync(string tree)
    {
        var tracked = await WorkingTree.RangeAsync(tree, "HEAD", "HEAD", WorkingTree.UncommittedRest, WorkingTree.ReadGitAsync, CancellationToken.None);
        var status = new StringBuilder();
        Assert.Equal(0, await WorkingTree.ReadGitAsync(tree, WorkingTree.UncommittedStatus, piece => { status.Append(piece.Span); return true; }, CancellationToken.None));
        var fold = new UntrackedFold(tracked!);
        foreach (var path in ReviewChoices.UntrackedPaths(status.ToString()))
        {
            if (fold.Spent)
            {
                fold.Drop(path);
                continue;
            }

            var (patch, added) = await OneAsync(tree, path);
            fold.Take(path, patch, added);
        }

        return fold.End(WorkingTree.UncommittedRest);
    }

    /// <summary>One untracked file's patch as its own start read it.</summary>
    private static async Task<(string? Patch, int? Added)> OneAsync(string tree, string path)
    {
        if (new FileInfo(Path.Combine(tree, path)) is { Exists: true, Length: > WorkingTree.UntrackedPatchLimit }) return (null, null);
        var text = new StringBuilder();
        var code = await WorkingTree.ReadGitAsync(
            tree,
            [
                "--no-optional-locks", "-c", "core.quotePath=false", "diff", "--no-index", "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/",
                "--", "/dev/null", path,
            ],
            piece => { text.Append(piece.Span); return true; },
            CancellationToken.None);
        if (code is not (0 or 1) || text.Length == 0) return (null, null);
        var patch = text.ToString();
        if (patch.Contains("\nBinary files ", StringComparison.Ordinal) || patch.Contains("\nGIT binary patch", StringComparison.Ordinal)) return (null, null);
        var added = patch.Split('\n').Count(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal));
        return (patch.Length > WorkingTree.PatchCap ? patch[..WorkingTree.PatchCap] + "\n… this file's patch is longer than the surface renders." : patch, added);
    }
}
