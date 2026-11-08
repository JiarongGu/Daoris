using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The allow supervisor's decision record: each allow kept as a file beside the session's other decisions and read back by the line,
/// <c>went-on</c> at its keeping; an allowed <c>land</c> <c>held</c> until it moves; a decision with no session kept with the machine's; and
/// a restart closing a held one <c>stale</c>. Each seen failing first with its guard taken out.
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

    private static SupervisorAllowed Allowed(string id, string shape, string? session, string act, DateTimeOffset? landsAt = null) =>
        new(id, shape, "r1", session, "engine", act, "supervisor-allows", shape == SupervisorAllowShapes.Steward ? "land" : null,
            new SupervisorHolder(RoleHolders.Terminal, null), Reasons, landsAt);

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
    public void An_allowed_land_is_held_until_it_moves_and_a_move_after_that_changes_nothing()
    {
        var decisions = Decisions();
        decisions.AllowKept(Allowed("d2", SupervisorAllowShapes.Steward, "s1", "steward", landsAt: At.AddMinutes(5)), At);
        Assert.Equal((SupervisorState.Held, At.AddMinutes(5)), (decisions.AllowsOf("s1").Single().State, decisions.AllowsOf("s1").Single().GoesOnAt));

        decisions.AllowMoved("d2", SupervisorState.Overruled, At.AddMinutes(1));
        decisions.AllowMoved("d2", SupervisorState.WentOn, At.AddMinutes(6));

        Assert.Equal(SupervisorState.Overruled, decisions.AllowsOf("s1").Single().State);
    }

    [Fact]
    public void A_restart_closes_a_held_allow_stale_and_leaves_a_went_on_one()
    {
        Decisions().AllowKept(Allowed("d3", SupervisorAllowShapes.Steward, "s1", "steward", landsAt: At.AddMinutes(5)), At);
        Decisions().AllowKept(Allowed("d4", SupervisorAllowShapes.Widening, "s1", "standing"), At);

        var restarted = Decisions();
        restarted.SweepAtStart(At.AddMinutes(1));

        var read = restarted.AllowsOf("s1").ToDictionary(each => each.Id);
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.Restarted), (read["d3"].State, read["d3"].Why));
        Assert.Equal(SupervisorState.WentOn, read["d4"].State);
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
