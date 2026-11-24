using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's held decisions and their overrule windows are a table a restart reads back: an agent's held
/// answer keeps its answer and what is left of its window across a restart, one whose window closed meanwhile applies at the next look,
/// one the owner overruled stays overruled, and a terminal's hold, which ends with its run, goes stale as before.
/// </summary>
public sealed class HeldDecisionsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "held-decisions", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T10:00:00Z");

    private static readonly string Agent = AgentHold.Generation("daoris-landing", 3);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>The role <c>daoris-landing</c> at its revision 3, held by an agent and granted every act, with a ten-minute window.</summary>
    private sealed class Holds(string generation) : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            new(generation, null) { Window = TimeSpan.FromMinutes(10), Role = "daoris-landing" };

        public PlaceRiskNow RiskAt(RolePlace place) => new(PlaceRisks.Low, "repository/reports", []);

        public string? GoAheadOwners(RolePlace place, string kind, string on) => RiskAskHolds.GoAheadOwners(DriverConfig.Parse("{}"), place, kind, on);
    }

    private static readonly SupervisorHolder Codex = SupervisorHolders.OfAgent(new("daoris-landing", 3, "codex-acp", "gpt-5", "high", "gpt-5", "read-only", "t1"));

    private static readonly SupervisorSubject Subject = new(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged);

    private static SupervisorDoorAnswer Staged(string generation) => new(
        Subject, "reports", generation, "keep", SupervisorDoorActs.Keep, [new SupervisorReason("The staged rows restate the area.", "the session's memory")], Codex)
    {
        Pin = "op-1,op-2",
    };

    private QuestionSupervisorDecisions Decisions(string generation = "") =>
        new(_home, new Holds(generation.Length == 0 ? Agent : generation)) { LandingApplied = LandingApplied.Of(_home) };

    private static DoorRelease Posting(List<SupervisorDoorDue> posted) => new(
        (_, _) => Task.FromResult<string?>(null),
        (due, _) =>
        {
            posted.Add(due);
            return Task.FromResult<string?>(null);
        },
        (_, _) => Task.FromResult<bool?>(true));

    /// <summary>🔴 A restart mid-window keeps the answer and its remaining window: nothing goes on before it closes, and it goes on after.</summary>
    [Fact]
    public async Task A_restart_mid_window_keeps_the_answer_and_its_remaining_window()
    {
        var kept = await Decisions().DoorAsync(Staged(Agent), null, At);
        Assert.True(kept.Kept, kept.Why);

        var after = Decisions();
        Assert.Empty(after.SweepAtStart(At.AddMinutes(4)));
        var held = Assert.Single(after.DoorsOf("s1"));
        Assert.Equal((kept.Decision, SupervisorState.Held, At.AddMinutes(10)), (held.Id, held.State, held.GoesOnAt));

        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await after.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(6)));
        Assert.Empty(posted);
        Assert.Equal([SupervisorState.WentOn], await after.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Equal(kept.Decision, Assert.Single(posted).Id);
    }

    /// <summary>🔴 A held go-ahead answer is read back after a restart as a memory keep is, its pin kept, and goes out when its window closes.</summary>
    [Fact]
    public async Task A_held_go_ahead_answer_is_read_back_after_a_restart()
    {
        var pin = new GoAheadPin(2, "write", "staging", "the dashboard's configuration");
        var answer = new SupervisorDoorAnswer(SupervisorSubject.GoAhead("s1", "a7", 2), "reports", Agent, SupervisorKinds.Answer, SupervisorDoorActs.Approved,
            [new SupervisorReason("The write is the plan's own step on staging.", "the ask's plan")], Codex) { Pin = pin.Said };
        var kept = await Decisions().DoorAsync(answer, null, At);
        Assert.True(kept.Kept, kept.Why);

        var after = Decisions();
        Assert.Empty(after.SweepAtStart(At.AddMinutes(4)));
        var held = Assert.Single(after.DoorsOf("s1"));
        Assert.Equal((kept.Decision, SupervisorState.Held), (held.Id, held.State));

        var posted = new List<SupervisorDoorDue>();
        Assert.Equal([SupervisorState.WentOn], await after.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Equal(pin.Said, Assert.Single(posted).Pin);
    }

    /// <summary>🔴 A held answer whose window closed during the restart applies at the next look, the admission asked again.</summary>
    [Fact]
    public async Task A_held_answer_whose_window_closed_during_the_restart_applies_at_the_next_look()
    {
        var landing = new SupervisorLanding("s2", "q7", "review", 1, Agent, SupervisorLandingActs.Accept,
            [new SupervisorReason("The diff is the quest's brief.", "the diff")], Codex);
        var before = Decisions();
        Assert.True((await before.DoorAsync(Staged(Agent), null, At)).Kept);
        Assert.True((await before.LandingAsync(landing, "reports", null, At)).Kept);

        var after = Decisions();
        after.SweepAtStart(At.AddMinutes(30));
        var posted = new List<SupervisorDoorDue>();
        var handed = new List<SupervisorLandingDue>();

        Assert.Equal([SupervisorState.WentOn], await after.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(30)));
        Assert.Equal([SupervisorState.Released], await after.ReleaseLandingsAsync(new LandingRelease(_ => Task.FromResult<string?>(null), due =>
        {
            handed.Add(due);
            return null;
        }), At.AddMinutes(30)));
        Assert.Equal(("reports", Agent, "The diff is the quest's brief."), (handed[0].Repository, handed[0].Generation, handed[0].Words));
    }

    /// <summary>🔴 One the owner overruled stays overruled across a restart: nothing is read back, and the subject stays theirs.</summary>
    [Fact]
    public async Task One_the_owner_overruled_stays_overruled_across_a_restart()
    {
        var before = Decisions();
        var kept = await before.DoorAsync(Staged(Agent), null, At);
        await before.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(2));

        var after = Decisions();
        after.SweepAtStart(At.AddMinutes(4));

        Assert.Empty(after.DoorsOf("s1"));
        Assert.False(after.RefersDoor(Subject));
        Assert.Equal(SupervisorWhy.Overruled, (await after.DoorAsync(Staged(Agent), null, At.AddMinutes(5))).Why);
    }

    /// <summary>A terminal's hold ends with its run, so its held answer goes stale at the restart and the table keeps no row of it.</summary>
    [Fact]
    public async Task A_terminals_held_answer_goes_stale_at_the_restart_and_leaves_no_row()
    {
        Assert.True((await Decisions("g-1").DoorAsync(Staged("g-1"), null, At)).Kept);
        Assert.Single(new HeldDecisions(_home).All());

        var after = Decisions("g-1");
        Assert.Equal(["s1"], after.SweepAtStart(At.AddMinutes(4)));

        Assert.Empty(after.DoorsOf("s1"));
        Assert.Empty(new HeldDecisions(_home).All());
    }
}
