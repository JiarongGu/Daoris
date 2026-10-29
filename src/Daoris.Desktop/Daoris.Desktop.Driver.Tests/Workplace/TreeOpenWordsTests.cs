using Daoris.Driver;
using static Daoris.Driver.TreeGit;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEOPENERR1: an opening git refused with nothing on its error stream said only "nothing" (or an empty pair of brackets), and the
/// failed run's evidence (git's exit code, how long it ran, the folder it ran in and the arguments it had) went with it. No git
/// runs here: git's answer is stood in (<see cref="WorkingTree.Answered"/>), which is the one way to make "exits non-zero, says
/// nothing" happen on demand.
/// </summary>
public sealed class TreeOpenWordsTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-open-words", Guid.NewGuid().ToString("N")[..8]);

    public TreeOpenWordsTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        WorkingTree.Answered.Value = null;
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task A_root_git_cannot_read_and_says_nothing_of_is_refused_with_what_git_did()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        WorkingTree.Answered.Value = arguments => arguments.Contains("--show-toplevel") ? (1, "", "") : null;

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new SessionTrees(Path.Combine(_scratch, "home")).OpenAsync(root, "engine", "aurora"));

        Assert.DoesNotContain("()", error.Message);
        Assert.Contains("git said nothing on its error stream", error.Message);
        Assert.Contains("`git rev-parse --show-toplevel`", error.Message);
        Assert.Contains("exited with code 1", error.Message);
        Assert.Contains(" ms", error.Message);
        Assert.Contains($"in {root}", error.Message);
        Assert.Contains("the folder stands, with no .git in it", error.Message);
    }

    [Fact]
    public async Task A_folder_that_is_gone_when_git_says_nothing_is_said_to_be_gone()
    {
        var root = Path.Combine(_scratch, "vanished");
        WorkingTree.Answered.Value = arguments => arguments.Contains("--show-toplevel") ? (128, "", "  \n") : null;

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new SessionTrees(Path.Combine(_scratch, "home")).OpenAsync(root, "engine", "aurora"));

        Assert.Contains("exited with code 128", error.Message);
        Assert.Contains("the folder is gone", error.Message);
    }

    [Fact]
    public async Task What_git_did_say_is_still_quoted_as_it_was()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        WorkingTree.Answered.Value = arguments => arguments.Contains("--show-toplevel")
            ? (128, "", "fatal: not a git repository (or any of the parent directories): .git\nhint: more\n")
            : null;

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new SessionTrees(Path.Combine(_scratch, "home")).OpenAsync(root, "engine", "aurora"));

        Assert.Contains("(fatal: not a git repository (or any of the parent directories): .git)", error.Message);
        Assert.DoesNotContain("said nothing", error.Message);
    }

    /// <summary>
    /// A code git never returns is the system's: a process ended at its start (a DLL that would not load, a resource the machine had
    /// run out of) leaves a status code and no words, and the sentence says it was seen, not that git failed.
    /// </summary>
    [Theory]
    [InlineData(-1073741502, "0xC0000142")]
    [InlineData(-1073741819, "0xC0000005")]
    public void A_status_code_only_the_system_gives_is_said_to_be_the_systems(int code, string hex)
    {
        var said = new GitSaid(code, "", "", TimeSpan.FromMilliseconds(212), _scratch, ["worktree", "unlock", "somewhere"]);

        var words = said.Unsaid;

        Assert.NotNull(words);
        Assert.Contains($"exited with code {code} ({hex})", words);
        Assert.Contains("a Windows status code, which git does not return itself", words);
        Assert.Contains("`git worktree unlock somewhere`", words);
        Assert.Contains("after 212 ms", words);
    }

    [Fact]
    public void Words_on_the_error_stream_leave_nothing_unsaid_and_a_success_is_never_one()
    {
        Assert.Null(new GitSaid(1, "", "fatal: why\n", TimeSpan.Zero, _scratch, ["status"]).Unsaid);
        Assert.Equal("fatal: why", new GitSaid(1, "", "warning: x\nfatal: why\n", TimeSpan.Zero, _scratch, ["status"]).Failure());
        Assert.Null(new GitSaid(0, "", "", TimeSpan.Zero, _scratch, ["status"]).Unsaid);
    }
}
