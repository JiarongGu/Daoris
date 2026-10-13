using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1a (D147 §4.1): the branch list's one <c>for-each-ref</c> per repository, its machine format and the pure readers of
/// what git answers. No process starts here: each answer is git's own bytes, written out.
/// </summary>
public sealed class GitBranchesParseTests
{
    private const string Main = "1111111111111111111111111111111111111111";
    private const string Topic = "2222222222222222222222222222222222222222";

    /// <summary>One record as the format writes it: seven fields, each ended by a NUL, and git's newline after the last.</summary>
    private static string Record(
        string name, string commit, string at = "2026-10-04T01:10:25+10:00", string symref = "", string worktree = "",
        string distance = "", string subject = "work") =>
        string.Join('\0', name, commit, at, symref, worktree, distance, subject) + "\0\n";

    [Fact]
    public void The_format_asks_every_field_in_a_machine_form_and_counts_from_the_line_named_in_full()
    {
        var arguments = GitRefs.Arguments(trees: true, against: "refs/heads/main");

        Assert.Equal(
            ["for-each-ref",
             "--format=%(refname)%00%(objectname)%00%(committerdate:iso-strict)%00%(symref)%00%(worktreepath)%00%(ahead-behind:refs/heads/main)%00%(contents:subject)%00",
             "refs/heads", "refs/remotes/origin"],
            arguments);
    }

    /// <summary>An atom left out keeps its place, empty, so one parser reads every form the fallbacks ask.</summary>
    [Fact]
    public void An_atom_left_out_keeps_its_place_empty()
    {
        Assert.Equal(
            "--format=%(refname)%00%(objectname)%00%(committerdate:iso-strict)%00%(symref)%00%00%00%(contents:subject)%00",
            GitRefs.Arguments(trees: false, against: null)[1]);
        Assert.Contains("%(worktreepath)%00%00", GitRefs.Arguments(trees: true, against: null)[1]);
    }

    [Fact]
    public void Each_record_is_read_whole()
    {
        var refs = GitRefs.Parse(
            Record("refs/heads/main", Main, worktree: "D:/repos/engine", distance: "0 0", subject: "Merge the line")
            + Record("refs/heads/topic", Topic, distance: "3 5", subject: "a subject with\ttabs and ünïcode")
            + Record("refs/remotes/origin/HEAD", Main, symref: "refs/remotes/origin/main", distance: "0 0"));

        Assert.Equal(3, refs.Count);
        var main = refs[0];
        Assert.Equal(("refs/heads/main", Main, "D:/repos/engine", 0, 0, "Merge the line"),
            (main.Name, main.Commit, main.Worktree, main.Ahead, main.Behind, main.Subject));
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 1, 10, 25, TimeSpan.FromHours(10)), main.At);
        Assert.True(main.Local);
        Assert.Equal("main", main.Short);

        var topic = refs[1];
        Assert.Null(topic.Worktree);
        Assert.Equal((3, 5), (topic.Ahead, topic.Behind));
        Assert.Equal("a subject with\ttabs and ünïcode", topic.Subject);

        var head = refs[2];
        Assert.Equal("refs/remotes/origin/main", head.Symref);
        Assert.False(head.Local);
        Assert.Equal("origin/HEAD", head.Short);
    }

    /// <summary>A field git left empty is no answer: no tree, no distance, no time.</summary>
    [Fact]
    public void An_empty_field_is_no_answer()
    {
        var only = Assert.Single(GitRefs.Parse(Record("refs/heads/topic", Topic, at: "", distance: "", subject: "")));

        Assert.Null(only.At);
        Assert.Null(only.Worktree);
        Assert.Null(only.Ahead);
        Assert.Null(only.Behind);
        Assert.Null(only.Symref);
        Assert.Equal("", only.Subject);
    }

    /// <summary>
    /// 🔴 A NUL ends each field, so a newline inside one (a path, however unlikely) is the field's, never a new record; and
    /// a record cut short, or one naming no commit, is passed over rather than read askew.
    /// </summary>
    [Fact]
    public void A_newline_inside_a_field_is_the_fields_and_a_broken_record_is_passed_over()
    {
        var refs = GitRefs.Parse(
            Record("refs/heads/odd", Topic, worktree: "D:/repos/a\nb")
            + Record("refs/heads/bad", "not-a-commit")
            + "refs/heads/cut\0" + Main + "\0");

        var only = Assert.Single(refs);
        Assert.Equal(("refs/heads/odd", "D:/repos/a\nb"), (only.Name, only.Worktree));
    }

    [Fact]
    public void Nothing_answered_is_no_refs()
    {
        Assert.Empty(GitRefs.Parse(""));
        Assert.Empty(GitRefs.Parse("\n"));
    }

    /// <summary>The fallback for a git older than <c>%(worktreepath)</c>: <c>git worktree list --porcelain</c>, a tree per branch.</summary>
    [Fact]
    public void The_worktree_list_names_the_tree_holding_each_branch()
    {
        var trees = GitRefs.ParseWorktrees(
            $"worktree D:/repos/engine\nHEAD {Main}\nbranch refs/heads/main\n\n"
            + $"worktree D:/data/trees/aurora/engine/s-1a2b3c4d\nHEAD {Topic}\nbranch refs/heads/daoris/s-1a2b3c4d\n\n"
            + $"worktree D:/scratch/detached\nHEAD {Topic}\ndetached\n\n");

        Assert.Equal(2, trees.Count);
        Assert.Equal("D:/repos/engine", trees["refs/heads/main"]);
        Assert.Equal("D:/data/trees/aurora/engine/s-1a2b3c4d", trees["refs/heads/daoris/s-1a2b3c4d"]);
    }

    [Theory]
    [InlineData("3\t5\n", 3, 5)]
    [InlineData("0\t0", 0, 0)]
    public void A_count_reads_both_sides(string answer, int left, int right) =>
        Assert.Equal((left, right), GitRefs.ParseCounts(answer));

    [Theory]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("a\tb")]
    public void A_count_that_does_not_read_is_none(string answer) => Assert.Null(GitRefs.ParseCounts(answer));

    /// <summary>
    /// The command a read ran, ready to copy into a shell: an argument holding a character a shell reads is quoted, and none
    /// holds a double quote or a dollar sign a shell would expand inside one.
    /// </summary>
    [Fact]
    public void The_command_a_read_ran_is_said_ready_to_copy()
    {
        Assert.Equal(
            "git for-each-ref \"--format=%(refname)%00%(objectname)%00%(committerdate:iso-strict)%00%(symref)%00%(worktreepath)%00%(ahead-behind:refs/heads/main)%00%(contents:subject)%00\" refs/heads refs/remotes/origin",
            GitRefs.Command(GitRefs.Arguments(trees: true, against: "refs/heads/main")));
        Assert.Equal("git rev-list --left-right --count refs/heads/a...refs/heads/main",
            GitRefs.Command(["rev-list", "--left-right", "--count", "refs/heads/a...refs/heads/main"]));
    }

    /// <summary>Each atom's floor, read from git's own documentation at its tags, said where a fallback is said.</summary>
    [Fact]
    public void Each_atom_names_the_git_it_needs()
    {
        Assert.Equal(new Version(2, 23), GitRefs.WorktreePathSince);
        Assert.Equal(new Version(2, 41), GitRefs.AheadBehindSince);
    }

    /// <summary>
    /// 🔴 git walks up: a root with no <c>.git</c> of its own would be answered for by whatever repository encloses it, so
    /// the git directory is found on disk first. A linked worktree's <c>.git</c> file names its own.
    /// </summary>
    [Fact]
    public void The_git_directory_is_the_roots_own_or_none()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "daoris-gitdir-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var plain = Directory.CreateDirectory(Path.Combine(scratch, "plain")).FullName;
            Assert.Null(GitBranches.GitDirectory(plain));
            Assert.Null(GitBranches.GitDirectory(Path.Combine(scratch, "absent")));

            var checkout = Directory.CreateDirectory(Path.Combine(scratch, "checkout")).FullName;
            var own = Directory.CreateDirectory(Path.Combine(checkout, ".git")).FullName;
            Assert.Equal(own, GitBranches.GitDirectory(checkout));

            var linked = Directory.CreateDirectory(Path.Combine(scratch, "linked")).FullName;
            var named = Directory.CreateDirectory(Path.Combine(own, "worktrees", "linked")).FullName;
            File.WriteAllText(Path.Combine(linked, ".git"), "gitdir: ../checkout/.git/worktrees/linked\n");
            Assert.Equal(Path.GetFullPath(named), GitBranches.GitDirectory(linked));

            File.WriteAllText(Path.Combine(linked, ".git"), "gitdir: ../nowhere\n");
            Assert.Null(GitBranches.GitDirectory(linked));
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    /// <summary>
    /// The last fetch is git's own: <c>FETCH_HEAD</c>'s time, never a guess. An empty one is a fetch that heard nothing
    /// (git empties it as a fetch begins), said as tried, not heard; none at all is never fetched here.
    /// </summary>
    [Fact]
    public void The_last_fetch_is_fetch_heads_time()
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "daoris-fetch-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        try
        {
            Assert.Null(GitBranches.FetchOf(scratch));

            var file = Path.Combine(scratch, "FETCH_HEAD");
            File.WriteAllText(file, $"{Main}\t\tbranch 'main' of origin\n");
            var then = new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(file, then);
            Assert.Equal(new GitFetch(new DateTimeOffset(then), Heard: true), GitBranches.FetchOf(scratch));

            File.WriteAllText(file, "");
            File.SetLastWriteTimeUtc(file, then);
            Assert.Equal(new GitFetch(new DateTimeOffset(then), Heard: false), GitBranches.FetchOf(scratch));
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }
}
