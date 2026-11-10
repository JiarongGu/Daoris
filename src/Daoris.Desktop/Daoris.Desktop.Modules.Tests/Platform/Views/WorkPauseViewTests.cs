using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>An ask's or a quest's pause and resume as their presses' cards show them: what the pause stops, and what a press changes nothing by.</summary>
public sealed class WorkPauseViewTests
{
    private static StubGrounds Work(bool pausable, bool paused, params string[] stopped)
    {
        var grounds = new StubGrounds();
        grounds.Asks["a1"] = "Open";
        grounds.Quests["q1"] = "Taken";
        grounds.Reads["WORK_PLAN"] = _ => new
        {
            pausable,
            paused = paused ? new { at = "2026-11-11T00:00:00Z" } : null,
            sessions = stopped.Select(id => new { session = id, pause = "stopped" }).Append(new { session = "x", pause = "parked" }).ToArray(),
        };
        return grounds;
    }

    private static Task<PressViewed> Pause(StubGrounds grounds, object payload) =>
        new WorkPauseView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    private static Task<PressViewed> Resume(StubGrounds grounds, object payload) =>
        new WorkResumeView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_pause_of_an_ask_says_how_many_running_sessions_it_stops_and_its_command()
    {
        var viewed = await Pause(Work(pausable: true, paused: false, "s1", "s2"), new { ask = "a1", said = true });

        Assert.Equal("work-pause", viewed.View!.Kind);
        Assert.Equal("2", viewed.View.Facts["stops"]);
        Assert.Equal("ask", viewed.View.Facts["scope"]);
        Assert.Equal("daoris ask --pause a1", viewed.View.Command);
    }

    [Fact]
    public async Task A_pause_of_a_quest_is_its_quest_command()
    {
        var viewed = await Pause(Work(pausable: true, paused: false), new { quest = "q1" });

        Assert.Equal("work-pause", viewed.View!.Kind);
        Assert.Equal("daoris quest pause q1", viewed.View.Command);
    }

    [Fact]
    public async Task A_pause_of_work_with_nothing_to_hold_or_paused_already_with_nothing_running_says_it_changes_nothing()
    {
        Assert.Equal("work-pause-nothing", (await Pause(Work(pausable: false, paused: false), new { ask = "a1" })).View!.Kind);
        Assert.Equal("work-pause-already", (await Pause(Work(pausable: true, paused: true), new { ask = "a1" })).View!.Kind);
        Assert.Equal("work-pause", (await Pause(Work(pausable: true, paused: true, "s1"), new { ask = "a1" })).View!.Kind);
    }

    [Fact]
    public async Task A_resume_of_paused_work_lifts_it_and_of_work_not_paused_says_it_changes_nothing()
    {
        var lifted = await Resume(Work(pausable: true, paused: true), new { quest = "q1" });
        var nothing = await Resume(Work(pausable: true, paused: false), new { ask = "a1" });

        Assert.Equal("work-resume", lifted.View!.Kind);
        Assert.Equal("daoris quest resume q1", lifted.View.Command);
        Assert.Equal("work-resume-nothing", nothing.View!.Kind);
        Assert.Equal("daoris ask --resume a1", nothing.View.Command);
    }

    [Theory]
    [InlineData("{\"ask\":\"nope\"}", "there is no ask `#nope` on this machine.")]
    [InlineData("{\"quest\":\"nope\"}", "there is no quest `#nope` on this machine.")]
    [InlineData("{}", "a press names one ask or one quest, by its id.")]
    [InlineData("{\"ask\":\"a1\",\"quest\":\"q1\"}", "a press names one ask or one quest, by its id.")]
    public async Task A_press_naming_no_one_work_the_service_holds_is_refused_in_its_words(string payload, string words)
    {
        var body = JsonDocument.Parse(payload).RootElement;

        Assert.Equal(words, (await new WorkPauseView().ViewAsync(Work(true, false), body, CancellationToken.None)).Refusal);
        Assert.Equal(words, (await new WorkResumeView().ViewAsync(Work(true, false), body, CancellationToken.None)).Refusal);
    }

    [Fact]
    public void The_routes_views_are_found_by_their_module_and_route()
    {
        Assert.IsType<WorkPauseView>(PressViews.Find("daoris.driver", "WORK_PAUSE"));
        Assert.IsType<WorkResumeView>(PressViews.Find("daoris.driver", "WORK_RESUME"));
    }
}
