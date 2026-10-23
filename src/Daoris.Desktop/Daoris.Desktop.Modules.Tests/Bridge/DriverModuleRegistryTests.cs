using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The repository row's *Refresh* over the bridge (`RegistryRoutes.cs`, WSSETUP5, D124 §3.1): the screen's half of
/// <c>daoris-driver register --repository</c>, through the loop's own client. A refusal is an answer, saying what the row
/// says; held here where no git is asked, the line's read being the driver library's (<c>RegistrationFollowTests</c>).
/// </summary>
public sealed class DriverModuleRegistryTests : DriverModuleBridge
{
    [Fact]
    public async Task Refreshing_a_row_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "REGISTRY_REFRESH", new { repository = "game" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }

    [Fact]
    public async Task A_row_with_no_checkout_here_and_one_the_registry_does_not_hold_are_answered_with_what_the_row_says()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInRegistry())));

        var teammate = await AnswerAsync(module, "REGISTRY_REFRESH", new { repository = "engine" });
        var nobody = await AnswerAsync(module, "REGISTRY_REFRESH", new { repository = "ghost" });

        Assert.Equal("engine", teammate.GetProperty("repository").GetString());
        Assert.Equal(RegistryOutcome.NoCheckout, teammate.GetProperty("outcome").GetString());
        Assert.Contains("its own machine's driver registers it", teammate.GetProperty("said").GetString());
        Assert.False(teammate.GetProperty("registered").GetBoolean());
        Assert.True(teammate.GetProperty("refused").GetBoolean());
        Assert.Equal(RegistryOutcome.NotOnRegistry, nobody.GetProperty("outcome").GetString());

        // Kept for the row, under the loop's home.
        Assert.Equal(RegistryOutcome.NoCheckout, RegistryFollowing.Read(loop.Home)["engine"].Outcome);
    }

    [Fact]
    public async Task A_row_whose_checkout_is_not_where_it_says_has_no_line_to_register_from()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://localhost:5177", null, new HttpClient(new StandInRegistry())));

        var answer = await AnswerAsync(module, "REGISTRY_REFRESH", new { repository = "game" });

        Assert.Equal(RegistryOutcome.NoLine, answer.GetProperty("outcome").GetString());
        Assert.Contains("its checkout is not where its row says", answer.GetProperty("said").GetString());
    }

    /// <summary>
    /// 🔴 SETUPSTATE1 (the owner's report, 2026-10-23): the look's follow registered a set-up repository from its line, adopted, and
    /// no route's press was behind it, so no window was told and the page read it not adopted until a reload. A follow that sent a
    /// registration tells the windows <c>registry</c>; one that sent nothing (unchanged, not set up, refused) tells nothing.
    /// </summary>
    [Fact]
    public async Task A_follow_that_registered_a_repository_tells_the_windows_the_registry_moved()
    {
        var loop = Loop();
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(new StandInRegistry()));
        await loop.ComeUpAsync(client);
        bool Told() => Raised.Any(message => message.Type == StatePublisher.Type
            && message.Payload is StateChanged { Scopes: var scopes } && scopes.SequenceEqual(["registry"]));

        client.Followed(new RegistrationFollowed("game", RegistryOutcome.NotSetUp, "game is not set up"));
        client.Followed(new RegistrationFollowed("engine", RegistryOutcome.Unchanged, "engine is as its row holds"));
        Assert.False(Told());

        client.Followed(new RegistrationFollowed("game", RegistryOutcome.Registered, "registered from its line", "main", "0123456789ab", Sent: true));
        await UntilAsync(Told);
        var told = Assert.Single(Raised, message => message.Type == StatePublisher.Type);
        Assert.Null(((StateChanged)told.Payload!).Sessions);
    }

    /// <summary>The registry door, standing in: `game` with a checkout that is not there, `engine` a teammate's row.</summary>
    private sealed class StandInRegistry : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var gone = Path.Combine(Path.GetTempPath(), "daoris-registry-gone-" + Guid.NewGuid().ToString("N")[..8]).Replace("\\", "\\\\");
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? $$"""[{"repository":"game","workspace":"aurora","adopted":false,"root":"{{gone}}"},{"repository":"engine","workspace":"aurora","adopted":true}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
