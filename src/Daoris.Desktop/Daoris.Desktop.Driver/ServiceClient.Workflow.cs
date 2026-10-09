using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's kind and workflow on an ask (WORKFLOW1e; the workflow design §4.1 row 1, §4.3), with when and their words: the
/// latest stands, and one naming neither is a choice cleared.
/// </summary>
/// <param name="Kind">A kind's id its workspace declares, or null for none.</param>
/// <param name="Workflow">A workflow's id, <c>current</c>, or null for none.</param>
public sealed record AskWorkflowChoiceView(string? Kind, string? Workflow, DateTimeOffset At, string? Words = null);

public sealed partial record AskView
{
    /// <summary>The person's kind and workflow choices on it, oldest first, the latest standing (WORKFLOW1e); empty where they made none.</summary>
    public IReadOnlyList<AskWorkflowChoiceView> WorkflowChoices { get; init; } = [];
}

public sealed partial class ServiceClient
{
    /// <summary>An ask's kind and workflow choices (<c>workflowChoices</c>), each with when it was made; one without a moment is half of one.</summary>
    private static IReadOnlyList<AskWorkflowChoiceView> ReadWorkflowChoices(JsonElement ask) =>
    [
        .. Items(ask, "workflowChoices")
            .Where(chosen => Moment(chosen, "at") is not null)
            .Select(chosen => new AskWorkflowChoiceView(Text(chosen, "kind"), Text(chosen, "workflow"), Moment(chosen, "at")!.Value, Text(chosen, "words"))),
    ];

    /// <summary>
    /// The person's kind and workflow on an ask (WORKFLOW1e; the workflow design §4.3): the local host's
    /// <c>POST /api/asks/{id}/workflow</c>, <c>{ kind?, workflow?, words? }</c>, a person's door (D156); neither clears the choice.
    /// The latest stands. The service's sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public Task<AskAnswer> ChooseAskWorkflowAsync(string ask, string? kind, string? workflow, string? words, CancellationToken ct = default) =>
        PostAskAsync($"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/workflow", writer =>
        {
            if (kind is not null) writer.WriteString("kind", kind);
            if (workflow is not null) writer.WriteString("workflow", workflow);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
        }, ct);
}
