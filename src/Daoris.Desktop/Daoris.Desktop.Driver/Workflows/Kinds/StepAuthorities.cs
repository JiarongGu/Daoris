namespace Daoris.Driver;

/// <summary>The run's own step (<see cref="StepAuthority"/>) of an attempt a kind keeps on its unit's record.</summary>
internal static class StepAuthorities
{
    /// <summary>The attempt by its invocation; a run's unit names its run.</summary>
    public static StepAuthority Of(string unit, NodeAttempt attempt) =>
        new(RunUnits.Is(unit) ? unit[RunUnits.Prefix.Length..] : unit, attempt.Node, attempt.Attempt, attempt.Call.Invocation);
}
