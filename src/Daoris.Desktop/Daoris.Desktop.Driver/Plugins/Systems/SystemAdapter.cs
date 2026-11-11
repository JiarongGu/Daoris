using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A system's adapter, over its plugin's channel: the plan reads a session's intended changes against the system as it stands and
/// keeps the typed operations as the record's next plan; the apply hands a reviewed plan to the adapter, which performs each operation
/// under its precondition, and keeps what it came to. A system whose <paramref name="guard"/> is <see cref="SystemPreconditions.None"/>
/// is applied by <see cref="SystemReadBack"/> instead. Nothing here changes the system but the apply.
/// </summary>
public sealed class SystemAdapter(IHookChannel channel, SystemPlace place, SystemPlans plans, Func<DateTimeOffset> clock, string guard = SystemPreconditions.Etag)
{
    /// <summary>
    /// <paramref name="record"/>'s turn's change set: <paramref name="intents"/> typed by the adapter (<see cref="HookPoints.PlanRead"/>), each
    /// with the version it read, kept as the record's next plan.
    /// </summary>
    public async Task<SystemChangeSet> PlanAsync(string record, IReadOnlyList<SystemIntent> intents, CancellationToken ct)
    {
        var answer = await channel.SystemAsync(HookPoints.PlanRead, SystemWire.Plan(place.System, intents), ct).ConfigureAwait(false);
        var operations = SystemWire.ReadPlan(answer) ?? throw NotPlanned(place, answer);
        return plans.Keep(record, place.ToString(), operations, clock());
    }

    /// <summary>
    /// <paramref name="plan"/>, reviewed, applied by the adapter (<see cref="HookPoints.ApplyPerform"/>) under each operation's precondition, and
    /// what it came to kept beside it: <c>applied</c>; <c>unchanged</c>; <c>stale</c>, naming each stale resource's present version, so the
    /// work plans again; <c>changed</c> while applying, for a system guarded by nothing; or <c>failed</c> in the adapter's words.
    /// </summary>
    public async Task<SystemApplied> ApplyAsync(string record, SystemChangeSet plan, CancellationToken ct)
    {
        SystemApplied applied;
        if (guard == SystemPreconditions.None)
        {
            applied = await new SystemReadBack(channel, place).ApplyAsync(plan, ct).ConfigureAwait(false);
        }
        else
        {
            var answer = await channel.SystemAsync(HookPoints.ApplyPerform, SystemWire.Apply(place.System, plan), ct).ConfigureAwait(false);
            applied = SystemWire.ReadApplied(answer) ?? throw NotApplied(place, answer);
        }

        plans.Applied(record, plan.Plan, applied);
        return applied;
    }

    internal static DriverException NotPlanned(SystemPlace place, JsonElement answer) =>
        new($"plugin `{place.Plugin}` answered `{HookPoints.PlanRead}` with {answer.GetRawText()}, which is not a change set — "
            + "`{ \"operations\": [{ \"resource\", \"op\": \"create\" | \"update\" | \"delete\", \"before\", \"after\", \"precondition\": { \"kind\", \"version\" } }] }`.");

    internal static DriverException NotApplied(SystemPlace place, JsonElement answer) =>
        new($"plugin `{place.Plugin}` answered `{HookPoints.ApplyPerform}` with {answer.GetRawText()}, which is not what an apply came to — "
            + "`{ \"state\": \"applied\" | \"unchanged\" | \"stale\" | \"failed\", \"applied\": [ … ] }`, a stale one naming `refused` and each resource's `present` version.");
}
