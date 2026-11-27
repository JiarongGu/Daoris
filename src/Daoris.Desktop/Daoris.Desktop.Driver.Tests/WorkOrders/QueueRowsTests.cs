using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one reader of the machine's waiting items: the starts and resumes the plan leaves waiting and the
/// checks waiting for a slot, each row with its sort inputs and why it waits, and each place composed by the one comparator with the
/// items ahead, their class and where it comes from. <see cref="QueueRowsTwinTests"/> holds the places against the page's twin.
/// </summary>
public sealed class QueueRowsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private const string Root = "D:/fam/Game";

    private static QuestView Open(string id, int minutesAgo, string from = "Asker") =>
        new(id, from, "Game", $"Ask {id}", "Here is why.", "Open") { Updated = Now.AddMinutes(-minutesAgo) };

    private static PriorityEvent Set(string target, string id, string @class, string why = "it matters") =>
        new(Now.AddMinutes(-1), target, id, "games", @class, why, "the person") { Until = @class == PriorityClasses.Urgent ? Now.AddHours(1) : null };

    private static IReadOnlyList<Consideration> Plan(int cap, QuestView[] quests, params PriorityEvent[] events)
    {
        var snapshot = new Snapshot(quests, [new RepoView("Game", true, Root)], [])
        {
            Priority = new PriorityLook(events, PriorityRules.Default, Now),
        }.Apart("Game");
        return Planner.Plan(snapshot, DriverConfig.Empty with { Drivable = ["Game"], Cap = cap });
    }

    private static VerifyWaiter Waiter(string run, int minutesAgo, string @class = PriorityClasses.Normal, string? by = null, string? why = null) =>
        new(1, 1, run, "i-" + run, "Daoris", Now.AddMinutes(-minutesAgo), 0)
        {
            Class = @class, Source = by is null ? PriorityReading.Unset : PriorityBook.Quest, By = by, Why = why, Wait = CheckWait.OfSlots(2, 2),
        };

    [Fact]
    public void Three_ordinary_checks_waiting_and_an_urgent_one_the_urgent_takes_the_next_slot_and_each_behind_names_it()
    {
        var look = new VerifyLook([], [Waiter("a", 30), Waiter("b", 20), Waiter("c", 10), Waiter("u", 1, PriorityClasses.Urgent, "the person", "the fix")]);

        var spots = QueueRows.Placed(QueueRows.Read([], look));

        Assert.Equal(["u", "a", "b", "c"], spots.Select(spot => spot.Row.Item));
        Assert.All(spots, spot => Assert.Equal(QueueRows.Checks, spot.Row.Queue));
        var last = spots[^1];
        Assert.Equal((4, 4), (last.Position, last.Of));
        Assert.Equal(["u", "a", "b"], last.Ahead.Select(each => each.Item));
        Assert.Equal(CheckSlotWaits.Full, last.Row.Code);
        Assert.Equal("2", last.Row.Values["running"]);
        Assert.Equal(
            "checks 4 of 4: the checks of run #c, normal, first checks; waits: Waits for a check slot: 2 of 2 run; "
            + "ahead: the checks of run #u, urgent (set by the person: the fix), first checks; the checks of run #a, normal, first checks; "
            + "the checks of run #b, normal, first checks.",
            QueueRows.Said(last));
    }

    [Fact]
    public void The_plan_s_waiting_starts_are_rows_with_their_class_and_its_source_and_a_start_planned_now_is_none()
    {
        var plan = Plan(1, [Open("old", 30), Open("mid", 20), Open("new", 10, "ask #cafee7")], Set(PriorityBook.Ask, "cafee7", PriorityClasses.High, "the release"));

        var rows = QueueRows.Read(plan, new VerifyLook([], []));

        Assert.Equal(["old", "mid"], rows.Select(row => row.Item));
        Assert.All(rows, row => Assert.Equal((QueueRows.Starts, QueueStage.NewStart, StartVerdict.AtCapacity.ToString(), SittingFact.CapSpent), (row.Queue, row.Stage, row.Verdict, row.Code)));
        Assert.Equal("1", rows[0].Values["cap"]);
        Assert.Equal(Now.AddMinutes(-30), rows[0].Arrived);
        Assert.Equal(PriorityReading.Unset, rows[0].Source);
    }

    [Fact]
    public void A_waiting_start_of_an_ask_s_class_says_it_comes_from_its_ask_and_a_promoted_one_says_it_is_aged()
    {
        var plan = Plan(
            1, [Open("top", 1), Open("first", 10, "ask #cafee7"), Open("aged", 200), Open("asked", 5, "ask #cafee7")],
            Set(PriorityBook.Ask, "cafee7", PriorityClasses.High, "the release"), Set(PriorityBook.Quest, "top", PriorityClasses.Urgent));

        var spots = QueueRows.Placed(QueueRows.Read(plan, new VerifyLook([], [])));

        var asked = spots.Single(spot => spot.Row.Item == "asked");
        Assert.Equal((PriorityClasses.High, PriorityBook.Ask, "cafee7"), (asked.Row.Class, asked.Row.Source, asked.Row.AskId));
        Assert.Equal("high (from its ask #cafee7, set by the person: the release)", QueueRows.Class(asked.Row));
        var aged = spots.Single(spot => spot.Row.Item == "aged");
        Assert.Equal((PriorityClasses.High, PriorityClasses.Normal), (aged.Row.Class, aged.Row.AgedFrom));
        Assert.Equal("high (aged from normal)", QueueRows.Class(aged.Row));
        // The urgent one starts; the rest are high alike, the ask's class and the aged class, and the one waiting longest goes first.
        Assert.Equal(["aged", "first", "asked"], spots.Select(spot => spot.Row.Item));
        Assert.Equal((3, 3), (asked.Position, asked.Of));
    }

    [Fact]
    public void A_quest_a_session_works_on_or_whose_start_opens_waits_in_no_queue()
    {
        var serving = new Consideration(Open("busy", 5), StartVerdict.RepositoryBusy, "already worked on")
        {
            Said = new SittingFact(SittingFact.Serving) { Session = "s-1" }, Place = new QueuePlace(PriorityClasses.Normal, QueueStage.NewStart, Now),
        };
        var queued = serving with { Quest = Open("behind", 5), Said = new SittingFact(SittingFact.Queued) { Quest = "busy" } };

        var rows = QueueRows.Read([serving, queued], new VerifyLook([], []));

        Assert.Equal(["behind"], rows.Select(row => row.Item));
        Assert.Equal("busy", rows[0].Values["quest"]);
    }

    [Fact]
    public void A_row_reads_back_as_the_route_answers_it()
    {
        var plan = Plan(1, [Open("old", 30), Open("asked", 5, "ask #cafee7")], Set(PriorityBook.Ask, "cafee7", PriorityClasses.Urgent, "the fix"));
        var rows = QueueRows.Read(plan, new VerifyLook([], [Waiter("u", 1, PriorityClasses.High, "session s-2", "unblocks")])).ToList();

        var read = rows.Select(row => QueueRows.Parse(System.Text.Json.JsonDocument.Parse(QueueRows.Json(row).ToJsonString()).RootElement)!).ToList();

        Assert.Equal(rows.Select(row => QueueRows.Json(row).ToJsonString()), read.Select(row => QueueRows.Json(row).ToJsonString()));
        Assert.Equal(QueueRows.Said(rows), QueueRows.Said(read));
    }

    [Fact]
    public void Nothing_waiting_is_said_so()
    {
        Assert.Equal("Nothing waits in the machine's queues.\n", QueueRows.Said([]));
    }
}
