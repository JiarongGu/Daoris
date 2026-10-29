using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACK1 part 1 (D87's LANDBACK1 note), the look's half: an Accept the person pressed on a session whose tree was not clean is kept on
/// it, and the landing runs once the carry-on's turn ends with the tree clean, by the repository's rule whatever its switch says. A turn
/// that ends still dirty or still conflicted carries it on once more, and at the second such end a note says so and the yes is taken back.
/// </summary>
/// <remarks>Real git, a stand-in for the service's quests, records and say door: nothing reaches a network or a harness.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingCarryTests : AutoLandingFixture
{
    private static readonly DateTimeOffset Pressed = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    /// <summary>A branch rule that does not accept automatically: only the person's yes lands it.</summary>
    private static readonly LandingRule ByPress = new(LandingForm.Branch, "feature/{quest}-{slug}");

    /// <summary>The person's Accept on a done session with a dirty tree: carried on once, its turn running (the press's own carry-on).</summary>
    private async Task<string> AcceptedDirtyAsync(string root, LandingRule rule)
    {
        Rule(rule);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await File.WriteAllTextAsync(Path.Combine(tree, "left.txt"), "not committed\n");
        var accepts = new CarriedAccepts(_home);
        accepts.Keep("s1", tree, Pressed);
        accepts.CarriedOn("s1");
        return tree;
    }

    private static async Task<string> StatusAsync(string tree) => AutoLandingRules.Fingerprint(await GitAsync(tree, "status", "--porcelain"));

    [Fact]
    public async Task A_kept_yes_lands_the_work_once_the_carry_ons_turn_ends_with_a_clean_tree_whatever_the_rules_switch_says()
    {
        var root = await RepositoryAsync("engine");
        var tree = await AcceptedDirtyAsync(root, ByPress);

        // While the carry-on's turn runs nothing is judged: no landing beside it.
        await LookAsync();
        Assert.False(await BranchAsync(root, PushBranch));
        Assert.Empty(_world.Said);

        // The session committed what it kept, and its turn ended: the look lands it, by the person's yes, and takes the yes back.
        await CommitAsync(tree, "left.txt", "not committed\n", "the rest of the work");
        ConcludeAgain("s1", "q1", tree);
        var lines = await LookAsync();

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", lines));
        Assert.Null(new CarriedAccepts(_home).Of("s1"));
        var note = Notes("s1").Single(each => each.Parts?[0].Code == NoteCodes.LandingAcceptedCarried.Code);
        Assert.StartsWith("accepted by your press once its tree was clean: its work is on `feature/q1-fix-the-gap`", note.Text);
        Assert.DoesNotContain(Notes("s1"), each => each.Text?.Contains("automatically", StringComparison.Ordinal) == true);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Tries[^1].Code);
        Assert.Empty(_world.Said);

        // The landing ran once: nothing more is tried at the next look.
        await LookAsync();
        Assert.Single(new AutoLandings(_home).Of("s1")!.Tries, each => each.Code == AutoLandingCode.Landed);
    }

    [Fact]
    public async Task A_kept_yes_lands_by_a_merge_rule_too_as_the_persons_press_does()
    {
        var root = await RepositoryAsync("engine");
        var tree = await AcceptedDirtyAsync(root, new LandingRule(LandingForm.Merge));
        await CommitAsync(tree, "left.txt", "not committed\n", "the rest of the work");
        ConcludeAgain("s1", "q1", tree);

        var lines = await LookAsync();

        Assert.True(File.Exists(Path.Combine(root, "s1.txt")), string.Join("\n", lines));
        Assert.True(File.Exists(Path.Combine(root, "left.txt")));
        Assert.Null(new CarriedAccepts(_home).Of("s1"));
    }

    [Fact]
    public async Task A_turn_that_ends_still_dirty_carries_it_on_once_more_and_the_second_such_end_says_so()
    {
        var root = await RepositoryAsync("engine");
        var tree = await AcceptedDirtyAsync(root, ByPress);
        var status = await StatusAsync(tree);

        // The first carry-on's turn ended with the tree as it was: carried on again, in a word of its own for the same tree.
        ConcludeAgain("s1", "q1", tree);
        var again = await LookAsync();

        var said = Assert.Single(_world.Said);
        Assert.Equal(("s1", DaorisKeys.Carry("s1", status, 2)), (said.Session, said.Key));
        Assert.Contains("1 uncommitted path(s)", said.Text);
        Assert.Contains(Notes("s1"), each => each.Parts?[0].Code == NoteCodes.LandingCarriedDirty.Code);
        Assert.Equal(new CarriedAccept("s1", tree, Pressed, 2, true), new CarriedAccepts(_home).Of("s1"));
        Assert.Equal(AutoLandingCode.CarriedOn, new AutoLandings(_home).Of("s1")!.Tries[^1].Code);
        Assert.Null(new AutoLandings(_home).Of("s1")!.Closed);
        Assert.False(await BranchAsync(root, PushBranch), string.Join("\n", again));

        // Nothing is judged while that turn runs.
        await LookAsync();
        Assert.Single(_world.Said);

        // And it ended still dirty: two carry-ons are all an Accept has. A note says so, the yes is taken back, nothing landed, and the
        // entry is closed with the reason: the session waits for the person's review, never read as landed.
        ConcludeAgain("s1", "q1", tree);
        await LookAsync();

        Assert.Single(_world.Said);
        Assert.Contains(Notes("s1"), each => each.Parts?[0].Code == NoteCodes.LandingCarryBound.Code);
        Assert.Null(new CarriedAccepts(_home).Of("s1"));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.CarryBound, entry.Tries[^1].Code);
        Assert.NotNull(entry.Closed);
        Assert.False(await BranchAsync(root, PushBranch));
        Assert.DoesNotContain(Notes("s1"), each => each.Parts?[0].Code == NoteCodes.LandingAcceptedCarried.Code);
    }

    [Fact]
    public async Task A_turn_that_ends_still_conflicted_is_carried_on_with_the_rebase_words_for_its_tip()
    {
        var root = await RepositoryAsync("engine");
        Rule(ByPress);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await CommitAsync(tree, "README.md", "# engine\nfrom the session", "the session's readme");
        await CommitAsync(root, "README.md", "# engine\nfrom the line", "the line's readme");
        var tip = await HeadAsync(tree);
        var accepts = new CarriedAccepts(_home);
        accepts.Keep("s1", tree, Pressed);
        accepts.CarriedOn("s1");

        ConcludeAgain("s1", "q1", tree);
        await LookAsync();

        var said = Assert.Single(_world.Said);
        Assert.Equal(("s1", DaorisKeys.Carry("s1", $"conflict-{tip}", 2)), (said.Session, said.Key));
        Assert.StartsWith("Rebase your work onto `main`", said.Text);
        Assert.Contains(Notes("s1"), each => each.Parts?[0].Code == NoteCodes.LandingCarriedConflict.Code);
        Assert.Equal(2, new CarriedAccepts(_home).Of("s1")!.Carries);
        Assert.False(await BranchAsync(root, PushBranch));
    }

    [Fact]
    public async Task A_stop_or_a_decline_takes_the_yes_back_and_a_carry_on_is_never_sent_to_a_session_the_person_ended()
    {
        var root = await RepositoryAsync("engine");
        var tree = await AcceptedDirtyAsync(root, ByPress);
        var quest = _world.Quests["q1"];

        // Its end is not a done: the conclusion takes the yes back.
        Assert.Equal(AutoConcluded.NotDone, AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), "s1",
            quest with { Status = "Open" }, "Open", "stopped", tree, "aurora"));
        Assert.Null(new CarriedAccepts(_home).Of("s1"));

        // A yes still kept where the record reads as stopped when the look judges it is taken back and tells the session nothing.
        new CarriedAccepts(_home).Keep("s1", tree, Pressed);
        _world.Records.Clear();
        _world.Records.Add(new SessionRecord("s1", "engine", "stopped") { Quest = "q1", Tree = tree, Created = DateTimeOffset.UtcNow });
        new AutoLandings(_home).Due(new AutoLanding("s1", "q1", "engine", "aurora", tree, Pressed));
        await LookAsync();

        Assert.Null(new CarriedAccepts(_home).Of("s1"));
        Assert.Empty(_world.Said);
        Assert.False(await BranchAsync(root, PushBranch));
    }

    [Fact]
    public async Task Where_nobody_pressed_a_dirty_tree_waits_as_it_always_did()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await File.WriteAllTextAsync(Path.Combine(tree, "left.txt"), "not committed\n");

        await LookAsync();

        Assert.Empty(_world.Said);
        Assert.Equal(AutoLandingCode.Uncommitted, new AutoLandings(_home).Of("s1")!.Tries[^1].Code);
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();
}
