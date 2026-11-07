namespace Daoris.Driver;

/// <summary>
/// The supervisor's decisions at a look, before anything plans: each decision not yet final compared, settled and released first, so
/// the release's own word is recorded before anything judges it; then a word claiming the supervisor that Daoris did not attribute is
/// taken back, and left out of the snapshot the plan reads.
/// </summary>
/// <remarks>
/// It reads the look's answers-alone bound into the round's state, its rules read once: each release asks the bound again, the
/// service's records read once a look and only where a release is due, and the look holds by the same bound after it.
/// </remarks>
[LookPass("supervisor", LookPhase.Read, "paused")]
internal sealed class SupervisorLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var service = wiring.Service;
        SupervisorTakeBack takeBack = async (session, word, token) =>
            (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done;
        var undecided = round.State.Snapshot;
        var alone = round.State.Alone = AnswersAloneHold.For(wiring.Events, wiring.Marks, wiring.Home, wiring.Supervisor.Admits);

        // CONTEXTDRIVE1j: each go-on the supervisor kept on a referred point, its window past, released through the count's door between turns.
        var looked = undecided.LastRun.Values.Concat(undecided.NamedByReopen.Values).DistinctBy(record => record.Session, StringComparer.Ordinal)
            .ToDictionary(record => record.Session, StringComparer.Ordinal);
        await wiring.Supervisor.ReleaseCoursesAsync(
            ReferredPoints.For(wiring.Home, wiring.Events, wiring.Marks).Line(looked.GetValueOrDefault, session => looked.GetValueOrDefault(session)?.Running == true),
            DateTimeOffset.UtcNow, [SupervisorRules.AnswersAlone], ct).ConfigureAwait(false);
        IReadOnlyDictionary<string, PriorSession>? lines = null;
        SupervisorBound bound = async (session, token) =>
        {
            if (alone.AtBound(session, lines ??= await AnswersAloneHold.RecordsAsync(service, token).ConfigureAwait(false))) return SupervisorWhy.SupervisorBound;
            var counts = (await LineCountsRead.FactsAsync(service, wiring.Home, session, token).ConfigureAwait(false)).Counts;
            return SupervisorRuleHold.Why(atBound: false, counts);
        };
        round.State.Snapshot = await wiring.Stages.TimeAsync("supervisor", async () => await SupervisorQuarantine.LookAsync(
            await SupervisorQuarantine.DecideAsync(undecided, wiring.Supervisor, SayAsync, takeBack, DateTimeOffset.UtcNow, ct, bound)
                .ConfigureAwait(false),
            wiring.Supervisor, takeBack, wiring.Log, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// The release through the service's say: no text, its decision, question and choice, the receipt as the say's key. Kept answers
    /// the word's id; a refusal its word; a lost response throws, which the release keeps held to repeat.
    /// </summary>
    private async Task<SupervisorSaid> SayAsync(string session, string decision, string question, string choice, string key, CancellationToken ct)
    {
        var said = await wiring.Service.SayAsync(
            session, "", ct: ct, from: SupervisorHanded.From, key: key, supervised: (decision, question, choice)).ConfigureAwait(false);
        return said.Kept && said.Word?.Id is { } word ? new SupervisorSaid(word, null) : new SupervisorSaid(null, said.Refusal ?? "unsaid");
    }
}
