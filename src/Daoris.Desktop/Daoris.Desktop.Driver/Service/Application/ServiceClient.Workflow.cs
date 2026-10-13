using System.Text.Json;

namespace Daoris.Driver;

public sealed partial class ServiceClient
{
    /// <summary>An ask's kind and workflow choices (<c>workflowChoices</c>), each with when it was made; one without a moment is half of one.</summary>
    private static IReadOnlyList<AskWorkflowChoiceView> ReadWorkflowChoices(JsonElement ask) =>
    [
        .. Items(ask, "workflowChoices")
            .Where(chosen => Moment(chosen, "at") is not null)
            .Select(chosen => new AskWorkflowChoiceView(Text(chosen, "kind"), Text(chosen, "workflow"), Moment(chosen, "at")!.Value, Text(chosen, "words"))),
    ];

    /// <summary>An ask's intake's kind proposals (<c>workflowProposals</c>), each a kind with its reason and when; one without is half of one.</summary>
    private static IReadOnlyList<AskWorkflowProposalView> ReadWorkflowProposals(JsonElement ask) =>
    [
        .. Items(ask, "workflowProposals")
            .Where(proposed => Text(proposed, "kind") is { Length: > 0 } && Text(proposed, "reason") is { Length: > 0 }
                               && Moment(proposed, "at") is not null)
            .Select(proposed => new AskWorkflowProposalView(
                Text(proposed, "kind")!, Text(proposed, "reason")!, Moment(proposed, "at")!.Value,
                Text(proposed, "session"), Text(proposed, "quest"))),
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
