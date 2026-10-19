using System.ComponentModel;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// QUEUEDTREE1b (D51, D181's QUEUEDTREE1 note): a start's own path, from the look to its record's open, when the driver closes after
/// its tree opened and before the record's open is asked. The tree and its branch go by the clean path on no token, and no record opens.
/// </summary>
/// <remarks>
/// The fast half: no process starts. The driver has no <c>Runner</c>, so the look reaches the start's own path; git is a small model
/// of one repository standing in through <see cref="WorkingTree.Runs"/>, which sees the token each git is asked on, and every other
/// start in the flow is refused through <see cref="ProcessStarts.Within"/>. The loop closes as the start reads its tree's head, the
/// last await between the open and the record's. The real-git twin is GOONTREE1's row in the Process half's
/// <c>SessionMessagesTickTests</c>.
/// </remarks>
public sealed class StartTreeCloseTests : IDisposable
{
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-start-tree-close-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new();

    // Each git asked, in order, marked where it was asked on no token; and each other start, refused.
    private readonly List<string> _asked = [];
    private readonly List<string> _seen = [];

    private readonly string _root;
    private readonly string _agent;

    // The tree the start opened; and the model's session tree and branch, while each stands.
    private string? _opened;
    private string? _tree;
    private string? _branch;

    public StartTreeCloseTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        _agent = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(_agent, "");
        _ledger.Register("engine", _root);
        _ledger.Publish("q1", "engine");
    }

    public void Dispose()
    {
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 The row's case through the driver: the close after the tree's open leaves no tree, no branch and no record.</summary>
    [Fact]
    public async Task A_close_after_the_starts_tree_opened_lets_the_tree_go_on_no_token_and_opens_no_record()
    {
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            _ledger.Client(),
            DriverConfig.Empty with
            {
                Drivable = ["engine"],
                Trees = ["engine"],
                Adapter = "acp-stub",
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = [_agent] },
            },
            adapters,
            _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        WorkingTree.Runs.Value = GitAsync;
        try
        {
            using (ProcessStarts.Within(info =>
            {
                lock (_seen) _seen.Add(Path.GetFileName(info.FileName));
                throw new Win32Exception(2, "The system cannot find the file specified.");
            }))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => driver.RunOnceAsync(_closing.Token).WaitAsync(Poll.Patience));
            }
        }
        finally
        {
            WorkingTree.Runs.Value = null;
        }

        var said = $"git asked:\n{string.Join('\n', _asked)}\nother starts: {string.Join(", ", _seen)}";
        var tree = _opened ?? throw new Xunit.Sdk.XunitException("the start opened no tree: " + said);
        Assert.False(Directory.Exists(tree), "the tree was left: " + said);
        Assert.Contains($"(no token) worktree remove {Path.GetFullPath(tree)}", _asked);
        Assert.Contains("(no token) update-ref --stdin", _asked);
        Assert.True(_branch is null, "the branch was left: " + said);
        Assert.Empty(_ledger.Sessions);
        Assert.DoesNotContain(Path.GetFileName(_agent), _seen);
    }

    /// <summary>git as one repository on <c>main</c>, as far as a start's open and a fresh tree's removal ask it.</summary>
    private Task<ChildRun> GitAsync(IReadOnlyList<string> arguments, TimeSpan bound, CancellationToken ct)
    {
        lock (_asked) _asked.Add((ct.CanBeCanceled ? "" : "(no token) ") + string.Join(' ', arguments));
        switch (arguments)
        {
            case ["rev-parse", "--show-toplevel"]:
                return Said(_root);
            case ["worktree", "add", .., "-b", var branch, var path, _]:
                Directory.CreateDirectory(path);
                (_opened, _tree, _branch) = (path, path, branch);
                return Said("");
            case ["rev-parse", "--verify", "--quiet", var name]:
                return name == "refs/heads/main" || name == "HEAD^{commit}" || (_branch is not null && name == $"refs/heads/{_branch}")
                    ? Said(Tip)
                    : Failed();
            case ["rev-parse", "HEAD"] when _tree is not null:
                // The tree's head, read just before the record's open is asked: the driver closes here.
                _closing.Cancel();
                ct.ThrowIfCancellationRequested();
                return Said(Tip);
            case ["rev-parse", "--path-format=absolute", "--git-common-dir", "--abbrev-ref", "HEAD"]:
                return Said($"{Path.Combine(_root, ".git")}\n{_branch}");
            case ["for-each-ref", "--contains", ..]:
                return Said($"{Tip}\trefs/heads/main\t");
            case ["worktree", "remove", var gone]:
                Directory.Delete(gone, recursive: true);
                _tree = null;
                return Said("");
            case ["update-ref", "--stdin"]:
                // The branch's delete, verified against the line's tip: the model's one transaction.
                _branch = null;
                return Said("");
            case ["status", ..] or ["log", ..] or ["ls-files", ..] or ["config", ..] or ["worktree", "unlock", _] or ["worktree", "list", ..]:
                return Said("");
            default:
                return Failed();
        }
    }

    private static Task<ChildRun> Said(string stdout) => Task.FromResult(new ChildRun(true, 0, stdout + "\n", "", false));

    private static Task<ChildRun> Failed() => Task.FromResult(new ChildRun(true, 1, "", "the stand-in does not answer this", false));
}
