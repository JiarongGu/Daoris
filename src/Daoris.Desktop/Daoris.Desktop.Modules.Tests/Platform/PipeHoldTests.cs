using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A terminal holds a conversation over the pipe: the person's Confirm, once, binds one connection to one conversation; that connection's words in the verb's form, its turn stops
/// and its finish run with no further card; the conversation's lines, events and end are forwarded to it alone, bounded, with a gap; and
/// the hold ends by the terminal's <c>LET_GO</c>, the window's <c>SESSION_LET_GO</c>, the conversation's end or the connection closing,
/// each told and logged.
/// </summary>
/// <remarks>
/// Each test serves its own pipe in this process, named by its own scratch home's id, so nothing here meets a running Daoris. The
/// window's answer is the confirmations' own, as the bridge's <c>PERSON_CONFIRM</c> gives it; the conversations are a stand-in module's.
/// The pipe is Windows': elsewhere a test returns at once.
/// </remarks>
public sealed class PipeHoldTests : Bridge
{
    private const string Session = "c1a2b3c4";
    private const string Other = "d5e6f7a8";

    [Fact]
    public async Task A_confirmed_start_with_hold_holds_what_it_opened_and_its_words_stop_and_finish_run_with_no_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, chats, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        // The start is carded as every terminal's press is, and its card says the Confirm also makes the hold.
        var start = await SendHeldAsync(client, "START_CHAT", new { repository = "alpha", text = "hello" });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal("START_CHAT", card.Route);
        Assert.True(card.Hold);
        Assert.Equal(0, chats.Ran("START_CHAT"));

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        var answer = await client.AnswerAsync(start);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(Session, answer.GetProperty("data").GetProperty("sessionId").GetString());

        // Held: the window is told which conversation and which terminal, and the terminal hears the hold began.
        var held = Assert.Single(pipe.Holding.Now().Holds);
        Assert.Equal(Session, held.Session);
        Assert.Equal(Environment.ProcessId, held.Asker.Pid);
        await UntilAsync(() => Raised.Any(each => each.Type == PipeHolding.Event && each.Payload is PipeHoldsNow { Holds: [{ Session: Session }] }));
        Assert.Equal("held", (await PipeNotificationAsync(client, PipeHolding.HeldType)).GetProperty("held").GetString());

        // The words in the verb's form, a turn's stop and the finish run with no card, each once.
        foreach (var (route, payload) in new (string, object)[]
        {
            ("SESSION_INPUT", new { id = Session, text = "go on", said = true }),
            ("CANCEL_TURN", new { id = Session, said = true }),
            ("END_CHAT", new { id = Session }),
        })
        {
            var sent = await client.SendAsync(DriverModule.Name, route, payload);
            var ran = await client.AnswerAsync(sent);
            Assert.True(ran.GetProperty("success").GetBoolean(), ran.GetRawText());
            Assert.Equal(1, chats.Ran(route));
            Assert.Empty(pipe.Confirmations.Now().Waiting);
        }

        // Words in the window's form ask: the terminal is told the press waits for the person, and nothing ran.
        var asked = await client.SendAsync(DriverModule.Name, "SESSION_INPUT", new { id = Session, text = "a choice", said = true, question = "q1" });
        string? waits;
        do
        {
            waits = (await PipeNotificationAsync(client, InstancePipe.Confirming)).GetProperty("id").GetString();
        }
        while (waits != asked);
        Assert.Equal(1, chats.Ran("SESSION_INPUT"));

        Assert.Contains("""{"state":"held","door":"terminal"}""", Holds());
    }

    [Fact]
    public async Task The_same_words_from_another_connection_are_carded_and_a_second_hold_is_refused_held()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, chats, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        await HoldAsync(pipe, client, Session);

        await using var second = await PipeClient.ConnectAsync(pipe.Name!);
        await second.HelloAsync(HomeId);
        var words = await second.SendAsync(DriverModule.Name, "SESSION_INPUT", new { id = Session, text = "from elsewhere", said = true });
        var told = await second.NotificationAsync(Poll.Patience);
        Assert.NotNull(told);
        Assert.Equal(words, Confirming(told.Value));
        Assert.Equal(0, chats.Ran("SESSION_INPUT"));

        var hold = await second.AnswerAsync(await second.SendAsync(InstancePipe.Module, "HOLD", new { session = Session }));
        Assert.Equal(PipeHolding.HeldElsewhere, Code(hold));
        Assert.Equal(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hold.GetProperty("error").GetProperty("parameters").GetProperty("pid").GetString());
        Assert.Equal(Session, Assert.Single(pipe.Holding.Now().Holds).Session);
    }

    [Fact]
    public async Task A_hold_refused_in_the_window_holds_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, services, log) = await ServeAsync();
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;

        var sent = await client.SendAsync(InstancePipe.Module, "HOLD", new { session = Session });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);
        Assert.Equal((InstancePipe.Module, "HOLD", true), (card.Module, card.Route, card.Hold));
        Assert.Equal([new PipeConfirmationField("/session", "string", Session)], card.Fields);

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: false)).Outcome);
        Assert.Equal(InstancePipe.Refused, Code(await client.AnswerAsync(sent)));
        Assert.Empty(pipe.Holding.Now().Holds);

        var unknown = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "HOLD", new { session = "nothing" }));
        Assert.Equal(PipeHolding.UnknownSession, Code(unknown));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
    }

    [Fact]
    public async Task A_hold_card_waiting_when_the_stop_begins_answers_its_line_lost_and_the_drain_does_not_wait_on_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, services, log) = await ServeAsync();
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;

        var sent = await client.SendAsync(InstancePipe.Module, "HOLD", new { session = Session });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Any(each => each.Route == "HOLD"));

        // The terminal reads its line, as one waiting on its answer does: the drain waits for the answer to be written.
        var reading = client.AnswerAsync(sent);
        pipe.BeginStopping();

        Assert.True(pipe.Drain(TimeSpan.FromSeconds(5)));
        Assert.Equal(InstancePipe.Lost, Code(await reading));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(pipe.Holding.Now().Holds);
    }

    [Fact]
    public async Task A_hold_ends_by_let_go_by_the_window_by_the_conversation_s_end_and_by_the_connection_closing_each_told_and_logged()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, services, log) = await ServeAsync();
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;

        // The terminal lets go.
        await HoldAsync(pipe, client, Session);
        var letGo = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "LET_GO", new { }));
        Assert.True(letGo.GetProperty("success").GetBoolean(), letGo.GetRawText());
        Assert.Equal(PipeHolding.LetGo, (await HeldEndedAsync(client)).GetProperty("reason").GetString());
        Assert.Empty(pipe.Holding.Now().Holds);
        Assert.Equal(PipeHolding.NotHeld, Code(await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "LET_GO", new { }))));

        // The person takes it back in the window.
        await HoldAsync(pipe, client, Session);
        Assert.True(pipe.Holding.TakeBack(Session));
        Assert.Equal(PipeHolding.Window, (await HeldEndedAsync(client)).GetProperty("reason").GetString());
        Assert.False(pipe.Holding.TakeBack(Session));

        // The conversation ends: its end is forwarded, then the hold's.
        await HoldAsync(pipe, client, Session);
        await Bus.EmitAsync(StatePublisher.Module, DriverLoop.EndedType, new { Session, State = "completed" });
        Assert.Equal(Session, (await NotificationOfAsync(client, StatePublisher.Module, DriverLoop.EndedType)).GetProperty("session").GetString());
        Assert.Equal(PipeHolding.Ended, (await HeldEndedAsync(client)).GetProperty("reason").GetString());
        Assert.Empty(pipe.Holding.Now().Holds);

        // The connection closes.
        await HoldAsync(pipe, client, Session);
        await client.DisposeAsync();
        await UntilAsync(() => pipe.Holding.Now().Holds.Count == 0);

        Assert.Equal(
            [
                """{"state":"held","door":"terminal"}""", """{"state":"ended","why":"let-go","door":"terminal"}""",
                """{"state":"held","door":"terminal"}""", """{"state":"ended","why":"window","door":"terminal"}""",
                """{"state":"held","door":"terminal"}""", """{"state":"ended","why":"ended","door":"terminal"}""",
                """{"state":"held","door":"terminal"}""", """{"state":"ended","why":"closed","door":"terminal"}""",
            ],
            Holds());
    }

    [Fact]
    public async Task The_held_conversation_s_lines_and_events_are_forwarded_in_sequence_and_nothing_of_another()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, services, log) = await ServeAsync();
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;
        await HoldAsync(pipe, client, Session);

        await Output(Other, 1);
        await Output(Session, 1, 2);
        await Bus.EmitAsync(StatePublisher.Module, "SESSION_EVENTS", new { Session = Other, Events = new[] { new { Kind = "said" } } });
        await Bus.EmitAsync(StatePublisher.Module, "SESSION_EVENTS", new { Session, Events = new[] { new { Kind = "said" } } });
        await Output(Session, 3);

        var lines = await NotificationOfAsync(client, StatePublisher.Module, "SESSION_OUTPUT");
        Assert.Equal(Session, lines.GetProperty("session").GetString());
        Assert.Equal([1L, 2L], lines.GetProperty("lines").EnumerateArray().Select(each => each.GetProperty("sequence").GetInt64()));
        var events = await NotificationOfAsync(client, StatePublisher.Module, "SESSION_EVENTS");
        Assert.Equal(Session, events.GetProperty("session").GetString());
        var last = await NotificationOfAsync(client, StatePublisher.Module, "SESSION_OUTPUT");
        Assert.Equal(3L, Assert.Single(last.GetProperty("lines").EnumerateArray()).GetProperty("sequence").GetInt64());
    }

    [Fact]
    public async Task A_terminal_that_does_not_read_loses_the_oldest_lines_and_is_told_a_gap()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, services, log) = await ServeAsync(new PipeLimits { Forwarded = 4 });
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;
        await HoldAsync(pipe, client, Session);

        // Each batch larger than the pipe's buffer, so the forwarding waits on the terminal while the bus goes on.
        var text = new string('x', 64 * 1024);
        const int Told = 60;
        for (var sequence = 1; sequence <= Told; sequence++)
        {
            await Bus.EmitAsync(StatePublisher.Module, "SESSION_OUTPUT",
                new { Session, Lines = new[] { new { Sequence = (long)sequence, Text = text } }, Live = true });
        }

        var seen = new List<long>();
        long? gap = null;
        long? afterGap = null;
        while (seen.Count == 0 || seen[^1] < Told)
        {
            var line = await client.ReadAsync() ?? throw new InvalidOperationException("the pipe closed");
            var told = Assert.Single(line.GetProperty("payload").EnumerateArray());
            var type = told.GetProperty("type").GetString();
            if (type == PipeHolding.GapType)
            {
                gap ??= told.GetProperty("payload").GetProperty("sequence").GetInt64();
                continue;
            }

            if (type != "SESSION_OUTPUT") continue;
            var sequence = Assert.Single(told.GetProperty("payload").GetProperty("lines").EnumerateArray()).GetProperty("sequence").GetInt64();
            if (gap is not null) afterGap ??= sequence;
            seen.Add(sequence);
        }

        Assert.NotNull(gap);
        Assert.True(seen.Count < Told);
        Assert.Equal(seen.Order(), seen);
        Assert.True(afterGap > gap);
        Assert.DoesNotContain(gap!.Value, seen);
    }

    [Fact]
    public async Task A_held_word_during_an_update_s_drain_is_refused_stopping_and_the_hold_stays()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, chats, services, log) = await ServeAsync();
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;
        await HoldAsync(pipe, client, Session);
        pipe.Draining = () => true;

        var words = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "SESSION_INPUT", new { id = Session, text = "now", said = true }));
        Assert.Equal(InstancePipe.Stopping, Code(words));
        Assert.Equal("update", words.GetProperty("error").GetProperty("parameters").GetProperty("why").GetString());
        Assert.Equal(0, chats.Ran("SESSION_INPUT"));
        Assert.Equal(Session, Assert.Single(pipe.Holding.Now().Holds).Session);

        var stop = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "CANCEL_TURN", new { id = Session, said = true }));
        Assert.True(stop.GetProperty("success").GetBoolean(), stop.GetRawText());
        Assert.Equal(1, chats.Ran("CANCEL_TURN"));
    }

    [Fact]
    public async Task A_connection_holding_a_conversation_is_refused_a_second_start_that_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, chats, services, log) = await ServeAsync();
        using var __ = services;
        using var ___ = log;
        using var ____ = pipe;
        await using var _____ = client;
        await HoldAsync(pipe, client, Session);

        var start = await client.AnswerAsync(await SendHeldAsync(client, "START_CHAT", new { repository = "alpha" }));
        Assert.Equal(PipeHolding.Holding, Code(start));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, chats.Ran("START_CHAT"));
    }

    /// <summary>The conversations' stand-in: a start answers the conversation it opened; every press is counted by its route.</summary>
    private sealed class Chats : ModuleBase
    {
        private readonly ConcurrentDictionary<string, int> _ran = new(StringComparer.Ordinal);

        public override string ModuleName => DriverModule.Name;

        public int Ran(string route) => _ran.GetValueOrDefault(route);

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            _ran.AddOrUpdate(request.Type, 1, (_, count) => count + 1);
            return Task.FromResult<object?>(request.Type is "START_CHAT" or "SESSION_START_FROM" or "SESSION_HAND_OVER"
                ? new { SessionId = Session, Message = "" }
                : new { Done = true });
        }
    }

    private string HomeId => HomeIdentity.Read(Home)!;

    private async Task<(InstancePipe Pipe, PipeClient Client, Chats Chats, ServiceProvider Services, MachineLog Log)> ServeAsync(PipeLimits? limits = null)
    {
        var log = new MachineLog(Home, "desktop");
        var chats = new Chats();
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        var options = new IpcRequestTrackerOptions { GracePeriod = TimeSpan.FromMilliseconds(50) };
        var tracker = new IpcRequestTracker(Bus, options);
        services.AddSingleton<IIpcRequestTracker>(tracker);
        services.AddSingleton<IIpcModule>(chats);
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        var provider = services.BuildServiceProvider();
        var grounds = new StubGrounds().Session(Session, "running").Session(Other, "running");
        grounds.Registered.Add("alpha");
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, version: "9.9.9-test", limits: limits, clock: new HeldClock())
        {
            Grounds = grounds,
        };
        var client = await PipeClient.ConnectAsync(pipe.Serve()!);
        await client.HelloAsync(HomeId);
        return (pipe, client, chats, provider, log);
    }

    /// <summary>A driver press sent with <c>hold: true</c> beside its envelope, as a start that holds is sent.</summary>
    private static async Task<string> SendHeldAsync(PipeClient client, string route, object payload)
    {
        var id = Guid.NewGuid().ToString();
        await client.SendLineAsync(JsonSerializer.Serialize(
            new { id, module = DriverModule.Name, type = route, payload, key = Guid.NewGuid().ToString("N"), hold = true },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        return id;
    }

    /// <summary><paramref name="session"/> held for <paramref name="client"/> by the pipe's <c>HOLD</c>, confirmed in the window.</summary>
    private async Task HoldAsync(InstancePipe pipe, PipeClient client, string session)
    {
        var sent = await client.SendAsync(InstancePipe.Module, "HOLD", new { session });
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Any(each => each.Route == "HOLD"));
        var card = pipe.Confirmations.Now().Waiting.Single(each => each.Route == "HOLD");
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        var answer = await client.AnswerAsync(sent);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.Equal(session, answer.GetProperty("data").GetProperty("session").GetString());
        Assert.Equal("held", (await PipeNotificationAsync(client, PipeHolding.HeldType)).GetProperty("held").GetString());
    }

    private Task Output(string session, params long[] sequences) =>
        Bus.EmitAsync(StatePublisher.Module, "SESSION_OUTPUT",
            new { Session = session, Lines = sequences.Select(each => new { Sequence = each, Text = $"line {each}" }).ToArray(), Live = true });

    private async Task<JsonElement> HeldEndedAsync(PipeClient client)
    {
        var told = await PipeNotificationAsync(client, PipeHolding.HeldType);
        Assert.Equal("ended", told.GetProperty("held").GetString());
        return told;
    }

    private Task<JsonElement> PipeNotificationAsync(PipeClient client, string type) => NotificationOfAsync(client, InstancePipe.Module, type);

    /// <summary>The notifications already taken, by their envelope's id: one kept while an answer is awaited is taken once.</summary>
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>
    /// The payload of the next notification of <paramref name="module"/>'s <paramref name="type"/> not taken yet: first among those kept while
    /// an answer is awaited, then read on past any other notification.
    /// </summary>
    private async Task<JsonElement> NotificationOfAsync(PipeClient client, string module, string type)
    {
        foreach (var kept in client.Told)
        {
            if (Take(kept, module, type) is { } found) return found;
        }

        while (true)
        {
            var line = await client.ReadAsync() ?? throw new InvalidOperationException($"the pipe closed before it told {type}");
            Assert.Equal("notification", line.GetProperty("category").GetString());
            if (Take(line, module, type) is { } found) return found;
        }
    }

    private JsonElement? Take(JsonElement line, string module, string type)
    {
        var told = Assert.Single(line.GetProperty("payload").EnumerateArray());
        if (told.GetProperty("module").GetString() != module || told.GetProperty("type").GetString() != type) return null;
        return _taken.Add(line.GetProperty("id").GetString()!) ? told.GetProperty("payload") : null;
    }

    private static string? Confirming(JsonElement told)
    {
        var said = Assert.Single(told.GetProperty("payload").EnumerateArray());
        return said.GetProperty("type").GetString() == InstancePipe.Confirming ? said.GetProperty("payload").GetProperty("id").GetString() : null;
    }

    private static string? Code(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object ? error.GetProperty("code").GetString() : null;

    /// <summary>The machine log's <see cref="PipeHolding.LogEvent"/> lines' data, in order.</summary>
    private List<string> Holds()
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
            .Where(line => line.GetProperty("event").GetString() == PipeHolding.LogEvent)
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
