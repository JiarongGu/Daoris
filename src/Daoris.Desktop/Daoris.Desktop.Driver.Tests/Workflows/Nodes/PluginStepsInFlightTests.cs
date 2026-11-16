using System.Collections.Immutable;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin step in flight is an attempt its plugin performs that is recorded, sent or unknown on a record whose run goes on: what an
/// update's drain waits on beside the sessions and the turns. Settled and held attempts, Daoris's own kinds and an ended run's
/// attempts are none; the home's records are read as the runtime keeps them.
/// </summary>
public sealed class PluginStepsInFlightTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 16, 3, 53, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-steps-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static NodeAttempt Attempt(string node, string step, string state, int number = 1) =>
        new(node, number, step.Contains('/') ? step[..step.IndexOf('/')] : step,
            new WorkPerformCall("unit-1", step, 1, $"i-{node}-{number}", $"k-{node}-{number}", "r1", [], At.AddMinutes(15)), state)
        { RecordedAt = At };

    [Theory]
    [InlineData(AttemptStates.Recorded, true)]
    [InlineData(AttemptStates.Sent, true)]
    [InlineData(AttemptStates.Unknown, true)]
    [InlineData(AttemptStates.Settled, false)]
    [InlineData(AttemptStates.Held, false)]
    public void A_plugin_s_attempt_is_in_flight_until_it_settles_or_is_held_for_the_person(string state, bool inFlight) =>
        Assert.Equal(inFlight, PluginStepsInFlight.InFlight(Attempt("release", "acme.deploy/release", state)));

    [Fact]
    public void Daoris_s_own_kinds_are_no_plugin_step()
    {
        Assert.False(PluginStepsInFlight.InFlight(Attempt("gates", NodeKinds.Check, AttemptStates.Sent)));
        Assert.False(PluginStepsInFlight.InFlight(Attempt("close", NodeKinds.QuestClose, AttemptStates.Unknown)));
        Assert.False(PluginStepsInFlight.InFlight(Attempt("ask", NodeKinds.Request, AttemptStates.Recorded)));
    }

    /// <summary>A run's record as the runtime keeps one: each attempt's node holding a status that names it.</summary>
    private static NodeRecord Run(string run, RunEnd? ended, params NodeAttempt[] attempts)
    {
        var unit = RunUnits.IdOf(run);
        return new NodeRecord(unit)
        {
            Statuses = attempts.ToImmutableSortedDictionary(
                attempt => attempt.Node, attempt => new NodeStatus(unit, attempt.Node, NodeStates.Active) { Attempt = attempt.Attempt, Tries = 1 },
                StringComparer.Ordinal),
            Attempts = attempts,
            Ended = ended,
        };
    }

    [Fact]
    public void The_home_s_records_count_each_run_s_plugin_steps_in_flight_and_none_of_a_run_that_ended()
    {
        var records = new NodeRecordFiles(_home, () => true);
        records.Write(Run("r1", null,
            Attempt("deploy", "acme.deploy/deploy", AttemptStates.Settled),
            Attempt("release", "acme.deploy/release", AttemptStates.Sent),
            Attempt("gates", NodeKinds.Check, AttemptStates.Sent)));
        records.Write(Run("r2", null, Attempt("release", "acme.deploy/release", AttemptStates.Unknown)));
        records.Write(Run("r3", new RunEnd("q3", At), Attempt("release", "acme.deploy/release", AttemptStates.Sent)));

        Assert.Empty(records.All().Unreadable);
        Assert.Equal(2, PluginStepsInFlight.Count(_home));

        records.Write(Run("r1", null, Attempt("release", "acme.deploy/release", AttemptStates.Settled) with { Outcome = "reached" }));

        Assert.Equal(1, PluginStepsInFlight.Count(_home));
    }

    [Fact]
    public void A_home_with_no_records_has_no_plugin_step_in_flight() =>
        Assert.Equal(0, PluginStepsInFlight.Count(_home));
}
