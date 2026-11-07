using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1r's list (D204, D228): a repository's deletes are read from the journal alone. A judged delete names the recovery ref its door kept at
/// the commit, the row carries its Undo until the act is undone or the name stands again, and a person's delete is a row the same way.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DeletedBranchesTests : TreeSyncFixture
{
    private static Task<string?> Clear(string? made) => Task.FromResult<string?>(null);

    [Fact]
    public async Task A_judged_delete_is_a_row_naming_the_recovery_ref_its_door_kept_and_its_Undo_closes_the_row()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-1");
        var tip = await RevAsync(root, "daoris/s-1");
        var main = await RevAsync(root, "main");
        var (keep, _) = await RecoveryKeeps.NameAsync(root, "daoris/s-1", tip, CancellationToken.None);

        var gone = await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-1", tip, [("refs/heads/main", main)], Clear, keep);

        var read = await DeletedBranches.ReadAsync(Home, root, "engine", CancellationToken.None);
        var row = Assert.Single(read.Value!.Rows);
        Assert.Equal((gone.Act, "branch", "daoris/s-1", tip, JournaledDeletes.Press, "refs/daoris/discarded/daoris/s-1", null),
            (row.Act, row.Kind, row.Name, row.Commit, row.Press, row.Recovery, row.Closed));

        var undone = await GitRefActs.UndoAsync(new ActRuns(new ActJournal(Home)), "engine", root, row.Act, "window", "window", CancellationToken.None);
        Assert.False(undone.Refused, undone.Said);

        Assert.Equal(DeletedBranches.Undone, Assert.Single((await DeletedBranches.ReadAsync(Home, root, "engine", CancellationToken.None)).Value!.Rows).Closed);
    }

    [Fact]
    public async Task A_name_that_stands_again_closes_the_Undo_and_a_repository_with_no_delete_lists_none()
    {
        var root = await RepositoryAsync("engine");
        Assert.Empty((await DeletedBranches.ReadAsync(Home, root, "engine", CancellationToken.None)).Value!.Rows);

        await GitAsync(root, "branch", "daoris/s-2");
        var tip = await RevAsync(root, "daoris/s-2");
        await JournaledDeletes.DeleteAsync(Home, root, "engine", "daoris/s-2", tip, [("refs/heads/main", await RevAsync(root, "main"))], Clear);
        await GitAsync(root, "branch", "daoris/s-2");

        var row = Assert.Single((await DeletedBranches.ReadAsync(Home, root, "engine", CancellationToken.None)).Value!.Rows);
        Assert.Equal((DeletedBranches.NameTaken, (string?)null), (row.Closed, row.Recovery));
    }
}
