namespace Daoris.Driver;

/// <summary>Where a failed check's words went, read from the run's record (<see cref="NodeTold"/>); both null while nothing is told yet.</summary>
public sealed record WorkflowRunTold(string? Session, string? Why)
{
    /// <summary>Whether the red came past the check's rounds' bound, so no way back is taken and its holder has its row.</summary>
    public bool Spent { get; init; }

    public string? Decided { get; init; }

    public string? Role { get; init; }

    public string? Reaches { get; init; }

    public static WorkflowRunTold Of(NodeRecord? record, string node, int attempt) =>
        record?.ToldOf(node, attempt) is { } told
            ? new WorkflowRunTold(told.Session, told.Why) { Spent = told.Spent, Decided = told.Decided, Role = told.Role, Reaches = told.Reaches }
            : new WorkflowRunTold(null, null);

    /// <summary>The step's words after <c>Failed: the gate `x` failed;</c>, never saying the words went where the say door kept none.</summary>
    public string Said => this switch
    {
        { Session: { } session, Spent: true } => $"its rounds back to the work are spent, so {Holds}, and {Went(session)}.",
        { Session: { } session } => $"{Went(session)}.",
        { Why: NodeTold.Running } => "its words wait: its session is running where no door here reaches it, and they are told once it stops running.",
        { Why: { } why } => $"its words were not told to the work: {why.TrimEnd('.')}.",
        _ => "its words are not told to the work yet.",
    };

    private string Went(string session) => Reaches switch
    {
        SayAnswer.NextStep => $"its words went to session `{session}`, which was working, and reach it at its next step",
        SayAnswer.TurnEnd => $"its words went to session `{session}`, which was working, and are held for its turn's end",
        SayAnswer.FirstPrompt => $"its words went to session `{session}`, which is opening, and are held for its first prompt",
        _ => $"its words went to session `{session}` as its next turn",
    };

    private string Holds => Decided switch
    {
        NodeTold.RoleHeld => $"the role `{Role}` holds it",
        NodeTold.Exit => "nobody else held it and it came to you as Daoris's final exit",
        _ => "it waits on you",
    };
}
