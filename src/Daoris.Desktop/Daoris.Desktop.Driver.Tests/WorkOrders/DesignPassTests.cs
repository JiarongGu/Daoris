using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The design pass the brainstorm runs. Its packet is the purpose brief alone, its designers and comparison reader
/// are told their jobs, its comparison is judged and kept in the document, and every brainstorm keeps its consultation.
/// </summary>
public sealed class DesignPassTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static BrainstormRequest Request(string? pass) =>
        new("d1", "A tool that runs a company of agents for one owner.", "daoris", ["a", "b"], At, ["docs/a.md"], pass);

    private static BrainstormAnonymous Handed(string id, string maker) =>
        new(id, new BrainstormBy($"g-{id}", maker, $"{maker}-door", null),
            new BrainstormProposal("One wait reader", "Every wait read once.", "medium", "low", "yes", null, "high", [], []));

    private static JsonObject Synthesis() => new()
    {
        ["brainstorm"] = 1,
        ["groups"] = new JsonArray(new JsonObject
        {
            ["title"] = "One wait reader", ["idea"] = "Every wait read once.", ["members"] = new JsonArray("P1", "P2"), ["rank"] = 1,
            ["why"] = "Most value for its effort.", ["flags"] = new JsonArray(),
        }),
        ["leftOut"] = new JsonArray(),
    };

    private static JsonObject Comparison() => new()
    {
        ["route"] = "Evolve the engine in place.",
        ["why"] = "Most target concepts stand in part already.",
        ["flips"] = new JsonArray("Two of three trial releases need a hand repair."),
        ["trial"] = new JsonObject
        {
            ["area"] = "the release workflow", ["baseline"] = "hand repairs per release, measured this week",
            ["keep"] = new JsonArray("no hand repair in three releases"), ["retire"] = new JsonArray("a hand repair in two of three"),
        },
        ["questions"] = new JsonArray(new JsonObject
        {
            ["question"] = "Is a second engine allowed?", ["yes"] = "a trial starts beside", ["no"] = "the engine evolves",
            ["where"] = "the comparison's route",
        }),
    };

    [Fact]
    public void A_design_pass_packet_holds_the_purpose_brief_alone()
    {
        var packet = DesignPasses.Packet(Request(DesignPasses.Design));

        Assert.Equal("A tool that runs a company of agents for one owner.", packet.Question);
        Assert.Empty(packet.Sources);
        Assert.Null(packet.Repository);
        Assert.Null(packet.Tier);
    }

    [Fact]
    public void A_design_pass_tells_its_designers_and_its_comparison_reader_their_jobs_and_a_brainstorm_is_unchanged()
    {
        Assert.Equal(BrainstormInstruction.Generator("sha256:x", 10), DesignPasses.Generator(Request(null), "sha256:x", 10));
        Assert.Equal(BrainstormInstruction.Synthesizer("sha256:x"), DesignPasses.Synthesizer(Request(null), "sha256:x"));
        Assert.Contains("purpose brief", DesignPasses.Generator(Request("design"), "sha256:x", 20), StringComparison.Ordinal);
        var comparer = DesignPasses.Synthesizer(Request("design"), "sha256:x");
        Assert.Contains("`comparison`", comparer, StringComparison.Ordinal);
        Assert.Contains("written now, before any trial", comparer, StringComparison.Ordinal);
    }

    [Fact]
    public void A_design_pass_synthesis_without_its_comparison_is_refused_and_a_brainstorm_one_is_not()
    {
        var handed = new[] { Handed("P1", "anthropic"), Handed("P2", "openai") };
        var text = Synthesis().ToJsonString();

        Assert.True(DesignPasses.Check(Request(null), text, handed).Valid);
        var refused = DesignPasses.Check(Request("design"), text, handed);
        Assert.False(refused.Valid);
        Assert.Equal([DesignPasses.NoComparison], refused.Codes);
    }

    [Fact]
    public void A_comparison_missing_its_criteria_is_refused_by_the_field_it_lacks()
    {
        var synthesis = Synthesis();
        var comparison = Comparison();
        ((JsonObject)comparison["trial"]!).Remove("retire");
        comparison["flips"] = new JsonArray();
        synthesis["comparison"] = comparison;

        var refused = DesignPasses.Check(Request("design"), synthesis.ToJsonString(), [Handed("P1", "anthropic"), Handed("P2", "openai")]);

        Assert.False(refused.Valid);
        Assert.Contains("comparison:flips", refused.Codes);
        Assert.Contains("comparison:trial.retire", refused.Codes);
    }

    [Fact]
    public void A_whole_comparison_is_kept_in_the_document_with_its_questions_keyed()
    {
        var synthesis = Synthesis();
        synthesis["comparison"] = Comparison();
        var handed = new[] { Handed("P1", "anthropic"), Handed("P2", "openai") };

        var check = DesignPasses.Check(Request("design"), synthesis.ToJsonString(), handed);
        Assert.True(check.Valid);
        var comparison = check.Synthesis!.Comparison!;
        Assert.Equal("Evolve the engine in place.", comparison.Route);
        Assert.Equal("Q1", Assert.Single(comparison.Questions).Key);

        var packet = DesignPasses.Packet(Request("design"));
        var document = BrainstormDocuments.Ranked("d1", packet, [], handed, check.Synthesis, []);
        var markdown = BrainstormDocuments.Markdown(document);
        Assert.Contains("## The comparison", markdown, StringComparison.Ordinal);
        Assert.Contains("Retired if:", markdown, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(BrainstormDocuments.Json(document));
        Assert.Equal("the release workflow", json.RootElement.GetProperty("comparison").GetProperty("trial").GetProperty("area").GetString());
        Assert.DoesNotContain("## The comparison", BrainstormDocuments.Markdown(document with { Comparison = null }), StringComparison.Ordinal);
    }

    [Fact]
    public void A_consultation_records_each_giver_and_counts_two_views_only_from_two_makers()
    {
        BrainstormPassRecord Pass(string pass, string maker, string role) =>
            new(new BrainstormBy(pass, maker, $"{maker}-door", "acct"), role, BrainstormPassRecord.Answered, [], 1);
        var two = new BrainstormDocument("d1", "q", "sha256:p", [Pass("g1", "anthropic", "generate"), Pass("g2", "openai", "generate"),
            Pass("s1", "anthropic", "synthesize")], [], [], [], null);

        var record = Consultations.Of(two, "design");
        Assert.True(record.Independent);
        Assert.All(record.Givers.Where(giver => giver.Role == "generate"), giver =>
        {
            Assert.Equal(Consultations.IndependentMode, giver.Mode);
            Assert.False(giver.SawAnother);
            Assert.Empty(giver.NotHeld);
            Assert.Equal("sha256:p", giver.Read);
        });
        var review = Assert.Single(record.Givers, giver => giver.Role == "synthesize");
        Assert.Equal(Consultations.ReviewMode, review.Mode);
        Assert.True(review.SawAnother);

        var one = Consultations.Of(two with { Passes = [Pass("g1", "anthropic", "generate"), Pass("g2", "anthropic", "generate")] }, "design");
        Assert.False(one.Independent);
        Assert.All(one.Givers, giver => Assert.Equal([Consultations.SameMaker], giver.NotHeld));
        using var json = JsonDocument.Parse(Consultations.Json(one));
        Assert.Equal(1, json.RootElement.GetProperty("consultation").GetInt32());
        Assert.False(json.RootElement.GetProperty("independent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("changed").ValueKind);
    }

    [Fact]
    public void The_records_read_a_request_s_pass_and_keep_the_consultation_beside_the_document()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "design-pass", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var files = new BrainstormFiles(home);
            Directory.CreateDirectory(files.Folder("d1"));
            File.WriteAllText(files.PathOf("d1", "request.json"),
                "{ \"brainstorm\": 1, \"id\": \"d1\", \"question\": \"The purpose.\", \"repository\": null, \"agents\": [], "
                + "\"at\": \"2026-11-16T09:00:00+00:00\", \"pass\": \"design\" }", Encoding.UTF8);

            Assert.Equal(DesignPasses.Design, files.Request("d1")!.Pass);
            files.WriteDocument("d1", new BrainstormDocument("d1", "The purpose.", "sha256:p", [], [], [], [], "no answer"));
            using var json = JsonDocument.Parse(File.ReadAllText(files.PathOf("d1", "consultation.json")));
            Assert.Equal("design", json.RootElement.GetProperty("kind").GetString());
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }
}
