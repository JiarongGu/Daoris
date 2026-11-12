using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The hand-back as Daoris reads it, never as the session says it: the base, the paths touched, the checks the landing's own selection
/// reaches with the verdict each has at this tip, and what was left running. A check with no verdict is Daoris's to run.
/// </summary>
public sealed class HandbackObservationTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private static readonly IReadOnlyList<DeclaredGate> Gates =
    [
        new("web", "npm test", null, false) { Paths = ["src/Daoris.Web/"] },
        new("driver", "dotnet test", null, true) { Paths = ["src/Daoris.Desktop/"] },
        new("docs", "node tools/docs.mjs", null, false),
    ];

    private static (CheckAsk, CheckState?) Done(string id, string gate, string state, string tip = "tip1", string session = "s1") =>
        (new CheckAsk(id, session, gate, null, CheckAsk.ByAgent, At), new CheckState(id, state, At) { Tip = tip });

    private static HandbackObservation Judge(
        IReadOnlyList<string> touched, IReadOnlyList<(CheckAsk, CheckState?)>? checks = null, int behind = 0,
        IReadOnlyList<LeftProcess>? left = null) =>
        HandbackObservation.Judge("tip1", "base0", behind, touched, Gates, checks ?? [], left ?? []);

    [Fact]
    public void The_checks_are_the_gates_the_touched_paths_reach_and_a_gate_with_no_paths_is_always_reached()
    {
        var seen = Judge(["src/Daoris.Desktop/Foo.cs"]);

        Assert.Equal(["driver", "docs"], seen.Checks.Select(check => check.Gate));
        Assert.All(seen.Checks, check => Assert.Equal(CheckVerdicts.Missing, check.Verdict));
        Assert.Equal(["driver", "docs"], seen.Missing.Select(check => check.Gate));
    }

    [Fact]
    public void A_verdict_counts_only_at_this_tip_and_the_newest_finished_one_for_the_gate_stands()
    {
        var seen = Judge(
            ["src/Daoris.Desktop/Foo.cs"],
            [
                Done("a1", "driver", CheckStates.Failed), Done("a2", "driver", CheckStates.Reached, tip: "older"),
                Done("a3", "docs", CheckStates.Reached), Done("a4", "docs", CheckStates.Reached, session: "s2"),
            ]);

        Assert.Equal([("driver", CheckVerdicts.Failed), ("docs", CheckVerdicts.Reached)], seen.Checks.Select(check => (check.Gate, check.Verdict)));
        Assert.Empty(seen.Missing);
        Assert.False(seen.Ready);
    }

    [Fact]
    public void A_check_still_queued_or_running_is_checking_and_not_missing()
    {
        var running = (new CheckAsk("a1", "s1", "driver", null, CheckAsk.ByDaoris, At), (CheckState?)new CheckState("a1", CheckStates.Running, At) { Tip = "tip1" });
        var queued = (new CheckAsk("a2", "s1", "docs", null, CheckAsk.ByDaoris, At), (CheckState?)null);

        var seen = Judge(["src/Daoris.Desktop/Foo.cs"], [running, queued]);

        Assert.Equal([CheckVerdicts.Checking, CheckVerdicts.Checking], seen.Checks.Select(check => check.Verdict));
        Assert.Empty(seen.Missing);
        Assert.False(seen.Ready);
    }

    [Fact]
    public void It_is_ready_only_when_based_on_the_line_every_check_reached_and_nothing_is_left_running()
    {
        var reached = new[] { Done("a1", "driver", CheckStates.Reached), Done("a2", "docs", CheckStates.Reached) };
        var touched = new[] { "src/Daoris.Desktop/Foo.cs" };

        Assert.True(Judge(touched, reached).Ready);
        Assert.False(Judge(touched, reached, behind: 2).Ready);
        Assert.False(Judge(touched, reached, left: [new LeftProcess(4242, "node", Owned: true)]).Ready);
    }

    [Fact]
    public void The_words_say_what_was_read_and_what_daoris_runs_itself()
    {
        var words = Judge(["src/Daoris.Desktop/Foo.cs"], behind: 3, left: [new LeftProcess(4242, "node", Owned: false)]).Words();

        Assert.Contains("base0", words);
        Assert.Contains("3 commits behind", words);
        Assert.Contains("1 path", words);
        Assert.Contains("driver", words);
        Assert.Contains("runs it", words);
        Assert.Contains("node", words);
        Assert.Contains("not started by this run", words);
    }
}
