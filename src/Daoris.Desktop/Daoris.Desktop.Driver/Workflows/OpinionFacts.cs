namespace Daoris.Driver;

/// <summary>
/// What one judgment of an owed opinion found (PARALLEL1g): the quests it sits, each with its sentence (<see cref="Snapshot.Opinions"/>);
/// the landing opinion's gate, as the attention band reads it (XAGENT1g3); and the pass it found owed, for a look to begin.
/// </summary>
internal sealed record OpinionFact(IReadOnlyDictionary<string, OpinionSit> Sits, OpinionJudged? Judged, OpinionAct? Act) : IOpinionFact
{
    IEnumerable<string> IOpinionFact.SatQuests => Sits.Keys;

    IOpinionAct? IOpinionFact.Act => Act;

    IOpinionFact IOpinionFact.WithoutAct() => this with { Act = null };
}

/// <summary>
/// A pass an owed opinion's judgment found owed (PARALLEL1g): the look's own ask at <paramref name="Occasion"/>, or the person's
/// (<paramref name="Person"/>, taken from the gate as it is begun), under the chain's claim, with what its ask reads.
/// </summary>
internal sealed record OpinionAct(
    OpinionDue Due, string Key, string Claim, IReadOnlyList<QuestView> Chain, string Occasion, bool Person, WorkflowProcess Process) : IOpinionAct;
