using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDRETIRE1 part 2 without git: a landing's branch the line has not held is named to the person with its age and its quest's
/// closing (<see cref="LandedWords"/>), and once its quest is closed and a day has passed the inventory blocks it on the person
/// (<see cref="InventoryCode.LandingStale"/>), which *What needs you* lists. Which quest is closed is the service's: the seam reads
/// it once for the entries that ask (<see cref="LandedStanding.ClosedAsync"/>).
/// </summary>
public sealed class LandedStandingTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private static LandedItem Differs(bool closed = true, string? quest = "q2", string kind = LandedKind.Differs) =>
        new("engine", "aurora", "feature/q2-fix", kind, null, ["a.txt", "b.txt"], null, null, 2)
        {
            LandedAt = At, Quest = quest, QuestClosed = closed,
        };

    private static LandedBranch Landing(
        string? quest = "q2", HeldFact? held = null, DateTimeOffset? at = null, bool pushed = false, string? pullRequest = null,
        DateTimeOffset? gone = null) =>
        new("engine", "aurora", "feature/q2-fix", "main", "t1", "s1", quest, null, at ?? At)
        {
            Units = ["unit-a"], Held = held, Pushed = pushed, PullRequest = pullRequest, GoneAt = gone,
        };

    private static QuestView Quest(string id, string status, bool held = false) =>
        new(id, "a", "engine", "Title", "Body", status) { Held = held };

    private static InventoryFacts Facts(LandedBranch landing, DateTimeOffset now, params string[] closed) =>
        new([], [landing], [], [], [], new HashSet<string>(), _ => false, now) { ClosedQuests = closed.ToHashSet(StringComparer.OrdinalIgnoreCase) };

    // ---- the words: the age, the closed quest, the door

    [Theory]
    [InlineData(30, "less than an hour")]
    [InlineData(60, "1 hour")]
    [InlineData(29 * 60, "29 hours")]
    [InlineData(47 * 60 + 59, "47 hours")]
    [InlineData(48 * 60, "2 days")]
    [InlineData(9 * 24 * 60, "9 days")]
    public void An_age_is_said_in_hours_up_to_two_days_and_in_days_after(int minutes, string said)
    {
        Assert.Equal(said, LandedStanding.Age(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void A_kept_row_names_its_age_and_its_closed_quest()
    {
        var said = LandedWords.Standing(Differs(), At.AddHours(29));

        Assert.Equal("; landed 29 hours ago, and its quest #q2 is closed", said);
    }

    [Fact]
    public void A_row_whose_quest_is_open_or_unknown_names_only_its_age()
    {
        Assert.Equal("; landed 29 hours ago", LandedWords.Standing(Differs(closed: false), At.AddHours(29)));
        Assert.Equal("; landed 29 hours ago", LandedWords.Standing(Differs(closed: false, quest: null), At.AddHours(29)));
    }

    [Fact]
    public void Only_a_row_the_line_does_not_hold_says_a_standing()
    {
        var item = new LandedItem("engine", "aurora", "feature/q2-fix", LandedKind.OnLine, "main", [], null, null, 2) { LandedAt = At, Quest = "q2", QuestClosed = true };

        Assert.Equal("", LandedWords.Standing(item, At.AddHours(29)));
        Assert.Equal("", LandedWords.Standing(Differs() with { LandedAt = null }, At.AddHours(29)));
    }

    [Fact]
    public void A_differing_row_offers_the_door_that_withdraws_it_and_others_offer_none()
    {
        var door = LandedWords.Door(Differs());

        Assert.NotNull(door);
        Assert.Contains("daoris-driver trees remove feature/q2-fix --repository engine --why", door);
        Assert.Contains("--plan", door);
        Assert.Null(LandedWords.Door(Differs(kind: LandedKind.CheckedOut)));
        Assert.Null(LandedWords.Door(Differs(kind: LandedKind.OnLine)));
    }

    [Fact]
    public void The_cleans_list_says_the_standing_and_the_door_under_a_kept_row()
    {
        var plan = new SweepPlan([], [Differs()]);

        var said = TreesPlanWords.CleanPlan(plan, [], new TreesAsk(TreesLook.Clean), At.AddHours(29)).Out;

        Assert.Contains(said, line => line.Contains("2 files still differ") && line.Contains("landed 29 hours ago, and its quest #q2 is closed"));
        Assert.Contains(said, line => line.Contains("trees remove feature/q2-fix --repository engine --why"));
    }

    // ---- the review of a session whose landing was withdrawn

    [Fact]
    public void The_review_of_a_withdrawn_landing_says_when_why_and_where_its_commits_stay()
    {
        var withdrawn = Landing() with
        {
            GoneAt = At.AddDays(2), GoneAs = LandedGone.Withdrawn, GoneWhy = "the row landed another way", KeptOn = "refs/daoris/discarded/feature/q2-fix",
        };

        var said = LandedReviewWords.Describe("s1", new LandedReview(withdrawn, LandedState.Gone));

        Assert.Contains("that branch is gone now.", said);
        Assert.Contains(" It was withdrawn on 2026-10-31 09:00 UTC because the row landed another way; its commits stay on `refs/daoris/discarded/feature/q2-fix`.", said);
        // A branch removed by the clean-up, or one that went outside Daoris, says nothing of a withdrawal.
        Assert.DoesNotContain("withdrawn", LandedReviewWords.Describe("s1", new LandedReview(Landing() with { GoneAt = At, RemovedAs = LandedKind.OnLine, RemovedOn = "main" }, LandedState.Gone)));
        Assert.DoesNotContain("withdrawn", LandedReviewWords.Describe("s1", new LandedReview(Landing() with { GoneAt = At, GoneAs = LandedGone.Missing }, LandedState.Gone)));
    }

    // ---- which quest is closed: the service's

    [Fact]
    public void A_quest_is_closed_when_its_done_is_in_and_nothing_holds_it_or_it_was_declined()
    {
        Assert.True(LandedStanding.IsClosed(Quest("q1", "Done")));
        Assert.True(LandedStanding.IsClosed(Quest("q1", "Declined")));
        Assert.False(LandedStanding.IsClosed(Quest("q1", "Done", held: true)));
        Assert.False(LandedStanding.IsClosed(Quest("q1", "Open")));
        Assert.False(LandedStanding.IsClosed(Quest("q1", "Taken")));
    }

    [Fact]
    public async Task The_service_is_read_once_for_the_quests_the_landings_name_and_only_where_one_asks()
    {
        var reads = 0;
        Task<IReadOnlyList<QuestView>> Read(CancellationToken _)
        {
            reads++;
            return Task.FromResult<IReadOnlyList<QuestView>>([Quest("q2", "Done"), Quest("q3", "Open")]);
        }

        // The line holds it, it is gone, or it names no quest: nothing asks, so the service is never read.
        var held = new HeldFact(At, At, EndBy.Merged, "t1", "main", "main", "m1", "repo-id");
        Assert.Empty(await LandedStanding.ClosedAsync([Landing(held: held), Landing(gone: At), Landing(quest: null)], Read, default));
        Assert.Equal(0, reads);

        var closed = await LandedStanding.ClosedAsync([Landing(), Landing(quest: "q3")], Read, default);
        Assert.Equal(["q2"], closed.ToArray());
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task A_service_that_does_not_answer_closes_nothing()
    {
        static Task<IReadOnlyList<QuestView>> Read(CancellationToken _) => throw new DriverException("the service did not answer");

        Assert.Empty(await LandedStanding.ClosedAsync([Landing()], Read, default));
    }

    // ---- the inventory: after a day, with its quest closed, the line not holding it

    [Fact]
    public void A_landing_the_line_has_not_held_for_a_day_with_its_quest_closed_blocks_on_the_person()
    {
        var unit = Assert.Single(WorkInventory.Derive(Facts(Landing(), At.AddHours(25), "q2")));

        var wait = Assert.Single(unit.Cleanup.Waits, entry => entry.Kind == InventoryKind.LandingBranch);
        Assert.Equal((InventoryDisposition.Blocked, InventoryCode.LandingStale), (wait.Disposition, wait.Code));
        Assert.Equal("feature/q2-fix", wait.Facts!["branch"]);
        Assert.Equal("q2", wait.Facts["quest"]);
        Assert.Equal(At.AddDays(1).ToString("O"), wait.Facts["since"]);
        Assert.Contains("withdraw", wait.Detail);
    }

    [Fact]
    public void Before_the_day_is_out_or_with_its_quest_still_open_the_landing_waits_as_it_did()
    {
        var early = Assert.Single(WorkInventory.Derive(Facts(Landing(), At.AddHours(23), "q2")));
        var open = Assert.Single(WorkInventory.Derive(Facts(Landing(), At.AddDays(3))));

        foreach (var unit in new[] { early, open })
        {
            var kept = Assert.Single(unit.Entries, entry => entry.Kind == InventoryKind.LandingBranch);
            Assert.Equal((InventoryDisposition.Kept, null), (kept.Disposition, kept.Code));
        }
    }

    [Fact]
    public void A_landing_the_line_holds_or_a_pull_request_may_carry_is_never_named_stale()
    {
        var held = new HeldFact(At, At, EndBy.Merged, "t1", "main", "main", "m1", "repo-id");
        var late = At.AddDays(5);

        var line = Assert.Single(WorkInventory.Derive(Facts(Landing(held: held), late, "q2")));
        var pull = Assert.Single(WorkInventory.Derive(Facts(Landing(pullRequest: "https://example.test/pr/7"), late, "q2")));
        var pushed = Assert.Single(WorkInventory.Derive(Facts(Landing(pushed: true), late, "q2")));

        foreach (var unit in new[] { line, pull, pushed })
        {
            Assert.DoesNotContain(unit.Entries, entry => entry.Code == InventoryCode.LandingStale);
        }
    }

    [Fact]
    public void An_abandoned_pull_request_does_not_keep_a_landing_from_being_named()
    {
        var abandoned = Landing(pullRequest: "https://example.test/pr/7") with
        {
            PullRequestState = new PullRequestState(PullRequestStates.Abandoned),
        };

        var unit = Assert.Single(WorkInventory.Derive(Facts(abandoned, At.AddDays(5), "q2")));

        Assert.Contains(unit.Entries, entry => entry.Code == InventoryCode.LandingStale);
    }
}
