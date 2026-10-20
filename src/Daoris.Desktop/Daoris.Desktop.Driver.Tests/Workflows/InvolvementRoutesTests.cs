using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1a (D157 point 10, D192 point 2, the pipeline design §1.3): the lowering table gains a landed route removed, set to
/// <c>current</c> or pointed at a pipeline whose checks run fewer gates; a verification pipeline's version that drops a check, narrows
/// its gates or removes the wire a node waits on it by; and a check's <c>failed</c> moved off <c>wait</c>. Raising one never lowers, and
/// an agent's choice that lowers is kept as a proposal, which only the person's press applies.
/// </summary>
public sealed class InvolvementRoutesTests
{
    private static NodeGraph Graph(string json)
    {
        var (graph, problems) = NodeGraphs.Read(json);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        return graph!;
    }

    private static string Checks(string checks, string wires = "", string entries = "\"gates\"") =>
        $$"""{"schemaVersion":2,"entries":[{{entries}}],"end":"gates","nodes":[{{checks}}],"wires":[{{wires}}]}""";

    /// <summary>The heavy gates' check and a sensitive scan's, each an entry of its own.</summary>
    private static string Both => Checks(Heavy + Scan, entries: "\"gates\",\"scan\"");

    private const string Heavy = """{"id":"gates","kind":"check","version":1,"reach":"performed"}""";

    private const string Scan = """,{"id":"scan","kind":"check","version":1,"reach":"performed","options":{"gates":["sensitive"]}}""";

    public static TheoryData<string, string, string, string> Versions => new()
    {
        { "a check dropped", Both, Checks(Heavy), "it drops the check `scan`" },
        { "a check's gates narrowed from heavy to names", Checks(Heavy),
            Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"gates":["unit"]}}"""), "it narrows the check `gates`'s gates" },
        { "a check's gates narrowed by a name",
            Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"gates":["unit","e2e"]}}"""),
            Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"gates":["unit"]}}"""), "it narrows the check `gates`'s gates" },
        { "a check's failure moved off wait to ask", Checks(Heavy),
            Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"failed":"ask"}}"""),
            "it moves the check `gates`'s failure off your press, to `ask`" },
        { "a check's failure moved off wait to stop", Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"failed":"wait"}}"""),
            Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"failed":"stop"}}"""),
            "it moves the check `gates`'s failure off your press, to `stop`" },
        { "the wire a node waits on a check by removed",
            Checks(Heavy + """,{"id":"ship","kind":"acme.deploy/stage","version":1,"reach":"performed"}""", """{"id":"a","kind":"after","from":"gates","to":"ship"}"""),
            """{"schemaVersion":2,"entries":["gates","ship"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"},{"id":"ship","kind":"acme.deploy/stage","version":1,"reach":"performed"}],"wires":[]}""",
            "it no longer holds `ship` on the check `gates`" },
        { "raising is no lowering: a check added, names made heavy, a failure back to wait",
            Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"gates":["unit"],"failed":"ask"}}"""),
            Both, "" },
    };

    [Theory]
    [MemberData(nameof(Versions))]
    public void A_verification_versions_lowering_is_a_table(string name, string otherwise, string chosen, string lowered) =>
        Assert.True(lowered == string.Join("; ", InvolvementRoutes.LowersVerification(Graph(chosen), Graph(otherwise))), name);

    private static readonly Dictionary<string, NodeGraph?> Newest = new(StringComparer.Ordinal)
    {
        ["full-verify"] = Graph(Both),
        ["quick-verify"] = Graph(Checks("""{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"gates":["unit"]}}""")),
        ["heavy-verify"] = Graph(Checks(Heavy)),
        ["gone-verify"] = null,
    };

    public static TheoryData<string, string?, string?, string> Routes => new()
    {
        { "the landed route removed", "full-verify", null, "it leaves the work that lands unverified" },
        { "the landed route set to current", "full-verify", "current", "it leaves the work that lands unverified" },
        { "pointed at a pipeline whose checks run fewer gates", "full-verify", "quick-verify",
            "it verifies the work that lands with fewer of its gates: the heavy gates and `sensitive`" },
        { "pointed at a pipeline whose checks cannot be read here", "full-verify", "gone-verify",
            "it verifies the work that lands with `gone-verify`, whose checks cannot be read here" },
        { "raising: a route set where none was", null, "quick-verify", "" },
        { "raising: current to a verification", "current", "heavy-verify", "" },
        { "the same gates by another pipeline", "heavy-verify", "full-verify", "" },
        { "none to none", null, "current", "" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void A_landed_routes_lowering_is_a_table(string name, string? otherwise, string? chosen, string lowered)
    {
        static WorkflowLanded? Route(string? workflow) => workflow is null ? null : new WorkflowLanded(workflow, null);
        var said = InvolvementRoutes.LowersRoute(Route(chosen), Route(otherwise), id => Newest.GetValueOrDefault(id));

        Assert.True(lowered == string.Join("; ", said), $"{name}: {string.Join("; ", said)}");
    }

    /// <summary>D157 point 10: an agent's choice that lowers the person's part is kept as a proposal; the person's press applies it.</summary>
    [Fact]
    public void An_agents_choice_that_lowers_is_kept_as_a_proposal_and_the_persons_press_applies_it()
    {
        IReadOnlyList<string> lowered = InvolvementRoutes.LowersRoute(null, new WorkflowLanded("full-verify", null), id => Newest.GetValueOrDefault(id));

        Assert.Equal(new InvolvementJudged(lowered, Applies: false), InvolvementRoutes.Judge(lowered, byThePerson: false));
        Assert.Equal(new InvolvementJudged(lowered, Applies: true), InvolvementRoutes.Judge(lowered, byThePerson: true));
        Assert.True(InvolvementRoutes.Judge([], byThePerson: false).Applies);
        Assert.True(InvolvementRoutes.Judge(InvolvementRoutes.LowersVerification(Newest["full-verify"]!, Newest["quick-verify"]!), byThePerson: false).Applies);
        Assert.False(InvolvementRoutes.Judge(InvolvementRoutes.LowersVerification(Newest["quick-verify"]!, Newest["full-verify"]!), byThePerson: false).Applies);
    }
}
