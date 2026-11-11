using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The allow supervisor's decision record: each allow kept as a file beside the session's other decisions and read back by the line,
/// <c>went-on</c> at its keeping; a decision with no session kept with the machine's; and a restart closing one whose state line was never
/// written <c>stale</c>. Each seen failing first with its guard taken out.
/// </summary>
public sealed class SupervisorAllowRecordTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-allow-record", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T14:00:00Z");

    private static readonly SupervisorReason[] Reasons = [new("It only holds.", "the call's title")];

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private sealed class Admits : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => new("g-1", null);
    }

    private QuestionSupervisorDecisions Decisions() => new(_home, new Admits());

    private static SupervisorAllowed Allowed(string id, string shape, string? session, string act) =>
        new(id, shape, "r1", session, "engine", act, "supervisor-allows", shape == SupervisorAllowShapes.Steward ? StewardActNames.Clean : null,
            new SupervisorHolder(RoleHolders.Terminal, null), Reasons);

    [Fact]
    public void An_allow_is_read_back_by_its_session_went_on_at_its_keeping()
    {
        var decisions = Decisions();
        decisions.AllowKept(Allowed("d1", SupervisorAllowShapes.Request, "s1", "once"), At);

        var read = Assert.Single(decisions.AllowsOf("s1"));

        Assert.Equal(("d1", SupervisorAllowShapes.Request, "r1", "once", SupervisorState.WentOn), (read.Id, read.Shape, read.Subject, read.Do, read.State));
        Assert.Equal(At, read.KeptAt);
        Assert.Equal("supervisor-allows", read.Role);
        Assert.Empty(decisions.AllowsOf("s2"));
    }

    [Fact]
    public void A_restart_closes_an_allow_whose_state_line_was_never_written_stale_and_leaves_a_went_on_one()
    {
        Decisions().AllowKept(Allowed("d3", SupervisorAllowShapes.Steward, "s1", "steward"), At);
        File.Delete(new SupervisorDecisionFiles(_home).StatesOf("s1", "d3"));
        Decisions().AllowKept(Allowed("d4", SupervisorAllowShapes.Widening, "s1", "standing"), At);
        Assert.Equal(SupervisorState.Held, Decisions().AllowsOf("s1").Single(each => each.Id == "d3").State);

        var restarted = Decisions();
        restarted.SweepAtStart(At.AddMinutes(1));

        var read = restarted.AllowsOf("s1").ToDictionary(each => each.Id);
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.Restarted), (read["d3"].State, read["d3"].Why));
        Assert.Equal(SupervisorState.WentOn, read["d4"].State);
    }

    [Fact]
    public void A_leave_is_kept_left_at_once_with_its_bound_and_reasons_and_a_restart_leaves_it_as_it_is()
    {
        var decisions = Decisions();
        var bound = SupervisorBounds.All[0];
        decisions.AllowKept(Allowed("d6", SupervisorAllowShapes.Request, "s1", SupervisorAllowActs.Leave) with { Bound = bound }, At);

        var restarted = Decisions();
        restarted.SweepAtStart(At.AddMinutes(2));

        var read = Assert.Single(restarted.AllowsOf("s1"));
        Assert.Equal((SupervisorAllowActs.Leave, SupervisorState.Left, bound, At), (read.Do, read.State, read.Bound, read.GoesOnAt));
        Assert.Equal(Reasons, read.Reasons);
    }

    [Fact]
    public void A_decision_with_no_session_is_kept_with_the_machines_and_read_by_no_session()
    {
        var decisions = Decisions();
        decisions.AllowKept(Allowed("d5", SupervisorAllowShapes.Widening, null, "standing"), At);

        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, SupervisorAllows.Machine + QuestionSupervisorDecisions.DecisionsSuffix), "d5.json")));
        Assert.Empty(decisions.AllowsOf("s1"));
    }
}
