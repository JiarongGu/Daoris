namespace Daoris.Driver;

/// <summary>Where a failed check's words went, read from the run's record (<see cref="NodeTold"/>); both null while nothing is told yet.</summary>
public sealed record WorkflowRunTold(string? Session, string? Why)
{
    /// <summary>Whether the red came past the check's rounds' bound, so no way back is taken and the person has its row.</summary>
    public bool Spent { get; init; }

    public static WorkflowRunTold Of(NodeRecord? record, string node, int attempt) =>
        record?.ToldOf(node, attempt) is { } told ? new WorkflowRunTold(told.Session, told.Why) { Spent = told.Spent } : new WorkflowRunTold(null, null);

    /// <summary>The step's words after <c>Failed: the gate `x` failed;</c>, never saying the words went where the say door kept none.</summary>
    public string Said => this switch
    {
        { Session: { } session, Spent: true } =>
            $"its rounds back to the work are spent, so it waits on you, and its words went to session `{session}` as its next turn.",
        { Session: { } session } => $"its words went to session `{session}` as its next turn.",
        { Why: { } why } => $"its words were not told to the work: {why.TrimEnd('.')}.",
        _ => "its words are not told to the work yet.",
    };
}
