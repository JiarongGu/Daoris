using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The tree a git start makes, read from its arguments as the scheduler places it; no process is started.</summary>
public sealed class GitStartAsksTests
{
    private static readonly string Checkout = Path.GetFullPath(Path.Combine("_asks", "checkout"));

    [Fact]
    public void A_worktree_add_makes_the_tree_its_path_names_past_its_options_and_no_other_git_makes_one()
    {
        Assert.Equal(
            Path.Combine(Checkout, "trees", "one"),
            GitStartAsks.Made(Checkout, ["-c", "core.hooksPath=held", "worktree", "add", "--lock", "--reason", "opened by 1", "-b", "daoris/one", "trees/one", "main"]));
        Assert.Equal(
            Path.Combine(Checkout, "aside"),
            GitStartAsks.Made(Checkout, ["worktree", "add", "--quiet", "--detach", "--lock", "--reason", "held", "aside", "abc123"]));
        Assert.Equal(Path.Combine(Checkout, "-odd"), GitStartAsks.Made(Checkout, ["worktree", "add", "--detach", "--", "-odd", "abc123"]));

        Assert.Null(GitStartAsks.Made(Checkout, ["worktree", "remove", "--force", "trees/one"]));
        Assert.Null(GitStartAsks.Made(Checkout, ["worktree", "add", "--lock"]));
        Assert.Null(GitStartAsks.Made(Checkout, ["status"]));
    }

    private static string? SharedKey(params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = Checkout };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return GitStartAsks.For(info, arguments, GitRunner.Whole, TimeSpan.FromSeconds(30), shares: true).Start;
    }

    [Fact]
    public void An_identical_read_may_share_a_queued_start_and_no_write_may()
    {
        Assert.NotNull(SharedKey("rev-parse", "HEAD"));
        Assert.NotNull(SharedKey("status", "--porcelain"));

        Assert.Null(SharedKey("worktree", "add", "-b", "x", "trees/x", "main"));
        Assert.Null(SharedKey("config", "--add", "k", "v"));
        Assert.Null(SharedKey("commit", "-m", "x"));
    }
}
