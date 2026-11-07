using System.Text.Json.Nodes;
using static Daoris.Driver.HelpProposals;
using static Daoris.Driver.HelpWorkflowProposals;

namespace Daoris.Driver;

/// <summary>
/// The <c>draft</c> door of the workflow proposal: a change to the draft open in
/// the person's editor, judged on its card and, at Apply, settled in the file without saving or choosing anything.
/// </summary>
internal static class HelpWorkflowDrafts
{
    /// <summary>The code a save's refusal of the version whole is said by on a draft's card, where no graph rule names it (its pipeline's shape).</summary>
    public const string VersionRefused = "version";

    /// <summary>
    /// A whole graph proposed against the draft this conversation has been told under its fingerprint, of either pipeline, read and judged as
    /// a save would judge it (each refusal the card's to put right, never the proposal's), and said as <see cref="WorkflowGraphDiff"/>'s
    /// changes. The candidate rides apart from what a save takes, so <see cref="HelpWorkflowCard.Saves"/> stays false; a base, a choice,
    /// a route or a name beside it is refused.
    /// </summary>
    public static HelpPlan Plan(HelpProposal proposal, HelpMachineFacts facts)
    {
        var fingerprint = proposal.Draft?.Trim() ?? "";
        if (proposal.Steps is not null || proposal.Graph is not { } drawn) return Refused("a change to the open draft carries its whole `graph`, never `steps`.");
        if (proposal.Base is not null) return Refused("a change to the open draft saves nothing, so it names no `base`: the draft names where it came from.");
        if (proposal.Choose || !string.IsNullOrWhiteSpace(proposal.Route))
        {
            return Refused("a change to the open draft chooses nothing, so it names no `choose` and no `route`: the person chooses where the workflow is used.");
        }

        if (!string.IsNullOrWhiteSpace(proposal.Name)) return Refused("a change to the open draft names no `name`: the person names the workflow as they save it.");
        if (!facts.Drafts.TryGetValue(fingerprint, out var told))
        {
            return Refused($"the draft `{fingerprint}` was never shown to this conversation: ask again from the editor, whose draft goes beside your words.");
        }

        var scope = proposal.Scope?.Trim() ?? "";
        if (!Same(scope, told.Place)) return Refused($"the draft `{fingerprint}` is for `{told.Place}`, not `{scope}`: a change to it names the place it is for.");
        var named = proposal.Workflow?.Trim() ?? "";
        if (named.Length > 0 && told.Workflow is { } held && named != held) return Refused($"the draft `{fingerprint}` is `{held}`'s, not `{named}`'s.");
        var id = told.Workflow ?? named;
        if (id.Length > 0 && WorkflowNamed.IdProblem(id) is { } idProblem) return Refused(idProblem);

        if (NodeGraphs.Parts(drawn.GetRawText(), out var unread) is not { } parts) return Refused(unread!.Words);
        if (NodeGraphs.Parts(told.Graph.GetRawText(), out var baseUnread) is not { } was)
        {
            return Refused($"the draft `{fingerprint}` does not read as a graph, so nothing is changed against it: {baseUnread!.Words}");
        }

        var proposed = new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion, Cleanup = parts.Cleanup ?? GraphCleanup.Held };
        var before = new NodeGraph(was.Nodes, was.Wires, was.Entries, was.End ?? "") { SchemaVersion = was.SchemaVersion, Cleanup = was.Cleanup ?? GraphCleanup.Held };
        var found = id.Length > 0 ? facts.Workflows.FirstOrDefault(each => each.Id == id) : null;
        var pipeline = found?.Read.Pipeline
            ?? (told.Pipeline == WorkflowPipelines.Verification ? WorkflowPipelines.Verification : WorkflowPipelines.Development);

        // Judged as the editor's save judges it: each graph rule's refusal at its step or way, and where those hold, the pipeline's own shape.
        var candidate = JsonNode.Parse(drawn.GetRawText())!;
        var problems = facts.Home is { } home ? WorkflowOperations.GraphProblems(home, candidate) : NodeGraphs.Problems(proposed, onSave: true);
        if (problems.Count == 0 && WorkflowNamed.ReadVersion(GraphVersion(drawn, 1), 1, [], pipeline, onSave: true).Problem is { } shape)
        {
            problems = [new WireProblem(VersionRefused, null, null, shape.StartsWith("v1: ", StringComparison.Ordinal) ? shape[4..] : shape)];
        }

        var changes = WorkflowGraphDiff.Of(before, proposed);
        var repository = facts.Repositories.FirstOrDefault(each => Same(each, told.Place));
        var whose = id.Length > 0 ? $"`{id}`" : "a new workflow";
        var said = new List<string>
        {
            (changes.Count == 0 ? $"A change to your workflow draft of {whose} for `{told.Place}` that changes nothing in it."
                : $"A change to your workflow draft of {whose} for `{told.Place}`: {changes.Count} change{(changes.Count == 1 ? "" : "s")}.")
            + " Nothing is saved or chosen: you take the changes into the draft in its editor, and only your Save there saves it.",
        };
        said.AddRange(changes.Select(change => $"  {change.Said}"));
        if (problems.Count > 0)
        {
            said.Add("To put right before it saves:");
            said.AddRange(problems.Select(problem => $"  {Capital(problem.Words)}"));
        }

        var card = new HelpWorkflowCard(
            id, found?.Read.Name ?? id, told.Base, 0, told.Place, [.. changes.Select(change => change.Said)], [], [], [],
            "Nothing is saved or chosen: you take the changes into the draft in its editor, and only your Save there saves it.", false)
        {
            ScopeKind = repository is not null ? "repository" : "workspace",
            Verifies = pipeline == WorkflowPipelines.Verification,
            Draft = fingerprint,
            Changes = changes,
            Problems = problems,
            Proposed = candidate,
        };

        // The place's Workflow, where its editor is.
        return new HelpPlan(null, string.Join('\n', said), "", null)
        {
            Workflow = card,
            Go = new HelpPlace("projects", null, repository is null ? "workspace-workflow" : null),
        };
    }

    /// <summary>
    /// A draft card's Apply: it writes no workflow and no choice. Pressed from the card, with no
    /// change named, it takes the person to the place's Workflow, where the editor shows the proposal, and settles nothing; pressed by the
    /// editor with every change taken or left once, it settles applied, the file recording which. A press naming them otherwise stands.
    /// </summary>
    public static HelpApplied Apply(HelpApplying applying, HelpWorkflowCard card)
    {
        var proposal = applying.Proposal;
        if (proposal.Taken is null && proposal.Left is null)
        {
            return new HelpApplied(true, $"`#{applying.Id}` is a change to your workflow draft: it is shown in the draft's editor, and nothing is saved or "
                + "chosen until you take its changes into the draft and press Save there.")
            {
                Go = applying.Plan.Go,
                Stands = true,
            };
        }

        IReadOnlyList<string> taken = proposal.Taken ?? [], left = proposal.Left ?? [];
        var ids = card.Changes.Select(change => change.Id).ToHashSet(StringComparer.Ordinal);
        var named = taken.Concat(left).ToList();
        if (named.Count != ids.Count || !ids.SetEquals(named) || named.Distinct(StringComparer.Ordinal).Count() != named.Count)
        {
            return new HelpApplied(false, $"Not applied: `#{applying.Id}` — the press names each of its {ids.Count} changes once, taken or left, and this one did not, "
                + "so the card stands for the editor to name them.")
            {
                Stands = true,
            };
        }

        var path = Path.Combine(FolderOf(applying.Home), $"{Path.GetFileName(applying.Id)}.json");
        var file = JsonNode.Parse(AtomicFile.ReadText(path))!.AsObject();
        file["taken"] = new JsonArray([.. taken.Select(each => (JsonNode)JsonValue.Create(each)!)]);
        file["left"] = new JsonArray([.. left.Select(each => (JsonNode)JsonValue.Create(each)!)]);
        JsonFile.Write(path, file);
        return applying.Settled(true, $"Applied: `#{applying.Id}` — {taken.Count} of its {ids.Count} changes taken into your workflow draft and {left.Count} left; "
            + "nothing is saved until you press Save in its editor.", $"taken {taken.Count}, left {left.Count}");
    }
}
