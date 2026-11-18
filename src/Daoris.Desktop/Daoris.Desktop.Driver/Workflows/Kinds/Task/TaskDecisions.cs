namespace Daoris.Driver;

/// <summary>
/// A per-run decision: a <c>task</c> step declaring two or more outcomes of its own, none a task's <c>done</c>, an
/// approval's <c>approved</c> or the <c>rejected</c> wire's name, answered by <c>decide</c> with one of them. A holder's proposal, with its
/// reason, stands as the answer at once, held for the owner's word; the owner's same answer confirms it and another overrules it, and the
/// owner may answer again until the run ends; an agent holding it settles it at once (<see cref="NodeWord.Decider"/>). Its record line names who proposed and who confirmed, and the step its answer's route closed is
/// passed with that line (<see cref="WireDecisions"/>). Pure.
/// </summary>
public static class TaskDecisions
{
    /// <summary>Who the owner actor's answer reads as on the record: the actor, whoever holds it.</summary>
    public const string Owner = "the owner";

    private static readonly string[] Reserved = ["done", "approved", "rejected"];

    /// <summary>Whether <paramref name="outcomes"/> are a decision's: two or more, none a task's, an approval's or a refusal's name.</summary>
    public static bool Takes(IReadOnlyList<string> outcomes) =>
        outcomes.Count >= 2 && !outcomes.Intersect(Reserved, StringComparer.Ordinal).Any();

    /// <summary>Whether <paramref name="node"/> is a decision.</summary>
    public static bool Is(NodeDeclaration node) => node.Kind == NodeKinds.Task && Takes(node.Outcomes);

    /// <summary>
    /// Why a <c>decide</c> naming <paramref name="outcome"/> by <paramref name="holder"/> is refused at <paramref name="status"/>: an outcome
    /// <paramref name="node"/> does not declare (<see cref="RunNodeWords.NotOutcome"/>), or a holder's proposal on an answer the owner gave
    /// (<see cref="RunNodeWords.NotWaiting"/>); null where it may be said.
    /// </summary>
    public static string? Refused(NodeDeclaration node, NodeStatus status, string? outcome, string? holder) =>
        outcome is null || !node.Outcomes.Contains(outcome, StringComparer.Ordinal) ? RunNodeWords.NotOutcome
        : status.State == NodeStates.Active || (status.State == NodeStates.Reached && (holder is null || status.Holds is not null)) ? null
        : RunNodeWords.NotWaiting;

    /// <summary>
    /// The answer <paramref name="word"/> gives at <paramref name="status"/>, on the binding it waits or stands on: a holder's proposal holds
    /// itself for the owner's word; the owner's confirms the standing proposal where it names its outcome, else is proposed and confirmed by them.
    /// </summary>
    public static NodeMove Move(NodeWord word, NodeStatus status)
    {
        var outcome = word.Outcome ?? "";
        var proposal = $"proposed by {word.Holder ?? Owner}{Why(word.Words)}";
        var (words, holds) = word.Decider is { } decider ? ($"{Label(outcome)}: decided by {decider}{Why(word.Words)}", (string?)null)
            : word.Holder is not null ? ($"{Label(outcome)}: {proposal}", proposal)
            : status is { State: NodeStates.Reached, Holds: { } standing } && status.Acceptance?.Outcome == outcome
                ? ($"{Label(outcome)}: {standing}, confirmed by {Owner}{Why(word.Words)}", null)
                : ($"{Label(outcome)}: proposed and confirmed by {Owner}{Why(word.Words)}", (string?)null);
        return new NodeMove(NodeStates.Reached, NodeCause.Authority)
        {
            Source = word.Source, Attempt = status.Attempt, Acceptance = status.Binding is { } bound ? bound with { Outcome = outcome } : null,
            Words = words, Holds = holds,
        };
    }

    /// <summary>A decision's answers as the run's view offers them, one per outcome in its order: <c>decide:&lt;outcome&gt;</c>.</summary>
    public static IEnumerable<string> Offered(NodeDeclaration node) => node.Outcomes.Select(outcome => $"{NodeWordActs.Decide}:{outcome}");

    /// <summary>An outcome as its line reads it: <c>not-needed</c> is <i>Not needed</i>.</summary>
    public static string Label(string outcome)
    {
        var spaced = outcome.Replace('-', ' ').Replace('_', ' ');
        return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static string Why(string? words) => string.IsNullOrWhiteSpace(words) ? "" : $" ({words.Trim()})";
}
