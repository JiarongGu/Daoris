namespace Daoris.Driver;

/// <summary>The quests' departures the risk policy passes, each given the policy's yes at the look (<see cref="QuestDepartures"/>).</summary>
[LookPass("departures", LookPhase.After, "step-settles")]
internal sealed class QuestDeparturesLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct) =>
        round.Events.AddRange(await QuestDepartures.AnswerAsync(wiring.Home, wiring.Config, round.State.Snapshot.Quests,
            (quest, pass, token) => wiring.Service.AcceptDepartureAsync(quest, token, policy: pass), wiring.Clock.GetUtcNow(), ct).ConfigureAwait(false));
}
