using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RECOVERYDROP1 over real git: the person's press deletes a recovery ref nothing of theirs holds, in one transaction that verifies the
/// commit they were shown, and skips (saying why) a ref a branch or a tag now holds, one that moved, one gone, and one that is no
/// recovery ref at all.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RecoveryDropGitTests : LandedFixture
{
    private static readonly IReadOnlySet<string> NoneMade = new HashSet<string>();

    private const string Prefix = "refs/daoris/discarded/";

    /// <summary>A commit held by no ref, a child of <paramref name="parent"/>, made distinct by its message.</summary>
    private static async Task<string> CommitAsync(string root, string parent, string message)
    {
        var tree = (await GitAsync(root, "rev-parse", $"{parent}^{{tree}}")).Trim();
        var body = $"tree {tree}\nparent {parent}\nauthor Tester <tester@example.invalid> 1600000000 +0000\n"
            + $"committer Tester <tester@example.invalid> 1600000000 +0000\n\n{message}\n";
        var (code, made, err) = await WorkingTree.GitAsync(root, ["hash-object", "-t", "commit", "-w", "--stdin"], body, CancellationToken.None);
        Assert.True(code == 0, err);
        return made.Trim();
    }

    private static async Task<string> TipAsync(string root) => (await GitAsync(root, "rev-parse", "main")).Trim();

    [Fact]
    public async Task A_ref_nothing_holds_is_deleted_at_the_commit_the_person_was_shown()
    {
        var root = await RepositoryAsync("engine");
        var alone = await CommitAsync(root, await TipAsync(root), "work nothing holds");
        await GitAsync(root, "update-ref", $"{Prefix}daoris/s-alone", alone);

        var drops = await RecoveryDropping.DropAsync(
            root, "engine", NoneMade, [new RecoveryDrop($"{Prefix}daoris/s-alone", alone)], CancellationToken.None);

        Assert.Equal([new RecoveryDropped($"{Prefix}daoris/s-alone", alone)], drops.Dropped);
        Assert.Empty(drops.Skipped);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", Prefix)).Trim());
    }

    [Fact]
    public async Task A_ref_a_branch_now_holds_is_skipped_and_stays()
    {
        var root = await RepositoryAsync("engine");
        var work = await CommitAsync(root, await TipAsync(root), "work a branch picked up");
        await GitAsync(root, "update-ref", $"{Prefix}daoris/s-picked", work);
        // The person shown it as held by nothing, and a branch of theirs has since come to hold its commit.
        await GitAsync(root, "update-ref", "refs/heads/feature/picked-up", work);

        var drops = await RecoveryDropping.DropAsync(
            root, "engine", NoneMade, [new RecoveryDrop($"{Prefix}daoris/s-picked", work)], CancellationToken.None);

        Assert.Empty(drops.Dropped);
        var skipped = Assert.Single(drops.Skipped);
        Assert.Equal($"{Prefix}daoris/s-picked", skipped.Ref);
        Assert.Contains("holds", skipped.Why);
        Assert.Equal(work, (await GitAsync(root, "rev-parse", $"{Prefix}daoris/s-picked")).Trim());
    }

    [Fact]
    public async Task A_ref_that_moved_since_it_was_shown_is_skipped_and_keeps_its_new_commit()
    {
        var root = await RepositoryAsync("engine");
        var tip = await TipAsync(root);
        var shown = await CommitAsync(root, tip, "work as shown");
        var moved = await CommitAsync(root, tip, "work as it is now");
        await GitAsync(root, "update-ref", $"{Prefix}daoris/s-moved", moved);

        var drops = await RecoveryDropping.DropAsync(
            root, "engine", NoneMade, [new RecoveryDrop($"{Prefix}daoris/s-moved", shown)], CancellationToken.None);

        Assert.Empty(drops.Dropped);
        Assert.Contains("moved", Assert.Single(drops.Skipped).Why);
        Assert.Equal(moved, (await GitAsync(root, "rev-parse", $"{Prefix}daoris/s-moved")).Trim());
    }

    [Fact]
    public async Task A_ref_gone_already_and_a_ref_that_is_no_recovery_ref_are_skipped_and_said()
    {
        var root = await RepositoryAsync("engine");
        var tip = await TipAsync(root);

        var drops = await RecoveryDropping.DropAsync(
            root, "engine", NoneMade, [new RecoveryDrop($"{Prefix}daoris/s-gone", tip), new RecoveryDrop("refs/heads/main", tip)], CancellationToken.None);

        Assert.Empty(drops.Dropped);
        Assert.Equal(2, drops.Skipped.Count);
        Assert.Contains("no longer", drops.Skipped[0].Why);
        Assert.Contains("recovery", drops.Skipped[1].Why);
        Assert.Equal(tip, await TipAsync(root));
    }

    [Fact]
    public async Task Unheld_takes_the_refs_nothing_holds_and_leaves_one_a_branch_holds()
    {
        var root = await RepositoryAsync("engine");
        var tip = await TipAsync(root);
        var alone = await CommitAsync(root, tip, "work nothing holds");
        var held = await CommitAsync(root, tip, "work a branch holds");
        await GitAsync(root, "update-ref", $"{Prefix}daoris/s-alone", alone);
        await GitAsync(root, "update-ref", $"{Prefix}daoris/s-held", held);
        await GitAsync(root, "update-ref", "refs/heads/feature/keeps-it", held);

        var drops = await RecoveryDropping.DropUnheldAsync(root, "engine", NoneMade, CancellationToken.None);

        Assert.Equal([new RecoveryDropped($"{Prefix}daoris/s-alone", alone)], drops.Dropped);
        Assert.Empty(drops.Skipped);
        Assert.Equal(held, (await GitAsync(root, "rev-parse", $"{Prefix}daoris/s-held")).Trim());
    }

    [Fact]
    public async Task A_ref_named_without_its_commit_is_deleted_at_the_commit_read_now()
    {
        var root = await RepositoryAsync("engine");
        var alone = await CommitAsync(root, await TipAsync(root), "work nothing holds");
        await GitAsync(root, "update-ref", $"{Prefix}daoris/s-typed", alone);

        var drops = await RecoveryDropping.DropAsync(root, "engine", NoneMade, [new RecoveryDrop($"{Prefix}daoris/s-typed", null)], CancellationToken.None);

        Assert.Equal([new RecoveryDropped($"{Prefix}daoris/s-typed", alone)], drops.Dropped);
    }

    [Fact]
    public void The_words_say_what_went_and_what_was_kept_and_why()
    {
        var words = RecoveryDropping.Words("engine", new RecoveryDrops(
            [new RecoveryDropped($"{Prefix}daoris/s-a", new string('a', 40))],
            [new RecoverySkipped($"{Prefix}daoris/s-b", "it moved")])).ToList();

        Assert.Contains(words, line => line.Contains("deleted") && line.Contains($"{Prefix}daoris/s-a") && line.Contains("engine"));
        Assert.Contains(words, line => line.Contains("kept") && line.Contains($"{Prefix}daoris/s-b") && line.Contains("it moved"));
    }
}
