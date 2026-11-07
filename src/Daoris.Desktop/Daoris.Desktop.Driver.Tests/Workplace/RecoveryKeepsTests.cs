using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each door's recovery ref is kept through the journal's act and dated from it. A
/// judged delete that keeps makes its ref in the delete's own transaction, which its Undo takes back; a keep alone is one <c>ref.keep</c> act;
/// the retention dates a recorded ref from its act; and the census counts the refs no journal record names, which the reader and press of
/// unrecorded refs stay for.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RecoveryKeepsTests : TreeSyncFixture
{
    private static readonly IReadOnlySet<string> NoneMade = new HashSet<string>();

    private static async Task<string?> RefAsync(string root, string name)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", name], CancellationToken.None);
        return code == 0 ? said.Trim() : null;
    }

    private static Task<string?> Clear(string? made) => Task.FromResult<string?>(null);

    [Fact]
    public async Task A_delete_that_keeps_makes_its_recovery_ref_in_its_own_act_dated_from_it_and_its_Undo_takes_it_back()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-1");
        var tip = await RevAsync(root, "daoris/s-1");
        var (name, why) = await RecoveryKeeps.NameAsync(root, "daoris/s-1", tip, CancellationToken.None);
        Assert.Null(why);
        Assert.Equal(($"{ContentHolds.DiscardedPrefix}daoris/s-1", true), (name!.Ref, name.Made));

        var gone = await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-1", tip, [("refs/heads/main", await RevAsync(root, "main"))], Clear, name);

        Assert.Null(gone.Kept);
        Assert.Null(await RefAsync(root, "refs/heads/daoris/s-1"));
        Assert.Equal(tip, await RefAsync(root, name.Ref));
        var journal = new ActJournal(Home);
        var act = journal.Act(gone.Act!)!;
        var plan = JsonNode.Parse(act.Plan)!;
        Assert.Equal((name.Ref, true), ((string?)plan["recovery"], (bool)plan["recoveryMade"]!));
        Assert.Equal(act.At, RecoveryKeeps.Recorded(Home, "engine")[name.Ref]);
        var (refs, _) = await RecoveryRetention.ReadAsync(root, "engine", NoneMade, CancellationToken.None, RecoveryKeeps.Recorded(Home, "engine"));
        Assert.Equal((act.At, true), (Assert.Single(refs!).Dated, refs![0].Recorded));

        var undone = await GitRefActs.UndoAsync(new ActRuns(journal), "engine", root, act.Id, "window", "window", CancellationToken.None);

        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/daoris/s-1"));
        Assert.Null(await RefAsync(root, name.Ref));
    }

    [Fact]
    public async Task A_refused_delete_makes_no_recovery_ref_and_a_late_checkout_takes_it_back_with_the_branch()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-2");
        await GitAsync(root, "branch", "daoris/s-3");
        var tip = await RevAsync(root, "daoris/s-2");
        var main = await RevAsync(root, "main");
        var (refusedName, _) = await RecoveryKeeps.NameAsync(root, "daoris/s-2", tip, CancellationToken.None);

        var refused = await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-2", tip, [("refs/heads/main", new string('1', 40))], Clear, refusedName);

        Assert.NotNull(refused.Said);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/daoris/s-2"));
        Assert.Null(await RefAsync(root, refusedName!.Ref));

        var late = Path.Combine(Path.GetDirectoryName(root)!, "late-s-3");
        var content = new ContentHolds(new TreeContext(Home, new LandingPlugins(Home))
        {
            BeforeRefDeleted = async branch => await GitAsync(root, "worktree", "add", late, branch),
        });
        var (name, _) = await RecoveryKeeps.NameAsync(root, "daoris/s-3", tip, CancellationToken.None);

        var stays = await content.DeleteJudgedAsync(root, "engine", "daoris/s-3", tip, [("refs/heads/main", main)], CancellationToken.None, name);

        Assert.Contains("put back", stays);
        Assert.Equal(tip, await RefAsync(root, "refs/heads/daoris/s-3"));
        Assert.Null(await RefAsync(root, name!.Ref));
    }

    [Fact]
    public async Task A_keep_alone_is_one_ref_keep_act_and_a_second_at_the_same_commit_verifies_the_one_standing()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-4");
        var tip = await RevAsync(root, "daoris/s-4");

        var (kept, why) = await RecoveryKeeps.KeepAsync(Home, root, "engine", "daoris/s-4", tip);
        var (again, _) = await RecoveryKeeps.KeepAsync(Home, root, "engine", "daoris/s-4", tip);
        var (refused, said) = await RecoveryKeeps.KeepAsync(Home, root, "engine", "daoris/s-5", tip, absent: "refs/heads/daoris/s-4");

        Assert.Null(why);
        Assert.Equal(($"{ContentHolds.DiscardedPrefix}daoris/s-4", tip), (kept, await RefAsync(root, kept!)));
        Assert.Equal(kept, again);
        var acts = new ActJournal(Home).OfKind(RecoveryKeeps.Kind, "engine", 10);
        Assert.Equal([ActOutcomes.Done, ActOutcomes.Done], acts.Where(each => each.Outcome == ActOutcomes.Done).Select(each => each.Outcome));
        Assert.All(acts, each => Assert.Equal(JournaledDeletes.Press, each.Press));
        Assert.Null(refused);
        Assert.NotNull(said);
        Assert.Null(await RefAsync(root, $"{ContentHolds.DiscardedPrefix}daoris/s-5"));
        Assert.True((await GitRefActs.UndoAsync(new ActRuns(new ActJournal(Home)), "engine", root, acts[0].Id, "window", "window", CancellationToken.None)).Refused);
    }

    [Fact]
    public async Task The_census_counts_the_recovery_refs_no_journal_record_names_and_the_old_reader_dates_those_alone_from_their_reflog()
    {
        var root = await RepositoryAsync("engine");
        var main = await RevAsync(root, "main");
        await GitAsync(root, "update-ref", "--create-reflog", $"{ContentHolds.DiscardedPrefix}daoris/s-old", main, "");
        var (kept, _) = await RecoveryKeeps.KeepAsync(Home, root, "engine", "daoris/s-new", main);

        var census = await RecoveryCensus.CountAsync(Home, root, "engine", CancellationToken.None);
        var (refs, _) = await RecoveryRetention.ReadAsync(root, "engine", NoneMade, CancellationToken.None, RecoveryKeeps.Recorded(Home, "engine"));

        Assert.Null(census.Unread);
        Assert.Equal([$"{ContentHolds.DiscardedPrefix}daoris/s-old"], census.Unrecorded);
        Assert.Contains("1 recovery ref", RecoveryCensus.Words(census));
        Assert.Equal(
            [($"{ContentHolds.DiscardedPrefix}daoris/s-new", true), ($"{ContentHolds.DiscardedPrefix}daoris/s-old", false)],
            refs!.OrderBy(each => each.Ref, StringComparer.Ordinal).Select(each => (each.Ref, each.Recorded)));
        Assert.All(refs!, each => Assert.NotNull(each.Dated));
        Assert.Equal(kept, refs!.Single(each => each.Recorded).Ref);
    }

    /// <summary>The one way: no driver source makes a recovery ref outside an act the journal records.</summary>
    [Fact]
    public void No_driver_source_keeps_a_recovery_ref_but_through_the_journal()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var keeping = Directory.EnumerateFiles(driver, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file) is var text
                && (text.Contains("KeepDiscardedAsync", StringComparison.Ordinal) || text.Contains("\"update-ref\", \"--create-reflog\"", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(keeping);
    }
}
