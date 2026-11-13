using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The ask's plan on the wire: read with the reader's blobs (an order whose evidence moved is named stale), the person's answer and waiver
/// at the settle door with the service's sentence where it refuses, and each ask carrying the questions its plan still asks, which the bell
/// and What needs you are made from.
/// </summary>
public sealed class AskPlanHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private static readonly DeliveryRef Schema = new("Keeper", "migration"), Mirror = new("Asker", "mirror-api");

    private async Task<string> PlannedAsync([System.Runtime.CompilerServices.CallerMemberName] string by = "")
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = $"Ship the schema change through the mirror and the web app ({by}).", to = "Keeper" });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var edge = new DeliveryEdge(Mirror, Schema, "The mirror reads what the migration changes.", DeliveryEdge.CodeRead, new("Asker", "src/client.ts", "1-9", "old-blob"));
        var saved = await host.Composed.Teams.SaveAsync(
            new TeamSave(ask, [new("keel", "Keeper", "Keel", "Do it.", "it works")], [], "plan")
            {
                Deployables = [new(Schema, new("Keeper", "daoris.json", "", "b1")), new(Mirror, new("Asker", "daoris.json", "", "b2"))],
                Releases = [edge],
                Questions = [new("contract:viewers", DeliveryQuestion.Open, "Do admins or viewers see candidates?") { Yes = "Both see them.", Blocks = "The web app.", Default = "Admins only." }],
            },
            DateTimeOffset.UtcNow);
        Assert.Equal(TeamRefusal.None, saved.Refusal);
        return ask;
    }

    [Fact]
    public async Task An_asks_open_plan_questions_ride_on_the_ask_and_an_answer_at_the_settle_door_removes_them()
    {
        var ask = await PlannedAsync();
        Assert.Contains(("POST", "/api/asks/{id}/plan/settle"), host.Routes());

        var listed = (await host.GetAsync("/api/asks")).Json.EnumerateArray().Single(row => row.GetProperty("id").GetString() == ask);
        var question = Assert.Single(listed.GetProperty("planQuestions").EnumerateArray());
        Assert.Equal("contract:viewers", question.GetProperty("key").GetString());
        Assert.Equal("Both see them.", question.GetProperty("yes").GetString());

        var answered = await host.PostAsync($"/api/asks/{ask}/plan/settle", new { key = "contract:viewers", words = "Admins only." });

        Assert.Equal(200, answered.Status);
        var revision = answered.Json.GetProperty("plans")[0];
        Assert.Equal(0, revision.GetProperty("questions").GetArrayLength());
        Assert.Equal("Admins only.", revision.GetProperty("answers")[0].GetProperty("words").GetString());
        Assert.False((await host.GetAsync($"/api/asks/{ask}")).Json.TryGetProperty("planQuestions", out _));
        Assert.False((await host.GetAsync("/api/asks")).Json.EnumerateArray().Single(row => row.GetProperty("id").GetString() == ask).TryGetProperty("planQuestions", out _));
    }

    [Fact]
    public async Task A_waiver_names_both_deployables_and_a_refusal_is_the_services_sentence()
    {
        var ask = await PlannedAsync();

        var badRef = await host.PostAsync($"/api/asks/{ask}/plan/settle", new { deployable = "mirror-api", on = "Keeper/migration", reason = "x" });
        var noOrder = await host.PostAsync($"/api/asks/{ask}/plan/settle", new { deployable = "Asker/mirror-api", on = "Keeper/other", reason = "x" });
        var unknown = await host.PostAsync($"/api/asks/{ask}/plan/settle", new { key = "contract:nobody", words = "yes" });
        var waived = await host.PostAsync($"/api/asks/{ask}/plan/settle", new { deployable = "Asker/mirror-api", on = "Keeper/migration", reason = "Additive change." });

        Assert.Equal(400, badRef.Status);
        Assert.Equal(400, noOrder.Status);
        Assert.Equal(400, unknown.Status);
        Assert.Contains("No open question", unknown.Json.GetProperty("error").GetString());
        Assert.Equal(200, waived.Status);
        Assert.Equal("Additive change.", waived.Json.GetProperty("plans")[0].GetProperty("releases").EnumerateArray()
            .Single(release => release.GetProperty("deployable").GetString() == "Asker/mirror-api").GetProperty("waived").GetString());
    }

    [Fact]
    public async Task A_plan_read_with_the_callers_blobs_names_the_order_whose_evidence_moved_and_asks_once_about_it()
    {
        var ask = await PlannedAsync();

        var plain = await host.PostAsync($"/api/asks/{ask}/plan", new { });
        var seen = await host.PostAsync($"/api/asks/{ask}/plan", new { seen = new Dictionary<string, string> { ["Asker:src/client.ts"] = "new-blob" } });

        Assert.False(plain.Json.GetProperty("plans")[0].GetProperty("releases").EnumerateArray().Any(release => release.GetProperty("stale").GetBoolean()));
        var revision = seen.Json.GetProperty("plans")[0];
        Assert.True(revision.GetProperty("releases").EnumerateArray().Single(release => release.GetProperty("deployable").GetString() == "Asker/mirror-api").GetProperty("stale").GetBoolean());
        Assert.Equal(["contract:viewers", "stale:Asker/mirror-api>Keeper/migration:code-read@new-blob"],
            revision.GetProperty("questions").EnumerateArray().Select(each => each.GetProperty("key").GetString()));
    }
}
