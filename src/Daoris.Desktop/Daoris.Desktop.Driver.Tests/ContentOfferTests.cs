using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SQUASHTIDY1b (D102's SQUASHTIDY1b note): what an ended session's tree offers, judged over real git, where a squash merge or
/// a cherry-pick holds its work by content. LAND4 offered *Accept…* by ancestry alone, so a session a squash-merged pull request
/// already carried was offered a landing that would make a branch of work already on the line. Now the reader asks
/// SQUASHTIDY1c's proof, with its guarantees: one judged id, no holder a landing made here, and a tree that holds what no
/// commit does stays. Real git in a scratch repository, so this is the suite's Process half; nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ContentOfferTests : LandedFixture
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The owner's case: the session's work went in as a squash-merged pull request and its tree still stands. No landing, the
    /// clause Discard says, and an unforced discard naming the ref its commits stay at, which is the ref the discard then keeps.
    /// </summary>
    [Fact]
    public async Task A_squash_merged_session_is_offered_its_discard_saying_where_its_work_is_and_never_its_landing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "shared.txt", "one\ntwo\n", "more of the work");
        await SquashAsync(root, tree.Branch);

        var row = await OfferAsync(trees, tree.Path);

        Assert.Null(row.Lands);
        var discards = Assert.IsType<DiscardOffer>(row.Discards);
        Assert.Equal((tree.Branch, Path.GetFileName(tree.Path)), (discards.Branch, discards.Tree));
        Assert.Equal("Its work is on `main` by content (a squash merge).", discards.Says);
        Assert.Equal($"refs/daoris/discarded/{tree.Branch}", discards.Work.Keeps);
        Assert.Equal(ContentHold.KeptAt(tree.Branch, discards.Work.Keeps!), discards.KeptAt);

        // The press the offer names: the review's Discard, unforced. It says the same clause and keeps the ref the offer named.
        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `main` by content (a squash merge)", removal.Message);
        Assert.Contains(discards.KeptAt!, removal.Message);
    }

    /// <summary>A cherry-pick onto a person's branch: the offer names that branch, as Discard does.</summary>
    [Fact]
    public async Task A_cherry_picked_session_is_offered_its_discard_naming_the_branch_that_holds_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/x");
        await GitAsync(root, "cherry-pick", $"main..{tree.Branch}");
        await GitAsync(root, "checkout", "--quiet", "main");

        var row = await OfferAsync(trees, tree.Path, state: "failed");

        Assert.Null(row.Lands);
        Assert.Equal(
            "Its work is on `feature/x` by content (each file it changed reads the same there).", row.Discards!.Says);
    }

    /// <summary>One file it changed reads otherwise everywhere: nothing holds the whole of it, so its landing is still offered.</summary>
    [Fact]
    public async Task A_session_with_one_file_differing_is_still_offered_its_landing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "notes.txt", "the notes\n", "the notes");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/x");
        await CommitAsync(root, "work.txt", "the work\n", "the work, taken");
        await CommitAsync(root, "notes.txt", "other notes\n", "the notes, taken otherwise");
        await GitAsync(root, "checkout", "--quiet", "main");

        var row = await OfferAsync(trees, tree.Path);

        Assert.Equal(new LandOffer(tree.Branch, Path.GetFileName(tree.Path), 2, 0), row.Lands);
        Assert.Null(row.Discards);
    }

    /// <summary>
    /// SQUASHTIDY1c's guarantee: a branch a landing made and recorded here never vouches for a session's work by content, since
    /// the clean-up removes it on its own proof. The redo reads as that branch's file, and is offered its landing as before.
    /// </summary>
    [Fact]
    public async Task A_branch_a_landing_made_here_never_vouches_for_the_offer()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "two\n", new LandingSubject("s1a2b3c4", "0fda18", "Two"));
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", landed.Branch!);
        await CommitAsync(root, "shared.txt", "one\n", "revert it on the line");
        var redo = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(redo.Path, "shared.txt", "two\n", "the redo");

        var row = await OfferAsync(trees, redo.Path);

        Assert.Equal(1, row.Lands!.Commits);
        Assert.Null(row.Discards);
    }

    /// <summary>
    /// SQUASHTIDY1c's guarantee: an ignored file only the session's tree holds keeps the tree, so no unforced discard is offered.
    /// Nor a landing, since its commits are on the line: the line says where its work is and why its tree stays.
    /// </summary>
    [Fact]
    public async Task A_held_tree_with_an_ignored_file_only_it_holds_offers_no_unforced_discard_and_says_why()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "*.db\n", "ignore databases");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "local.db"), "rows only this tree has\n");

        var row = await OfferAsync(trees, tree.Path);

        Assert.Null(row.Lands);
        Assert.Null(row.Discards!.Work.Keeps);
        Assert.Null(row.Discards.KeptAt);
        Assert.StartsWith("Its work is on `main` by content (a squash merge). Its tree stays: ", row.Discards.Says);
        Assert.Contains("local.db", row.Discards.Says);
        // Judging wrote nothing: the branch and its commits are where they were, and no recovery ref was made.
        Assert.Contains(tree.Branch, await GitAsync(root, "branch", "--list", "daoris/*"));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/")).Trim());
    }

    /// <summary>
    /// A recovery ref of the branch's name holding another commit already (an earlier discard of a branch by that name): the
    /// offer names the one the discard would keep, as the discard numbers it.
    /// </summary>
    [Fact]
    public async Task The_offer_names_the_recovery_ref_the_discard_would_take_where_the_first_is_taken()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        var earlier = (await GitAsync(root, "rev-parse", "main~1")).Trim();
        await GitAsync(root, "update-ref", $"refs/daoris/discarded/{tree.Branch}", earlier);

        var row = await OfferAsync(trees, tree.Path);

        Assert.Equal($"refs/daoris/discarded/{tree.Branch}-2", row.Discards!.Work.Keeps);
        var removal = await trees.RemoveAsync(tree.Path);
        Assert.True(removal.Removed, removal.Message);
        Assert.Contains(row.Discards.KeptAt!, removal.Message);
    }

    /// <summary>The reader over one ended session of this machine's on <paramref name="tree"/>, its trees judged by real git.</summary>
    private static async Task<SessionGrouping> OfferAsync(SessionTrees trees, string tree, string state = "completed")
    {
        var records = new JsonArray(new JsonObject
        {
            ["id"] = "s1",
            ["repository"] = "engine",
            ["adapter"] = "claude-code",
            ["state"] = state,
            ["kind"] = "driven",
            ["quest"] = "q1",
            ["tree"] = tree,
            ["created"] = T0.ToString("O"),
            ["updated"] = T0.AddMinutes(1).ToString("O"),
        }).ToJsonString();
        var look = SessionLook.From(records, [new QuestView("q1", "game", "engine", "The work of #q1", "A body.", "Done")], [], _ => 0);

        var judged = await SessionGroups.JudgeAsync(look, trees.Holds, trees.WorkAsync);

        return Assert.Single(SessionGroups.Read(judged));
    }
}
