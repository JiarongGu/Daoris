namespace Daoris.Driver;

/// <summary>
/// The run's own step a flow works for: its run, node and attempt, and the attempt's invocation where its kind keeps one, else its door's
/// start key. It has no setter: a flow carries one only inside <see cref="RunAsync(StepAuthority, Func{Task})"/>, whose callers are where a
/// kind's work begins (the kit's door runner, <see cref="IntegrateStart"/> around each attempt's answer, <see cref="WorkPerformer"/> for a
/// <c>check</c> and a plugin step, and the hand-off for its resolver), so a callee never sets one for its caller.
/// </summary>
public sealed record StepAuthority(string Run, string Node, int Attempt, string Invocation)
{
    private static readonly AsyncLocal<StepAuthority?> Flowing = new();

    /// <summary>The step this flow works for; null in a flow no kind's work began in.</summary>
    public static StepAuthority? Current => Flowing.Value;

    /// <summary>A door kind's start, which keeps no attempt journal: its invocation is the start's own key.</summary>
    public static StepAuthority OfDoor(string run, string node, int attempt) => new(run, node, attempt, $"door/{run}/{node}/{attempt}");

    /// <summary>An attempt a kind keeps on <paramref name="unit"/>'s record, by its invocation; a run's unit names its run.</summary>
    public static StepAuthority Of(string unit, NodeAttempt attempt) =>
        new(RunUnits.Is(unit) ? unit[RunUnits.Prefix.Length..] : unit, attempt.Node, attempt.Attempt, attempt.Call.Invocation);

    /// <summary><paramref name="work"/> run under <paramref name="authority"/>; the caller's flow is as it was once it returns.</summary>
    internal static async Task RunAsync(StepAuthority authority, Func<Task> work)
    {
        Flowing.Value = authority;
        await work().ConfigureAwait(false);
    }

    /// <inheritdoc cref="RunAsync(StepAuthority, Func{Task})"/>
    internal static async Task<T> RunAsync<T>(StepAuthority authority, Func<Task<T>> work)
    {
        Flowing.Value = authority;
        return await work().ConfigureAwait(false);
    }
}
