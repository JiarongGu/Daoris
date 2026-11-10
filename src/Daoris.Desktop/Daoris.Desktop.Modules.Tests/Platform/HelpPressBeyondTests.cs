using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A press Ask Daoris made in full access keeps the place it named and whether that lay beyond its conversation's focus at the press
/// (`docs/2026-11-11-ask-scope-ux-design.md` §4), which Notifications' line and the turn's receipt say; <c>HELP_BEYOND</c> answers a
/// conversation's receipt from what its connector noted and those presses.
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
        Assert.Equal((HelpFocus.OfRepository("Work", "api"), true), (api!.Place, api.Beyond));
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
