using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ONEWAY1ad-ii (the oneway design §1.3): <c>KEPT_READS</c>, This machine's list of old records over the bridge: the workflow part's census as the
/// loop read it at its come-up, first, then every entry of the register with its condition's state, and the home's floor. The list's own rules are
/// the driver's <c>TransitionalRegisterTests</c>.
/// </summary>
public sealed class DriverModuleKeptReadsTests : DriverModuleBridge
{
    [Fact]
    public async Task ONEWAY1ad_ii_the_list_says_the_workflow_parts_census_first_then_each_kept_read_and_the_floor()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code-acp", "landings": { "atlas": { "form": "merge" } } }""");
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandIn())));
        await loop.Settling;
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "KEPT_READS", new { });

        var lines = answer.GetProperty("lines").EnumerateArray().ToList();
        // ONEWAY1b: the staffing part's census beside the workflow part's, in apply order; this home's staffing is not imported, so it holds nothing.
        Assert.Equal(TransitionalRegister.Entries.Count + 2, lines.Count);
        Assert.Equal([("part", "staffing", true), ("part", "workflows", false)], lines.Take(2).Select(each =>
            (each.GetProperty("kind").GetString(), each.GetProperty("id").GetString(), each.GetProperty("ended").GetBoolean())));
        Assert.True(lines[1].GetProperty("count").GetInt32() > 0);
        var cycles = lines.Single(each => each.GetProperty("id").GetString() == "work-cycles");
        Assert.True(cycles.GetProperty("ended").GetBoolean());
        Assert.Equal("WORKFLOWCLEAN1j", cycles.GetProperty("row").GetString());
        var steps = lines.Single(each => each.GetProperty("id").GetString() == "steps-engine");
        Assert.False(steps.GetProperty("ended").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, answer.GetProperty("floor").GetProperty("build").ValueKind);
    }

    /// <summary>The registry holds atlas; no quest is open.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry" ? """[{"repository":"atlas","workspace":"aurora","root":"elsewhere-atlas"}]""" : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
