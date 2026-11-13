using System.ComponentModel;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A start's own path, from the look to its record's open, when the driver closes after
/// its tree opened and before the record's open is asked. The tree goes by the clean path on no token, and no record opens.
/// </summary>
/// <remarks>
/// The fast half: no process starts. The driver has no <c>Runner</c>, so the look reaches the start's own path; git is a small model
/// of one repository standing in through <see cref="GitFlow.Runs"/>, which sees the token each git is asked on, and every other
/// start in the flow is refused through <see cref="ProcessStarts.Within"/>. The loop closes as the start reads its tree's head, the
/// last await between the open and the record's. The branch's delete is the journal's <c>ref.delete</c> act, whose git
/// starts as a held child no stand-in reaches, so that half of the case, with the tree's, is <see cref="StartTreeCloseGitTests"/>', over
/// real git in the git process family.
/// </remarks>
public sealed class StartTreeCloseTests : IDisposable
{
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-start-tree-close-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new();

    // Each git asked, in order, marked with where it is asked on no token; and each other start, refused.
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
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
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
        var adapters = ProviderRegistry.Built();
        var driver = new Daoris.Driver.Driver(
            _ledger.Client(),
            ActorChoices.With(
                DriverConfig.Empty with
                {
                    Drivable = ["engine"],
                    DefaultWorkflow = IsolatedPlaces.In(_home),
                    Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = [_agent] },
                },
                "build", [new ActorChoice("acp-stub")]),
            adapters,
            _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        GitFlow.Runs.Value = GitAsync;
        Exception? thrown;
        TickReport? report = null;
        try
        {
            using (ProcessStarts.Within(info =>
            {
                lock (_seen) _seen.Add(Path.GetFileName(info.FileName));
                throw new Win32Exception(2, "The system cannot find the file specified.");
            }))
            {
                thrown = await Record.ExceptionAsync(async () => report = await driver.RunOnceAsync(_closing.Token).WaitAsync(Poll.Patience));
            }
        }
        finally
        {
            GitFlow.Runs.Value = null;
        }

        var said = $"git asked:\n{string.Join('\n', _asked)}\nother starts: {string.Join(", ", _seen)}"
            + (report is null ? "" : $"\nthe tick said: {string.Join(" | ", report.Events)}; {string.Join(" | ", report.Considerations)}");
        Assert.True(thrown is OperationCanceledException, $"the run ended on {thrown?.GetType().Name ?? "no exception"}, not the close: {said}");
        var tree = _opened ?? throw new Xunit.Sdk.XunitException("the start opened no tree: " + said);
        Assert.False(Directory.Exists(tree), "the tree was left: " + said);
        Assert.Contains($"(no token) worktree remove {Path.GetFullPath(tree)}", _asked);
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
            case ["-c", var hooks, "worktree", "add", .., "-b", var branch, var path, _] when hooks.StartsWith("core.hooksPath=", StringComparison.Ordinal):
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
            case ["rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD"]:
                // The tree's repository, its own working root and its branch, in one start.
                return Said($"{Path.Combine(_root, ".git")}\n{_tree}\n{_branch}");
            case ["for-each-ref", "--contains", ..]:
                return Said($"{Tip}\trefs/heads/main\t");
            case ["worktree", "remove", var gone]:
                Directory.Delete(gone, recursive: true);
                _tree = null;
                return Said("");
            case ["--no-optional-locks", "status", ..] or ["log", ..] or ["ls-files", ..] or ["config", ..] or ["worktree", "unlock", _] or ["worktree", "list", ..]:
                return Said("");
            default:
                return Failed();
        }
    }

    private static Task<ChildRun> Said(string stdout) => Task.FromResult(new ChildRun(true, 0, stdout + "\n", "", false));

    private static Task<ChildRun> Failed() => Task.FromResult(new ChildRun(true, 1, "", "the stand-in does not answer this", false));
}
