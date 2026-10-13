using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One preset built in (the workflow design §2.7): its id, its name, and its steps as a version saves them, as JSON.</summary>
public sealed record WorkflowPreset(string Id, string Name, string StepsJson)
{
    /// <summary>The steps as saved, fresh each time, for a version to take.</summary>
    public JsonArray Steps() => JsonNode.Parse(StepsJson)!.AsArray();
}

/// <summary>
/// The presets built in (WORKFLOW1d, the workflow design §2.7), a table both twins read as the tools list is (D121): nothing is
/// stored until one is saved. Each is offered with a second opinion and with your look first (<see cref="WithGates"/>), and
/// each is shown by a line composed from its steps (<see cref="Line"/>), never typed, so it cannot drift from them.
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>WORKFLOW_PRESETS</c>, <c>withGates</c> and <c>workflowLine</c> (<c>namedworkflows.ts</c>): both hold
/// this suite's tests' <c>fixtures/workflow-named.json</c> <c>presets</c>, <c>variants</c> and <c>lines</c>, cell for cell.
/// </remarks>
public static class WorkflowPresets
{
    private const string Work = """{"id":"work","kind":"work"}""";
    private const string PullRequest = """{"id":"pull-request","kind":"pull-request"}""";

    public static IReadOnlyList<WorkflowPreset> Table { get; } =
    [
        new("merge-after-accept", "Merge after you accept", $$"""[{{Work}},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]"""),
        new("branch-to-push", "A branch for you to push",
            $$"""[{{Work}},{"id":"landing","kind":"landing","form":"branch","accept":"you","plugin":"none"}]"""),
        new("pull-request-after-accept", "A pull request after you accept",
            $$"""[{{Work}},{"id":"landing","kind":"landing","form":"branch","accept":"you"},{{PullRequest}}]"""),
        new("pull-request-no-press", "A pull request with no press",
            $$"""[{{Work}},{"id":"landing","kind":"landing","form":"branch","accept":"automatic"},{{PullRequest}}]"""),
    ];

    /// <summary>A preset by its id, or null.</summary>
    public static WorkflowPreset? Of(string id) => Table.FirstOrDefault(preset => preset.Id == id);

    /// <summary>
    /// Steps with a second opinion and your look added where they are not (design §2.7's <i>with a second opinion</i>,
    /// <i>with your look first</i>), each reading what the repository declares: the opinion before the look, both before the landing.
    /// </summary>
    public static JsonArray WithGates(JsonArray steps, bool opinion, bool look)
    {
        var next = steps.Select(step => step!.DeepClone()).ToList();
        int At(string kind) => next.FindIndex(step => (string?)step!["kind"] == kind);
        if (opinion && At(WorkflowKinds.Opinion) == -1)
        {
            var before = At(WorkflowKinds.Look);
            next.Insert(before == -1 ? At(WorkflowKinds.Landing) : before, new JsonObject { ["id"] = "opinion", ["kind"] = WorkflowKinds.Opinion });
        }

        if (look && At(WorkflowKinds.Look) == -1) next.Insert(At(WorkflowKinds.Landing), new JsonObject { ["id"] = "look", ["kind"] = WorkflowKinds.Look });
        return new JsonArray([.. next]);
    }

    /// <summary>
    /// The line a workflow is shown by (design §2.5, §2.7), composed from its steps: who reads it first, whether you look, how it
    /// lands and what follows, and the go-aheads on the way.
    /// </summary>
    public static string Line(IReadOnlyList<NamedStep> steps)
    {
        var said = new List<string>();
        bool Has(string kind) => steps.Any(step => step.Kind == kind);
        if (Has(WorkflowKinds.Opinion)) said.Add("Another agent reads the work first.");
        if (Has(WorkflowKinds.Look)) said.Add("You look at it running before it lands.");
        var landing = steps.First(step => step.Kind == WorkflowKinds.Landing);
        var you = (string?)landing.Field("accept") == "you";
        if ((string?)landing.Field("form") == LandingForm.Merge)
        {
            said.Add("You accept each piece of work, and it merges into the line.");
        }
        else if (Has(WorkflowKinds.PullRequest))
        {
            said.Add(you ? "You accept; the plugin pushes and opens a pull request; you merge it."
                : "A pull request opens once its quest is done; you merge it.");
        }
        else if ((string?)landing.Field("plugin") == "none")
        {
            said.Add(you ? "You accept each piece of work onto a branch, and push it yourself."
                : "Each piece of work lands on a branch once its quest is done, and you push it yourself.");
        }
        else
        {
            said.Add(you ? "You accept each piece of work onto a branch, and its plugin pushes it."
                : "Each piece of work lands on a branch once its quest is done, and its plugin pushes it.");
        }

        var goAheads = steps.Count(step => step.Kind == WorkflowKinds.GoAhead);
        if (goAheads == 1) said.Add("You answer a go-ahead on the way.");
        else if (goAheads > 1) said.Add($"You answer {goAheads} go-aheads on the way.");
        return string.Join(' ', said);
    }
}
