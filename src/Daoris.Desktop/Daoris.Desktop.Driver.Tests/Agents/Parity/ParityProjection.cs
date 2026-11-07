namespace Daoris.Desktop.Driver.Tests;

/// <summary>How a field in the projection is compared across the two paths, never dropped.</summary>
internal enum ParityCorrespondence
{
    /// <summary>A provenance (<c>AgentError.By</c> and the log's <c>by</c>, a usage reading's source and reader): by the door's relation.</summary>
    Provenance,

    /// <summary>A generated id: by the order of its first appearance, so one renaming maps every id of one path onto the other's.</summary>
    GeneratedId,

    /// <summary>A time: the same instant, or the same interval from the run's start; a named library clock within its interval.</summary>
    Time,

    /// <summary>The library clock a time is read on: named on the layer path alone, and only a clock the projection names.</summary>
    Clock,
}

/// <summary>
/// The projection (the agent-lyntai design §6.3 point 3): the fields that name the path, fixed here before any move and never grown inside
/// one. Every other field is compared exactly and in order: semantic outcomes, payloads, the order of writes and of lines. A recovery, a
/// limit's end, a delivery, an access disposition, what is hit and a write's place are never here, so no correspondence lets one change.
/// A field a move would need added is a correction or a request before the move.
/// </summary>
internal static class ParityProjection
{
    public const string By = "by";

    public const string Source = "source";

    public const string Reader = "reader";

    public const string Id = "id";

    /// <summary>
    /// The lineage token a run stamps on each reading: issued fresh for each process by the lineage, so a generated id whichever path ran.
    /// A run not held by its executor stamps none, and a null against a token is a difference.
    /// </summary>
    public const string Process = "process";

    public const string At = "at";

    public const string Until = "until";

    public const string Clock = "clock";

    /// <summary>The usage source's own clock, which no seam reaches (Lyntai's <c>CliUsageSource</c>).</summary>
    public const string UsageSourceClock = "lyntai:usage-source";

    /// <summary>The Claude reader's clock, against which a relative reset is read.</summary>
    public const string ClaudeReaderClock = "lyntai:claude-reader";

    public static IReadOnlyDictionary<string, ParityCorrespondence> Fields { get; } = new Dictionary<string, ParityCorrespondence>
    {
        [By] = ParityCorrespondence.Provenance,
        [Source] = ParityCorrespondence.Provenance,
        [Reader] = ParityCorrespondence.Provenance,
        [Id] = ParityCorrespondence.GeneratedId,
        [Process] = ParityCorrespondence.GeneratedId,
        [At] = ParityCorrespondence.Time,
        [Until] = ParityCorrespondence.Time,
        [Clock] = ParityCorrespondence.Clock,
    };

    /// <summary>Each library clock no seam reaches, with the interval a time read on it is held to.</summary>
    public static IReadOnlyDictionary<string, TimeSpan> LibraryClocks { get; } = new Dictionary<string, TimeSpan>
    {
        [UsageSourceClock] = TimeSpan.FromSeconds(5),
        [ClaudeReaderClock] = TimeSpan.FromSeconds(5),
    };
}

/// <summary>One pair a door's relation permits: a Daoris provenance and the layer's that stands for it.</summary>
internal sealed record ParityPair(string Daoris, string Layer)
{
    public override string ToString() => $"({Daoris}, {Layer})";
}

/// <summary>
/// A door's correspondence for its provenances (the agent-lyntai design §6.3, round 4): the set of pairs it permits, neither one to one nor
/// a function, since the two sides differ in grain both ways. Recorded in the moving row's commit beside its retained tables.
/// </summary>
internal sealed record ParityRelation(IReadOnlyList<ParityPair> Pairs)
{
    public bool Permits(ParityPair pair) => Pairs.Contains(pair);

    /// <summary>Whether some pair names <paramref name="layer"/>: every layer entry the door's cases produce must.</summary>
    public bool Names(string layer) => Pairs.Any(pair => pair.Layer == layer);
}
