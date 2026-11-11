using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's presses and its answered holds, kept on disk for the second check's selection: a press's settled outcome wins whatever
/// order its lines were written in, the record is read again after a restart and past the press record's cap, the live presses fold in
/// beside the kept ones, and an answered hold's call is read back as the person's yes.
/// </summary>
public sealed class AskDaorisKeptTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-kept", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void A_press_s_settled_outcome_wins_whatever_order_its_lines_came_in_and_is_read_again_after_a_restart()
    {
        var kept = new AskDaorisKept(_home);
        kept.Press(new KeptPress("p1", "s1", At, "WORK_PAUSE", null, "done", Asked: false));
        kept.Press(new KeptPress("p1", "s1", At, "WORK_PAUSE", "games/atlas", "running", Asked: false));
        kept.Press(new KeptPress("p2", "s1", At.AddSeconds(1), "QUEST_DELETE", null, "waiting", Asked: true));
        kept.Press(new KeptPress("p2", "s1", At.AddSeconds(1), "QUEST_DELETE", null, "allowed", Asked: true));

        var presses = new AskDaorisKept(_home).Presses("s1");

        Assert.Equal(
            [new TurnPress(At, "WORK_PAUSE", "games/atlas", "done", false), new TurnPress(At.AddSeconds(1), "QUEST_DELETE", null, "allowed", true)],
            presses);
        Assert.Empty(new AskDaorisKept(_home).Presses("s2"));
    }

    [Fact]
    public void Every_press_is_kept_past_the_press_record_s_cap_and_a_live_one_not_yet_written_is_folded_in()
    {
        var kept = new AskDaorisKept(_home);
        for (var each = 0; each < 130; each++) kept.Press(new KeptPress($"p{each}", "s1", At.AddSeconds(each), "SET_HOLD", null, "done", Asked: false));

        var presses = kept.Presses("s1", [new KeptPress("p0", "s1", At, "SET_HOLD", null, "running", false), new KeptPress("late", "s1", At.AddMinutes(9), "WORK_PAUSE", null, "running", false)]);

        Assert.Equal(131, presses.Count);
        Assert.Equal("done", presses[0].Outcome);
        Assert.Equal(("WORK_PAUSE", "running"), (presses[^1].Route, presses[^1].Outcome));
    }

    [Fact]
    public void An_answered_hold_s_call_is_read_back_as_the_person_s_yes()
    {
        var kept = new AskDaorisKept(_home);
        kept.Answered("s1", "toolu_7");
        kept.Answered("s1", "toolu_7");
        kept.Answered("s1", "toolu_9");

        Assert.Equal(["toolu_7", "toolu_9"], new AskDaorisKept(_home).Answered("s1").Order(StringComparer.Ordinal));
        Assert.Empty(new AskDaorisKept(_home).Answered("s2"));
    }
}
