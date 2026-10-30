using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A repository's integration over the bridge (`IntegrateRoutes.cs`, INTEGRATE1h): the read the view draws and the owner's acts.</summary>
public sealed class DriverModuleIntegrateTests : DriverModuleBridge
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private IntegrationStore Store() => new(Home);

    private void Queue(string id, string branch, string unit) =>
        Assert.Null(Store().Add(new IntegrationRequest(
            id, "engine", "aurora", branch, "1111111111111111111111111111111111111111", IntegrationRoutes.Recorded, "main",
            IntegrationRequester.Card, new LandingIntegrate().Resolved(), At) { Unit = unit }));

    [Fact]
    public async Task The_view_reads_a_repositorys_queue_and_a_unit_reads_its_head()
    {
        Queue("r1", "feature/a", "unit-a");

        var look = await AnswerAsync(Module(), "INTEGRATION", new { repository = "engine" });
        var head = await AnswerAsync(Module(), "INTEGRATION", new { unit = "unit-a" });

        Assert.Equal("r1", look.GetProperty("look").GetProperty("queue")[0].GetProperty("request").GetString());
        Assert.Equal("queued", head.GetProperty("head").GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_drop_closes_a_request_in_no_candidate_and_an_unbuilt_act_changes_nothing()
    {
        Queue("r1", "feature/a", "unit-a");
        var module = Module();

        var refused = await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "decide" });
        Assert.False(refused.GetProperty("done").GetBoolean());
        Assert.Single(Store().Open());

        var dropped = await AnswerAsync(module, "INTEGRATE", new { repository = "engine", act = "drop", request = "r1" });
        Assert.True(dropped.GetProperty("done").GetBoolean());
        Assert.Empty(Store().Open());
    }
}
