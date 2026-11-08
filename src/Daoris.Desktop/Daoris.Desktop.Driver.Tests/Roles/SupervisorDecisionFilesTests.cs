using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A decision read from its files after a restart: a state line a crash tore hides nothing the lines before it say.</summary>
public sealed class SupervisorDecisionFilesTests : IDisposable
{
    private const string Session = "a1b2c3d4";
    private const string Repository = "Reports";
    private static readonly DateTimeOffset Kept = DateTimeOffset.Parse("2026-10-24T09:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "supervisor-files", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorDecisionFilesTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Appointed : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            place.Repository == Repository ? new("g-1", null) : DecisionAdmitted.Refused("not-appointed");
    }

    private QuestionSupervisorDecisions Decisions() => new(_home, new Appointed());

    private static SupervisedPark Park() => new(
        Repository, true, true,
        new ParkQuestion("q-1", Session, "Which report?", [new("The monthly one", "I push."), new("The weekly one", "I write.")], 1, null, Kept.AddMinutes(-1), "It is asked for."),
        false, false, false, false);

    private static SupervisorAnswer Answer(string choice) =>
        new(Session, "q-1", "g-1", choice, [new("The decision names it.", "D201 point 3")], new("terminal", "pwsh"));

    [Fact]
    public async Task ROLESREVIEW2h_a_torn_state_line_does_not_hide_a_decision_from_the_restart_that_closes_it()
    {
        var kept = await Decisions().KeepAsync(Answer("The monthly one"), Park(), Kept);
        var states = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, Session + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".states");
        File.AppendAllText(states, "{\"state\":\"over");

        var after = Decisions();

        Assert.Equal([Session], after.SweepAtStart(Kept.AddHours(1)));
        Assert.False((await after.KeepAsync(Answer("The weekly one"), Park(), Kept.AddHours(1))).Kept);
    }
}
