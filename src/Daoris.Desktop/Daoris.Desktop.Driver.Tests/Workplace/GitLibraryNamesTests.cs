using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's names of two commits' changed paths held to git's <c>diff --name-only --no-renames</c> on real git, value for value:
/// a case-only rename as two names, a mode change, a type change, a gitlink, a path in NFD, one past 260 characters, a file become a
/// folder and a deletion, NUL-ended and newline-ended; and the reads that stay git's: a gitlink without
/// <c>--ignore-submodules=none</c>, a path git quotes, and rename detection. The trees are made with git's plumbing, so no file
/// system bounds a path.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryNamesTests : LandedFixture
{
    private const string Nfd = "café.txt";
    private static readonly string Long = "deep/" + string.Join('/', Enumerable.Repeat("a-segment-of-twenty", 14)) + "/file.txt";

    [Fact]
    public async Task Two_commits_changed_names_are_git_s()
    {
        var root = await BuildRepositoryAsync("names");
        var (from, to) = await PairAsync(root);
        Assert.True(Long.Length > 260);

        await SameAsync(root, "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", from, to);
        await SameAsync(root, "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", to, from);
        await SameAsync(root, "--no-optional-locks", "diff", "--name-only", "-z", "--no-renames", "--ignore-submodules=none", from, to, "--");
        var plain = await CommitTreeAsync(root, to, ("100644", "plain.txt", "plain\n"));
        await SameAsync(root, "diff", "--name-only", "--no-renames", to, plain);
        await SameAsync(root, "-c", "core.quotePath=false", "diff", "--name-only", "-z", "--no-renames", from, from);

        await GitsAsync(root, "--no-optional-locks", "diff", "--name-only", "-z", "--no-renames", from, to, "--");
        await GitsAsync(root, "diff", "--name-only", "--no-renames", "--ignore-submodules=none", from, to);
        Assert.Null(Daoris.Driver.GitLibraryReads.Asked(["diff", "--name-only", "-z", from, to]));
        Assert.Null(Daoris.Driver.GitLibraryReads.Asked(["diff", "--name-only", "-z", "--no-renames", from, to, "--", "deep"]));
    }

    [Fact]
    public async Task A_split_and_a_sparse_index_leave_two_commits_names_git_s()
    {
        var root = await BuildRepositoryAsync("indexed");
        var (from, to) = await PairAsync(root);
        string[] asked = ["diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", from, to];

        await GitAsync(root, "update-index", "--split-index");
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, ".git"), "sharedindex.*"));
        await SameAsync(root, asked);

        await GitAsync(root, "update-index", "--no-split-index");
        await GitAsync(root, "sparse-checkout", "set", "--cone", "--sparse-index", "deep");
        await SameAsync(root, asked);
    }

    /// <summary>Two commits whose trees differ in every kind the names read, the first holding the repository's head.</summary>
    private async Task<(string From, string To)> PairAsync(string root)
    {
        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var from = await CommitTreeAsync(root, head,
            ("100644", "Case.txt", "case\n"), ("100644", "mode.sh", "echo\n"), ("100644", "link", "target\n"), ("100644", "swap", "a file\n"),
            ("100644", Nfd, "nfd\n"), ("100644", Long, "long\n"), ("100644", "keep/gone.txt", "gone\n"));
        var to = await CommitTreeAsync(root, from,
            ("-", "Case.txt", ""), ("100644", "case.txt", "case\n"), ("100755", "mode.sh", "echo\n"), ("120000", "link", "target"),
            ("-", "swap", ""), ("100644", "swap/inside.txt", "a folder\n"), ("160000", "sub", head), ("100644", Nfd, "nfd changed\n"),
            ("100644", Long, "long changed\n"), ("-", "keep/gone.txt", ""));
        return (from, to);
    }

    /// <summary>A commit on <paramref name="parent"/> whose tree is its parent's with each entry set (a mode) or removed (<c>-</c>).</summary>
    private async Task<string> CommitTreeAsync(string root, string parent, params (string Mode, string Path, string Content)[] entries)
    {
        var index = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = Path.Combine(Scratch, $"index-{Guid.NewGuid():N}") };
        await RunAsync(root, index, null, "read-tree", parent);
        foreach (var (mode, path, content) in entries)
        {
            if (mode == "-")
            {
                await RunAsync(root, index, null, "update-index", "--force-remove", "--", path);
                continue;
            }

            var id = mode == "160000" ? content : (await RunAsync(root, index, content, "hash-object", "-w", "--stdin")).Stdout.Trim();
            Assert.Equal(0, (await RunAsync(root, index, null, "update-index", "--add", "--cacheinfo", $"{mode},{id},{path}")).Code);
        }

        var tree = (await RunAsync(root, index, null, "write-tree")).Stdout.Trim();
        return (await RunAsync(root, index, null, "commit-tree", tree, "-p", parent, "-m", "names")).Stdout.Trim();
    }
}
