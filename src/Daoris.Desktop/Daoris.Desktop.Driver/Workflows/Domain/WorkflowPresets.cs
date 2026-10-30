using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One preset built in (the workflow design §2.7): its id, its name, and its steps as a version saves them, as JSON.</summary>
public sealed record WorkflowPreset(string Id, string Name, string StepsJson)
{
    /// <summary>The steps as saved, fresh each time, for a version to take.</summary>
    public JsonArray Steps() => JsonNode.Parse(StepsJson)!.AsArray();
}

/// <summary>One preset built in that is a graph (the integration design §17.7): its id, its name and its graph as a version saves it, as JSON.</summary>
/// <param name="NoPress">It integrates with no press: the repository must declare its gates and have them accepted (§4.1).</param>
public sealed record WorkflowGraphPreset(string Id, string Name, string GraphJson, bool NoPress = false)
{
    /// <summary>The graph as saved, fresh each time, for a version to take.</summary>
    public JsonObject Graph() => JsonNode.Parse(GraphJson)!.AsObject();
}

/// <summary>
/// The presets built in (WORKFLOW1d, the workflow design §2.7), a table both twins read as the tools list is (D121): nothing is
/// stored until one is saved. Each is offered with a second opinion and with your look first (<see cref="WithGates"/>), and
/// each is shown by a line composed from its steps (<see cref="Line"/>), never typed, so it cannot drift from them.
/// </summary>
/// <remarks>
/// Held to this suite's tests' <c>fixtures/workflow-named.json</c> <c>presets</c>, <c>variants</c> and <c>lines</c>, cell for cell
/// (the CLI's twin, <c>namedworkflows.ts</c>, left in PUSH1f2b).
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

    private const string Claimed = """{"id":"work","kind":"claimed","version":1,"reach":"observed"}""";
    private const string Requests = """{"id":"requests","kind":"work.requests","version":1,"reach":"observed"}""";
    private const string Wires = """{"id":"a","kind":"after","from":"work","to":"requests"},{"id":"b","kind":"after","from":"requests","to":"landing"}""";

    /// <summary>
    /// The two presets that are graphs (INTEGRATE1q; the integration design §17.7), beside the four of steps: <i>Integrated into the line</i> with
    /// its variant <i>with no press</i>, and <i>A pull request</i>. Each holds <c>work.requests</c>, and neither holds <c>quest.close</c>, so the
    /// quest is closed by its agent's done and the flow's end is the work's.
    /// </summary>
    public static IReadOnlyList<WorkflowGraphPreset> GraphTable { get; } =
    [
        new("integrated", "Integrated into the line", Integrated(accept: "you", request: "owner")),
        new("integrated-no-press", "Integrated into the line with no press", Integrated(accept: "automatic", request: "done"), NoPress: true),
        new("pull-request", "A pull request",
            $$$"""
            {"schemaVersion":2,"entries":["work"],"end":"pull-request","nodes":[{{{Claimed}}},{{{Requests}}},
              {"id":"landing","kind":"handed","version":1,"reach":"observed","options":{"form":"branch","accept":"you"}},
              {"id":"pushed","kind":"pushed","version":1,"reach":"observed"},
              {"id":"pull-request","kind":"pull-request","version":1,"reach":"observed"}],
             "wires":[{{{Wires}}},{"id":"c","kind":"after","from":"landing","to":"pushed"},{"id":"d","kind":"after","from":"pushed","to":"pull-request"}]}
            """),
    ];

    private static string Integrated(string accept, string request) =>
        $$$"""
        {"schemaVersion":2,"entries":["work"],"end":"line","nodes":[{{{Claimed}}},{{{Requests}}},
          {"id":"landing","kind":"handed","version":1,"reach":"observed","options":{"form":"branch","accept":"{{{accept}}}"}},
          {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{request}}}","conflicts":"session"}},
          {"id":"line","kind":"line","version":1,"reach":"observed"}],
         "wires":[{{{Wires}}},{"id":"c","kind":"after","from":"landing","to":"integrate"},{"id":"d","kind":"after","from":"integrate","to":"line"}]}
        """;

    /// <summary>A graph preset by its id, or null.</summary>
    public static WorkflowGraphPreset? GraphOf(string id) => GraphTable.FirstOrDefault(preset => preset.Id == id);

    /// <summary>
    /// A graph with a second opinion and your look added where it holds none: each a node wired from what led into the landing and straight into
    /// it, the opinion never wired into the look (FABRIC-CUTOVER1b), so the graph reads under the order of D157 §2.2.
    /// </summary>
    public static JsonObject WithGraphGates(JsonObject graph, bool opinion, bool look)
    {
        var next = (JsonObject)graph.DeepClone();
        var nodes = next["nodes"]!.AsArray();
        var wires = next["wires"]!.AsArray();
        bool Has(string kind) => nodes.Any(node => (string?)node!["kind"] == kind);
        var landing = (string)nodes.First(node => (string?)node!["kind"] == WorkflowKinds.Landing || (string?)node!["kind"] == "handed")!["id"]!;
        var lead = wires.First(wire => (string?)wire!["to"] == landing)!;
        var from = (string)lead["from"]!;
        wires.Remove(lead);
        var gates = new List<string>();
        if (opinion && !Has(WorkflowKinds.Opinion)) gates.Add(WorkflowKinds.Opinion);
        if (look && !Has(WorkflowKinds.Look)) gates.Add(WorkflowKinds.Look);
        if (gates.Count == 0) wires.Add(new JsonObject { ["id"] = lead["id"]!.DeepClone(), ["kind"] = "after", ["from"] = from, ["to"] = landing });
        foreach (var gate in gates)
        {
            nodes.Add(new JsonObject { ["id"] = gate, ["kind"] = gate, ["version"] = 1, ["reach"] = "observed" });
            wires.Add(new JsonObject { ["id"] = $"{gate}-in", ["kind"] = "after", ["from"] = from, ["to"] = gate });
            wires.Add(new JsonObject { ["id"] = $"{gate}-out", ["kind"] = "after", ["from"] = gate, ["to"] = landing });
        }

        return next;
    }

    /// <summary>The line a graph preset is shown by, composed from its nodes and their options, never typed (§17.7).</summary>
    public static string GraphLine(JsonObject graph)
    {
        var nodes = graph["nodes"]!.AsArray().Select(node => node!.AsObject()).ToList();
        JsonObject? Of(string kind) => nodes.FirstOrDefault(node => (string?)node["kind"] == kind);
        string? Option(JsonObject? node, string name) => (string?)node?["options"]?[name];
        var said = new List<string>();
        if (Of(WorkflowKinds.Opinion) is not null) said.Add("Another agent reads the work first.");
        if (Of(WorkflowKinds.Look) is not null) said.Add("You look at it running before it lands.");
        var you = Option(Of("handed"), "accept") == "you";
        if (Of("pull-request") is not null)
        {
            said.Add(you ? "You accept; the plugin pushes and opens a pull request; you merge it."
                : "A pull request opens once its agent is done; you merge it.");
        }
        else if (Of("integrate") is { } integrate)
        {
            said.Add(Option(integrate, "request") == "done"
                ? "Each piece of work is integrated into the line once its agent is done, on gates the repository accepted."
                : you ? "You accept each piece of work onto a branch, and you ask for it to be integrated into the line."
                    : "Each piece of work lands on a branch, and you ask for it to be integrated into the line.");
        }

        said.Add("Its agent's done closes the quest; the flow's end is the work's.");
        return string.Join(' ', said);
    }

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
