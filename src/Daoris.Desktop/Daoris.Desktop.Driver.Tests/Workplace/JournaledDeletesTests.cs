using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1r's proof (D204, D228; the oneway design §5 G3): every branch delete is the journal's <c>ref.delete</c> act with its Undo. A delete a
/// door of Daoris's judged (a discard, a withdrawal, a sweep, the clean-up, a housekeeping discard, a removal) passes
/// <see cref="ContentHolds.DeleteJudgedAsync"/>, which runs <see cref="JournaledDeletes"/>; a forced one reads its tip and passes the same. The
/// record is pressed by <c>daoris</c>, its section journaled, and <c>GIT_REF_UNDO</c>'s act makes the branch again; under a tidy's hold the act
/// runs inside it. A census of the sources holds the one way.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class JournaledDeletesTests : TreeSyncFixture
{
    private static async Task<string?> RefAsync(string root, string name)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name], CancellationToken.None);
        return code == 0 ? said.Trim() : null;
    }

    private static Task<string?> Clear() => Task.FromResult<string?>(null);

    [Fact]
    public async Task A_judged_delete_is_the_journals_act_pressed_by_daoris_and_its_Undo_makes_the_branch_again_with_its_section()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-1");
        await GitAsync(root, "config", "branch.daoris/s-1.remote", "origin");
        var tip = await RevAsync(root, "daoris/s-1");
        var main = await RevAsync(root, "main");

        var gone = await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-1", tip, [("refs/heads/main", main)], Clear);

        Assert.Null(gone.Kept);
        Assert.Null(gone.Said);
        Assert.Null(await RefAsync(root, "refs/heads/daoris/s-1"));
        var journal = new ActJournal(Home);
        var act = journal.Act(gone.Act!)!;
        Assert.Equal((GitRefActs.DeleteKind, JournaledDeletes.Press, "engine", false), (act.Kind, act.Press, act.Repository, act.Open));
        Assert.Equal([ActStepKinds.Ref, ActStepKinds.Config], journal.Steps(act.Id).Select(step => step.Kind));
        Assert.Equal(tip, (string?)JsonNode.Parse(act.Plan)!["at"]);

        var undone = await GitRefActs.UndoAsync(new ActRuns(journal), "engine", root, act.Id, "window", "window", CancellationToken.None);

        Assert.False(undone.Refused, undone.Said);
        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/daoris/s-1"));
        Assert.Equal("origin", (await GitAsync(root, "config", "branch.daoris/s-1.remote")).Trim());
    }

    [Fact]
    public async Task A_proof_ref_that_moved_refuses_the_delete_and_the_branch_stays()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-2");
        var tip = await RevAsync(root, "daoris/s-2");

        var kept = await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-2", tip, [("refs/heads/main", new string('1', 40))], Clear);

        Assert.NotNull(kept.Act);
        Assert.NotNull(kept.Said);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/daoris/s-2"));
        Assert.Equal(ActOutcomes.NotDone, new ActJournal(Home).Act(kept.Act!)!.Outcome);
    }

    [Fact]
    public async Task A_late_checkout_puts_the_branch_back_and_stops_before_its_section_goes()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-3");
        await GitAsync(root, "config", "branch.daoris/s-3.remote", "origin");
        var tip = await RevAsync(root, "daoris/s-3");

        var kept = await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-3", tip, [("refs/heads/daoris/s-3", tip)], async () =>
        {
            await GitAsync(root, "update-ref", "refs/heads/daoris/s-3", tip, "");
            return "it was checked out at elsewhere as it went, so it was put back where it was";
        });

        Assert.Contains("put back", kept.Kept);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/daoris/s-3"));
        Assert.Equal("origin", (await GitAsync(root, "config", "branch.daoris/s-3.remote")).Trim());
    }

    [Fact]
    public async Task Under_a_tidys_hold_the_delete_runs_inside_it_and_a_forced_door_is_journaled_the_same_way()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-4");
        var content = new ContentHolds(new TreeContext(Home, new LandingPlugins(Home)));

        using (var hold = TreeLock.TryReplaying(Home, root, "engine", TreeLock.Tidying))
        {
            Assert.NotNull(hold);
            Assert.Null(await JournaledDeletes.ForcedAsync(content, root, "engine", "daoris/s-4", CancellationToken.None));
        }

        Assert.Null(await RefAsync(root, "refs/heads/daoris/s-4"));
        Assert.Contains("no such branch", await JournaledDeletes.ForcedAsync(content, root, "engine", "daoris/s-4", CancellationToken.None));
    }

    /// <summary>The one way: no driver source deletes a branch itself but an opening undoing the branch it just made.</summary>
    [Fact]
    public void No_driver_source_deletes_a_branch_but_through_the_journal()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var deleting = Directory.EnumerateFiles(driver, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file) is var text
                && (text.Contains("\"branch\", \"-D\"", StringComparison.Ordinal) || text.Contains("\"branch\", \"--delete\"", StringComparison.Ordinal)
                    || text.Contains(".Delete($\"refs/heads/", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["TreeOpenings.cs"], deleting);
    }
}
