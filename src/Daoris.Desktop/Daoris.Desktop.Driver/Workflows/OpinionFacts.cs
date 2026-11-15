namespace Daoris.Driver;

/// <summary>
/// What one judgment of an owed opinion found: the quests it sits, each with its sentence (<see cref="Snapshot.Opinions"/>);
/// the landing opinion's gate, as the attention band reads it; and the pass it found owed, for a look to begin.
/// </summary>
internal sealed record OpinionFact(IReadOnlyDictionary<string, OpinionSit> Sits, OpinionJudged? Judged, OpinionAct? Act) : IOpinionFact
{
    IEnumerable<string> IOpinionFact.SatQuests => Sits.Keys;

    IOpinionAct? IOpinionFact.Act => Act;

    IOpinionFact IOpinionFact.WithoutAct() => this with { Act = null };
}

/// <summary>
/// A pass an owed opinion's judgment found owed: the look's own ask at <paramref name="Occasion"/>, or the person's
/// (<paramref name="Person"/>, taken from the gate as it is begun), under the chain's claim, with what its ask reads.
/// </summary>
internal sealed record OpinionAct(
    OpinionDue Due, string Key, string Claim, IReadOnlyList<QuestView> Chain, string Occasion, bool Person, WorkflowProcess Process) : IOpinionAct
{
    /// <summary>
    /// Asked before its cooling pass's reset: it goes only where the walk finds a holder that is ready (an account's cap does not hold the work
    /// for the others), else it waits for the reset as before and writes nothing.
    /// </summary>
    public bool Early { get; init; }

    /// <summary>The limited pass's opinion this ask goes on from, where the gate read one.</summary>
    public string? Ended { get; init; }
}
