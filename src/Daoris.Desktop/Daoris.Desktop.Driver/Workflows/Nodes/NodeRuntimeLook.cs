namespace Daoris.Driver;

/// <summary>
/// One pass's reading: the standing choices, whether it is the look's deadline (every governed unit judged) or a wake (only the units its
/// answers concern), and the two reads it makes only once a graph naming a plugin's performed node governs something.
/// </summary>
public sealed record NodeRuntimeLook(DriverConfig Config, bool Whole)
{
    /// <summary>The units and their checkouts: read at the look's deadline, and at a wake only when none is read yet.</summary>
    public Func<CancellationToken, Task<NodeUnitsRead>> Units { get; init; } = _ => Task.FromResult(new NodeUnitsRead([], new Dictionary<string, string>()));

    /// <summary>The plugins' steps as the catalogue declares them (<see cref="PluginManifest.Steps"/>): a performed node's contract.</summary>
    public Func<IReadOnlyList<NodeDeclaration>> Steps { get; init; } = () => [];

    /// <summary>
    /// The resources a plugin's manifest declares (<see cref="PluginManifest.Resources"/>), by its id: a receipt's role. Null for a plugin the
    /// catalogue does not contribute, whose receipts are taken as undeclared and never cleaned by themselves.
    /// </summary>
    public Func<string, IReadOnlyList<PluginResource>?> Resources { get; init; } = _ => null;

    /// <summary>
    /// Each named branch's tip in one checkout, read fresh: one read of the checkout's refs per pass (<see cref="NodeTips.ReadAsync"/>), never
    /// a cached unit's revision. A branch it could not read is absent, and establishes nothing.
    /// </summary>
    public Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> Tips { get; init; } =
        (_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    /// <summary>
    /// Each named branch's remote-tracking ref in one checkout (<see cref="NodeTips.RemotesAsync"/>): what <c>pushed</c> reads, once per
    /// checkout per pass, and only where a <c>pushed</c> node waits on a landing a plugin answered it pushed. Empty where the ref is not
    /// there; a branch it could not read is absent, and its node stays unknown.
    /// </summary>
    public Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> Remotes { get; init; } =
        (_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    /// <summary>
    /// The second opinion's and the look's gates for a branch unit's work as a landing door reads them (<see cref="OpinionGate.JudgeAsync"/>
    /// and <see cref="ReviewGate"/>'s live read), what its <c>opinion</c> and <c>look</c> nodes observe: read once a pass for a unit, and only
    /// where one of them waits. Either is null where it is not read, and its node waits. Judged at the commit given, where one is (a run's
    /// branch at the tip the pass read, which its gate nodes bind); null judges the unit's newest tree's <c>HEAD</c>.
    /// </summary>
    public Func<WorkUnit, string?, CancellationToken, Task<(OpinionGateState? Opinion, ReviewGateState? Review)>> Gates { get; init; } =
        (_, _, _) => Task.FromResult<(OpinionGateState?, ReviewGateState?)>((null, null));

    /// <summary>
    /// What the line and the person's branches hold of a run's branch at the commit its pass read, what its <c>content.holds</c> observes:
    /// read once a pass, and only where that node waits. Null where it is not read at that commit, and the node waits.
    /// </summary>
    public Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> Content { get; init; } = (_, _, _) => Task.FromResult<HoldContent?>(null);

    /// <summary>
    /// The named workflow's version a quest unit's run is bound to (the decoder's read for a run, through <see cref="WorkflowRunNodes.VersionOf"/>),
    /// given the quests the pass read for its chain; null where its run follows Current or has no binding. A unit bound to one is governed by
    /// that version, never by Current's graph (<see cref="NodeRuntime.BoundIn"/> reads the home's).
    /// </summary>
    public Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?> Bound { get; init; } = (_, _) => null;

    /// <summary>The units a press waits on, by id: judged first in the turn, however many units the pass governs.</summary>
    public IReadOnlySet<string> Asked { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Told each unit's id once its judgement is kept, so a press waiting on it reads it before the pass ends.</summary>
    public Action<string> Judged { get; init; } = _ => { };
}
