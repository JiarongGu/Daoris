namespace Daoris.Driver;

/// <summary>
/// The person's Accept as a step of its own, placed before a <c>land</c> where a workflow wants one, or the end of a graph that lands
/// nothing (<i>Work in place</i>). Never skipped, since a skip would read as accepted.
/// </summary>
[NodeKind(NodeKinds.Review, NodeGroup.You)]
internal sealed class ReviewNode : BuiltInNode, IObserves, IPressed, IReadsSteps
{
    /// <summary>
    /// The person's Accept reaches it and nothing fails it. Their changes, with words, are its rejection, so an <c>after</c> from it still
    /// means on Accept and a <c>rejected</c> or <c>back on: rejected</c> wire is the changes route.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Review, NodeReach.Pressed, NodeSource.Person, null, true, PersonSkips: false, MayEnd: true, Evidenced: false)
        {
            ObservedBy = NodeSource.Git,
        };

    /// <summary>The run's work read as its review record (<see cref="ReviewWork"/>): a branch at its tip, else the run at the record's revision.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Record;

    /// <summary>A run's work with no branch, read for the pass so its review binds at the record's revision.</summary>
    public Task<NodeGitReads> ReadAsync(GovernedUnit governed, string home, NodeGitReads reads, CancellationToken ct) =>
        Task.FromResult(governed.Scope is { } scope && !ReviewWork.OnBranch(scope) ? reads with { Reviewed = ReviewWork.Read(home, scope, null) } : reads);

    /// <summary>Nothing: a run's review records outlast its end, read after its trees and sessions are gone.</summary>
    public void Release(string home, string run)
    {
    }

    public override NodePart Part(NodeDeclaration node) => new(NodeKinds.Review, WorkflowParticipation.You, WorkflowExecutor.Daoris, WorkflowPress.Accept);

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Review);

    public override string? Title => "Your Accept";

    public override NodeLook Look => NodeLook.You;

    /// <summary>The person's Accept, a gate before every landing as the look is.</summary>
    public override LandingGuard Guard => LandingGuard.Before;

    public override IReadOnlyList<string> LandingActs(NodeDeclaration node) => [NodeWordActs.Accept];

    /// <summary>Reached by what it reads only where a branch's landing is seen made, as the landing record's: a landing seen is not a press.</summary>
    public NodeObservation Observe(NodeReading reading) => ReviewReading.Observe(reading);

    /// <summary>The person's Accept, and their changes with their words.</summary>
    public IReadOnlyList<string> Takes { get; } = [NodeWordActs.Accept, NodeWordActs.Changes];

    public string Refusal => RunNodeWords.NotReview;

    /// <summary>A review waiting on the person's Accept takes their changes, with their words, beside it.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) => status.State == NodeStates.Active ? [NodeWordActs.Changes] : [];

    /// <summary>
    /// Their Accept is its reach by its authority, the person, on the binding it stands on, so one given before the review began is refused; their
    /// changes its rejection on that attempt, with their words, which the table needs.
    /// </summary>
    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) => word.Act == NodeWordActs.Accept
        ? new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = word.Source, Attempt = status.Attempt, Acceptance = status.Binding, Words = word.Words }
        : new NodeMove(NodeStates.Rejected, NodeCause.Person) { Attempt = status.Attempt, Words = word.Words };
}
