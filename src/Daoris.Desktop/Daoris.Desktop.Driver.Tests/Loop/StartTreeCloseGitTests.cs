using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: the case of <see cref="StartTreeCloseTests"/>, whose stand-in git cannot model the branch's delete. That delete
/// is the journal's <c>ref.delete</c> act, whose git starts as a held child outside the gate's seam, so only a real repository
/// answers it. A start's tree opens, the driver closes before the record's open is asked, and the tree and its branch go by the clean path
/// on no token with no record opened and no harness started.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Harness)]
[Trait(Category.Parallel, Category.Git)]
public sealed class StartTreeCloseGitTests : LandedFixture
{
    /// <summary>🔴 The close after the tree's open leaves no tree, no branch and no record, in a real repository.</summary>
    [Fact]
    public async Task A_close_after_the_starts_tree_opened_lets_the_tree_and_its_branch_go_in_a_real_repository()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var agent = Path.Combine(Scratch, "agent.mjs");
        await File.WriteAllTextAsync(agent, "");
        using var ledger = new StandInLedger();
        ledger.Register("engine", root);
        ledger.Publish("q1", "engine");
        using var closing = new CancellationTokenSource();
        var adapters = ProviderRegistry.Built();
        var driver = new Daoris.Driver.Driver(
            ledger.Client(),
            ActorChoices.With(
                DriverConfig.Empty with
                {
                    Drivable = ["engine"],
                    DefaultWorkflow = IsolatedPlaces.In(Home),
                    Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = [agent] },
                },
                "build", [new ActorChoice("acp-stub")]),
            adapters,
            Home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)));

        var asked = new List<string>();
        var others = new List<string>();
        var opened = false;
        WorkingTree.Started.Value = arguments =>
        {
            lock (asked) asked.Add(string.Join(' ', arguments));
            if (arguments is ["-c", _, "worktree", "add", ..]) opened = true;

            // The tree's head, read just before the record's open is asked: the driver closes here.
            else if (opened && arguments is ["rev-parse", "HEAD"]) closing.Cancel();
        };
        try
        {
            // Git goes through to the real process; every other start in the flow is refused.
            using (ProcessStarts.Within(info =>
            {
                if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase)) return Process.Start(info);
                lock (others) others.Add(Path.GetFileName(info.FileName));
                throw new Win32Exception(2, "The system cannot find the file specified.");
            }))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => driver.RunOnceAsync(closing.Token).WaitAsync(Poll.Patience));
            }
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        var said = $"git asked:\n{string.Join('\n', asked)}\nother starts: {string.Join(", ", others)}";
        Assert.True(opened, "the start opened no tree: " + said);
        var worktrees = await GitAsync(root, "worktree", "list", "--porcelain");
        Assert.True(worktrees.Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal)) == 1, "a tree was left: " + worktrees + said);
        var branches = (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/heads")).Trim();
        Assert.True(branches == "refs/heads/main", "the branch was left: " + branches + "\n" + said);
        Assert.Empty(ledger.Sessions);
        Assert.DoesNotContain(Path.GetFileName(agent), others);
    }
}
