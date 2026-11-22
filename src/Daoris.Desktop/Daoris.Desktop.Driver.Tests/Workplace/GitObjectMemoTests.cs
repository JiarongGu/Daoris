using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 A read named by full object ids alone is a pure function of them, so it is asked of git once and kept in the home: a second judgment
/// of an unchanged branch pair, in this process or after a restart, starts no git and takes no library read; a moved tip asks only the
/// pairs it is in; a read naming a ref, HEAD or the working tree is never kept.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitObjectMemoTests(ITestOutputHelper output) : LandedFixture
{
    private async Task<(string Root, string Line, string Tip)> BranchAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var line = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "-q", "-b", "feature");
        await CommitFileAsync(root, "work.txt", "work\n");
        await GitAsync(root, "checkout", "-q", "main");
        await CommitFileAsync(root, "line.txt", "line\n");
        return (root, line, (await GitAsync(root, "rev-parse", "feature")).Trim());
    }

    private static async Task CommitFileAsync(string root, string file, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(root, file), content);
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-q", "-m", file);
    }

    private async Task<int> AsksAsync(string root, params string[][] reads)
    {
        using var count = GitLaunch.Count();
        // Is-ancestor's exit 1 is its answer "no".
        foreach (var read in reads) Assert.InRange((await WorkingTree.GitAsync(root, read, CancellationToken.None)).Code, 0, 1);
        output.WriteLine(string.Join(" | ", count.Starts.Select(start => string.Join(' ', start.Arguments)).Concat(count.Reads.Select(read => "library " + read.Question))));
        return count.Starts.Count + count.Reads.Count;
    }

    private static string[][] Proof(string tip, string line) =>
    [
        ["merge-base", "--is-ancestor", tip, line],
        ["merge-base", tip, line],
        ["rev-list", "--count", tip, "--not", line],
        ["--no-optional-locks", "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", tip, line],
        ["cherry", line, tip],
    ];

    [Fact]
    public async Task A_second_proof_of_an_unchanged_pair_starts_no_git_and_takes_no_library_read()
    {
        var (root, line, tip) = await BranchAsync();
        using var home = GitObjectMemo.Within(Home);
        var proof = Proof(tip, line);
        var tally = GitAskTally.Shared;
        tally.Take();
        tally.TakeMemo();

        var first = await AsksAsync(root, proof);
        var second = await AsksAsync(root, proof);

        Assert.True(first > 0);
        Assert.Equal(0, second);
        var rows = tally.TakeMemo();
        Assert.Contains(rows, row => row.Kind == "merge-base" && row.Hits >= 2 && row.Misses >= 2);
        Assert.Contains(rows, row => row.Kind == "cherry" && row.Hits == 1 && row.Misses == 1);
    }

    [Fact]
    public async Task The_landed_proof_of_a_branch_asks_git_once_for_its_ids_and_the_next_look_starts_none()
    {
        var (root, _, tip) = await BranchAsync();
        using var home = GitObjectMemo.Within(Home);
        var tally = GitAskTally.Shared;
        tally.TakeMemo();

        async Task<(int Starts, int Reads, string Kind)> LookAsync()
        {
            using var count = GitLaunch.Count();
            var forms = await LandedProofs.LineFormsAsync(root, "main", CancellationToken.None);
            var proof = await LandedProofs.ProveAsync(root, tip, "main", forms, CancellationToken.None);
            output.WriteLine($"{proof.Kind}: starts [{string.Join(" | ", count.Starts.Select(start => string.Join(' ', start.Arguments)))}] library [{string.Join(", ", count.Reads.Select(read => read.Question))}]");
            return (count.Starts.Count, count.Reads.Count, proof.Kind);
        }

        var cold = await LookAsync();
        var warm = await LookAsync();

        output.WriteLine($"cold {cold.Starts} starts and {cold.Reads} library reads; warm {warm.Starts} and {warm.Reads}; memo {string.Join(" ", tally.TakeMemo().Select(row => $"{row.Kind}:{row.Hits}/{row.Misses}"))}");
        Assert.Equal(cold.Kind, warm.Kind);
        Assert.True(cold.Starts + cold.Reads > 0);
        Assert.Equal(0, warm.Starts + warm.Reads);
    }

    [Fact]
    public async Task A_moved_tip_asks_only_the_new_pairs()
    {
        var (root, line, tip) = await BranchAsync();
        using var home = GitObjectMemo.Within(Home);
        await AsksAsync(root, Proof(tip, line));

        await GitAsync(root, "checkout", "-q", "feature");
        await CommitFileAsync(root, "more.txt", "more\n");
        var moved = (await GitAsync(root, "rev-parse", "feature")).Trim();

        Assert.Equal(0, await AsksAsync(root, Proof(tip, line)));
        Assert.True(await AsksAsync(root, Proof(moved, line)) > 0);
        Assert.Equal(0, await AsksAsync(root, Proof(moved, line)));
    }

    [Fact]
    public async Task The_memo_survives_a_restart()
    {
        var (root, line, tip) = await BranchAsync();
        using (GitObjectMemo.Within(Home))
        {
            Assert.True(await AsksAsync(root, Proof(tip, line)) > 0);
            GitObjectMemo.Restart(Home);
        }

        using var again = GitObjectMemo.Within(Home);
        Assert.True(File.Exists(Path.Combine(Home, "memo", "git-objects.json")));
        Assert.Equal(0, await AsksAsync(root, Proof(tip, line)));
    }

    [Fact]
    public async Task A_read_naming_a_ref_or_head_is_never_kept()
    {
        var (root, line, _) = await BranchAsync();
        using var home = GitObjectMemo.Within(Home);
        string[][] reads = [["merge-base", "--is-ancestor", "feature", "main"], ["rev-list", "--count", "HEAD"], ["diff", "--name-only", line, "HEAD"], ["diff", "--name-only", line]];

        await AsksAsync(root, reads);

        Assert.True(await AsksAsync(root, reads) >= reads.Length);
    }

    [Fact]
    public void Only_reads_of_whole_ids_that_no_ref_or_file_decides_qualify()
    {
        var one = new string('a', 40);
        var two = new string('b', 40);
        Assert.True(GitObjectArguments.Qualifies(["merge-base", "--is-ancestor", one, two]));
        Assert.True(GitObjectArguments.Qualifies(["--no-optional-locks", "rev-list", "--count", $"{one}..{two}"]));
        Assert.True(GitObjectArguments.Qualifies(["cherry", one, two]));
        Assert.True(GitObjectArguments.Qualifies(["diff", "--no-renames", "--name-only", one, two]));
        Assert.True(GitObjectArguments.Qualifies(["log", "--reverse", "--no-merges", "--format=%H%x09%s", $"{one}..{two}"]));
        Assert.False(GitObjectArguments.Qualifies(["log", "--format=%H %an", $"{one}..{two}"]));
        Assert.False(GitObjectArguments.Qualifies(["log", $"{one}..{two}"]));
        Assert.False(GitObjectArguments.Qualifies(["log", "--format=%H", "main..HEAD"]));
        Assert.False(GitObjectArguments.Qualifies(["merge-base", "main", two]));
        Assert.False(GitObjectArguments.Qualifies(["rev-list", "--all", "--count", one]));
        Assert.False(GitObjectArguments.Qualifies(["rev-list", "--count", one, "--stdin"]));
        Assert.False(GitObjectArguments.Qualifies(["diff", "--name-only", one]));
        Assert.False(GitObjectArguments.Qualifies(["diff", "--cached", "--name-only", one, two]));
        Assert.False(GitObjectArguments.Qualifies(["merge-base", "--fork-point", one, two]));
        Assert.False(GitObjectArguments.Qualifies(["status", "--porcelain"]));
        Assert.False(GitObjectArguments.Qualifies(["-c", "core.pager=x", "merge-base", one, two]));
    }
}
