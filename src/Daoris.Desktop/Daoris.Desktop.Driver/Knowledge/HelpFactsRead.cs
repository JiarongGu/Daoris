namespace Daoris.Driver;

/// <summary>
/// What is not read yet when the room is written: the repositories whose line is not judged, and whether the
/// sessions, the history, the work and the waits were not. Said <i>not read yet</i> where each is said, never a guess and never a wait.
/// </summary>
public sealed record HelpNotReadYet(IReadOnlyList<string> Repositories)
{
    /// <summary>Everything read.</summary>
    public static HelpNotReadYet None { get; } = new([]);

    public bool Sessions { get; init; }

    public bool History { get; init; }

    /// <summary>Whether any part of the work is never judged: the work is then read as none, as the whole is.</summary>
    public bool Work { get; init; }

    /// <summary>Whether what each line waits on is never judged.</summary>
    public bool Waits { get; init; }

    /// <summary>Whether anything is not read yet.</summary>
    public bool Any => Repositories.Count > 0 || Sessions || History || Work || Waits;
}

/// <summary>The room's reading of the facts (<see cref="HelpFacts.Read"/>): each part as last judged, null where never.</summary>
public sealed record HelpFactsRead(
    HelpMachineWhole Whole, IReadOnlyList<RepositoryLine> Lines, IKnowledgeSessionLook? Sessions, IReadOnlyList<HistoryReading>? History,
    HelpWork? Work, HelpNotReadYet NotReadYet)
{
    /// <summary>What each line waits on, as last judged; null where never.</summary>
    public HelpWaits? Waits { get; init; }
}

/// <summary>
/// What reads each of the room's facts, handed by the module that holds the service and the loop: each reads afresh
/// and answers its value, or null where it could not. The work's reads one part (<see cref="HelpFacts.ReadWorkAsync"/>).
/// </summary>
public sealed record HelpFactJudges(
    Func<CancellationToken, Task<HelpMachineWhole?>> Machine,
    Func<CancellationToken, Task<IKnowledgeSessionLook?>> Sessions,
    Func<CancellationToken, Task<IReadOnlyList<HistoryReading>?>> History,
    Func<HelpWorkPart, CancellationToken, Task<HelpWork?>> Work)
{
    /// <summary>A checkout's guess at its line, by its root: git's, asked once while its files stand, unless a test hands its own.</summary>
    public Func<string, CancellationToken, Task<string?>> Guess { get; init; } = KnowledgeWorkplace.Current.GuessAsync;

    /// <summary>
    /// One part of the sessions' trees: the module hands git's judgement of its home's trees
    /// (<see cref="HelpFacts.ReadTreesAsync"/>); none judged where a test standing in for the look hands none.
    /// </summary>
    public Func<HelpTreesPart, CancellationToken, Task<HelpTrees?>> Trees { get; init; } = (_, _) => Task.FromResult<HelpTrees?>(HelpTrees.None);

    /// <summary>
    /// One part of the clean-up's list: the module hands git's judgement of its home's branches by the kept answers
    /// (<see cref="HelpFacts.ReadSweepAsync"/>). None kept where a test hands none, so a slice over it stays not judged rather than
    /// say there is nothing to clean up.
    /// </summary>
    public Func<HelpSweepPart, CancellationToken, Task<IKnowledgeSweep?>> Sweep { get; init; } = (_, _) => Task.FromResult<IKnowledgeSweep?>(null);

    /// <summary>
    /// What each line waits on: the module hands the exchanges' one read over its home's files and the service's records; none waits
    /// where a test standing in for the records hands none, as the trees' parts.
    /// </summary>
    public Func<CancellationToken, Task<HelpWaits?>> Exchanges { get; init; } = _ => Task.FromResult<HelpWaits?>(HelpWaits.None);
}
