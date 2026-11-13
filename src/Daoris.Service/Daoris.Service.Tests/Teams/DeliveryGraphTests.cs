using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The delivery graph's judge on the case that gave it: a schema owner with a migration and an API, a backend that mirrors the schema, a
/// frontend that calls the backend. Pure and in memory; each runs in microseconds.
/// </summary>
public sealed class DeliveryGraphTests
{
    private static readonly DeliveryRef D1 = new("schema-owner", "config-migration"), D2 = new("schema-owner", "config-api");
    private static readonly DeliveryRef D3 = new("backend", "mirror-api"), D4 = new("frontend", "web-app");

    private static DeliveryEvidence Manifest(DeliveryRef at, string blob) => new(at.Repository, "daoris.json", "", blob);

    private static DeliveryDeployable Declared(DeliveryRef id, DeliveryUse[] provides, DeliveryUse[] consumes) =>
        new(id, Manifest(id, "blob-" + id.Repository)) { Provides = provides, Consumes = consumes };

    /// <summary>The case's four deployables as their declarations read.</summary>
    private static DeliveryDeployable[] Case() =>
    [
        Declared(D1, [new("config-schema", DeliveryUse.Migrates)], []),
        Declared(D2, [new("config-api", DeliveryUse.Owns)], [new("config-schema", DeliveryUse.Reads)]),
        Declared(D3, [new("mirror-api", DeliveryUse.Owns)], [new("config-schema", DeliveryUse.Mirror), new("config-api", DeliveryUse.Calls)]),
        Declared(D4, [], [new("mirror-api", DeliveryUse.Calls)]),
    ];

    private static DeliveryEdge StageCondition() => new(D2, D1, "Stage 2 starts on stage 1 of the release definition.", DeliveryEdge.StageCondition,
        new("schema-owner", "release-definition/stage-2", "", "rev-7"));

    private static DeliveryEdge CodeRead(DeliveryRef later, DeliveryRef earlier, string repository, string blob) =>
        new(later, earlier, $"{later.Id} calls what {earlier.Id} changes.", DeliveryEdge.CodeRead, new(repository, "src/client.ts", "10-24", blob));

    private static DeliveryEdge[] CaseWritten() => [StageCondition(), CodeRead(D3, D2, "backend", "blob-cache"), CodeRead(D4, D3, "frontend", "blob-header")];

    private static List<(string From, string On, string Source)> Pairs(DeliveryJudged judged) =>
        [.. judged.Edges.Select(edge => (edge.Deployable.Id, edge.On.Id, edge.Source))];

    [Fact]
    public void The_case_gives_four_orders_with_the_source_each_stands_on()
    {
        var judged = DeliveryGraph.Judge(Case(), CaseWritten(), []);

        Assert.Null(judged.Refusal);
        Assert.Equal(["config-api>config-migration", "mirror-api>config-api", "mirror-api>config-migration", "web-app>mirror-api"],
            judged.Edges.Select(edge => $"{edge.Deployable.Id}>{edge.On.Id}").Distinct().Order().ToList());
        Assert.Contains(("config-api", "config-migration", DeliveryEdge.StageCondition), Pairs(judged));
        Assert.Contains(("config-api", "config-migration", DeliveryEdge.Declaration), Pairs(judged));
        Assert.Contains(("mirror-api", "config-migration", DeliveryEdge.Declaration), Pairs(judged));
        Assert.Contains(("mirror-api", "config-api", DeliveryEdge.CodeRead), Pairs(judged));
        Assert.Contains(("web-app", "mirror-api", DeliveryEdge.CodeRead), Pairs(judged));
        Assert.Empty(judged.Flags);
        Assert.Empty(judged.Questions);
        Assert.All(judged.Edges, edge => Assert.False(string.IsNullOrWhiteSpace(edge.Why)));
        Assert.All(judged.Edges, edge => Assert.NotNull(edge.Evidence));
    }

    [Fact]
    public void A_declaration_edge_cites_the_consumers_declaration_and_names_what_it_reads()
    {
        var judged = DeliveryGraph.Judge(Case(), [], []);

        var mirror = judged.Edges.Single(edge => edge.Deployable == D3);
        Assert.Equal(D1, mirror.On);
        Assert.Equal(new DeliveryEvidence("backend", "daoris.json", "", "blob-backend"), mirror.Evidence);
        Assert.Contains("config-schema", mirror.Why);
        Assert.Contains("mirror", mirror.Why);
    }

    [Fact]
    public void Calls_alone_give_no_edge_and_a_resource_nobody_provides_gives_none()
    {
        var calls = DeliveryGraph.Judge(
            [Declared(D3, [new("mirror-api", DeliveryUse.Owns)], []), Declared(D4, [], [new("mirror-api", DeliveryUse.Calls)])], [], []);
        var orphan = DeliveryGraph.Judge([Declared(D4, [], [new("elsewhere", DeliveryUse.Reads)])], [], []);

        Assert.Empty(calls.Edges);
        Assert.Empty(orphan.Edges);
    }

    [Fact]
    public void An_order_with_no_evidence_is_policy_and_says_so()
    {
        var bare = new DeliveryEdge(D3, D2, "The owner releases the API first.", "", null);
        var unevidenced = CodeRead(D4, D3, "frontend", "") with { Evidence = null };

        var judged = DeliveryGraph.Judge(Case(), [bare, unevidenced], []);

        Assert.Contains(("mirror-api", "config-api", DeliveryEdge.Policy), Pairs(judged));
        Assert.Contains(("web-app", "mirror-api", DeliveryEdge.Policy), Pairs(judged));
        Assert.All(judged.Edges.Where(edge => edge.Source == DeliveryEdge.Policy), edge => Assert.Null(edge.Evidence));
    }

    [Fact]
    public void A_source_a_writer_may_not_give_is_refused()
    {
        var judged = DeliveryGraph.Judge(Case(), [StageCondition() with { Source = "hunch" }], []);

        Assert.Contains("hunch", judged.Refusal);
    }

    [Fact]
    public void An_edge_naming_a_deployable_no_plan_holds_is_flagged_and_kept()
    {
        var ghost = new DeliveryEdge(D4, new("backend", "ghost"), "Reads it.", DeliveryEdge.Policy, null);

        var judged = DeliveryGraph.Judge(Case(), [ghost], []);

        Assert.Contains(judged.Flags, flag => flag.Kind == TeamFlags.MissingReference);
        Assert.Contains(judged.Edges, edge => edge.On.Id == "ghost");
    }

    [Fact]
    public void A_cycle_is_one_question_however_often_it_is_found()
    {
        var back = new DeliveryEdge(D1, D3, "The migration waits for the backend's mirror.", DeliveryEdge.Policy, null);
        var asked = new DeliveryQuestion(DeliveryGraph.CycleKey([D1, D3]), DeliveryQuestion.Cycle, "The lead found it too.");

        var judged = DeliveryGraph.Judge(Case(), [back], [asked]);

        Assert.Contains(judged.Flags, flag => flag.Kind == TeamFlags.Cycle);
        var cycles = judged.Questions.Where(question => question.Kind == DeliveryQuestion.Cycle).ToList();
        Assert.Single(cycles);
        Assert.Equal("The lead found it too.", cycles[0].Text);
        Assert.Contains("staged rollout", DeliveryGraph.Judge(Case(), [back], []).Questions.Single().Text);
    }

    [Fact]
    public void A_question_written_twice_is_counted_once()
    {
        var open = new DeliveryQuestion("contract:viewers", DeliveryQuestion.Open, "Do admins or viewers see candidates?");

        var judged = DeliveryGraph.Judge(Case(), CaseWritten(), [open, open with { Text = "Reworded." }]);

        Assert.Single(judged.Questions);
        Assert.Equal("Do admins or viewers see candidates?", judged.Questions[0].Text);
    }

    [Fact]
    public void A_moved_blob_makes_its_edge_stale_and_it_is_asked_once()
    {
        var judged = DeliveryGraph.Judge(Case(), CaseWritten(), []);
        var seen = new Dictionary<string, string>
        {
            ["frontend:src/client.ts"] = "blob-moved", ["backend:src/client.ts"] = "blob-cache", ["backend:daoris.json"] = "blob-backend",
        };

        var stale = DeliveryGraph.Stale(judged.Edges, seen);
        var queue = DeliveryGraph.Queue(judged.Questions, judged.Edges, seen);

        Assert.Equal([D4], stale.Select(edge => edge.Deployable));
        Assert.Single(queue);
        Assert.Equal(DeliveryQuestion.Stale, queue[0].Kind);
        Assert.Single(DeliveryGraph.Queue(queue, judged.Edges, seen));
    }

    [Fact]
    public void Evidence_the_reader_did_not_look_at_is_not_judged()
    {
        var judged = DeliveryGraph.Judge(Case(), CaseWritten(), []);

        Assert.Empty(DeliveryGraph.Stale(judged.Edges, new Dictionary<string, string>()));
    }

    [Fact]
    public void Two_deployables_with_one_id_in_one_repository_are_refused()
    {
        var judged = DeliveryGraph.Judge([Declared(D1, [], []), Declared(D1, [], [])], [], []);

        Assert.Contains("config-migration", judged.Refusal);
    }
}
