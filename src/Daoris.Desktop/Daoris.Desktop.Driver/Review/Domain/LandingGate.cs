namespace Daoris.Driver;

/// <summary>
/// The whole landing gate a door hands <see cref="SessionTrees.LandAsync"/>: the second
/// opinion and the look, read together, refused in that order. The run's own part comes before the opinion, and the
/// landing step's own declarations after the look, all read from one process.
/// </summary>
public sealed record LandingGate(OpinionGateState Opinion, ReviewGateState Review) : IWorkplaceGate
{
    /// <summary>
    /// The run's own part: the process every part is read with, and what of the run's own holds the work. Null for a
    /// gate no door read one for.
    /// </summary>
    public IWorkflowGatePart? Workflow { get; init; }

    /// <summary>The process the gate is read with, which the landing lands by; null where none is read.</summary>
    public IWorkplaceProcess? Process => Workflow?.Process;

    /// <summary>
    /// The graph's part, for a run bound to a graph version: the run's record read, never
    /// evaluated, after the look's part and before the landing step's own. Where it lets the work go, <see cref="Tip"/> is the commit its
    /// <c>handed</c> is bound to, so every path lands that commit. Null for every other run.
    /// </summary>
    public IGraphGatePart? Graph { get; init; }

    /// <summary>
    /// The tree's tip the gate is judged at, where the door pins it (the look's automatic landing): the landing
    /// lands exactly that commit, or refuses where the tree moved past it, so a commit made after the judgement never lands unseen. Null
    /// lands the tree as it stands, as the person's press does.
    /// </summary>
    public string? Tip { get; init; }

    /// <summary>Whether the work may land: every part lets it go.</summary>
    public bool LetsGo => Refusal is null;

    /// <summary>
    /// The first part that holds, as a landing's refusal: its sentence and its code; null where all let go. The run's
    /// own part, its graph's and the landing step's are refused as <see cref="LandingCode.Refused"/>: their sentence says
    /// what holds them.
    /// </summary>
    public TreeLanding? Refusal =>
        Workflow is { LetsGo: false } workflow ? new(false, workflow.Says) { Refusal = LandingCode.Refused }
        : !Opinion.LetsGo ? new(false, Opinion.Says) { Refusal = LandingCode.Opinion }
        : !Review.LetsGo ? new(false, Review.Says) { Refusal = LandingCode.Unreviewed }
        : Graph is { LetsGo: false } graph ? new(false, graph.Says) { Refusal = LandingCode.Refused }
        : Workflow?.Process is { LandingCannot: not null } landing ? new(false, landing.LandingSays) { Refusal = LandingCode.Refused }
        : null;

    /// <summary>Whether what holds the work is the run's own part or its landing step's, where it holds.</summary>
    public bool WorkflowHolds => Workflow is { LetsGo: false }
        || (Opinion.LetsGo && Review.LetsGo && Graph is not { LetsGo: false } && Process?.LandingCannot is not null);

    /// <summary>Whether what holds the work is its graph's part: every part before it lets it go.</summary>
    public bool GraphHolds => Workflow is not { LetsGo: false } && Opinion.LetsGo && Review.LetsGo && Graph is { LetsGo: false };

    /// <summary>
    /// The gate with the person's press counted: where the press is made with what is unsettled shown, and the gate
    /// still stands as drawn (<see cref="OpinionGateState.Token"/>), its disputes or its commits unread are answered.
    /// </summary>
    public bool Answers(string? token) => Opinion.PressAnswers && token is not null && string.Equals(token, Opinion.Token, StringComparison.Ordinal);

    /// <summary>What the trees read of the gate (<see cref="IWorkplaceGate"/>): the gate pinned to a tip, every other part as it stands.</summary>
    IWorkplaceGate IWorkplaceGate.WithTip(string? tip) => this with { Tip = tip };

    /// <summary>The process the gate is read with, as the trees read it.</summary>
    IWorkplaceProcess? IWorkplaceGate.Process => Process;

    /// <summary>What the review's gate keeps of the set-up that let the work go; null where it asked none.</summary>
    LandingReview? IWorkplaceGate.ReviewLanding => Review.Landing;

    /// <summary>What the second opinion's gate keeps of the pass that let the work go; null where the level asked none.</summary>
    LandingOpinion? IWorkplaceGate.OpinionLanding => Opinion.Landing;

    /// <summary>Whether what holds the work is its graph's part and that part asks a pass of the run's next look.</summary>
    bool IWorkplaceGate.GraphAsksPass => GraphHolds && Graph is { AsksPass: true };

    IReadOnlyList<string> IWorkplaceGate.PlanLines() => LandingGateWords.Plan(this);

    LandingLine? IWorkplaceGate.Held(string session, (string Workspace, string Repository) owner, TreeLanding landed, string door) =>
        LandingGateWords.Held(session, owner, this, landed, door);

    /// <summary>A gate that holds nothing, for a door that asked none.</summary>
    public static LandingGate Open(string repository) =>
        new(new OpinionGateState(OpinionGateStates.None, repository), new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository }));
}

/// <summary>What a door says of the whole landing gate: the plan's lines, and the log's line for a landing it held.</summary>
public static class LandingGateWords
{
    /// <summary>
    /// What <c>trees land --plan</c> says of the gate before the press: each
    /// part that asks anything, in the gate's order, what holds it or what let it go. Nothing where neither asks.
    /// </summary>
    public static IReadOnlyList<string> Plan(LandingGate gate)
    {
        var lines = new List<string>();
        // The run's own part first: which workflow decides, where a named one does, and what of its own holds it.
        if (gate.Workflow is { Speaks: true } workflow) lines.Add($"trees: {workflow.Says}");
        if (gate.Opinion.State != OpinionGateStates.None) lines.Add($"trees: {gate.Opinion.Says}");
        if (gate.Review.State != ReviewStates.None) lines.Add($"trees: {gate.Review.Says}");
        // The graph's part, after the look's, as the gate refuses it.
        if (gate.Graph is { } graph) lines.Add($"trees: {graph.Says}");
        if (gate.Process is { LandingCannot: not null } process) lines.Add($"trees: {process.LandingSays}");
        return lines;
    }

    /// <summary>The machine log's line for a landing the gate held, by the part that held it; null where none did.</summary>
    /// <param name="owner">The tree's workspace and repository.</param>
    public static LandingLine? Held(string session, (string Workspace, string Repository) owner, LandingGate gate, TreeLanding landed, string door) =>
        landed.Refusal switch
        {
            LandingCode.Refused when gate.GraphHolds && gate.Graph is { } graph =>
                graph.Held(session, owner.Repository, owner.Workspace, gate.Process, door),
            LandingCode.Refused when gate.WorkflowHolds && gate.Workflow is { } workflow =>
                workflow.Held(session, owner.Repository, owner.Workspace, door),
            LandingCode.Opinion => OpinionLines.Held(session, owner.Repository, owner.Workspace, gate.Opinion, door),
            LandingCode.Unreviewed => ReviewLines.Held(session, owner.Repository, owner.Workspace, gate.Review, door),
            _ => null,
        };
}
