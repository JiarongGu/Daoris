using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// COWORK1e (D188 point 4; the message-urgency design §8.3) over the bridge: the answers the answers-alone bound holds for the person,
/// as *What needs you* and the session's page read them, and the person's two acts on one: *Go on with this answer*, which takes the
/// mark away and keeps the press as their word, and *Not now*, which keeps the answer for them. What waits on nothing of theirs is
/// refused by code, and nothing is written.
/// </summary>
public sealed class DriverModuleSessionAnswersAloneTests : DriverModuleBridge
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

    private static IReadOnlyList<SessionEvent> Notes(DriverLoop loop, string session, string code) =>
        [.. loop.Events.After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true)];

    [Fact]
    public async Task Each_answer_held_for_the_person_is_listed_with_the_agent_that_answered()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        Assert.Empty((await AnswerAsync(module, "ANSWER_WAITS")).GetProperty("waits").EnumerateArray());

        new GoOnMarks(loop.Home).Mark("d1", ["w3"], GoOnMarks.AnswersAlone, At, agent: "codex-acp");
        new GoOnMarks(loop.Home).Mark("d2", ["w9"], ContinueWhy.Of(ContinueWhy.Tree), At);

        var wait = Assert.Single((await AnswerAsync(module, "ANSWER_WAITS")).GetProperty("waits").EnumerateArray());
        Assert.Equal(("d1", "codex-acp"), (wait.GetProperty("session").GetString(), wait.GetProperty("agent").GetString()));
        Assert.Equal(["w3"], wait.GetProperty("words").EnumerateArray().Select(id => id.GetString()));
        Assert.True(wait.TryGetProperty("since", out _));
    }

    /// <summary>*Go on with this answer*: the press kept as the person's word, the mark gone, and the same press again finds nothing held.</summary>
    [Fact]
    public async Task Go_on_with_this_answer_takes_the_mark_away_and_keeps_the_press_as_the_persons_word()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        new GoOnMarks(loop.Home).Mark("d1", ["w3"], GoOnMarks.AnswersAlone, At, agent: "codex-acp");

        var answer = await AnswerAsync(module, "SESSION_ANSWER_ALONE", new { session = "d1", goOn = true });

        Assert.True(answer.GetProperty("answered").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("why").ValueKind);
        Assert.Null(new GoOnMarks(loop.Home).Read("d1"));
        Assert.Equal(["w3"], Assert.Single(Notes(loop, "d1", NoteCodes.AnswersPassed.Code)).Words);
        Assert.Empty((await AnswerAsync(module, "ANSWER_WAITS")).GetProperty("waits").EnumerateArray());

        var again = await AnswerAsync(module, "SESSION_ANSWER_ALONE", new { session = "d1", goOn = true });
        Assert.Equal((false, AnswersAloneWord.Unheld), (again.GetProperty("answered").GetBoolean(), again.GetProperty("why").GetString()));
    }

    /// <summary>*Not now*: the answer is the person's, marked so nothing takes it on by itself, and the wait is no longer listed.</summary>
    [Fact]
    public async Task Not_now_keeps_the_answer_for_the_person_and_the_wait_is_gone()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        new GoOnMarks(loop.Home).Mark("d1", ["w3"], GoOnMarks.AnswersAlone, At, agent: "codex-acp");

        var answer = await AnswerAsync(module, "SESSION_ANSWER_ALONE", new { session = "d1", goOn = false });

        Assert.True(answer.GetProperty("answered").GetBoolean());
        Assert.Equal(GoOnMarks.NotNow, new GoOnMarks(loop.Home).Read("d1")!.Why);
        Assert.Single(Notes(loop, "d1", NoteCodes.AnswersKept.Code));
        Assert.Empty((await AnswerAsync(module, "ANSWER_WAITS")).GetProperty("waits").EnumerateArray());
    }

    [Fact]
    public async Task An_answer_nothing_holds_for_the_person_is_refused_by_code_and_nothing_is_written()
    {
        var loop = Loop();
        new GoOnMarks(loop.Home).Mark("d2", ["w9"], ContinueWhy.Of(ContinueWhy.Tree), At);

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_ANSWER_ALONE", new { session = "d2", goOn = false });

        Assert.Equal((false, AnswersAloneWord.Unheld), (answer.GetProperty("answered").GetBoolean(), answer.GetProperty("why").GetString()));
        Assert.Equal(ContinueWhy.Tree, new GoOnMarks(loop.Home).Read("d2")!.Why);
        Assert.Empty(loop.Events.After("d2", 0).Events);
    }
}
