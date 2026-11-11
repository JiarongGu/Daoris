using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A press Ask Daoris made in full access keeps the place it named and whether that lay beyond its conversation's focus at the press
/// (`docs/2026-11-11-ask-scope-ux-design.md` §4), which Notifications' line and the turn's receipt say; one it asked the person on a
/// card is kept apart with its card's state; <c>HELP_BEYOND</c> answers a conversation's receipt from what its connector noted and those
/// presses.
/// </summary>
public sealed class HelpPressBeyondTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly RepoView[] Registry =
    [
        new("engine", true, "C:/checkouts/engine", "Games"),
        new("api", true, "C:/checkouts/api", "Work"),
    ];

    private static IpcRequest Press(string route, string payload) => new()
    {
        Id = "p", Module = "DAORIS.DRIVER", Type = route, Payload = JsonDocument.Parse(payload).RootElement.Clone(),
    };

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void A_press_s_place_is_the_workspace_or_repository_it_named_judged_against_the_focus_at_the_press()
    {
        var games = HelpFocus.OfWorkspace("Games");
        var api = HelpPressPlace.Of(Press("SET_HOLD", """{"repository":"api","held":true}""").Payload, games, Registry);
        Assert.Equal((HelpFocus.OfRepository("Work", "api"), true), (api!.Place, api.Beyond));

        var engine = HelpPressPlace.Of(Press("SET_HOLD", """{"repository":"engine"}""").Payload, games, Registry);
        Assert.Equal((HelpFocus.OfRepository("Games", "engine"), false), (engine!.Place, engine.Beyond));

        var work = HelpPressPlace.Of(Press("WORK_PAUSE", """{"quest":"q1","workspace":"Work"}""").Payload, games, Registry);
        Assert.Equal((HelpFocus.OfWorkspace("Work"), true), (work!.Place, work.Beyond));

        Assert.Null(HelpPressPlace.Of(Press("WORK_PAUSE", """{"quest":"q1"}""").Payload, games, Registry));
    }

    [Fact]
    public void A_press_naming_only_a_quest_is_placed_at_the_repository_the_quest_asks()
    {
        var games = HelpFocus.OfWorkspace("Games");
        var questOnly = Press("WORK_PAUSE", """{"quest":"q1"}""").Payload;
        Assert.Equal("q1", HelpPressPlace.QuestOnly(questOnly));
        Assert.Null(HelpPressPlace.QuestOnly(Press("WORK_PAUSE", """{"quest":"q1","workspace":"Work"}""").Payload));
        Assert.Null(HelpPressPlace.QuestOnly(Press("SET_HOLD", """{"repository":"api"}""").Payload));

        var api = HelpPressPlace.Of(questOnly, games, Registry, questTo: "api");
        Assert.Equal((HelpFocus.OfRepository("Work", "api"), true, games), (api!.Place, api.Beyond, api.Focus));
        var engine = HelpPressPlace.Of(questOnly, games, Registry, questTo: "engine");
        Assert.Equal((HelpFocus.OfRepository("Games", "engine"), false), (engine!.Place, engine.Beyond));
        Assert.Null(HelpPressPlace.Of(questOnly, games, Registry, questTo: "gone"));
    }

    [Fact]
    public void A_placed_press_keeps_its_place_and_whether_it_was_beyond_and_answers_them()
    {
        var record = new HelpPressRecord(new Clock(Noon));
        var id = record.Began(Press("SET_HOLD", """{"repository":"api"}"""), "s1");
        Assert.Null(Assert.Single(record.Now()).Place);

        record.Placed(id, new HelpPressPlace(HelpFocus.OfRepository("Work", "api"), true));

        var placed = Assert.Single(record.Now());
        Assert.Equal(HelpFocus.OfRepository("Work", "api").Wire(), placed.Place);
        Assert.True(placed.Beyond);
        var wire = JsonSerializer.SerializeToElement(placed, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Work", wire.GetProperty("place").GetProperty("workspace").GetString());
        Assert.True(wire.GetProperty("beyond").GetBoolean());
    }

    [Theory]
    [InlineData(PipeConfirmationEnd.Confirmed, "allowed")]
    [InlineData(PipeConfirmationEnd.Refused, "refused")]
    [InlineData(PipeConfirmationEnd.Withdrawn, "unanswered")]
    [InlineData(PipeConfirmationEnd.Lost, "unanswered")]
    [InlineData(PipeConfirmationEnd.Changed, "changed")]
    public void A_press_that_asked_the_person_is_kept_apart_from_those_that_ran_with_its_card_s_state(PipeConfirmationEnd end, string state)
    {
        var record = new HelpPressRecord(new Clock(Noon));
        var place = new HelpPressPlace(HelpFocus.OfRepository("Work", "api"), true, HelpFocus.OfWorkspace("Games"));
        var id = record.Asked(Press("WORK_PAUSE", """{"quest":"q1"}"""), "s1", place);

        var asked = Assert.Single(record.Now());
        Assert.Equal((true, HelpPressRecord.Waiting, true, "q1", "s1"), (asked.Asked, asked.Outcome, asked.Beyond, asked.Target, asked.Session));
        Assert.Equal(HelpFocus.OfRepository("Work", "api").Wire(), asked.Place);

        record.Answered(id, end);
        Assert.Equal(state, Assert.Single(record.Now()).Outcome);
        var wire = JsonSerializer.SerializeToElement(record.Now()[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(wire.GetProperty("asked").GetBoolean());
    }

    [Fact]
    public void The_receipt_lists_a_press_that_asked_the_person_beyond_its_focus_as_asked_with_its_card_s_state()
    {
        var record = new HelpPressRecord(new Clock(Noon));
        var games = HelpFocus.OfWorkspace("Games");
        var beyond = record.Asked(Press("WORK_PAUSE", """{"quest":"q1"}"""), "s1", new HelpPressPlace(HelpFocus.OfWorkspace("Work"), true, games));
        record.Asked(Press("WORK_PAUSE", """{"quest":"q2"}"""), "s1", new HelpPressPlace(games, false, games));
        record.Asked(Press("WORK_PAUSE", """{"quest":"q3"}"""), "s1", null);
        record.Answered(beyond, PipeConfirmationEnd.Refused);

        var entry = Assert.Single(HelpBeyondRoutes.Entries([], record.Now(), "s1"));
        Assert.Equal(("asked", "DAORIS.DRIVER.WORK_PAUSE", "refused"), (entry.Act, entry.What, entry.Outcome));
        Assert.Equal(HelpFocus.OfWorkspace("Work"), entry.Place);
    }

    [Fact]
    public void The_receipt_is_the_noted_places_and_the_conversation_s_presses_beyond_its_focus_first_to_last()
    {
        var record = new HelpPressRecord(new Clock(Noon.AddSeconds(30)));
        var beyond = record.Began(Press("SET_HOLD", """{"repository":"api"}"""), "s1");
        record.Placed(beyond, new HelpPressPlace(HelpFocus.OfRepository("Work", "api"), true));
        record.Ended(beyond, true);
        var within = record.Began(Press("SET_HOLD", """{"repository":"engine"}"""), "s1");
        record.Placed(within, new HelpPressPlace(HelpFocus.OfRepository("Games", "engine"), false));
        var another = record.Began(Press("SET_HOLD", """{"repository":"api"}"""), "s2");
        record.Placed(another, new HelpPressPlace(HelpFocus.OfRepository("Work", "api"), true));

        var entries = HelpBeyondRoutes.Entries(
            [new HelpBeyondEntry(Noon, "search", HelpFocus.OfWorkspace("Work"), "release"), new HelpBeyondEntry(Noon.AddMinutes(1), "read", HelpFocus.OfRepository("Work", "api"), "README.md")],
            record.Now(), "s1");

        Assert.Equal(
            [("search", null, null), ("press", "DAORIS.DRIVER.SET_HOLD", "done"), ("read", "README.md", null)],
            entries.Select(entry => (entry.Act, entry.Act == "search" ? null : entry.What, entry.Outcome)));
    }
}
