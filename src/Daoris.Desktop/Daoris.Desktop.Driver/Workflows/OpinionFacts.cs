namespace Daoris.Driver;

/// <summary>
/// What one judgment of an owed opinion found: the quests it sits, each with its sentence (<see cref="Snapshot.Opinions"/>);
/// the landing opinion's gate, as the attention band reads it; the pass it found owed, for a look to begin; when what it read turns
/// over unmoved (<see cref="OpinionGateLook.Turns"/>); and whether its gate or the review beside it could not be read, kept unstamped.
/// </summary>
internal sealed record OpinionFact(IReadOnlyDictionary<string, OpinionSit> Sits, OpinionJudged? Judged, OpinionAct? Act) : IOpinionFact
{
    public IReadOnlyList<DateTimeOffset> Turns { get; init; } = [];

    public bool Unread { get; init; }

    public bool Begun { get; init; }

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
    public bool Early { get; init; }

    public string? Ended { get; init; }
}
