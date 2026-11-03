using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// WORKFLOWCLEAN1 1e (the cleanup design §4.4): <c>CONVERT_PLAN</c>, <c>CONVERT_APPLY</c> and <c>CONVERT_UNDO</c> over the bridge. The digest the plan
/// answers is the one the apply names; a plan that moved is an answer, <c>moved</c>, with the plan now and nothing written; the undo is a plan of its
/// own. The conversion's every shape and boundary is the driver's own <c>ConversionTests</c>.
/// </summary>
public sealed class DriverModuleConvertTests : DriverModuleBridge
{
    private const string Config = """
        {
          "adapter": "claude-code-acp",
          "drivable": ["engine"],
          "landings": { "atlas": { "form": "merge", "autoAccept": true } }
        }
        """;

    private async Task<DriverModule> ModuleAsync()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var client = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn()));
        var loop = Loop();
        await loop.ComeUpAsync(client);
        return new DriverModule(Bus, loop);
    }

    [Fact]
    public async Task The_apply_converts_the_plan_it_names_by_its_digest_and_keeps_its_receipt()
    {
        var module = await ModuleAsync();
        var plan = await AnswerAsync(module, "CONVERT_PLAN", new { });
        var digest = plan.GetProperty("digest").GetString()!;
        Assert.False(plan.GetProperty("empty").GetBoolean());
        Assert.False(plan.GetProperty("census").GetProperty("ended").GetBoolean());

        var answer = await AnswerAsync(module, "CONVERT_APPLY", new { digest });

        Assert.True(answer.GetProperty("done").GetBoolean(), answer.ToString());
        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("merge-after-accept", config.DefaultWorkflow);
        Assert.Equal("merge-automatic", WorkflowSelection.Resolve(config, "atlas", "aurora", null).Workflow);
        Assert.False(config.Landings.ContainsKey("atlas"));
        Assert.True(new ConversionReceipts(Home).Read(digest)!.Applied);
        // Planned again, only what is kept is left.
        Assert.True((await AnswerAsync(module, "CONVERT_PLAN", new { })).GetProperty("empty").GetBoolean());
    }

    [Fact]
    public async Task A_terminals_plan_and_apply_say_the_verbs_own_lines()
    {
        var module = await ModuleAsync();

        var plan = (await AnswerAsync(module, "CONVERT_PLAN", new { said = true })).GetProperty("said");
        var lines = plan.GetProperty("out").EnumerateArray().Select(line => line.GetString()!).ToList();
        Assert.Equal(0, plan.GetProperty("code").GetInt32());
        Assert.StartsWith("workflow convert:", lines[0]);
        var digest = lines[^1][(lines[^1].LastIndexOf(' ') + 1)..];
        Assert.Contains("workflow convert --apply", lines[^1]);

        var applied = (await AnswerAsync(module, "CONVERT_APPLY", new { digest, said = true })).GetProperty("said");

        Assert.Equal(0, applied.GetProperty("code").GetInt32());
        Assert.Equal("workflow convert: the conversion is applied.", applied.GetProperty("out")[0].GetString());
    }

    [Fact]
    public async Task A_plan_that_moved_is_refused_with_the_plan_now_and_nothing_is_written()
    {
        var module = await ModuleAsync();
        var plan = await AnswerAsync(module, "CONVERT_PLAN", new { });
        var digest = plan.GetProperty("digest").GetString()!;
        File.WriteAllText(DriverConfigPath, Config.Replace(", \"autoAccept\": true", "", StringComparison.Ordinal));

        var answer = await AnswerAsync(module, "CONVERT_APPLY", new { digest });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(ConversionRefusals.Moved, answer.GetProperty("refused").GetString());
        Assert.NotEqual(digest, answer.GetProperty("plan").GetProperty("digest").GetString());
        Assert.Null(DriverConfig.Load(DriverConfigPath).DefaultWorkflow);
        Assert.Empty(new ConversionReceipts(Home).All());
    }

    [Fact]
    public async Task A_digest_that_is_none_is_refused_and_the_undo_is_a_plan_of_its_own()
    {
        var module = await ModuleAsync();
        var none = await AnswerAsync(module, "CONVERT_APPLY", new { digest = "plan" });
        Assert.Equal(ConversionRefusals.Digest, none.GetProperty("refused").GetString());

        var before = await AnswerAsync(module, "CONVERT_PLAN", new { undo = true });
        Assert.Contains("nothing to undo", before.GetProperty("refusal").GetString());

        var digest = (await AnswerAsync(module, "CONVERT_PLAN", new { })).GetProperty("digest").GetString()!;
        await AnswerAsync(module, "CONVERT_APPLY", new { digest });
        var undo = await AnswerAsync(module, "CONVERT_PLAN", new { undo = true });
        var undoDigest = undo.GetProperty("digest").GetString()!;

        var answer = await AnswerAsync(module, "CONVERT_UNDO", new { digest = undoDigest });

        Assert.True(answer.GetProperty("done").GetBoolean(), answer.ToString());
        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Null(config.DefaultWorkflow);
        Assert.True(config.Landings["atlas"].AutoAccept);
    }

    /// <summary>The registry holds atlas, its checkout elsewhere; no quest is open.</summary>
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
