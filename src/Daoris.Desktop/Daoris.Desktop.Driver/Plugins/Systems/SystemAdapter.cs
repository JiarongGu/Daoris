namespace Daoris.Driver;

/// <summary>
/// A system's adapter, over its plugin's channel: the plan reads a session's intended changes against the system as it stands and
/// keeps the typed operations as the record's next plan; the apply hands a reviewed plan to the adapter, which performs each operation
/// under its precondition, and keeps what it came to. Nothing here changes the system but the apply.
/// </summary>
public sealed class SystemAdapter(IHookChannel channel, SystemPlace place, SystemPlans plans, Func<DateTimeOffset> clock)
{
    /// <summary>
    /// <paramref name="record"/>'s turn's change set: <paramref name="intents"/> typed by the adapter (<see cref="HookPoints.PlanRead"/>), each
    /// with the version it read, kept as the record's next plan.
    /// </summary>
    public async Task<SystemChangeSet> PlanAsync(string record, IReadOnlyList<SystemIntent> intents, CancellationToken ct)
    {
        var answer = await channel.SystemAsync(HookPoints.PlanRead, SystemWire.Plan(place.System, intents), ct).ConfigureAwait(false);
        var operations = SystemWire.ReadPlan(answer)
            ?? throw new DriverException($"plugin `{place.Plugin}` answered `{HookPoints.PlanRead}` with {answer.GetRawText()}, which is not a change set — "
                + "`{ \"operations\": [{ \"resource\", \"op\": \"create\" | \"update\" | \"delete\", \"before\", \"after\", \"precondition\": { \"kind\", \"version\" } }] }`.");
        return plans.Keep(record, place.ToString(), operations, clock());
    }

    /// <summary>
    /// <paramref name="plan"/>, reviewed, applied by the adapter (<see cref="HookPoints.ApplyPerform"/>) under each operation's precondition, and
    /// what it came to kept beside it: <c>applied</c>; <c>stale</c>, naming each stale resource's present version, so the work plans again;
    /// or <c>failed</c> in the adapter's words.
    /// </summary>
    public async Task<SystemApplied> ApplyAsync(string record, SystemChangeSet plan, CancellationToken ct)
    {
        var answer = await channel.SystemAsync(HookPoints.ApplyPerform, SystemWire.Apply(place.System, plan), ct).ConfigureAwait(false);
        var applied = SystemWire.ReadApplied(answer)
            ?? throw new DriverException($"plugin `{place.Plugin}` answered `{HookPoints.ApplyPerform}` with {answer.GetRawText()}, which is not what an apply came to — "
                + "`{ \"state\": \"applied\" | \"none\" | \"stale\" | \"failed\", \"applied\": [ … ] }`, a stale one naming `refused` and each resource's `present` version.");
        plans.Applied(record, plan.Plan, applied);
        return applied;
    }
}
