using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A person's press a session's verb sends with the proof of the delegate key handed to its start: an Ask Daoris conversation in full
/// access presses as the person, with no card, said on its console and logged; one in restricted mode asks on a card naming Ask Daoris;
/// every other kind asks naming its session; a proof that does not hold, a nonce used already, or a session holding no key is refused,
/// nothing asked or run; and a press with no proof is asked as before.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id, so nothing here meets a running Daoris. The keys are
/// a registry of the test's own, as the driver's processes hold them. The pipe is Windows': elsewhere a test returns at once.
/// </remarks>
public sealed class PipeDelegateTests : Bridge
{
    private const string Archived = "s1a2b3c4";

    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SessionDelegates _delegates = new();
    private readonly ConcurrentDictionary<string, string> _modes = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(string Session, string Route)> _said = new();
    private readonly ConcurrentDictionary<string, string> _access = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _families = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HelpPressPlace> _places = new(StringComparer.Ordinal);

    [Fact]
    public async Task A_full_access_Ask_Daoris_press_runs_at_once_with_no_card_said_on_its_conversation_and_logged()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        var answer = await client.AnswerAsync(await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, "h1", key, nonce));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran("WORK_PAUSE"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(("h1", $"{DriverModule.Name}.WORK_PAUSE"), Assert.Single(_said));
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses is [{ Outcome: HelpPressRecord.Done }]);
        Assert.Equal(("WORK_PAUSE", "h1", "q1"), pipe.StandingConfirmation.Now().HelpPresses.Select(each => (each.Route, each.Session, each.Target)).Single());
        Assert.Contains(
            $$"""{"session":"h1","route":"{{DriverModule.Name}}.WORK_PAUSE","door":"terminal"}""", Logged(PipeDelegate.PressedEvent));
    }

    /// <summary>
    /// <c>daoris ask --to &lt;repo&gt; "…"</c> from a full access conversation is its delegate's press of <c>ASK_NEW</c>: made at once with no
    /// card. In restricted mode the same press asks on a card naming Ask Daoris, and runs nothing before the person's yes.
    /// </summary>
    [Theory]
    [InlineData(HelpModes.Full)]
    [InlineData(HelpModes.Restricted)]
    public async Task Ask_Daoris_s_new_ask_runs_in_full_access_and_asks_on_a_card_in_restricted(string mode)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var grounds = new StubGrounds { Facts = new HelpMachineFacts(["engine"], ["default", "games"], []) };
        grounds.Placed["engine"] = "games";
        grounds.Registered.Add("engine");
        pipe.Grounds = grounds;
        var key = _delegates.Draw("h1");
        _modes["h1"] = mode;

        var id = await SendProvedAsync(client, "ASK_NEW", new { workspace = "games", sentence = "fix the stall", to = "engine", said = true }, "h1", key, nonce);

        if (mode == HelpModes.Full)
        {
            Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
            Assert.Equal(1, driver.Ran("ASK_NEW"));
            Assert.Empty(pipe.Confirmations.Now().Waiting);
            return;
        }

        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(("h1", true, "ask-new-to"), (card.Asker.Session, card.Asker.Help, card.View?.Kind));
        Assert.Equal(0, driver.Ran("ASK_NEW"));
    }

    [Fact]
    public async Task A_restricted_Ask_Daoris_press_asks_on_one_card_naming_Ask_Daoris_and_runs_on_the_person_s_yes()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;

        var id = await SendProvedAsync(client, "SESSION_ARCHIVE", new { ids = new[] { Archived }, archived = true }, "h1", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(("h1", true, (string?)null), (card.Asker.Session, card.Asker.Help, card.Standing));
        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran("SESSION_ARCHIVE"));
        Assert.Empty(_said);
    }

    [Fact]
    public async Task A_restricted_Ask_Daoris_press_beyond_its_focus_says_so_on_its_card_and_is_kept_as_asked_until_answered()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;
        _places["h1"] = new HelpPressPlace(HelpFocus.OfWorkspace("Work"), true, HelpFocus.OfWorkspace("Games"));

        await SendProvedAsync(client, "SESSION_ARCHIVE", new { ids = new[] { Archived }, archived = true }, "h1", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(HelpFocus.OfWorkspace("Work").Wire(), card.HelpPlace!.Place);
        Assert.Equal(HelpFocus.OfWorkspace("Games").Wire(), card.HelpPlace.Focus);
        Assert.True(JsonSerializer.SerializeToElement(card, Wire).GetProperty("helpPlace").GetProperty("beyond").GetBoolean());
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses is [{ Asked: true, Outcome: HelpPressRecord.Waiting, Beyond: true }]);

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: false)).Outcome);
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses is [{ Asked: true, Outcome: HelpPressRecord.Refused }]);
        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
        Assert.Empty(_said);
    }

    [Fact]
    public async Task Another_kind_s_proved_press_asks_naming_its_session_and_an_unproved_press_asks_as_before()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("s-quest");

        await SendProvedAsync(client, "SESSION_ARCHIVE", new { ids = new[] { Archived }, archived = true }, "s-quest", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var proved = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal(("s-quest", false), (proved.Asker.Session, proved.Asker.Help));
        Assert.Equal(Environment.ProcessId, proved.Asker.Pid);

        await client.SendAsync(DriverModule.Name, "SESSION_ARCHIVE", new { ids = new[] { Archived }, archived = false });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 2);
        var unproved = Assert.Single(pipe.Confirmations.Now().Waiting, each => each.Id != proved.Id);
        Assert.Equal(((string?)null, false), (unproved.Asker.Session, unproved.Asker.Help));
        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
    }

    /// <summary>Each refusal is the same code and words, whichever check failed: nothing asked, nothing run, nothing said.</summary>
    [Theory]
    [InlineData("wrong key")]
    [InlineData("other payload")]
    [InlineData("never handed")]
    [InlineData("forgotten")]
    [InlineData("no session read")]
    public async Task A_proof_that_does_not_hold_is_refused_and_nothing_is_asked_or_run(string why)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync(delegates: why != "no session read");
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;
        var session = "h1";
        switch (why)
        {
            case "wrong key":
                key = HolderKey.Draw();
                break;
            case "never handed":
                session = "h9";
                break;
            case "forgotten":
                // As the session's process end and an access swap forget it.
                _delegates.Forget("h1");
                break;
        }

        var id = why == "other payload"
            ? await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, session, key, nonce, proofOver: """{"quest":"q2"}""")
            : await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, session, key, nonce);
        var answer = await client.AnswerAsync(id);

        Assert.Equal(PipeDelegate.Unproved, Code(answer));
        Assert.Equal(PipeDelegate.UnprovedSentence, answer.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(0, driver.Ran("WORK_PAUSE"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(_said);
    }

    /// <summary>The hello's nonce is used up by the first proved press: a second over the same nonce is refused.</summary>
    [Fact]
    public async Task A_nonce_used_once_refuses_the_next_proof_over_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;

        Assert.True((await client.AnswerAsync(await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, "h1", key, nonce)))
            .GetProperty("success").GetBoolean());
        var again = await client.AnswerAsync(await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q2" }, "h1", key, nonce));

        Assert.Equal(PipeDelegate.Unproved, Code(again));
        Assert.Equal(1, driver.Ran("WORK_PAUSE"));
    }

    /// <summary>A working session whose repository's access setup is unrestricted presses with no card as itself, naming nothing the owner's.</summary>
    [Fact]
    public async Task An_unrestricted_working_session_s_press_runs_at_once_as_its_own_and_marks_nothing_the_owner_s()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("s-quest");
        _access["s-quest"] = AccessRules.Unrestricted;

        var answer = await client.AnswerAsync(await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, "s-quest", key, nonce));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran("WORK_PAUSE"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(_said);
        Assert.Equal(("s-quest", false), Assert.Single(driver.Doors));
        Assert.Contains(
            $$"""{"session":"s-quest","route":"{{DriverModule.Name}}.WORK_PAUSE","by":"session s-quest","door":"terminal"}""",
            Logged(PipeDelegate.SessionPressedEvent));
        Assert.Empty(Logged(PipeDelegate.PressedEvent));
    }

    /// <summary>Under asking or the judged default, a working session's press is put on a card naming it, and runs on the person's yes.</summary>
    [Theory]
    [InlineData(AccessRules.Asking)]
    [InlineData(AccessRules.Auto)]
    [InlineData(null)]
    public async Task An_asking_or_judged_working_session_s_press_asks_on_a_card_naming_it(string? permissions)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("s-quest");
        if (permissions is not null) _access["s-quest"] = permissions;

        var id = await SendProvedAsync(client, "SESSION_ARCHIVE", new { ids = new[] { Archived }, archived = true }, "s-quest", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(("s-quest", false), (card.Asker.Session, card.Asker.Help));
        Assert.Equal(0, driver.Ran("SESSION_ARCHIVE"));
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran("SESSION_ARCHIVE"));
        Assert.Equal(((string?)null, true), Assert.Single(driver.Doors));
        Assert.Empty(Logged(PipeDelegate.SessionPressedEvent));
    }

    /// <summary>A granted family of acts runs Ask Daoris's press with no card in restricted mode; a press outside it still asks.</summary>
    [Fact]
    public async Task A_restricted_Ask_Daoris_press_in_a_granted_family_runs_at_once_and_one_outside_asks()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;
        _families["h1"] = [ActFamilies.Pause];

        var answer = await client.AnswerAsync(await SendProvedAsync(client, "WORK_PAUSE", new { quest = "q1" }, "h1", key, nonce));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran("WORK_PAUSE"));
        Assert.Equal(("h1", $"{DriverModule.Name}.WORK_PAUSE"), Assert.Single(_said));
        Assert.Equal(((string?)null, true), Assert.Single(driver.Doors));

        // A fresh connection for the next proof, as each press is proved over its own hello's nonce.
        await using var again = await PipeClient.ConnectAsync(pipe.Serve()!);
        var hello = await again.HelloAsync(HomeIdentity.Read(Home)!);
        await SendProvedAsync(again, "SESSION_ARCHIVE", new { ids = new[] { Archived }, archived = true }, "h1", key,
            hello.GetProperty("data").GetProperty("nonce").GetString()!);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        Assert.Equal(("h1", true), (Assert.Single(pipe.Confirmations.Now().Waiting).Asker.Session, Assert.Single(pipe.Confirmations.Now().Waiting).Asker.Help));
    }

    /// <summary>The driver's stand-in: each press counted by route and answered done, with the session it ran as and whether it named the owner's.</summary>
    private sealed class StandIn : ModuleBase
    {
        private readonly ConcurrentDictionary<string, int> _ran = new(StringComparer.Ordinal);

        public override string ModuleName => DriverModule.Name;

        public ConcurrentQueue<(string? Worker, bool Owned)> Doors { get; } = new();

        public int Ran(string route) => _ran.GetValueOrDefault(route);

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            _ran.AddOrUpdate(request.Type, 1, (_, count) => count + 1);
            Doors.Enqueue((PipeDoor.Worker, DriverModule.OwnerDoor(request.Type, request.Payload) is { Count: > 0 }));
            return Task.FromResult<object?>(new { Done = true });
        }
    }

    private async Task<(InstancePipe Pipe, PipeClient Client, string Nonce, StandIn Driver, ServiceProvider Services, MachineLog Log)> ServeAsync(
        bool delegates = true)
    {
        var log = new MachineLog(Home, "desktop");
        var driver = new StandIn();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<IIpcRequestTracker>(new IpcRequestTracker(Bus, new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton<IIpcModule>(driver);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test", clock: new HeldClock())
        {
            Grounds = new StubGrounds().Session(Archived, "completed"),
        };
        if (delegates)
        {
            pipe.Delegates = session => new PipeDelegateSession(
                _delegates.Of(session), _modes.GetValueOrDefault(session), route => _said.Enqueue((session, route)))
            {
                Permissions = _access.GetValueOrDefault(session),
                Families = _families.GetValueOrDefault(session) ?? [],
                PlaceOf = _places.TryGetValue(session, out var place) ? (_, _) => Task.FromResult<HelpPressPlace?>(place) : null,
            };
        }

        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        var hello = await client.HelloAsync(HomeIdentity.Read(Home)!);
        return (pipe, client, hello.GetProperty("data").GetProperty("nonce").GetString()!, driver, provider, log);
    }

    /// <summary>
    /// A driver press sent as the verb sends a session's: its payload's text on the line as the proof is made over it (or over
    /// <paramref name="proofOver"/>), and beside it <c>delegate: { session, proof }</c>.
    /// </summary>
    private static async Task<string> SendProvedAsync(
        PipeClient client, string route, object payload, string session, string key, string nonce, string? proofOver = null)
    {
        var id = Guid.NewGuid().ToString();
        var requestKey = Guid.NewGuid().ToString("N");
        var text = JsonSerializer.Serialize(payload, Wire);
        var proof = DelegateProof.Prove(key, nonce, DriverModule.Name, route, proofOver ?? text, requestKey);
        var said = JsonSerializer.Serialize(new { session, proof }, Wire);
        await client.SendLineAsync(
            $$"""{"id":"{{id}}","module":"{{DriverModule.Name}}","type":"{{route}}","payload":{{text}},"key":"{{requestKey}}","{{DelegateProof.Field}}":{{said}}}""");
        return id;
    }

    private static string? Code(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object ? error.GetProperty("code").GetString() : null;

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
