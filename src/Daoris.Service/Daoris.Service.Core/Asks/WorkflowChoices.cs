using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// The kind and workflow on an ask (WORKFLOW1e, D157 point 10; the workflow design §4.1 row 1, §4.3): the person's choice for every
/// chain the ask publishes, kept with when and their words, the latest standing; one naming neither clears it.
/// </summary>
/// <param name="Kind">A kind's id, which the ask's workspace declares on the machine that drives it; null for none.</param>
/// <param name="Workflow">A workflow's id, or <c>current</c>; null for none.</param>
/// <param name="At">When the person chose it.</param>
/// <param name="Words">Their words with it, where they gave any.</param>
public sealed record AskWorkflowChoice(string? Kind, string? Workflow, DateTimeOffset At, string? Words = null);

/// <summary>
/// The kind and workflow on a quest published with no ask (WORKFLOW1e2; the workflow design §4.1 row 1, §4.3): the person's choice
/// on its page before it starts, kept in a <see cref="QuestOperationKind.Chosen"/> operation, the latest standing; one naming neither
/// clears it. Only a local host's door makes one, since the choice is the person's.
/// </summary>
/// <param name="Kind">A kind's id, which the quest's workspace declares on the machine that drives it; null for none.</param>
/// <param name="Workflow">A workflow's id, or <c>current</c>; null for none.</param>
public sealed record QuestWorkflowChoice(string? Kind, string? Workflow)
{
    /// <summary>Their words with it, where they gave any.</summary>
    public string? Words { get; init; }

    /// <summary>When it was made: the operation's time, set on the quest as the replay keeps it.</summary>
    public DateTimeOffset? At { get; init; }
}

/// <summary>
/// What a kind and a workflow on an ask may be, by their shape alone (WORKFLOW1e): the service holds no workflow and no kind, which
/// are the driving machine's (design §2.4), so whether the workspace declares the kind and the workflow reads is the door's to say.
/// The driver's <c>WorkflowSelection.IsKindId</c> and <c>WorkflowNamed.IsId</c> spell the same shapes.
/// </summary>
public static partial class WorkflowChoices
{
    /// <summary>The longest a kind's id is.</summary>
    public const int KindLimit = 24;

    /// <summary>The longest a workflow's id is.</summary>
    public const int WorkflowLimit = 40;

    /// <summary>The most characters the person's words with a choice hold, as a review choice's do.</summary>
    public const int WordsLimit = Reviews.WordsLimit;

    /// <summary>The most characters an intake's reason for a proposed kind holds, as a review proposal's does (design §4.3).</summary>
    public const int ReasonLimit = Reviews.ReasonLimit;

    /// <summary>
    /// What is wrong with a kind proposal's own words, as a clause the sentence closes, or null (WORKFLOW1i): the kind's shape, a
    /// reason said, and at most <see cref="ReasonLimit"/> characters. Whether the reason quotes the person is the exchange's.
    /// </summary>
    public static string? JudgeProposal(string? kind, string? reason) =>
        Judge(kind ?? "", null) is { } shape ? shape
        : string.IsNullOrWhiteSpace(reason) ? "A kind proposal says why, in the person's own words: they decide on it"
        : reason.Trim().Length > ReasonLimit ? $"A kind proposal's reason is at most {ReasonLimit} characters; this is {reason.Trim().Length}"
        : null;

    /// <summary>A kind's id: lower-case letters, digits and dashes, at most 24.</summary>
    public static bool IsKind(string? value) => value is { Length: > 0 and <= KindLimit } && Shape().IsMatch(value);

    /// <summary>A workflow's id, or <c>current</c>, which has an id's shape: lower-case letters, digits and dashes, at most 40.</summary>
    public static bool IsWorkflow(string? value) => value is { Length: > 0 and <= WorkflowLimit } && Shape().IsMatch(value);

    /// <summary>What is wrong with a choice's shape, as a clause the ask's sentence closes, or null.</summary>
    public static string? Judge(string? kind, string? workflow) =>
        kind is not null && !IsKind(kind)
            ? $"kind `{kind}` is not a kind's id: lower-case letters, digits and dashes, at most {KindLimit}, such as `docs`"
            : workflow is not null && !IsWorkflow(workflow)
                ? $"workflow `{workflow}` is not a workflow's id: lower-case letters, digits and dashes, at most {WorkflowLimit}, such as "
                  + "`docs-to-pr`; or `current`"
                : null;

    /// <summary>
    /// A quest's choice in its one shape in the log's payload and on the wire, and, <paramref name="standing"/>, in the quest's column,
    /// which alone keeps when it was made (WORKFLOW1e2).
    /// </summary>
    internal static void Write(Utf8JsonWriter writer, QuestWorkflowChoice choice, bool standing = false)
    {
        writer.WriteStartObject();
        if (choice.Kind is not null) writer.WriteString("kind", choice.Kind);
        if (choice.Workflow is not null) writer.WriteString("workflow", choice.Workflow);
        if (choice.Words is not null) writer.WriteString("words", choice.Words);
        if (standing && choice.At is { } at) writer.WriteString("at", Moments.Write(at));
        writer.WriteEndObject();
    }

    /// <summary>
    /// A quest's choice read from JSON, or null where it is not whole: an object whose kind and workflow, where it names them, have
    /// their shapes (<see cref="Judge"/>), and whose words are a string within <see cref="WordsLimit"/>. One naming neither is a clear.
    /// </summary>
    internal static QuestWorkflowChoice? Judged(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in new[] { "kind", "workflow", "words", "at" })
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String) return null;
        }

        var choice = new QuestWorkflowChoice(JsonFields.Text(element, "kind"), JsonFields.Text(element, "workflow"))
        {
            Words = JsonFields.Text(element, "words"),
            At = Moments.Read(element, "at"),
        };
        return Judge(choice.Kind, choice.Workflow) is null && choice.Words is not { Length: > WordsLimit } ? choice : null;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Shape();
}
