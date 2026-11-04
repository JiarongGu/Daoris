namespace Daoris.Driver;

/// <summary>
/// The second opinions owed (XAGENT1f, the second-agent design §2.1, §7, §8.1): asked and delivered beside the look, and the starts
/// they sit held. A pass finishes work already done, so it takes its slot before any quest's first start (D130). PARALLEL1g (D181
/// point 3): under the watch each is judged as its topic's judge beside the look, and the plan reads what each last found; one not
/// judged yet holds its chain. The facts' wake is taken first, so a judgment landing after this wakes the next look.
/// </summary>
[LookPass("opinions", LookPhase.Read, "reviews")]
internal sealed class OpinionsLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        wiring.Context.Runs.TakeFactsMoved();
        var judgedOpinions = new List<OpinionJudged>();
        var looked = round.State.Looked;
        var (opinionSits, passes) = await wiring.Stages.TimeAsync("opinions", () => wiring.Beside
            ? Task.FromResult(wiring.Gate.OpinionsBeside(looked, judgedOpinions, wiring.Stages, ct, round.Events))
            : wiring.Gate.OpinionsAsync(looked, round.Events, ct, judgedOpinions)).ConfigureAwait(false);
        round.State.Snapshot = round.State.Snapshot with { Opinions = opinionSits };
        round.State.Passes = passes;
        round.State.JudgedOpinions = judgedOpinions;
    }
}
