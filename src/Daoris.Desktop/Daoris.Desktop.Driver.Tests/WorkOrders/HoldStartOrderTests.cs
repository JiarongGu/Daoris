using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A held quest's queued start or resume stands aside at each look: it spends no place under the cap, the next goes, and it says it waits for
/// its lift. Lifted, it comes back at its own arrival, not after the ones that went meanwhile.
/// </summary>
public sealed class HoldStartOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static QuestView Open(string id, int minutesAgo, string from = "Asker") =>
        new(id, from, "Game", $"Ask {id}", "Here is why.", "Open") { Updated = Now.AddMinutes(-minutesAgo) };

    private static HoldEvent Hold(string target, string id, bool held = true, int minutes = -2, string? run = null) =>
        new(Now.AddMinutes(minutes), target, id, "games", held, "make room", "the person") { Run = run };

    private static IReadOnlyList<Consideration> Plan(int cap, QuestView[] quests, params HoldEvent[] holds) =>
        Planner.Plan(
            new Snapshot(quests, [new RepoView("Game", true, "D:/fam/Game")], [])
            {
                Priority = new PriorityLook([], PriorityRules.Default, Now) { Holds = holds },
            }.Apart("Game"),
            DriverConfig.Empty with { Drivable = ["Game"], Cap = cap });

    private static string[] Started(IReadOnlyList<Consideration> plan) => [.. plan.Where(each => each.Verdict == StartVerdict.Start).Select(each => each.Quest.Id)];

    [Fact]
    public void A_held_quest_spends_no_place_and_the_next_one_starts_in_its_stead()
    {
        var plan = Plan(1, [Open("a", 30), Open("b", 20)], Hold(PriorityBook.Quest, "a"));

        Assert.Equal(["b"], Started(plan));
        var held = plan.Single(each => each.Quest.Id == "a");
        Assert.Equal((StartVerdict.RepositoryBusy, SittingFact.HoldWaiting), (held.Verdict, held.Said?.Code));
        Assert.Contains("held by the person: make room", held.Reason);
    }

    [Fact]
    public void A_lifted_quest_returns_at_its_first_arrival_ahead_of_a_later_one()
    {
        var lifted = Plan(1, [Open("a", 30), Open("b", 20)], Hold(PriorityBook.Quest, "a"), Hold(PriorityBook.Quest, "a", held: false, minutes: -1));

        Assert.Equal(["a"], Started(lifted));
    }

    [Fact]
    public void A_hold_on_an_ask_holds_its_quests_and_a_quest_s_own_lift_stands_over_it()
    {
        var quests = new[] { Open("a", 30, "ask #cafee7"), Open("b", 20, "ask #cafee7"), Open("c", 10) };

        Assert.Equal(["c"], Started(Plan(1, quests, Hold(PriorityBook.Ask, "cafee7"))));
        Assert.Equal(["a"], Started(Plan(1, quests, Hold(PriorityBook.Ask, "cafee7"), Hold(PriorityBook.Quest, "a", held: false, minutes: -1))));
    }
}
