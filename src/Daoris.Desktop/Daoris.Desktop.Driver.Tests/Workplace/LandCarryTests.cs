using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACK1 part 1 (D87's LANDBACK1 note): an Accept on a session whose tree holds uncommitted work, or whose landing conflicts, is not
/// refused and left: the session is carried on, told in Daoris's own words through the say door (keyed by its tree's state, so an
/// unchanged tree is told once), and the person's yes is kept on it. Daoris never commits for it, and a claim is not an act: the landing
/// lands only once the tree is clean.
/// </summary>
/// <remarks>Real git in a scratch repository, a stand-in for the service's say door: nothing reaches a network or a harness.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandCarryTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private static readonly DateTimeOffset Pressed = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    private readonly List<(string Session, string Text, string Key)> _said = [];

    /// <summary>The door keeps one word per key, as the service's does: a key it holds is answered kept, and nothing more is said.</summary>
    private Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct)
    {
        if (_said.All(each => each.Key != key)) _said.Add((session, text, key));
        return Task.FromResult<SayAnswer?>(new SayAnswer(true, "kept"));
    }

    [Fact]
    public async Task An_accept_on_a_dirty_tree_carries_its_session_on_once_and_lands_when_its_tree_is_clean()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "left.txt"), "not committed\n");

        var refused = await trees.LandAsync(tree.Path, Subject);
        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(AutoLandingCode.Uncommitted, refused.Refusal);

        var carried = await LandCarry.CarryAsync(SayAsync, Home, Subject.Session, tree.Path, refused, record: null, Pressed);

        Assert.True(carried!.Told, carried.Sentence);
        Assert.Equal(LandCarry.Uncommitted, carried.Why);
        var said = Assert.Single(_said);
        var status = AutoLandingRules.Fingerprint(await GitAsync(tree.Path, "status", "--porcelain"));
        Assert.Equal(DaorisKeys.Carry(Subject.Session, status, 1), said.Key);
        Assert.Equal(Subject.Session, said.Session);
        Assert.Contains("uncommitted", said.Text);
        Assert.Contains("restore the ones you do not want and say which", said.Text);
        Assert.Equal(new CarriedAccept(Subject.Session, tree.Path, Pressed, Carries: 1, Awaiting: true), new CarriedAccepts(Home).Of(Subject.Session));
        Assert.Contains("lands when its tree is clean", carried.Sentence);

        // An identical second press at the unchanged tree sends nothing again.
        var again = await LandCarry.CarryAsync(SayAsync, Home, Subject.Session, tree.Path, refused, record: null, Pressed.AddMinutes(1));
        Assert.True(again!.Told);
        Assert.Single(_said);

        // The session goes on with the words in its own tree and commits; nothing was committed for it, and the landing lands.
        Assert.Equal("", (await GitAsync(tree.Path, "log", "--format=%an", "-1", "--", "left.txt")).Trim());
        await CommitAsync(tree.Path, "left.txt", "not committed\n", "the rest of the work");
        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Single(_said);
    }

    /// <summary>
    /// LANDBACK1 part 2: a done quest's carry-on in the run is no Accept. It keeps no <see cref="CarriedAccepts"/> entry, which is the
    /// person's yes and would make the later landing read accepted by them, and says nothing through the say door; its words are its
    /// own, and part 1's still open with the Accept's.
    /// </summary>
    [Fact]
    public async Task A_done_quests_carry_on_in_the_run_keeps_no_accept_and_says_nothing_through_the_door()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "left.txt"), "not committed\n");

        var look = await TurnCarries.LookAsync(closed: true, () => false, tree.Path, CancellationToken.None);

        Assert.True(look.Dirty);
        Assert.Equal(1, look.Paths);
        Assert.Empty(new CarriedAccepts(Home).All());
        Assert.False(File.Exists(new CarriedAccepts(Home).FilePath));
        Assert.Empty(_said);
        Assert.DoesNotContain("accepted", look.Words, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Your work is accepted", LandCarry.DirtyWords(1), StringComparison.Ordinal);
        Assert.Contains(LandCarry.CommitWords, LandCarry.DirtyWords(1), StringComparison.Ordinal);
        Assert.Contains(LandCarry.CommitWords, look.Words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dirty_tree_under_a_merge_rule_is_carried_on_as_a_branch_rule_is()
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge)).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "left.txt"), "not committed\n");

        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(AutoLandingCode.Uncommitted, refused.Refusal);
        var carried = await LandCarry.CarryAsync(SayAsync, Home, Subject.Session, tree.Path, refused, record: null, Pressed);
        Assert.True(carried!.Told, carried.Sentence);
        Assert.Single(_said);
    }

    [Fact]
    public async Task A_conflict_is_carried_on_with_the_rebase_words_keyed_by_its_tip()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "one\nfrom the tree\n", "the work");
        await CommitAsync(root, "shared.txt", "one\nfrom the line\n", "the line's own");
        var tip = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();

        var refused = await trees.LandAsync(tree.Path, Subject);
        var carried = await LandCarry.CarryAsync(SayAsync, Home, Subject.Session, tree.Path, refused, record: null, Pressed);

        Assert.True(carried!.Told, carried.Sentence);
        Assert.Equal(LandCarry.Conflicted, carried.Why);
        var said = Assert.Single(_said);
        Assert.Equal(DaorisKeys.Carry(Subject.Session, $"conflict-{tip}", 1), said.Key);
        Assert.Equal(refused.Conflict!.Words, said.Text);
        Assert.Equal(1, new CarriedAccepts(Home).Of(Subject.Session)!.Carries);
    }

    [Fact]
    public async Task A_session_the_person_stopped_or_declined_is_carried_on_to_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "left.txt"), "not committed\n");
        var refused = await trees.LandAsync(tree.Path, Subject);

        foreach (var state in new[] { "stopped", "declined" })
        {
            var carried = await LandCarry.CarryAsync(
                SayAsync, Home, Subject.Session, tree.Path, refused, new SessionRecord(Subject.Session, "engine", state), Pressed);
            Assert.Null(carried);
        }

        Assert.Empty(_said);
        Assert.Null(new CarriedAccepts(Home).Of(Subject.Session));
    }

    [Fact]
    public async Task Words_the_door_refuses_keep_no_yes_and_are_given_to_the_person_with_why()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "left.txt"), "not committed\n");
        var refused = await trees.LandAsync(tree.Path, Subject);

        var refusing = await LandCarry.CarryAsync(
            (_, _, _, _) => Task.FromResult<SayAnswer?>(new SayAnswer(false, "the session is running.")), Home, Subject.Session, tree.Path, refused, null, Pressed);
        var thrown = await LandCarry.CarryAsync(
            (_, _, _, _) => throw new HttpRequestException("no service answered"), Home, Subject.Session, tree.Path, refused, null, Pressed);

        Assert.False(refusing!.Told);
        Assert.StartsWith($"Its session `{Subject.Session}` could not be carried on (the session is running), so nothing was kept of your Accept. Say this to it: ", refusing.Sentence);
        Assert.False(thrown!.Told);
        Assert.Contains("no service answered", thrown.Sentence);
        Assert.Null(new CarriedAccepts(Home).Of(Subject.Session));
    }

    [Fact]
    public void A_new_press_starts_the_count_again_and_a_take_back_removes_the_yes()
    {
        var accepts = new CarriedAccepts(Home);
        accepts.Keep("s1", "X:/daoris/trees/a", Pressed);
        accepts.CarriedOn("s1");
        accepts.TurnEnded("s1");
        accepts.CarriedOn("s1");
        Assert.Equal(new CarriedAccept("s1", "X:/daoris/trees/a", Pressed, 2, true), accepts.Of("s1"));

        accepts.Keep("s1", "X:/daoris/trees/a", Pressed.AddMinutes(5));
        Assert.Equal(new CarriedAccept("s1", "X:/daoris/trees/a", Pressed.AddMinutes(5), 0, false), accepts.Of("s1"));

        accepts.Take("s1");
        Assert.Null(accepts.Of("s1"));

        // A deleted session's record takes its yes with it (SessionHomeFiles): a tree that is gone is never landed.
        accepts.Keep("s1", "X:/daoris/trees/a", Pressed);
        accepts.Keep("s2", "X:/daoris/trees/b", Pressed);
        Assert.Equal(["s1"], accepts.Forget(["s1", "s3"]));
        Assert.Null(accepts.Of("s1"));
        Assert.NotNull(accepts.Of("s2"));
    }
}
