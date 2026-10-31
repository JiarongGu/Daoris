using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1p (the integration design §17.5, §17.6): the <c>request</c> kind, a quest published to another repository at the node's place in
/// the flow and waited on: its authority (the commitment's, as <c>wait.unit</c>'s), its options, its subject, its title, an older build's
/// refusal of it, and the person's part compared by its node's id (R5's C29).
/// </summary>
public sealed class RequestNodeKindTests
{
    private static NodeDeclaration Request(string options = """{"to":"engine","title":"Expose the frame cap","body":"The game paces on it."}""") =>
        new("ask", NodeKinds.Request, 1, NodeReach.Waited)
        {
            Options = System.Text.Json.JsonDocument.Parse(options).RootElement.EnumerateObject()
                .ToDictionary(option => option.Name, option => option.Value.GetRawText()),
        };

    /// <summary>A graph of these nodes in order, each after the one before, the first its entry and the last its end.</summary>
    private static NodeGraph Graph(string nodes)
    {
        var declared = System.Text.Json.Nodes.JsonNode.Parse(nodes)!.AsArray();
        var ids = declared.Select(node => (string)node!["id"]!).ToList();
        var wires = new System.Text.Json.Nodes.JsonArray([.. ids.Skip(1).Select((id, at) => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = $"{ids[at]}>{id}", ["kind"] = "after", ["from"] = ids[at], ["to"] = id,
        })]);
        var (graph, problems) = NodeGraphs.Read(new System.Text.Json.Nodes.JsonObject
        {
            ["schemaVersion"] = 2, ["entries"] = new System.Text.Json.Nodes.JsonArray(ids[0]), ["end"] = ids[^1],
            ["nodes"] = declared.DeepClone(), ["wires"] = wires,
        }.ToJsonString());
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        return graph!;
    }

    [Fact]
    public void A_requests_reach_and_failure_are_the_commitments_as_a_wait_units_are()
    {
        var authority = NodeAuthorities.Of(Request())!;
        var waitUnit = NodeAuthorities.BuiltIn[NodeKinds.WaitUnit];

        Assert.Equal(NodeReach.Waited, authority.Reach);
        Assert.Equal((waitUnit.ReachBy, waitUnit.FailureBy), (authority.ReachBy, authority.FailureBy));
        Assert.False(authority.PersonSkips);
    }

    [Theory]
    [InlineData("""{"title":"t","body":"b"}""", "to")]
    [InlineData("""{"to":"engine","body":"b"}""", "title")]
    [InlineData("""{"to":"engine","title":"t"}""", "body")]
    [InlineData("""{"to":"engine","title":"t","body":"b","start":"later"}""", "start")]
    [InlineData("""{"to":"engine","title":"t","body":"b","until":"read"}""", "until")]
    [InlineData("""{"to":"engine","title":"t","body":"b","declined":"retry"}""", "declined")]
    [InlineData("""{"to":"engine","title":"t","body":"b","start":"automatic","until":"taken","declined":"stop"}""", null)]
    [InlineData("""{"to":"engine:core","title":"t","body":"b"}""", null)]
    public void A_requests_options_are_each_held_to_their_choices(string options, string? problem)
    {
        Assert.Equal(problem, NodeDeclarations.Problem(Request(options)));
    }

    [Fact]
    public void A_build_before_the_kind_reads_it_as_unknown()
    {
        var older = NodeAuthorities.BuiltIn.Where(row => row.Key != NodeKinds.Request).ToDictionary();

        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Request(), older));
    }

    [Fact]
    public void A_requests_subject_is_the_run_and_its_title_the_request()
    {
        Assert.Equal(RunSubject.Run, RunSubjects.Of(Request(), new HashSet<string>()));
        var step = new WorkflowStep("ask", NodeKinds.Request, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris, null,
            new WorkflowSource("workflow", WorkflowLevels.Repository), [], WorkflowRuntime.Built, null);

        Assert.Equal("The quest to another repository", WorkflowRunWords.Title(step));
    }

    private static IReadOnlyList<NamedStep> Two(string firstStart, string secondStart) => Involvement.StepsOf(Graph($$$"""
        [{"id":"ask-engine","kind":"request","version":1,"reach":"waited","options":{"to":"engine","title":"Expose the cap","body":"b","start":"{{{firstStart}}}"}},
         {"id":"ask-docs","kind":"request","version":1,"reach":"waited","options":{"to":"docs","title":"Document the cap","body":"b","start":"{{{secondStart}}}"}}]
        """));

    /// <summary>
    /// C29: two request nodes in one graph, only the second moved to <c>start: automatic</c>, is reported as lowering naming the second alone,
    /// so choosing the version waits for the person's press; a request added with <c>start: automatic</c> lowers too, and one removed, or
    /// added with <c>start: you</c>, lowers nothing.
    /// </summary>
    [Fact]
    public void Two_requests_with_only_the_second_moved_to_automatic_lower_naming_the_second()
    {
        Assert.Equal(["it sends `Document the cap` to `docs` with no press of yours"], Involvement.Lowers(Two("you", "automatic"), Two("you", "you")));
        Assert.Empty(Involvement.Lowers(Two("you", "you"), Two("you", "automatic")));

        var one = Involvement.StepsOf(Graph("""
            [{"id":"ask-engine","kind":"request","version":1,"reach":"waited","options":{"to":"engine","title":"Expose the cap","body":"b"}}]
            """));
        Assert.Equal(["it sends `Document the cap` to `docs` with no press of yours"], Involvement.Lowers(Two("you", "automatic"), one));
        Assert.Empty(Involvement.Lowers(Two("you", "you"), one));
        Assert.Empty(Involvement.Lowers(one, Two("automatic", "automatic")));
    }
}
