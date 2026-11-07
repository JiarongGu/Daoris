namespace Daoris.Driver;

/// <summary>
/// One planned start's run, and its claim on the record its words wait on let go once it has ended, however it ended. The plugins'
/// say comes first, before a start costs anything: the first hold in catalogue order is the quest's reason for sitting, exactly as a
/// dirty tree or a missing binary would be, and a plugin that could not decide holds too, naming itself. Each start asks on its own,
/// at once with the others, so one plugin slow to answer for one quest holds that start alone.
/// </summary>
/// <param name="registry">The registry as this look's snapshot answered it: what the session may reach across.</param>
/// <param name="goingOn">The session each quest's run goes on in, by quest, whose claim is let go with the run.</param>
internal sealed class ClaimedStart(IStartsWiring wiring, IReadOnlyList<RepoView> registry, Dictionary<string, string> goingOn)
{
    public async Task<StartRun> RunAsync(Consideration start, List<TrustHold> own, Action opened, CancellationToken ct)
    {
        try
        {
            if (wiring.Context.Hooks is { } considering)
            {
                var decision = await wiring.Stages.TimeAsync("consider", () => considering.ConsiderAsync(start, ct), $"#{start.Quest.Id}")
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                {
                    return new StartRun($"held  #{start.Quest.Id} → {start.Quest.To}: {decision.Reason}", Opened: false, Held: decision.Reason);
                }
            }

            // The tree the start opens goes on the driver's close before its record's open is asked, on no token.
            var fresh = NewTree();
            var run = await fresh.UntilOpenAsync(
                    () => wiring.Runner is { } runner ? runner(start, opened, ct) : wiring.RunAsync(start, registry, own, opened, fresh, ct), ct)
                .ConfigureAwait(false);

            // A start refused for its account's sign-in goes on under the next account the walk gives, once, in this start; where the
            // walk gives none (or the same), the first run's end stands.
            if (run.SignedOutOn is { } signedOut
                && await MovedAsync(start, again => wiring.RunAsync(
                    start, registry, own, () => wiring.Runs.Release(again.Holding), again.Fresh, ct, movedOff: signedOut, movedFrom: run.Ended?.Adapter), ct)
                    .ConfigureAwait(false) is { } movedOn)
            {
                return movedOn;
            }

            // The same, for a model its agent refused; a walk that gives nothing runnable leaves the first run's end standing.
            if (run.ModelRefusedOn is { } refusedOn
                && await MovedAsync(start, again => wiring.RunAsync(
                    start, registry, own, () => wiring.Runs.Release(again.Holding), again.Fresh, ct, movedOffModel: refusedOn), ct)
                    .ConfigureAwait(false) is { } movedOff)
            {
                return movedOff;
            }

            return run;
        }
        finally
        {
            if (goingOn.TryGetValue(start.Quest.Id, out var session)) wiring.Runs.WentOn(session);
        }
    }

    /// <summary>The reservation that keeps a look from planning a quest again between a start's refused end and its move to another account.</summary>
    public static StartReservation Moving(DriverConfig config, Consideration start) =>
        new(start.Quest.Id, null, start.Quest.To, config.OpensOwnTree(start.Quest.To) ? start.Entry?.Into?.Tree : null);

    private FreshTree NewTree() => new(path => wiring.Context.Trees.RemoveAsync(path));

    /// <summary>
    /// The start run again, held by a reservation that keeps a look from planning the quest again between the refused end and the move;
    /// its run when it opened, else null.
    /// </summary>
    private async Task<StartRun?> MovedAsync(Consideration start, Func<(StartReservation Holding, FreshTree Fresh), Task<StartRun>> run, CancellationToken ct)
    {
        var holding = Moving(wiring.Config, start);
        try
        {
            var again = NewTree();
            var moved = await again.UntilOpenAsync(() => run((holding, again)), ct).ConfigureAwait(false);
            return moved.Opened ? moved : null;
        }
        finally
        {
            wiring.Runs.Release(holding);
        }
    }
}
