using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The person's growth switch at a press's admission, each value in each tier for each of the two presses that open
/// a place: a department's (<c>REGISTRY_CREATE</c>) and a room's (<c>PLACE_APPLY</c> with a <c>room</c>). Ask Daoris in full access and a working
/// session in an unrestricted setup run a press at once as the mode says; <c>asked</c> puts it on a card naming its asker even there; <c>off</c> refuses it
/// in the route's words, recorded as refused. In restricted mode the card waits as always, and the view refuses an <c>off</c> one at the ask. The
/// routes' bodies are stand-ins: what is proved is who runs, asks or refuses.
/// </summary>
public sealed class PipeDelegateGrowthSwitchTests : PipeDelegateHarness
{
    private const string OffOpening = "Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms).";
    private const string OffRoom = "Opening rooms in `engine` is off (the Settings of `engine`, row New repositories and rooms).";

    private static readonly object Creates = new { name = "ledger", purpose = "keeps the books", workspace = "aurora" };

    private static readonly object Rooms = new { repositories = new[] { "engine" }, preset = "", room = "tools/web", purpose = "the web app needs its own conventions" };

    private static (object Payload, string Kind, string Off) Of(string route) =>
        route == "REGISTRY_CREATE" ? (Creates, "repository-create", OffOpening) : (Rooms, "place-room", OffRoom);

    /// <summary>The press's own ground and its card's plan: the route's planner said as its wire says it, with the switch it read.</summary>
    private static StubGrounds Grounds(string growth, string? refusal)
    {
        var grounds = new StubGrounds();
        grounds.Reads["REGISTRY_CREATE_PLAN"] = _ => new
        {
            workspace = "aurora", folder = "/work/aurora/ledger", line = "main", author = new { name = "Fixture", email = "fixture@example.test" },
            steps = new object[] { new { id = "folder", said = "make the folder" } },
            refusals = refusal is null ? Array.Empty<object>() : [new { code = "growth-off", sentence = refusal }],
            digest = new string('a', 64), growth, quests = Array.Empty<object>(),
        };
        grounds.Reads["PLACE_PLAN"] = _ => new
        {
            repositories = new[] { new { repository = "engine", workspace = "aurora" } },
            changes = Array.Empty<object>(),
            refusals = refusal is null ? Array.Empty<object>() : [new { repository = "engine", part = "room", code = "growth-off", sentence = refusal }],
            digest = new string('b', 64), growth,
        };
        return grounds;
    }

    /// <summary>A pipe whose switch reads <paramref name="value"/> for the press of <paramref name="route"/>, and whose plan says the same.</summary>
    private async Task<(InstancePipe Pipe, PipeClient Client, string Nonce, StandIn Driver, IDisposable Held)> ServedAsync(string value, string route)
    {
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        var off = Of(route).Off;
        pipe.Growth = (_, _) => Task.FromResult(new PipeGrowthStanding(value, value == GrowthKey.Off ? off : null));
        pipe.Grounds = Grounds(value, value == GrowthKey.Off ? off : null);
        return (pipe, client, nonce, driver, new Held(pipe, client, services, log));
    }

    private sealed class Held(InstancePipe pipe, PipeClient client, IDisposable services, IDisposable log) : IDisposable
    {
        public void Dispose()
        {
            services.Dispose();
            log.Dispose();
            pipe.Dispose();
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    // ---- Ask Daoris in full access ----

    [Theory]
    [InlineData("REGISTRY_CREATE")]
    [InlineData("PLACE_APPLY")]
    public async Task In_full_access_a_switch_at_mode_runs_the_press_at_once_said_and_kept_as_before(string route)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(GrowthKey.Mode, route);
        using var _ = held;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        var answer = await client.AnswerAsync(await SendProvedAsync(client, route, Of(route).Payload, "h1", key, nonce));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran(route));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(("h1", $"{DriverModule.Name}.{route}"), Assert.Single(_said));
    }

    [Theory]
    [InlineData("REGISTRY_CREATE")]
    [InlineData("PLACE_APPLY")]
    public async Task In_full_access_a_switch_at_asked_puts_the_press_on_a_card_naming_Ask_Daoris_with_the_line_that_says_so_and_runs_it_on_the_persons_yes(string route)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(GrowthKey.Asked, route);
        using var _ = held;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        var id = await SendProvedAsync(client, route, Of(route).Payload, "h1", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(("h1", true, Of(route).Kind), (card.Asker.Session, card.Asker.Help, card.View?.Kind));
        Assert.Contains(card.View!.Also!, said => said.Kind == "growth-asked");
        Assert.Equal(0, driver.Ran(route));
        Assert.Empty(_said);
        var kept = Assert.Single(pipe.StandingConfirmation.Now().HelpPresses);
        Assert.Equal((route, true, HelpPressRecord.Waiting), (kept.Route, kept.Asked, kept.Outcome));

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran(route));
    }

    [Theory]
    [InlineData("REGISTRY_CREATE", OffOpening)]
    [InlineData("PLACE_APPLY", OffRoom)]
    public async Task In_full_access_a_switch_at_off_refuses_the_press_in_the_routes_words_with_no_card_and_keeps_it_as_refused(string route, string words)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(GrowthKey.Off, route);
        using var _ = held;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        var answer = await client.AnswerAsync(await SendProvedAsync(client, route, Of(route).Payload, "h1", key, nonce));

        Assert.Equal((InstancePipe.NotAllowed, words), (Code(answer), answer.GetProperty("error").GetProperty("message").GetString()));
        Assert.Equal(0, driver.Ran(route));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(_said);
        var kept = Assert.Single(pipe.StandingConfirmation.Now().HelpPresses);
        Assert.Equal((route, "h1", HelpPressRecord.Refused, false, words), (kept.Route, kept.Session, kept.Outcome, kept.Asked, kept.Why));
        Assert.Contains(words.Replace("`", "\\u0060"), Logged(PipeDelegate.RefusedEvent).Single().Replace("`", "\\u0060"), StringComparison.Ordinal);
        Assert.Empty(Logged(PipeDelegate.PressedEvent));
    }

    // ---- Ask Daoris in restricted mode ----

    [Theory]
    [InlineData("REGISTRY_CREATE", GrowthKey.Mode, false)]
    [InlineData("REGISTRY_CREATE", GrowthKey.Asked, true)]
    [InlineData("PLACE_APPLY", GrowthKey.Mode, false)]
    [InlineData("PLACE_APPLY", GrowthKey.Asked, true)]
    public async Task In_restricted_mode_the_card_waits_either_way_and_says_the_switch_asks_for_it_only_where_it_does(string route, string value, bool says)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(value, route);
        using var _ = held;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;

        await SendProvedAsync(client, route, Of(route).Payload, "h1", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(("h1", true), (card.Asker.Session, card.Asker.Help));
        Assert.Equal(says, card.View!.Also!.Any(said => said.Kind == "growth-asked"));
        Assert.Equal(0, driver.Ran(route));
    }

    [Theory]
    [InlineData("REGISTRY_CREATE", OffOpening)]
    [InlineData("PLACE_APPLY", OffRoom)]
    public async Task In_restricted_mode_a_switch_at_off_is_refused_at_the_ask_by_the_view_in_the_same_words_and_kept_as_refused(string route, string words)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(GrowthKey.Off, route);
        using var _ = held;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;

        var answer = await client.AnswerAsync(await SendProvedAsync(client, route, Of(route).Payload, "h1", key, nonce));

        Assert.Equal((InstancePipe.NotAllowed, words), (Code(answer), answer.GetProperty("error").GetProperty("message").GetString()));
        Assert.Equal(0, driver.Ran(route));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses.Count == 1);
        var kept = Assert.Single(pipe.StandingConfirmation.Now().HelpPresses);
        Assert.Equal((route, "h1", HelpPressRecord.Refused, words), (kept.Route, kept.Session, kept.Outcome, kept.Why));
    }

    // ---- a working session in an unrestricted setup ----

    [Theory]
    [InlineData("REGISTRY_CREATE")]
    [InlineData("PLACE_APPLY")]
    public async Task A_working_session_at_mode_runs_the_press_as_its_own_at_asked_asks_naming_itself_and_at_off_is_refused(string route)
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var value in new[] { GrowthKey.Mode, GrowthKey.Asked, GrowthKey.Off })
        {
            var (pipe, client, nonce, driver, held) = await ServedAsync(value, route);
            using var _ = held;
            var key = _delegates.Draw("s-quest");
            _access["s-quest"] = AccessRules.Unrestricted;

            var id = await SendProvedAsync(client, route, Of(route).Payload, "s-quest", key, nonce);
            if (value == GrowthKey.Asked)
            {
                await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
                var card = Assert.Single(pipe.Confirmations.Now().Waiting);
                Assert.Equal(("s-quest", false), (card.Asker.Session, card.Asker.Help));
                Assert.Contains(card.View!.Also!, said => said.Kind == "growth-asked");
                Assert.Equal(0, driver.Ran(route));
                Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
                Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
                Assert.Equal(1, driver.Ran(route));
                continue;
            }

            var answer = await client.AnswerAsync(id);
            if (value == GrowthKey.Mode)
            {
                Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
                Assert.Equal(1, driver.Ran(route));
                Assert.Empty(pipe.Confirmations.Now().Waiting);
                Assert.Single(Logged(PipeDelegate.SessionPressedEvent));
            }
            else
            {
                Assert.Equal((InstancePipe.NotAllowed, Of(route).Off), (Code(answer), answer.GetProperty("error").GetProperty("message").GetString()));
                Assert.Equal(0, driver.Ran(route));
                Assert.Empty(pipe.Confirmations.Now().Waiting);
                Assert.Single(Logged(PipeDelegate.RefusedEvent));
            }
        }
    }

    // ---- what the switch does not touch ----

    [Fact]
    public async Task A_switch_that_cannot_be_read_asks_rather_than_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(GrowthKey.Mode, "REGISTRY_CREATE");
        using var _ = held;
        pipe.Growth = (_, _) => throw new IOException("driver.json would not read");
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        await SendProvedAsync(client, "REGISTRY_CREATE", Creates, "h1", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        Assert.Equal(0, driver.Ran("REGISTRY_CREATE"));
        Assert.True(Assert.Single(pipe.Confirmations.Now().Waiting).Asker.Help);
    }

    [Fact]
    public async Task A_place_apply_that_asks_no_room_is_no_press_the_switch_reads()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, held) = await ServedAsync(GrowthKey.Off, "PLACE_APPLY");
        using var _ = held;
        var read = 0;
        pipe.Growth = (_, _) =>
        {
            read++;
            return Task.FromResult(new PipeGrowthStanding(GrowthKey.Off, OffRoom));
        };
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        var answer = await client.AnswerAsync(await SendProvedAsync(client, "PLACE_APPLY", new { repositories = new[] { "engine" }, preset = "", adoption = true }, "h1", key, nonce));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal((1, 0), (driver.Ran("PLACE_APPLY"), read));
    }
}
