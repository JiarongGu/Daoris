using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The planner's starts and resumes take the one comparator: a quest's class, then a resume before a new start,
/// then first arrival; a normal item past its bound is ordered as high and says so. Nothing in flight is cut: it only decides who
/// takes the next place under the cap.
/// </summary>
public sealed class PriorityStartOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private const string Root = "D:/fam/Game";

    private static QuestView Open(string id, int minutesAgo = 10, string from = "Asker") =>
        new(id, from, "Game", $"Ask {id}", "Here is why.", "Open") { Updated = Now.AddMinutes(-minutesAgo) };

    /// <summary>A taken quest whose session's turn ended on its check, whose verdict word waits: it resumes at the first look.</summary>
    private static QuestView Resume(string id, int minutesAgo = 10) =>
        new(id, "Asker", "Game", $"Ask {id}", "Here is why.", "Taken") { Updated = Now.AddMinutes(-minutesAgo) };

    private static PriorSession Record(string session, string tree) =>
        new(session, tree, "awaiting-step", null, "Game")
        {
            Said = [new SaidWordView("w-" + session, "Daoris ran `npm run verify` and it reached.", Now, [], false) { Key = $"check/{session}", From = DaorisWords.From }],
        };

    private static SessionView Waiting(string session, string quest, string tree) =>
        new(session, "Game", "awaiting-step") { Kind = "driven", Quest = quest, Tree = tree };

    private static PriorityEvent Set(string target, string id, string @class) =>
        new(Now.AddMinutes(-1), target, id, "games", @class, "it matters", "the person") { Until = @class == PriorityClasses.Urgent ? Now.AddHours(1) : null };

    private static IReadOnlyList<Consideration> Plan(int cap, QuestView[] quests, PriorityEvent[] events, params (string Quest, PriorSession Record)[] resumes)
    {
        var snapshot = new Snapshot(quests, [new RepoView("Game", true, Root)], [.. resumes.Select(each => Waiting(each.Record.Session, each.Quest, each.Record.Tree!))])
        {
            LastRun = resumes.ToDictionary(each => each.Quest, each => each.Record, StringComparer.OrdinalIgnoreCase),
            Priority = new PriorityLook(events, PriorityRules.Default, Now),
        }.Apart("Game");
        return Planner.Plan(snapshot, DriverConfig.Empty with { Drivable = ["Game"], Cap = cap });
    }

    private static IEnumerable<string> Started(IReadOnlyList<Consideration> plan) => plan.Where(each => each.Verdict == StartVerdict.Start).Select(each => each.Quest.Id);

    [Fact]
    public void A_resume_goes_before_a_new_start_of_the_same_class()
    {
        var plan = Plan(1, [Open("new", 100), Resume("back", 5)], [], ("back", Record("s1", "D:/trees/s1")));

        Assert.Equal(["back"], Started(plan));
        Assert.Equal(StartVerdict.AtCapacity, plan.Single(each => each.Quest.Id == "new").Verdict);
    }

    [Fact]
    public void A_high_new_start_goes_before_a_normal_resume_and_a_normal_new_start_before_a_low_resume()
    {
        var high = Plan(1, [Resume("back", 5), Open("new", 1)], [Set(PriorityBook.Quest, "new", PriorityClasses.High)], ("back", Record("s1", "D:/trees/s1")));
        var low = Plan(1, [Resume("back", 5), Open("new", 1)], [Set(PriorityBook.Quest, "back", PriorityClasses.Low)], ("back", Record("s1", "D:/trees/s1")));

        Assert.Equal(["new"], Started(high));
        Assert.Equal(["new"], Started(low));
    }

    [Fact]
    public void An_urgent_quest_of_an_ask_takes_the_next_place_and_two_of_one_class_go_in_arrival_order()
    {
        var urgent = Plan(1, [Open("a", 30), Open("b", 20), Open("c", 10, "ask #cafee7")], [Set(PriorityBook.Ask, "cafee7", PriorityClasses.Urgent)]);
        var same = Plan(2, [Open("c", 10), Open("a", 30), Open("b", 20)], []);

        Assert.Equal(["c"], Started(urgent));
        Assert.Equal(["a", "b"], Started(same));
    }

    [Fact]
    public void A_normal_item_past_its_bound_is_ordered_as_high_and_says_so()
    {
        var plan = Plan(
            2,
            [Open("young", 60), Open("fresh-high", 60), Open("aged", 180)],
            [Set(PriorityBook.Quest, "fresh-high", PriorityClasses.High)]);

        Assert.Equal(["aged", "fresh-high"], Started(plan));
        var aged = plan.Single(each => each.Quest.Id == "aged");
        Assert.Equal("normal", aged.AgedFrom);
        Assert.Contains("aged from normal", aged.Reason);
        var left = plan.Single(each => each.Quest.Id == "young");
        Assert.Equal(StartVerdict.AtCapacity, left.Verdict);
        Assert.Null(left.AgedFrom);
        Assert.DoesNotContain("aged", left.Reason);
    }

    [Fact]
    public void A_start_that_came_to_a_hold_is_still_planned_after_every_quest_that_can_start()
    {
        var snapshot = new Snapshot([Open("held", 900), Open("new", 1)], [new RepoView("Game", true, Root)], [])
        {
            HeldStarts = new HashSet<string>(["held"], StringComparer.OrdinalIgnoreCase),
            Priority = new PriorityLook([Set(PriorityBook.Quest, "held", PriorityClasses.Urgent)], PriorityRules.Default, Now),
        }.Apart("Game");

        var plan = Planner.Plan(snapshot, DriverConfig.Empty with { Drivable = ["Game"], Cap = 1 });

        Assert.Equal(["new"], Started(plan));
    }

    private static IReadOnlyList<Consideration> PlanRecords(
        int cap, QuestView[] quests, PriorityEvent[] events, Dictionary<string, PriorSession> records, PriorityRules? rules = null)
    {
        var snapshot = new Snapshot(quests, [new RepoView("Game", true, Root)], [])
        {
            LastRun = new Dictionary<string, PriorSession>(records, StringComparer.OrdinalIgnoreCase),
            Priority = new PriorityLook(events, rules ?? PriorityRules.Default, Now),
        }.Apart("Game");
        return Planner.Plan(snapshot, DriverConfig.Empty with { Drivable = ["Game"], Cap = cap });
    }

    private static PriorSession CutOff(int endedMinutesAgo) =>
        new("s1", "D:/trees/s1", "failed", "timed out.") { Updated = Now.AddMinutes(-endedMinutesAgo) };

    [Fact]
    public void A_resume_waits_from_its_session_s_end_and_not_from_the_take_that_counts_its_running()
    {
        var events = new[] { Set(PriorityBook.Quest, "x", PriorityClasses.High) };
        var justEnded = PlanRecords(1, [Open("x", 1), Resume("y", 180)], events, new() { ["y"] = CutOff(1) });
        var longEnded = PlanRecords(1, [Open("x", 1), Resume("y", 180)], events, new() { ["y"] = CutOff(180) });

        Assert.Equal(["x"], Started(justEnded));
        Assert.Null(justEnded.Single(each => each.Quest.Id == "y").AgedFrom);
        Assert.Equal(["y"], Started(longEnded));
        Assert.Equal("normal", longEnded.Single(each => each.Quest.Id == "y").AgedFrom);
    }

    [Fact]
    public void A_reply_to_a_session_goes_before_a_new_start_of_its_class_and_after_a_higher_class()
    {
        var reply = new PriorSession("s0", "D:/trees/s0", "completed", "ended.") { Said = PersonWords.Of("Go on."), Updated = Now.AddMinutes(-1) };
        var records = new Dictionary<string, PriorSession> { ["r"] = reply };

        var same = PlanRecords(1, [Open("new", 100), Resume("r", 30)], [], records);
        var higher = PlanRecords(1, [Resume("r", 30), Open("new", 1)], [Set(PriorityBook.Quest, "new", PriorityClasses.High)], records);

        Assert.Equal(["r"], Started(same));
        Assert.Equal(["new"], Started(higher));
        Assert.Equal(StartVerdict.AtCapacity, higher.Single(each => each.Quest.Id == "r").Verdict);
    }

    [Fact]
    public void A_quest_with_no_known_arrival_sorts_after_the_known_ones_and_never_ages()
    {
        var unknown = Open("unknown") with { Updated = null };

        var plan = PlanRecords(1, [unknown, Open("known", 30)], [], []);

        Assert.Equal(["known"], Started(plan));
        Assert.Null(plan.Single(each => each.Quest.Id == "unknown").AgedFrom);
    }

    [Fact]
    public void The_aged_note_says_its_bound_in_hours_whatever_the_culture()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var plan = PlanRecords(1, [Open("aged", 100)], [], [], PriorityRules.Default with { AgeNormal = TimeSpan.FromMinutes(90) });

            Assert.Contains("past 1.5 h", plan.Single().Reason);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = before;
        }
    }
}
