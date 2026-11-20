namespace Daoris.Driver;

/// <summary>
/// The working sessions' settles of the steps handed to them (<see cref="StepSettleAsks"/>), taken at each look as the agent each session's
/// start ran, a refusal said to its session as its next turn; the next pass applies what is kept.
/// </summary>
[LookPass("step-settles", LookPhase.After, "through-notices")]
internal sealed class StepSettlesLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var taken = await StepSettleAsks.TakeAsync(
                new NodeRecordFiles(wiring.Home), wiring.Processes.Delegates.AgentOf,
                async (session, text, key, token) => (await WordsHead.FollowAsync(session, at => wiring.Service.SayAsync(at, text, ct: token, from: DaorisWords.From, key: key))
                    .ConfigureAwait(false)).Said,
                () => wiring.Clock.GetUtcNow(), ct, wiring.RunWorkers)
            .ConfigureAwait(false);
        if (taken.Count > 0) round.Events.Add($"{taken.Count(each => each.Done)} of {taken.Count} step settle(s) from working sessions kept.");
    }
}
