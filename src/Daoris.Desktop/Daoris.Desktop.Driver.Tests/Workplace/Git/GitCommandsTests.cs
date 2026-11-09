using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one table of git's commands read against a folder laid out as git lays one out, so no git runs: each class, the branches a drop
/// names with the commit it verifies, the trees and refs a start touches, and the columns the bounds, the waits and the write test read.
/// </summary>
public sealed class GitCommandsTests : IDisposable
{
    private const string Old = "1111111111111111111111111111111111111111";
    private const string New = "2222222222222222222222222222222222222222";
    private const string Zero = "0000000000000000000000000000000000000000";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-commands-" + Guid.NewGuid().ToString("N")[..8]);

    public GitCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git", "refs", "heads"));
        File.WriteAllText(Path.Combine(_root, ".git", "HEAD"), "ref: refs/heads/main\n");
        foreach (var branch in new[] { "main", "x", "b" }) File.WriteAllText(Path.Combine(_root, ".git", "refs", "heads", branch), Old + "\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private GitCommandRead Read(string? input, params string[] arguments) => GitCommands.Read(_root, arguments, input);

    private static string[] Drops(GitCommandRead read) => [.. read.Drops.Select(each => each.Verifies is { } at ? $"{each.Branch}@{at[..4]}" : each.Branch)];

    [Theory]
    [InlineData("merge-tree", "--write-tree", "a", "b")]
    [InlineData("diff-tree", "-r", "a")]
    [InlineData("diff-index", "--cached", "HEAD")]
    [InlineData("grep", "-n", "word")]
    [InlineData("count-objects", "-v")]
    [InlineData("check-ref-format", "--branch", "x")]
    [InlineData("notes", "list")]
    [InlineData("replace", "-l")]
    [InlineData("submodule", "status")]
    [InlineData("branch", "--list")]
    [InlineData("symbolic-ref", "HEAD")]
    [InlineData("worktree", "list", "--porcelain")]
    public void A_read_is_started_and_written_nothing(params string[] arguments)
    {
        var read = Read(null, arguments);
        Assert.Equal((GitClass.Read, true), (read.Class, read.Rowed));
        Assert.False(GitCommands.Writes(arguments));
    }

    [Theory]
    [InlineData("notes", "add", "-m", "n", "HEAD")]
    [InlineData("replace", "-d", "abc")]
    [InlineData("submodule", "update", "--init")]
    [InlineData("maintenance", "run")]
    [InlineData("worktree", "add", "-b", "fresh", "../t", "HEAD")]
    [InlineData("commit", "-m", "work")]
    [InlineData("reset", "-q", "HEAD", "--", "file")]
    [InlineData("update-ref", "refs/heads/x", New, Old)]
    public void A_write_drops_no_branch(params string[] arguments)
    {
        var read = Read(null, arguments);
        Assert.Equal((GitClass.Write, true), (read.Class, read.Rowed));
        Assert.Empty(read.Drops);
        Assert.True(GitCommands.Writes(arguments));
    }

    [Fact]
    public void Update_refs_input_names_each_branch_it_drops_with_the_commit_it_verifies()
    {
        var read = Read($"verify refs/heads/b {Old}\ndelete refs/heads/x {Old}\nupdate refs/heads/b {Zero} {Old}\ncreate refs/daoris/k {Old}\n", "update-ref", "--stdin");
        Assert.Equal(GitClass.Drops, read.Class);
        Assert.Equal(["x@1111", "b@1111"], Drops(read));
        Assert.Contains("refs/daoris/k", read.Refs);
    }

    [Fact]
    public void The_nul_format_reads_the_same_lines()
    {
        var read = Read($"delete refs/heads/x\0{Old}\0create refs/daoris/k\0{Old}\0", "update-ref", "-z", "--stdin");
        Assert.Equal(["x@1111"], Drops(read));
    }

    [Fact]
    public void Symref_lines_and_a_verb_the_reader_does_not_know_drop_the_ref_they_name()
    {
        Assert.Equal(["main"], Drops(Read("symref-delete HEAD\n", "update-ref", "--stdin")));
        Assert.Equal(["x"], Drops(Read("symref-update refs/heads/x refs/heads/b\n", "update-ref", "--stdin")));
        Assert.Equal(["b"], Drops(Read("frob refs/heads/b\n", "update-ref", "--stdin")));
    }

    [Theory]
    [InlineData(new[] { "update-ref", "-d", "HEAD" }, new[] { "main" })]
    [InlineData(new[] { "update-ref", "-d", "refs/heads/x", Old }, new[] { "x@1111" })]
    [InlineData(new[] { "update-ref", "refs/heads/x", Zero, Old }, new[] { "x@1111" })]
    [InlineData(new[] { "branch", "-D", "x" }, new[] { "x" })]
    [InlineData(new[] { "branch", "-m", "y" }, new[] { "main" })]
    [InlineData(new[] { "branch", "-M", "x", "b" }, new[] { "x", "b" })]
    [InlineData(new[] { "branch", "-f", "x", "HEAD" }, new[] { "x" })]
    [InlineData(new[] { "branch", "-C", "x", "b" }, new[] { "b" })]
    [InlineData(new[] { "switch", "-C", "x" }, new[] { "x" })]
    [InlineData(new[] { "checkout", "-B", "x" }, new[] { "x" })]
    [InlineData(new[] { "worktree", "add", "-B", "x", "../t" }, new[] { "x" })]
    [InlineData(new[] { "push", ".", ":refs/heads/x" }, new[] { "x" })]
    [InlineData(new[] { "symbolic-ref", "-d", "HEAD" }, new[] { "main" })]
    [InlineData(new[] { "reset", "--hard", Old }, new[] { "main" })]
    [InlineData(new[] { "commit", "--amend", "--no-edit" }, new[] { "main" })]
    [InlineData(new[] { "cherry-pick", "--abort" }, new[] { "main" })]
    public void Each_drop_names_its_branches(string[] arguments, string[] drops)
    {
        var read = Read(null, arguments);
        Assert.Equal((GitClass.Drops, true), (read.Class, read.Rowed));
        Assert.Equal(drops, Drops(read));
    }

    [Fact]
    public void An_overwrite_of_a_name_that_does_not_stand_drops_nothing()
    {
        Assert.Empty(Read(null, "branch", "-f", "fresh", "HEAD").Drops);
        Assert.Empty(Read(null, "worktree", "add", "-B", "fresh", "../t").Drops);
    }

    [Fact]
    public void A_fetch_pruning_by_the_repositorys_configuration_into_a_mirror_refspec_drops_every_branch()
    {
        File.WriteAllText(Path.Combine(_root, ".git", "config"), "[remote \"origin\"]\n\turl = ../o\n\tfetch = +refs/*:refs/*\n\tmirror = true\n[fetch]\n\tprune = true\n");
        Assert.Equal(["refs/heads/*"], Drops(Read(null, "fetch", "origin")));
        Assert.Empty(Read(null, "fetch", "origin", "+refs/heads/*:refs/remotes/origin/*", "--prune").Drops);
    }

    [Fact]
    public void An_unrowed_start_is_read_as_a_drop_of_the_branches_it_names_and_the_current_one()
    {
        var read = Read(null, "frobnicate", "--hard", "x");
        Assert.Equal((GitClass.Drops, false), (read.Class, read.Rowed));
        Assert.Equal(["x", "main"], Drops(read));
        Assert.False(Read(null, "notes", "frob").Rowed);
    }

    [Fact]
    public void The_trees_and_refs_a_start_touches_are_read_wherever_it_runs()
    {
        Assert.Equal([Path.GetFullPath(Path.Combine(_root, "sub"))], Read(null, "-C", "sub", "status").Trees);
        Assert.Contains(Path.GetFullPath(Path.Combine(_root, "..", "t")), Read(null, "worktree", "unlock", "../t").Trees);
        Assert.Equal(["refs/heads/x"], Read(null, "update-ref", "refs/heads/x", New, Old).Refs);
        Assert.Contains("refs/heads/main", Read(null, "commit", "-m", "work").Refs);
    }

    [Fact]
    public void The_bounds_and_the_waits_read_their_columns_from_the_table()
    {
        Assert.Equal(GitBounds.Question, GitBounds.Of(["merge-tree", "--write-tree", "a", "b"]));
        Assert.Equal(GitBounds.Work, GitBounds.Of(["commit", "-m", "x"]));
        Assert.Equal(GitBounds.Question, GitBounds.Of(["-c", "a=b", "worktree", "list"]));
        Assert.Equal(GitWait.Read, GitStartAsks.WaitOf(["commit-tree", "t", "-m", "x"], writes: true));
        Assert.Equal(GitWait.Outside, GitStartAsks.WaitOf(["commit", "-m", "x"], writes: true));
    }
}
