using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation on the protocol door (CONV3b, D76 §4): one ACP session for the whole conversation,
/// each message a turn on it, and the person's words in the record.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The door was broken, not merely unrecorded.</b> A chat on an ACP harness was spawned as a
/// pipe — no <c>initialize</c>, no <c>session/new</c> — and a person's message was written into the
/// JSON-RPC stream as raw text. The agent here writes down any line that is not a frame, which is what
/// that message was.</para>
///
/// <para>A real process (node) speaking the wire, and a stand-in service on a loopback port. No model,
/// no account.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ProtocolChatTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-acp-chat-" + Guid.NewGuid().ToString("N")[..8]);

    public ProtocolChatTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    /// <summary>
    /// The whole conversation, end to end: one handshake and one session; two messages sent back to back
    /// become two turns, the second waiting for the first; finishing closes the session and the agent
    /// exits on its own. The record reads as the conversation happened.
    /// </summary>
    [Fact]
    public async Task A_chat_on_the_protocol_door_is_one_session_and_each_message_a_turn_in_order()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId;
        Assert.NotNull(id);
        string Seen() => $"heard [{string.Join(" | ", HeardLines())}], state {service.State(id!)}, "
                         + $"record [{string.Join(" | ", events.Page(id!).Events.Select(e => e.Kind))}]";
        await Until(() => service.State(id!) == "working", Seen);

        Assert.True(runner.Say(id!, "first"));
        Assert.True(runner.Say(id!, "second"));
        await Until(() => HeardLines().Any(line => line == "prompt: second") && service.State(id!) == "working"
                          && events.Page(id!).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);

        Assert.True(runner.Finish(id!));
        await Until(() => service.State(id!) == "completed", Seen);

        Assert.Equal(
            ["initialize", "session/new", "prompt: first", "prompt: second", "session/close", "stdin ended"],
            HeardLines());

        var said = events.Page(id!).Events
            .Where(e => e.Kind is SessionEventKind.User or SessionEventKind.Message or SessionEventKind.Turn)
            .Select(e => e.Kind switch
            {
                SessionEventKind.User => $"{e.Origin}: {e.Text}",
                SessionEventKind.Message => $"agent: {e.Text}",
                _ => $"turn: {e.StopReason}",
            });
        Assert.Equal(
            ["person: first", "agent: heard first", "turn: end_turn", "person: second", "agent: heard second", "turn: end_turn"],
            said);
        Assert.False(runner.Say(id!, "too late"));
    }

    /// <summary>
    /// When a conversation's end is told, its console has already ended: a page that asks again on
    /// hearing it reads the console as ended. Found looking at CONSOLE3c: the ending was told first and
    /// the console closed after, so a page asking at once still read it as live, beside `completed`.
    /// </summary>
    [Fact]
    public async Task A_conversations_console_has_ended_by_the_time_its_end_is_told()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var output = new SessionOutput();
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), output,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        bool? liveWhenTold = null;
        var id = (await runner.StartAsync("engine", "acp-stub", config, onEnded: (session, _) =>
        {
            liveWhenTold = output.Tail(session).Live;
            return Task.CompletedTask;
        })).SessionId!;
        await Until(() => service.State(id) == "working", () => $"state {service.State(id)}");

        Assert.True(runner.Finish(id));
        await Until(() => liveWhenTold is not null, () => $"state {service.State(id)}");

        Assert.False(liveWhenTold);
    }

    /// <summary>
    /// USAGE1: a conversation counts toward the account it ran as, at its high-water mark — what each
    /// account has carried was driven sessions and intakes only, so every conversation was missing.
    /// </summary>
    [Fact]
    public async Task A_conversation_counts_toward_its_account_at_its_high_water_mark()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var usage = new SessionUsage(_home);
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")), usage: usage);

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        string Seen() => $"state {service.State(id)}, measured [{string.Join(" | ", usage.Sessions.Select(e => $"{e.Session}:{e.Used}"))}]";
        await Until(() => service.State(id) == "working", Seen);

        Assert.True(runner.Say(id, "a longer message"));
        Assert.True(runner.Say(id, "short"));
        await Until(() => events.Page(id).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);
        Assert.Empty(usage.Sessions);

        Assert.True(runner.Finish(id));
        await Until(() => service.State(id) == "completed" && usage.Sessions.Count == 1, Seen);

        var measured = Assert.Single(usage.Sessions);
        Assert.Equal(id, measured.Session);
        Assert.Equal("engine", measured.Repository);
        Assert.Equal("acp-stub", measured.Harness);
        Assert.Equal("a longer message".Length * 1000, measured.Used);
        Assert.Equal(200_000, measured.Size);
    }

    /// <summary>
    /// 🔴 REV3: a turn the agent refused (a JSON-RPC error — auth expired, overloaded) recorded a note and
    /// never a turn's end, so the page drew *working…* under the driver's own failure note until the
    /// next message, while the composer beside it said nothing was running. A refused turn ENDS.
    /// </summary>
    [Fact]
    public async Task A_turn_the_agent_refused_ends_in_the_record_and_the_next_one_is_taken()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        string Seen() => $"record [{string.Join(" | ", events.Page(id).Events.Select(e => $"{e.Kind}:{e.StopReason}"))}]";
        await Until(() => service.State(id) == "working", Seen);

        Assert.True(runner.Say(id, "refuse"));
        Assert.True(runner.Say(id, "after"));
        await Until(() => events.Page(id).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);

        var turns = events.Page(id).Events.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason).ToList();
        Assert.Equal(["error", "end_turn"], turns);
        Assert.Contains(events.Page(id).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("overloaded"));
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// SIGNIN1b (D125 ROSTER1b): a turn the agent refused for its sign-in reads the account it runs as signed out, from the
    /// door's refusal as a driven start's conclusion reads it, so the next start walks past it. The record says so by the
    /// line's code, the turn ends, and the conversation goes on.
    /// </summary>
    [Fact]
    public async Task A_turn_refused_for_its_sign_in_reads_its_account_signed_out_and_the_conversation_goes_on()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        string Seen() => $"record [{string.Join(" | ", events.Page(id).Events.Select(e => $"{e.Kind}:{e.StopReason}:{e.Text}"))}]";
        await Until(() => service.State(id) == "working", Seen);

        Assert.True(runner.Say(id, "signed-out"));
        Assert.True(runner.Say(id, "after"));
        await Until(() => events.Page(id).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);

        var turns = events.Page(id).Events.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason).ToList();
        Assert.Equal(["error", "end_turn"], turns);
        var said = Assert.Single(events.Page(id).Events, e => e.Parts is { Count: > 0 } parts && parts[^1].Code == "account.signed-out-own");
        Assert.StartsWith("The agent refused `stub`'s own sign-in", said.Text);
        Assert.Equal(LoginState.Out, AccountReads.Of(_home, "stub").Own!.Login);
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// 🔴 REV3 chat F9: a person stopping the session mid-turn ended the agent, and the turn's prompt then
    /// failed with "the stream ended" — which was recorded as the turn "could not be taken", a failure
    /// of the agent's. The person stopped it. The turn ends as cancelled, and no failure is written.
    /// </summary>
    [Fact]
    public async Task A_stop_mid_turn_ends_the_turn_as_cancelled_and_writes_no_failure()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var processes = new SessionProcesses(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, processes, events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        string Seen() => $"state {service.State(id)}, record [{string.Join(" | ", events.Page(id).Events.Select(e => $"{e.Kind}:{e.StopReason}:{e.Text}"))}]";
        await Until(() => service.State(id) == "working", Seen);

        Assert.True(runner.Say(id, "hold"));
        await Until(() => HeardLines().Contains("prompt: hold"), Seen);
        Assert.True(processes.Stop(id));
        await Until(() => service.State(id) == "stopped", Seen);
        await Until(() => events.Page(id).Events.Any(e => e.Kind == SessionEventKind.Turn), Seen);

        Assert.DoesNotContain(events.Page(id).Events, e => e.Text?.Contains("could not be taken") == true);
        Assert.Equal("cancelled", events.Page(id).Events.Single(e => e.Kind == SessionEventKind.Turn).StopReason);
    }

    /// <summary>
    /// 🔴 REV3 chat F7: a session that could not open for any reason but a <c>DriverException</c> — here a
    /// <c>session/new</c> answer whose id is a number — skipped the open's own ending. The queue waited
    /// for a session that would never come, and the transcript closed under the stderr pump still
    /// writing to it. It concludes like any other open that failed: a note, and the record ended.
    /// </summary>
    [Fact]
    public async Task A_session_that_cannot_open_for_any_reason_ends_the_conversation_with_a_note()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard, "numeric"] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        string Seen() => $"state {service.State(id)}, record [{string.Join(" | ", events.Page(id).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

        await Until(() => service.State(id) == "failed", Seen);
        Assert.Contains(events.Page(id).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("could not open"));
    }

    /// <summary>
    /// A text-only door keeps the person's words where it keeps the agent's — the console — so its
    /// record holds no half a conversation: questions with no answers beside them would read as an
    /// agent that never replied.
    /// </summary>
    [Fact]
    public async Task A_text_only_chat_records_no_half_a_conversation()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var echo = Path.Combine(_home, "echo.mjs");
        File.WriteAllText(echo, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            for await (const line of createInterface({ input: process.stdin })) console.log('heard ' + line);
            """);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", echo] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "stub", config)).SessionId!;
        await Until(() => service.State(id) == "working", () => $"state {service.State(id)}");
        Assert.True(runner.Say(id, "hello"));
        Assert.True(runner.Finish(id));
        await Until(() => service.State(id) == "completed", () => $"state {service.State(id)}");

        Assert.Empty(events.Page(id).Events);
        Assert.Contains("heard hello", File.ReadAllText(Path.Combine(_home, "sessions", $"{id}.log")));
    }

    // ——— One conversation's model and effort (AGT6b, D98), over the stand-in's config options.

    /// <summary>
    /// A conversation on the protocol door offers the person its model and its effort, as the agent
    /// offered them on <c>session/new</c> — and never its mode, which is the posture Daoris runs it under.
    /// </summary>
    [Fact]
    public async Task A_conversation_offers_its_model_and_effort_as_the_agent_offered_them_and_never_its_mode()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        using var talking = await TalkAsync(service);
        var (runner, id) = (talking.Runner, talking.Id);

        await Until(() => runner.Options(id).Count > 0, () => $"heard [{string.Join(" | ", HeardLines())}]");

        var options = runner.Options(id);
        Assert.Equal(["model", "effort"], options.Select(option => option.Id));
        Assert.Equal("default", options[0].Current);
        Assert.Equal(["default", "sonnet", "haiku"], options[0].Choices.Select(choice => choice.Value));
        Assert.Equal(["low", "high", "max"], options[1].Choices.Select(choice => choice.Value));
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// A change goes over the wire as <c>session/set_config_option</c>, the options the agent answers
    /// with are the conversation's from then on — an effort a model does not take goes with it — the
    /// change is told, and the record says the person made it.
    /// </summary>
    [Fact]
    public async Task Setting_a_conversations_model_goes_over_the_wire_and_the_record_says_the_person_did()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        using var talking = await TalkAsync(service);
        var (runner, events, id) = (talking.Runner, talking.Events, talking.Id);
        var told = new System.Collections.Concurrent.ConcurrentQueue<(string Session, IReadOnlyList<AcpConfigOption> Options)>();
        runner.OptionsChanged += (session, options) => told.Enqueue((session, options));
        await Until(() => runner.Options(id).Count > 0, () => $"heard [{string.Join(" | ", HeardLines())}]");

        var after = await runner.SetOptionAsync(id, "model", "haiku", CancellationToken.None);

        Assert.Contains("set: model=haiku", HeardLines());
        Assert.Equal(["model"], after.Select(option => option.Id));
        Assert.Equal("haiku", after[0].Current);
        Assert.Equal("haiku", runner.Options(id)[0].Current);
        Assert.Contains(told, entry => entry.Session == id && entry.Options.Count == 1);
        Assert.Contains(events.Page(id).Events,
            e => e.Kind == SessionEventKind.Note && e.Text == "the person set Model to Haiku for this conversation.");
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// 🔴 The mode is not the person's to set here: it is the posture D37 and D81 set, and a menu that
    /// widened it would be the approval surface D52 refuses. Refused before anything reaches the agent.
    /// So is an option the agent never offered, and a conversation nothing here holds.
    /// </summary>
    [Fact]
    public async Task The_mode_an_option_never_offered_and_a_conversation_nothing_holds_are_refused()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        using var talking = await TalkAsync(service);
        var (runner, id) = (talking.Runner, talking.Id);
        await Until(() => runner.Options(id).Count > 0, () => $"heard [{string.Join(" | ", HeardLines())}]");

        var mode = await Assert.ThrowsAsync<DriverException>(() => runner.SetOptionAsync(id, "mode", "bypassPermissions", CancellationToken.None));
        Assert.Contains("mode", mode.Message);
        Assert.Contains("posture", mode.Message);
        await Assert.ThrowsAsync<DriverException>(() => runner.SetOptionAsync(id, "temperature", "hot", CancellationToken.None));
        Assert.DoesNotContain(HeardLines(), line => line.StartsWith("set:", StringComparison.Ordinal));

        var nothing = await Assert.ThrowsAsync<DriverException>(() => runner.SetOptionAsync("nothing-here", "model", "haiku", CancellationToken.None));
        Assert.Contains("nothing-here", nothing.Message);
        Assert.Empty(runner.Options("nothing-here"));
        Assert.True(runner.Finish(id));
    }

    /// <summary>A conversation whose agent offers no options offers the person none.</summary>
    [Fact]
    public async Task A_conversation_whose_agent_offers_no_options_offers_none()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        using var talking = await TalkAsync(service, "bare");
        var (runner, id) = (talking.Runner, talking.Id);
        await Until(() => HeardLines().Contains("session/new"), () => $"heard [{string.Join(" | ", HeardLines())}]");

        Assert.Empty(runner.Options(id));
        Assert.True(runner.Finish(id));
    }

    /// <summary>One conversation started on the stand-in agent and working, with the record it keeps.</summary>
    private sealed record Talking(ServiceClient Client, ChatRunner Runner, SessionEvents Events, string Id) : IDisposable
    {
        // The runner first: its conclusions go through the client (ChatRunner.Dispose).
        public void Dispose()
        {
            Runner.Dispose();
            Client.Dispose();
        }
    }

    private async Task<Talking> TalkAsync(StandInService service, string? mode = null)
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = mode is null ? ["node", Agent(), Heard] : ["node", Agent(), Heard, mode],
            },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var client = new ServiceClient(service.Url, null);
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Until(() => service.State(id) == "working", () => $"state {service.State(id)}");
        return new Talking(client, runner, events, id);
    }

    /// <summary>
    /// A stand-in ACP agent: it writes down every method it is called with and every prompt's words,
    /// answers each prompt a moment later so a second message arrives mid-turn, and writes down any
    /// line that is not a frame — the raw text the broken door used to send.
    /// </summary>
    /// <remarks>
    /// Its <c>session/new</c> offers config options in the shape <c>claude-agent-acp</c> 0.84.0 does — a
    /// mode, a model, an effort as a thought level — unless it is started <c>bare</c>, and it takes
    /// <c>session/set_config_option</c>, writing the change down and answering with the whole list: a
    /// model that takes no effort drops the effort, as the adapter's own does.
    /// </remarks>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-chat-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let model = 'default';
            const options = () => [
              { id: 'mode', name: 'Mode', category: 'mode', type: 'select', currentValue: 'acceptEdits',
                options: [{ value: 'acceptEdits', name: 'Accept Edits' }, { value: 'bypassPermissions', name: 'Bypass' }] },
              { id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: model,
                options: [{ value: 'default', name: 'Default' }, { value: 'sonnet', name: 'Sonnet' }, { value: 'haiku', name: 'Haiku' }] },
              ...(model === 'haiku' ? [] : [{ id: 'effort', name: 'Effort', category: 'thought_level', type: 'select', currentValue: 'high',
                options: [{ value: 'low', name: 'Low' }, { value: 'high', name: 'High' }, { value: 'max', name: 'Max' }] }]),
            ];
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { note('RAW: ' + line); continue; }
              if (frame.method === 'initialize') {
                note('initialize');
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                // A session id of the wrong kind: an agent this client cannot open a session on.
                const sessionId = process.argv[3] === 'numeric' ? 5 : 'acp-chat';
                send({ jsonrpc: '2.0', id: frame.id, result: process.argv[3] === 'bare' ? { sessionId } : { sessionId, configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                note(`set: ${frame.params.configId}=${frame.params.value}`);
                if (frame.params.configId === 'model') model = frame.params.value;
                send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
              } else if (frame.method === 'session/prompt') {
                const said = frame.params.prompt[0].text;
                note('prompt: ' + said);
                if (said === 'hold') continue;   // a turn that runs until someone stops it
                if (said === 'refuse') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'overloaded' } });
                  continue;
                }
                // SIGNIN1b: the install's refusal of a sign-in, as Claude Code's adapter answers it (D125 ROSTER1b).
                if (said === 'signed-out') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32000, message: 'Authentication required' } });
                  continue;
                }
                await new Promise((resolve) => setTimeout(resolve, 150));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-chat',
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard ' + said } } } });
                // Context as the prompt's length in thousands, so a test knows each turn's reading.
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-chat',
                  update: { sessionUpdate: 'usage_update', used: said.length * 1000, size: 200000 } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                note('session/close');
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            note('stdin ended');
            """);
        return script;
    }

    /// <summary>
    /// Wait for a condition, bounded for a loaded machine — the suite runs classes in parallel, each
    /// spawning node — and saying what it saw when it gives up: this test failed once in a full gate
    /// run (2026-09-25) and passed five full runs after, and a bare timeout said nothing about why.
    /// </summary>
    private static Task Until(Func<bool> condition, Func<string>? seen = null) => Poll.Until(condition, seen);
}
