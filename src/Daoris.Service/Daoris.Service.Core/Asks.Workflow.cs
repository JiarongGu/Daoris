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

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Shape();
}

public sealed partial class AskDesk
{
    /// <summary>
    /// The person sets the ask's kind and workflow (design §4.3): a kind, a workflow, both, or neither to clear them, with any words
    /// they give, kept with when; the latest stands, as a review choice does. It reaches each chain the ask publishes whose work has
    /// not started: a run is bound at its first start (§4.5).
    /// </summary>
    /// <remarks>
    /// <b>The person's door</b> (D156 §3.2), a local host's as every ask route is: an agent proposes a kind, the intake's (WORKFLOW1i),
    /// and never sets one. Whether the ask's workspace declares the kind and the workflow reads here is the driving machine's to
    /// say; the service holds neither.
    /// </remarks>
    public async Task<AskOutcome> ChooseWorkflowAsync(
        string id, string? kind, string? workflow, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);
        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}): nothing it publishes waits for a workflow.", ask);
        }

        var (chosen, unfit) = JudgeWorkflowChoice(kind, workflow, words, now);
        if (unfit is not null) return new(AskRefusal.BadWorkflow, unfit, ask);

        await asks.RecordWorkflowChoiceAsync(ask.Id, chosen!, now, ct).ConfigureAwait(false);
        var stands = await FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, WorkflowSaid(ask.Id, chosen!) + (chosen!.Words is null ? "" : " Your words are kept with it."), stands);
    }

    /// <summary>A choice as the ask keeps it, or why not: each blank one none, its shape, and the person's words trimmed and bounded.</summary>
    private static (AskWorkflowChoice? Choice, string? Refusal) JudgeWorkflowChoice(string? kind, string? workflow, string? words, DateTimeOffset now)
    {
        var namedKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        var named = string.IsNullOrWhiteSpace(workflow) ? null : workflow.Trim();
        if (WorkflowChoices.Judge(namedKind, named) is { } unfit) return (null, $"The ask's {unfit}. Nothing was kept.");
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        return said is { Length: > WorkflowChoices.WordsLimit }
            ? (null, $"Your words with a kind or a workflow are at most {WorkflowChoices.WordsLimit} characters; these are {said.Length}. Nothing was kept.")
            : (new AskWorkflowChoice(namedKind, named, now, said), null);
    }

    /// <summary>What a choice kept says: the kind, the workflow, both, or neither, for the chains the ask publishes.</summary>
    private static string WorkflowSaid(string id, AskWorkflowChoice chosen)
    {
        var follows = chosen.Workflow switch
        {
            null => null,
            "current" => "follow Current, each repository's rules as they stand,",
            var workflow => $"follow `{workflow}`",
        };
        return (chosen.Kind, follows) switch
        {
            (null, null) => $"Ask `#{id}`: no kind and no workflow; each repository's choice decides the workflow the chains it publishes follow.",
            ({ } kind, null) => $"Ask `#{id}`: the chains it publishes are of kind `{kind}`; each repository's choice for it decides their workflow.",
            (null, { } named) => $"Ask `#{id}`: the chains it publishes {named} in every repository they reach.",
            ({ } kind, { } named) => $"Ask `#{id}`: the chains it publishes are of kind `{kind}`, and {named} in every repository they reach.",
        } + " Work already started keeps the workflow it bound.";
    }
}
