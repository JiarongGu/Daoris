using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>PLACE_APPLY</c> over the bridge. The digest the plan answers is the one the apply
/// names; the configuration it plans is written in one edit and its receipt kept; a digest the plan no longer has is an answer, <c>moved</c>,
/// with nothing written. The steps' every boundary is the driver's own <c>PlaceApplyTests</c>.
/// </summary>
public sealed class DriverModulePlaceApplyTests : DriverModuleBridge
{
    private const string Config = """
        {
          "adapter": "claude-code-acp",
          "drivable": ["engine"],
          "trees": ["engine"]
        }
        """;

    private static readonly object Ask = new { repositories = new[] { "atlas" }, preset = SetupPresets.Merge };

    private async Task<DriverModule> ModuleAsync()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var client = new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandIn())));
        var loop = Loop();
        await loop.ComeUpAsync(client);
        return new DriverModule(Bus, loop);
    }

    [Fact]
    public async Task The_apply_writes_the_plan_it_names_by_its_digest_and_keeps_its_receipt()
    {
        var module = await ModuleAsync();
        var plan = await AnswerAsync(module, "PLACE_PLAN", Ask);
        var digest = plan.GetProperty("digest").GetString()!;
        Assert.False(DriverConfig.Load(DriverConfigPath).OpensOwnTree("atlas"));

        var answer = await AnswerAsync(module, "PLACE_APPLY", new { repositories = new[] { "atlas" }, preset = SetupPresets.Merge, digest });

        Assert.True(answer.GetProperty("done").GetBoolean(), answer.ToString());
        Assert.Equal(digest, answer.GetProperty("digest").GetString());
        // A delivery is a saved workflow's: its copy is saved first, then the configuration chooses it.
        Assert.Equal("workflow `merge-after-accept` saved; configuration applied.", answer.GetProperty("message").GetString());
        var operations = answer.GetProperty("operations").EnumerateArray().ToList();
        Assert.Equal(2, operations.Count);
        var operation = Assert.Single(operations, each => each.GetProperty("id").GetString() == PlaceOperations.Configuration);
        Assert.Equal((PlaceOperations.Configuration, PlaceOutcomes.Done), (operation.GetProperty("id").GetString(), operation.GetProperty("outcome").GetString()));
        Assert.All(operations, each => Assert.Equal(PlaceOutcomes.Done, each.GetProperty("outcome").GetString()));
        Assert.True(DriverConfig.Load(DriverConfigPath).OpensOwnTree("atlas"));
        Assert.Equal(PlaceOutcomes.Done, new PlaceReceipts(Home).Read(digest)!.Of(PlaceOperations.Configuration)!.Outcome);
    }

    /// <summary>
    /// A terminal's apply, <c>said</c>, plans now and applies that plan in the verb's own lines, the alone path's: the
    /// plan's lines without the <c>--plan</c> closing line, then the outcome, exit 0; and the configuration is written.
    /// </summary>
    [Fact]
    public async Task A_terminals_apply_says_the_plan_then_the_outcome_in_the_verbs_own_lines()
    {
        var module = await ModuleAsync();
        var plan = await PlaceCommandPlan(module);

        var answer = (await AnswerAsync(module, "PLACE_APPLY", new { repositories = new[] { "atlas" }, preset = SetupPresets.Merge, said = true, accept = Array.Empty<object>() })).GetProperty("said");

        var lines = answer.GetProperty("out").EnumerateArray().Select(line => line.GetString()).ToList();
        Assert.Equal(0, answer.GetProperty("code").GetInt32());
        Assert.Equal(plan, lines.Take(plan.Count));
        Assert.Equal("place: workflow `merge-after-accept` saved; configuration applied.", lines[^1]);
        Assert.DoesNotContain(lines, line => line!.StartsWith("--plan:", StringComparison.Ordinal));
        Assert.True(DriverConfig.Load(DriverConfigPath).OpensOwnTree("atlas"));
    }

    private async Task<List<string?>> PlaceCommandPlan(DriverModule module)
    {
        var said = (await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "atlas" }, preset = SetupPresets.Merge, said = true })).GetProperty("said");
        return [.. said.GetProperty("out").EnumerateArray().Select(line => line.GetString()).SkipLast(1)];
    }

    /// <summary>The apply names its repositories as `repositories`; a single `repository`, which its owner row and grounds do not read, is refused.</summary>
    [Fact]
    public async Task An_apply_naming_a_single_repository_is_refused_and_nothing_is_written()
    {
        var module = await ModuleAsync();
        var written = File.ReadAllBytes(DriverConfigPath);

        var refusal = await RefusalAsync(module, "PLACE_APPLY", new { repository = "atlas", preset = SetupPresets.Merge, digest = new string('0', 64) });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
    }

    [Fact]
    public async Task A_digest_the_plan_no_longer_has_is_refused_moved_and_nothing_is_written()
    {
        var module = await ModuleAsync();
        var written = File.ReadAllBytes(DriverConfigPath);
        var stale = new string('0', 64);

        var answer = await AnswerAsync(module, "PLACE_APPLY", new { repositories = new[] { "atlas" }, preset = SetupPresets.Merge, digest = stale });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(PlaceApplyRefusals.Moved, answer.GetProperty("refused").GetString());
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
        Assert.Null(new PlaceReceipts(Home).Read(stale));
    }

    /// <summary>🔴 A restart reads the receipts: a step a stop left uncertain is settled by its postcondition at the loop's come-up, writing no configuration.</summary>
    [Fact]
    public async Task A_come_up_settles_an_uncertain_step_by_its_postcondition()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var digest = new string('b', 64);
        var receipts = new PlaceReceipts(Home);
        receipts.Write(new PlaceReceipt(digest, DateTimeOffset.UtcNow, new PlaceAsk(["atlas"], SetupPresets.Merge), [],
        [
            new PlaceOperation(PlaceOperations.Configuration, PlaceOperations.Configuration, null, "a tree per session", PlaceOutcomes.Uncertain),
        ]));
        var written = File.ReadAllBytes(DriverConfigPath);
        var loop = Loop();

        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandIn()))));
        await loop.Settling;

        // Atlas still has no tree of its own, so the edit did not land: pending again, and nothing was written but the receipt.
        Assert.Equal(PlaceOutcomes.Pending, receipts.Read(digest)!.Of(PlaceOperations.Configuration)!.Outcome);
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
    }

    /// <summary>The registry holds atlas, its checkout elsewhere: no line is read, so no git runs.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/api/registry" => """[{"repository":"engine","workspace":"aurora","adopted":true,"root":"elsewhere-engine"},{"repository":"atlas","workspace":"aurora","root":"elsewhere-atlas"}]""",
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
