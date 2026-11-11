using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A person's press from a terminal no Daoris session owns, judged by the machine's terminal tier: under <c>act</c>, from a process the
/// pipe places outside every agent Daoris runs, it runs at once as the person's own with no card, kept in the pressed list as a
/// terminal's and logged; under <c>ask</c>, the default, it waits on the card as before; a process inside an agent's job or one the pipe
/// cannot place is asked; a session's proved press keeps its own tier; and a refusal answers the terminal at once under both.
/// </summary>
/// <remarks>Each test serves its own pipe in this process, named by its own scratch home's id. The pipe is Windows': elsewhere a test returns at once.</remarks>
public sealed class PipeTerminalTierTests : Bridge
{
    private const string Archived = "s1a2b3c4";

    /// <summary>A press whose grounds the stand-in reads, so one asked reaches its card.</summary>
    private static readonly object Archive = new { ids = new[] { Archived }, archived = true };

    private readonly SessionDelegates _delegates = new();
    private readonly ConcurrentDictionary<string, string> _modes = new(StringComparer.Ordinal);

    [Fact]
    public async Task Under_act_a_terminal_s_press_runs_at_once_with_no_card_listed_as_a_terminal_s_and_logged()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(TerminalTiers.Act, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "WORK_PAUSE", new { quest = "q1" }));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran("WORK_PAUSE"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(((string?)null, true), Assert.Single(driver.Doors));
        Assert.Equal((TerminalTierBy.Terminal, (string?)null), Assert.Single(driver.By));
        await UntilAsync(() => pipe.TerminalActs!.Now().Presses is [{ Outcome: TerminalActs.Done }]);
        var kept = Assert.Single(pipe.TerminalActs!.Now().Presses);
        Assert.Equal(("WORK_PAUSE", "quest", "q1", (string?)null), (kept.Route, kept.Scope, kept.Target, kept.Code));
        Assert.Empty(pipe.StandingConfirmation.Now().HelpPresses);
        Assert.Contains(
            $$"""{"route":"{{DriverModule.Name}}.WORK_PAUSE","by":"terminal","door":"terminal"}""", Logged(PipeDelegate.TerminalPressedEvent));
        Assert.Empty(Logged(PipeDelegate.PressedEvent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TerminalTiers.Ask)]
    public async Task Under_ask_and_with_no_tier_a_terminal_s_press_waits_on_the_card_as_before(string? tier)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(tier, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, "SESSION_ARCHIVE", Archive);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
        Assert.Empty(pipe.StandingConfirmation.Now().HelpPresses);
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(Assert.Single(pipe.Confirmations.Now().Waiting).Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran("SESSION_ARCHIVE"));
        Assert.Equal((TerminalTierBy.Card, (string?)null), Assert.Single(driver.By));
        Assert.Empty(pipe.TerminalActs!.Now().Presses);
        Assert.Empty(Logged(PipeDelegate.TerminalPressedEvent));
    }

    [Fact]
    public async Task Under_act_a_terminal_s_press_its_route_refuses_is_kept_failed_with_its_code()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(TerminalTiers.Act, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "WORK_PAUSE", new { quest = StandIn.Refuses }));

        Assert.False(answer.GetProperty("success").GetBoolean());
        await UntilAsync(() => pipe.TerminalActs!.Now().Presses is [{ Outcome: TerminalActs.Failed }]);
        var kept = Assert.Single(pipe.TerminalActs!.Now().Presses);
        Assert.Equal(answer.GetProperty("error").GetProperty("code").GetString(), kept.Code);
        Assert.NotNull(kept.Ended);
    }

    /// <summary>A route answers a refusal as data (<c>done: false</c>), or a verb it ran said it with a code beside its lines: the press did not happen, and the record says so.</summary>
    [Theory]
    [InlineData(StandIn.Declines)]
    [InlineData(StandIn.Exits)]
    public async Task Under_act_a_press_answered_as_not_done_is_kept_failed_though_the_pipe_succeeded(string quest)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(TerminalTiers.Act, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "WORK_PAUSE", new { quest }));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        await UntilAsync(() => pipe.TerminalActs!.Now().Presses is [{ Outcome: not TerminalActs.Running }]);
        Assert.Equal(TerminalActs.Failed, Assert.Single(pipe.TerminalActs!.Now().Presses).Outcome);
    }

    [Fact]
    public async Task Under_act_Ask_Daoris_s_press_in_full_access_is_its_own_and_never_a_terminal_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(TerminalTiers.Act, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, "h1", key, client.Nonce!);
        await UntilAsync(() => driver.Ran("WORK_PAUSE") == 1);

        Assert.Equal((TerminalTierBy.AskDaoris, "h1"), Assert.Single(driver.By));
        Assert.Empty(pipe.TerminalActs!.Now().Presses);
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses.Count == 1);
    }

    [Theory]
    [InlineData(StandingPlace.InAgentJob)]
    [InlineData(StandingPlace.Untold)]
    public async Task Under_act_a_process_inside_an_agent_s_job_or_one_the_pipe_cannot_place_is_asked_on_the_card(StandingPlace place)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(TerminalTiers.Act, place);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        await client.SendAsync(DriverModule.Name, "SESSION_ARCHIVE", Archive);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
        Assert.Empty(pipe.StandingConfirmation.Now().HelpPresses);
    }

    [Fact]
    public async Task Under_act_a_session_s_proved_press_keeps_its_own_tier()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(TerminalTiers.Act, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;

        await SendProvedAsync(client, "SESSION_ARCHIVE", Archive, "h1", key, client.Nonce!);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);

        var card = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(("h1", true), (card.Asker.Session, card.Asker.Help));
        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
        Assert.Empty(pipe.TerminalActs!.Now().Presses);
    }

    [Theory]
    [InlineData(TerminalTiers.Act)]
    [InlineData(TerminalTiers.Ask)]
    public async Task A_refusal_answers_the_terminal_at_once_under_both_tiers_and_nothing_runs(string tier)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, driver, services, log) = await ServeAsync(tier, StandingPlace.Outside);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "GIT_IDENTITY", new { name = "work", token = "t0k3n" }));

        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(InstancePipe.Shape, answer.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, driver.Ran("GIT_IDENTITY"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(pipe.StandingConfirmation.Now().HelpPresses);
    }

    /// <summary>The driver's stand-in: each press counted by route and answered done, with the session it ran as and whether it named the owner's.</summary>
    private sealed class StandIn : ModuleBase
    {
        private readonly ConcurrentDictionary<string, int> _ran = new(StringComparer.Ordinal);

        public override string ModuleName => DriverModule.Name;

        /// <summary>The quest whose pause the stand-in refuses, as a route's judge refuses.</summary>
        public const string Refuses = "q-refused";

        /// <summary>The quest whose pause the stand-in answers <c>done: false</c>, a refusal as data.</summary>
        public const string Declines = "q-declined";

        /// <summary>The quest whose pause the stand-in answers as a verb's lines and a non-zero exit code.</summary>
        public const string Exits = "q-exits";

        public ConcurrentQueue<(string? Worker, bool Owned)> Doors { get; } = new();

        /// <summary>Who pressed each press, as the route reads it.</summary>
        public ConcurrentQueue<(string By, string? Session)> By { get; } = new();

        public int Ran(string route) => _ran.GetValueOrDefault(route);

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            _ran.AddOrUpdate(request.Type, 1, (_, count) => count + 1);
            Doors.Enqueue((PipeDoor.Worker, DriverModule.OwnerDoor(request.Type, request.Payload) is { Count: > 0 }));
            By.Enqueue(PipeDoor.By);
            if (request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty("quest", out var quest) && quest.GetString() == Refuses)
            {
                throw new DriverException("this quest is not paused here.");
            }

            if (request.Payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("quest", out var named))
            {
                if (named.GetString() == Declines) return Task.FromResult<object?>(new { Done = false, Message = "not paused." });
                if (named.GetString() == Exits) return Task.FromResult<object?>(new { Said = new { Out = Array.Empty<string>(), Error = new[] { "refused." }, Code = 1 } });
            }

            return Task.FromResult<object?>(new { Done = true });
        }
    }

    private async Task<(InstancePipe Pipe, TieredClient Client, StandIn Driver, ServiceProvider Services, MachineLog Log)> ServeAsync(
        string? tier, StandingPlace place)
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new StandIn();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(driver);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var standing = new StandingConfirmation(Home, log, new StandingConfirmationTests.Placed(place));
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test", clock: new HeldClock(), standing: standing)
        {
            Grounds = new StubGrounds().Session(Archived, "completed"),
            TerminalTier = () => tier,
            TerminalActs = new TerminalActs(DaorisHome.Entry(Home, HomeEntry.TerminalActs)),
            Delegates = session => new PipeDelegateSession(_delegates.Of(session), _modes.GetValueOrDefault(session)),
        };
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        var hello = await client.HelloAsync(HomeIdentity.Read(Home)!);
        return (pipe, new TieredClient(client, hello.GetProperty("data").GetProperty("nonce").GetString()), driver, provider, log);
    }

    /// <summary>The test's connection and the nonce handed at its hello, which a session's proof is made over.</summary>
    private sealed class TieredClient(PipeClient client, string? nonce) : IAsyncDisposable
    {
        public string? Nonce { get; } = nonce;

        public Task<string> SendAsync(string module, string route, object payload) => client.SendAsync(module, route, payload);

        public Task<JsonElement> AnswerAsync(string id) => client.AnswerAsync(id);

        public Task SendLineAsync(string line) => client.SendLineAsync(line);

        public ValueTask DisposeAsync() => client.DisposeAsync();
    }

    /// <summary>A driver press sent as a session's verb sends it: its payload's text on the line and beside it <c>delegate: { session, proof }</c>.</summary>
    private static async Task SendProvedAsync(TieredClient client, string route, object payload, string session, string key, string nonce)
    {
        var wire = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestKey = Guid.NewGuid().ToString("N");
        var text = JsonSerializer.Serialize(payload, wire);
        var proof = DelegateProof.Prove(key, nonce, DriverModule.Name, route, text, requestKey);
        var said = JsonSerializer.Serialize(new { session, proof }, wire);
        await client.SendLineAsync(
            $$"""{"id":"{{Guid.NewGuid()}}","module":"{{DriverModule.Name}}","type":"{{route}}","payload":{{text}},"key":"{{requestKey}}","{{DelegateProof.Field}}":{{said}}}""");
    }

    /// <summary>The machine log's <paramref name="event"/> lines' data, in order.</summary>
    private List<string> Logged(string @event)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == @event)
            .Select(line => line.GetProperty("data").GetRawText()),
        ];
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }
}
