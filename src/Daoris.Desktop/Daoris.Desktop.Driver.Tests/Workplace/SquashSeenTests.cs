using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SQUASHSEEN1 (D102's SQUASHSEEN1 note): the parent landed two sessions' work on the line as one squashed commit, beside Daoris's own
/// landing. Neither branch's tree was the squash's, and the backlog and the records both changed read otherwise on the line than on
/// either branch, so SQUASHTIDY1's proofs saw neither as landed: each landing replayed its commits, conflicted in the backlog, and told
/// its session to rebase onto the line, which ran again and found everything there. Now the content proof reads a squash that carried a
/// branch with other work (<see cref="SquashCarries"/>), so the landing is refused as carried, no rebase is told, and the tree goes
/// unforced as a landed one does; a commit truly not on the line is still the session's to rebase.
/// </summary>
/// <remarks>Real git in a scratch repository, a stand-in for the say door: nothing reaches a network or a harness.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SquashSeenTests : LandedFixture
{
    private static readonly LandingSubject First = new("s1a2b3c4", "0fda18", "First");

    private static readonly LandingSubject Second = new("s5d6e7f8", "0fda19", "Second");

    private readonly List<(string Session, string Text, string Key)> _said = [];

    private Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct)
    {
        _said.Add((session, text, key));
        return Task.FromResult<SayAnswer?>(new SayAnswer(true, "kept"));
    }

    /// <summary>
    /// The owner's case: both sessions' work landed as one squash, the line moved on after it. Each session's landing is refused as
    /// carried in the head's clause, with no conflict to tell, and writes nothing; its tree is offered its discard.
    /// </summary>
    [Fact]
    public async Task Each_session_a_squash_carried_with_the_others_work_is_judged_landed_and_told_no_rebase()
    {
        var (root, trees, a, b, _) = await SquashedTogetherAsync();

        foreach (var (tree, subject) in new[] { (a, First), (b, Second) })
        {
            var before = await StateAsync(root, tree);

            var landed = await trees.LandAsync(tree.Path, subject);

            Assert.False(landed.Landed, landed.Message);
            Assert.Null(landed.Conflict);
            Assert.Equal(AutoLandingCode.Carried, landed.Refusal);
            Assert.Equal(
                "Its work is on `main` by content (a squash carried it: merging it there adds nothing), so it is not landed again: that "
                + "would make a second copy of it. Discard its tree instead.",
                landed.Message);
            Assert.Equal(before, await StateAsync(root, tree));
            Assert.NotNull(await trees.DiscardOfferAsync(tree.Path));
        }

        Assert.Empty(_said);
        Assert.Empty(trees.Recorded.All());
    }

    /// <summary>Tidied as a landed tree is: an unforced removal goes, saying where its work is, its commits kept at the recovery ref.</summary>
    [Fact]
    public async Task A_carried_sessions_tree_goes_unforced_saying_where_its_work_is()
    {
        var (root, trees, a, _, _) = await SquashedTogetherAsync();
        var tip = (await GitAsync(root, "rev-parse", a.Branch)).Trim();

        var removal = await trees.RemoveAsync(a.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `main` by content (a squash carried it: merging it there adds nothing)", removal.Message);
        Assert.False(Directory.Exists(a.Path));
        Assert.Equal("", (await GitAsync(root, "branch", "--list", a.Branch)).Trim());
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{a.Branch}")).Trim());
    }

    /// <summary>
    /// The control: a session beside them with a commit the line truly lacks, which conflicts with the line, is still told to rebase,
    /// and one the squash carried only part of is not judged landed.
    /// </summary>
    [Fact]
    public async Task A_session_with_a_commit_not_on_the_line_is_still_told_to_rebase()
    {
        var (root, trees, a, _, other) = await SquashedTogetherAsync(beside: true);
        await CommitAsync(other!.Path, "shared.txt", "one\nfrom the tree\n", "work the squash never took");
        await CommitAsync(root, "shared.txt", "one\nfrom the line\n", "the line's own");
        await CommitAsync(a.Path, "more.txt", "more of the work\n", "more work, after the squash");
        var offered = await trees.DiscardOfferAsync(a.Path);

        var refused = await trees.LandAsync(other.Path, new LandingSubject("s9a8b7c6", "0fda1a", "Other"));
        var told = await RebaseTelling.TellTreeAsync(SayAsync, "s9a8b7c6", other.Path, refused.Conflict!);

        Assert.False(refused.Landed, refused.Message);
        Assert.NotEqual(AutoLandingCode.Carried, refused.Refusal);
        Assert.Contains("shared.txt", refused.Conflict!.Words);
        Assert.StartsWith("Its session `s9a8b7c6` was asked to rebase", told);
        Assert.Single(_said);
        Assert.Null(offered);
    }

    /// <summary>
    /// The line as it stands, never a commit it passed (D102, D148): the line changed a session's own file again after the squash, so
    /// it no longer holds that work, and the session is not judged landed.
    /// </summary>
    [Fact]
    public async Task Work_the_line_changed_again_after_the_squash_is_not_judged_landed()
    {
        var (root, trees, a, _, _) = await SquashedTogetherAsync();
        await CommitAsync(root, "a.txt", "the a work, changed again\n", "the line changes it again");

        Assert.Null(await trees.DiscardOfferAsync(a.Path));
        Assert.NotEqual(AutoLandingCode.Carried, (await trees.LandAsync(a.Path, First)).Refusal);
    }

    /// <summary>
    /// Two sessions' trees, each moving its own row out of the backlog (next to the other's), adding a line to a record the repository
    /// merges by union and a file of its own; then the parent's one squash of both on the line, and the line moving on after it.
    /// </summary>
    /// <param name="beside">Whether a third session's tree opens beside them before the squash, its work left to the test.</param>
    private async Task<(string Root, SessionTrees Trees, TreeOpened A, TreeOpened B, TreeOpened? Other)> SquashedTogetherAsync(bool beside = false)
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "notes.md merge=union\n");
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "# Notes\n");
        await CommitAsync(root, "TASKS.md", "# Tasks\n\n- [ ] **A1** the first\n- [ ] **B1** the second\n- [ ] **C1** the third\n", "the backlog");
        var trees = new SessionTrees(Home);
        var a = await trees.OpenAsync(root, "engine", "aurora");
        var b = await trees.OpenAsync(root, "engine", "aurora");
        var other = beside ? await trees.OpenAsync(root, "engine", "aurora") : null;
        await WorkAsync(a.Path, "A1", "a");
        await WorkAsync(b.Path, "B1", "b");

        await File.WriteAllTextAsync(Path.Combine(root, "TASKS.md"), "# Tasks\n\n- [ ] **C1** the third\n");
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "# Notes\nthe a work\nthe b work\n");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "the a work\n");
        await CommitAsync(root, "b.txt", "the b work\n", "Merge A1 and B1 (squashed)");
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        // Neither branch is the squash's tree, and neither one's backlog or record reads as the line's: SQUASHTIDY1's proofs see neither.
        Assert.NotEqual((await GitAsync(root, "rev-parse", "HEAD~1^{tree}")).Trim(), (await GitAsync(root, "rev-parse", $"{a.Branch}^{{tree}}")).Trim());
        return (root, trees, a, b, other);

        static async Task WorkAsync(string tree, string row, string name)
        {
            var backlog = await File.ReadAllTextAsync(Path.Combine(tree, "TASKS.md"));
            await File.WriteAllTextAsync(Path.Combine(tree, "TASKS.md"), string.Join('\n', backlog.Split('\n').Where(line => !line.Contains($"**{row}**"))));
            await File.AppendAllTextAsync(Path.Combine(tree, "notes.md"), $"the {name} work\n");
            await CommitAsync(tree, $"{name}.txt", $"the {name} work\n", $"the {name} work");
        }
    }

    /// <summary>What a refused landing must leave as it was: every branch and its tip, refs under <c>refs/daoris/</c>, and the tree's head and status.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
