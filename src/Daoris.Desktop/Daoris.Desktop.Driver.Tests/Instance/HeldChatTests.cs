using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A terminal that holds the running Daoris's conversation (RPC1g2 and RPC1g3, D238, the terminal-conversation design §3.3), against a
/// stand-in of the pipe: each row of the design's two tables, the lines <c>chat</c> printed before unchanged, and no instance refused with
/// nothing done. The instance's own half (the hold, its forwarding and its ends) is the modules' over a real pipe.
/// </summary>
/// <remarks>Windows only, as the pipe is: each test serves its own stand-in in process, on its own home.</remarks>
public sealed class HeldChatTests : IAsyncLifetime
{
    private readonly InstanceTestHome _home = new();
    private readonly Lines _out = new();
    private readonly Lines _error = new();
    private readonly ScriptedInput _input = new();
    private readonly List<(string Line, long Sequence)> _backlog = [];
    private InstanceStandIn _standIn = null!;
    private Action? _interrupt;
    private Task<int>? _run;

    public Task InitializeAsync()
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        _standIn = _home.Serve();
        _standIn.Respond = Answer;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _input.End();
        if (_run is { } run) await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));
        await _home.DisposeAsync();
    }

    private object? Answer(string type, JsonElement payload) => type switch
    {
        "START_CHAT" => new { sessionId = "c1", message = "conversation c1 started." },
        "SESSION_START_FROM" => new { sessionId = "c2", sent = true, why = (string?)null, message = "words sent to c2." },
        "HOLD" => new { held = true, session = "c1" },
        "TAIL_SESSION" => new
        {
            session = "c1",
            lines = _backlog.Where(each => each.Sequence > (payload.TryGetProperty("after", out var after) ? after.GetInt64() : 0))
                .Select(each => new { sequence = each.Sequence, text = each.Line }).ToArray(),
            sequence = _backlog.Count == 0 ? 0 : _backlog[^1].Sequence,
            live = true,
            dropped = 0,
        },
        "SESSION_INPUT" => new { said = new { @out = Array.Empty<string>(), error = Array.Empty<string>(), code = 0 } },
        "CANCEL_TURN" => new { cancelled = true, withdrawn = Array.Empty<object>() },
        "LET_GO" => new { held = false, session = "c1" },
        "END_CHAT" => new { ended = true },
        _ => new { },
    };

    private Task<int> Start(string? agent = null, string? account = null, bool ownTree = false) =>
        _run = HeldVerbs.StartAsync(World("chat"), new("game", agent, account, ownTree), Hook);

    private InstanceWorld World(string verb) => _home.World(verb, _input, _out, _error);

    private IDisposable Hook(Action interrupt)
    {
        _interrupt = interrupt;
        return new Nothing();
    }

    private IReadOnlyList<JsonElement> Sent(string type) => [.. _standIn.Sent.Where(each => each.GetProperty("type").GetString() == type)];

    private static async Task UntilAsync(Func<bool> done, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        while (!done())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException($"never: {what}");
            await Task.Delay(15);
        }
    }

    private Task Running() => UntilAsync(() => Sent("TAIL_SESSION").Count > 0, "the conversation is held and its backlog read");

    private Task EndedAsync(string session = "c1", string state = "completed") =>
        _standIn.PushAsync("DAORIS", "SESSION_ENDED", new { session, state });

    // ——— §3.3, the verbs

    [Fact]
    public async Task Chat_with_a_repository_starts_through_START_CHAT_with_its_hold_and_the_flags_as_the_payload()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start("stub", "work", ownTree: true);
        await Running();

        var start = Assert.Single(Sent("START_CHAT"));
        Assert.True(start.GetProperty("hold").GetBoolean());
        var payload = start.GetProperty("payload");
        Assert.Equal("game", payload.GetProperty("repository").GetString());
        Assert.Equal("stub", payload.GetProperty("adapter").GetString());
        Assert.Equal("work", payload.GetProperty("profile").GetString());
        Assert.True(payload.GetProperty("ownTree").GetBoolean());
        Assert.Contains("chat: conversation c1 started.", _error.Text);

        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
        Assert.Contains("chat: session c1 is completed.", _error.Text);
    }

    [Fact]
    public async Task Chat_without_an_agent_names_none_so_the_instance_walks_Chats_order()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();

        var payload = Assert.Single(Sent("START_CHAT")).GetProperty("payload");
        Assert.False(payload.TryGetProperty("adapter", out _));
        Assert.False(payload.TryGetProperty("profile", out _));
        Assert.False(payload.TryGetProperty("ownTree", out _));
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task Chat_with_a_session_holds_it_and_prints_its_last_lines_first()
    {
        if (!OperatingSystem.IsWindows()) return;
        _backlog.AddRange([("earlier one", 1), ("earlier two", 2)]);
        _run = HeldVerbs.HoldSessionAsync(World("chat"), "c1", Hook);
        await Running();

        var hold = Assert.Single(Sent("HOLD"));
        Assert.Equal(InstanceClient.PipeModule, hold.GetProperty("module").GetString());
        Assert.Equal("c1", hold.GetProperty("payload").GetProperty("session").GetString());
        Assert.False(hold.TryGetProperty("hold", out _));
        await UntilAsync(() => _out.Text.Contains("earlier two"), "the backlog printed");
        Assert.Equal("earlier one\nearlier two\n", _out.Text);
        Assert.Empty(Sent("START_CHAT"));

        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await _run);
    }

    [Fact]
    public async Task A_hold_the_instance_refuses_is_said_and_nothing_is_held()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn.RefuseBy = (type, _) => type == "HOLD" ? "held" : null;

        var exit = await HeldVerbs.HoldSessionAsync(World("chat"), "c1", Hook);

        Assert.Equal(2, exit);
        Assert.Equal("chat: refused held. (held)\n", _error.Text);
        Assert.Empty(Sent("TAIL_SESSION"));
    }

    [Fact]
    public async Task Start_from_sends_its_route_with_the_hold_and_holds_the_conversation_the_answer_names()
    {
        if (!OperatingSystem.IsWindows()) return;
        _run = HeldVerbs.StartFromAsync(World("sessions"), "s1", Hook);
        await Running();

        var start = Assert.Single(Sent("SESSION_START_FROM"));
        Assert.True(start.GetProperty("hold").GetBoolean());
        Assert.Equal("s1", start.GetProperty("payload").GetProperty("id").GetString());
        Assert.False(start.GetProperty("payload").TryGetProperty("said", out _));
        Assert.Contains("sessions: words sent to c2.", _error.Text);

        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        Assert.Equal("c2", Assert.Single(Sent("END_CHAT")).GetProperty("payload").GetProperty("id").GetString());
        await EndedAsync("c2");
        Assert.Equal(0, await _run);
    }

    [Fact]
    public async Task Hand_over_holds_the_conversation_it_opens_and_holds_nothing_where_the_answer_names_none()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn.Respond = (type, _) => type == "SESSION_HAND_OVER"
            ? new { sessionId = (string?)null, kept = true, why = (string?)null, message = "the hand-over waits for the turn to end." }
            : Answer(type, default);

        var exit = await HeldVerbs.HandOverAsync(World("sessions"), "s1", "codex", "work", Hook);

        Assert.Equal(0, exit);
        var press = Assert.Single(Sent("SESSION_HAND_OVER"));
        Assert.True(press.GetProperty("hold").GetBoolean());
        Assert.Equal("codex", press.GetProperty("payload").GetProperty("agent").GetString());
        Assert.Equal("work", press.GetProperty("payload").GetProperty("account").GetString());
        Assert.Equal("sessions: the hand-over waits for the turn to end.\n", _error.Text);
        Assert.Empty(Sent("TAIL_SESSION"));
    }

    [Fact]
    public async Task With_no_instance_it_says_so_and_runs_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var none = new InstanceTestHome();

        var exit = await HeldVerbs.StartAsync(none.World("chat", _input, _out, _error), new("game", null, null, false), Hook);

        Assert.Equal(2, exit);
        Assert.Equal(HeldDoor.NotRunning("chat") + "\n", _error.Text);
        Assert.Equal("", _out.Text);
        Assert.Equal(0, _standIn.Connections);
    }

    [Fact]
    public async Task With_only_a_headless_loop_it_names_it_and_runs_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var lease = HomeLease.TryAcquire(_home.Home, LeaseKind.Headless, out _);

        var exit = await HeldVerbs.StartFromAsync(World("sessions"), "s1", Hook);

        Assert.Equal(2, exit);
        Assert.Equal(HeldDoor.Headless("sessions") + "\n", _error.Text);
        Assert.Equal(0, _standIn.Connections);
    }

    // ——— §3.3, in the terminal

    [Fact]
    public async Task A_line_is_said_in_the_verbs_form_with_no_question_draft_preface_or_level_and_nothing_is_printed_for_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();

        _input.Say("what is this repository for?");
        await UntilAsync(() => Sent("SESSION_INPUT").Count > 0, "the line said");

        var said = Assert.Single(Sent("SESSION_INPUT")).GetProperty("payload");
        Assert.Equal("c1", said.GetProperty("id").GetString());
        Assert.Equal("what is this repository for?", said.GetProperty("text").GetString());
        Assert.True(said.GetProperty("said").GetBoolean());
        foreach (var absent in new[] { "question", "draft", "preface", "level", "paths" }) Assert.False(said.TryGetProperty(absent, out _), absent);

        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
        Assert.Equal("chat: conversation c1 started.\nchat: session c1 is completed.\n", _error.Text);
    }

    [Fact]
    public async Task A_refused_line_is_said_and_the_hold_goes_on()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn.RefuseBy = (type, payload) => type == "SESSION_INPUT" && payload.GetProperty("text").GetString() == "during the drain" ? "stopping" : null;
        var run = Start();
        await Running();

        _input.Say("during the drain");
        await UntilAsync(() => _error.Text.Contains("chat: refused stopping."), "the refusal said");
        _input.Say("after it");
        await UntilAsync(() => Sent("SESSION_INPUT").Count == 2, "the next line said");

        Assert.Empty(Sent("LET_GO"));
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task An_attach_sends_the_path_with_the_next_message_only_and_a_missing_file_attaches_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var file = Path.Combine(_home.Home, "crash.log");
        await File.WriteAllTextAsync(file, "boom");
        var run = Start();
        await Running();

        _input.Say(":attach " + Path.Combine(_home.Home, "nope.log"));
        _input.Say($":attach \"{file}\"");
        _input.Say("what does this log say?");
        _input.Say("and now?");
        await UntilAsync(() => Sent("SESSION_INPUT").Count == 2, "both lines said");

        Assert.Contains($"chat: `{Path.Combine(_home.Home, "nope.log")}` is not a file on this machine, so nothing was attached.", _error.Text);
        Assert.Contains("chat: crash.log goes with your next message.", _error.Text);
        var first = Sent("SESSION_INPUT")[0].GetProperty("payload");
        Assert.Equal(file, Assert.Single(first.GetProperty("paths").EnumerateArray()).GetString());
        Assert.False(Sent("SESSION_INPUT")[1].GetProperty("payload").TryGetProperty("paths", out _));

        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task A_line_of_ETX_stops_the_turn_and_hands_back_what_waited_in_the_words_chat_always_said()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn.Respond = (type, payload) => type == "CANCEL_TURN"
            ? new { cancelled = true, withdrawn = new[] { new { id = "w1", text = "queued behind it", files = new[] { "a.txt", "b.txt" } } } }
            : Answer(type, payload);
        var run = Start();
        await Running();

        _input.Say("\u0003");
        await UntilAsync(() => _error.Text.Contains("chat: not sent: queued behind it"), "the stop said");

        var stop = Assert.Single(Sent("CANCEL_TURN")).GetProperty("payload");
        Assert.Equal("c1", stop.GetProperty("id").GetString());
        Assert.False(stop.TryGetProperty("keep", out _));
        Assert.Contains("chat: the turn was asked to stop; the conversation goes on.", _error.Text);
        Assert.Contains("chat: not sent: queued behind it (with a.txt, b.txt)", _error.Text);
        Assert.Empty(Sent("LET_GO"));

        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task A_line_of_ETX_with_no_turn_running_says_so_and_keeps_the_hold()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn.Respond = (type, payload) => type == "CANCEL_TURN" ? new { cancelled = false, withdrawn = Array.Empty<object>() } : Answer(type, payload);
        var run = Start();
        await Running();

        _input.Say("\u0003");
        await UntilAsync(() => _error.Text.Contains("chat: no turn was running."), "the stop said");

        Assert.Empty(Sent("LET_GO"));
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task Ctrl_C_during_a_turn_stops_the_turn_and_keeps_the_hold()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();

        _interrupt!();
        await UntilAsync(() => _error.Text.Contains("chat: the turn was asked to stop; the conversation goes on."), "the stop said");
        _input.Say("still here");
        await UntilAsync(() => Sent("SESSION_INPUT").Count == 1, "a line after it said");

        Assert.Empty(Sent("LET_GO"));
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task Ctrl_C_at_rest_lets_go_and_the_conversation_goes_on_in_Daoris()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn.Respond = (type, payload) => type == "CANCEL_TURN" ? new { cancelled = false, withdrawn = Array.Empty<object>() } : Answer(type, payload);
        var run = Start();
        await Running();

        _interrupt!();

        Assert.Equal(0, await run);
        Assert.Single(Sent("LET_GO"));
        Assert.Empty(Sent("END_CHAT"));
        Assert.Contains("chat: the conversation goes on in Daoris; `daoris chat --session c1` holds it again.", _error.Text);
        Assert.DoesNotContain("no turn was running", _error.Text);
    }

    [Fact]
    public async Task The_end_of_input_finishes_the_conversation_and_waits_for_how_it_ended()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();

        _input.Say("one");
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");

        Assert.False(run.IsCompleted);
        Assert.Equal("SESSION_INPUT", _standIn.Presses.Select(each => each.GetProperty("type").GetString()).First(each => each is "SESSION_INPUT" or "END_CHAT"));
        await EndedAsync(state: "failed");
        Assert.Equal(0, await run);
        Assert.Contains("chat: session c1 is failed.", _error.Text);
    }

    [Fact]
    public async Task The_connection_lost_says_Daoris_is_closing_and_the_conversation_is_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();

        _standIn.Drop();

        Assert.Equal(2, await run);
        Assert.Contains("chat: Daoris is closing; the conversation is kept; hold it again once it is back (`daoris chat --session c1`).", _error.Text);
    }

    [Fact]
    public async Task The_window_taking_the_conversation_back_ends_the_hold_with_exit_0()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();

        await _standIn.PushAsync(InstanceClient.PipeModule, "HELD", new { session = "c1", held = "ended", reason = "window" });

        Assert.Equal(0, await run);
        Assert.Contains("chat: Daoris's window took this conversation back.", _error.Text);
        Assert.Empty(Sent("END_CHAT"));
    }

    // ——— §3.2, what the terminal reads

    [Fact]
    public async Task Forwarded_lines_print_in_order_a_line_already_printed_is_dropped_and_another_conversations_is_never_printed()
    {
        if (!OperatingSystem.IsWindows()) return;
        _backlog.AddRange([("line one", 1), ("line two", 2)]);
        var run = Start();
        await Running();
        await UntilAsync(() => _out.Text.Contains("line two"), "the backlog printed");

        await _standIn.PushAsync("DAORIS", "SESSION_OUTPUT", new { session = "c1", lines = new[] { new { sequence = 2L, text = "line two" }, new { sequence = 3L, text = "line three" } }, live = true });
        await _standIn.PushAsync("DAORIS", "SESSION_OUTPUT", new { session = "c9", lines = new[] { new { sequence = 1L, text = "not mine" } }, live = true });
        await _standIn.PushAsync("DAORIS", "SESSION_OUTPUT", new { session = "c1", lines = new[] { new { sequence = 4L, text = "line four" } }, live = true });
        await UntilAsync(() => _out.Text.Contains("line four"), "the forwarded lines printed");

        Assert.Equal("line one\nline two\nline three\nline four\n", _out.Text);
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task A_GAP_reads_the_backlog_again_from_the_last_line_printed()
    {
        if (!OperatingSystem.IsWindows()) return;
        _backlog.AddRange([("line one", 1), ("line two", 2), ("line three", 3)]);
        var run = Start();
        await Running();
        await UntilAsync(() => _out.Text.Contains("line three"), "the backlog printed");

        _backlog.AddRange([("line four", 4), ("line five", 5), ("line six", 6)]);
        await _standIn.PushAsync(InstanceClient.PipeModule, "GAP", new { session = "c1", sequence = 5L });
        await UntilAsync(() => _out.Text.Contains("line six"), "the backlog read again");

        Assert.Equal(3, Sent("TAIL_SESSION")[1].GetProperty("payload").GetProperty("after").GetInt64());
        Assert.Equal("line one\nline two\nline three\nline four\nline five\nline six\n", _out.Text);
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task The_persons_words_said_in_the_window_are_printed_and_the_terminals_own_are_not_said_back()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = Start();
        await Running();
        _input.Say("mine");
        await UntilAsync(() => Sent("SESSION_INPUT").Count == 1, "the line said");

        await _standIn.PushAsync("DAORIS", "SESSION_EVENTS", new
        {
            session = "c1",
            events = new object[]
            {
                new { seq = 1, kind = "user", origin = "person", text = "mine", id = "e1" },
                new { seq = 2, kind = "user", origin = "target", text = "the composed prompt", id = "e2" },
                new { seq = 3, kind = "user", origin = "person", text = "from the window", id = "e3" },
                new { seq = 4, kind = "user", origin = "person", text = "from the window", id = "e3" },
            },
        });
        await UntilAsync(() => _error.Text.Contains("chat: said in Daoris: from the window"), "the window's words printed");

        Assert.DoesNotContain("said in Daoris: mine", _error.Text);
        Assert.DoesNotContain("composed prompt", _error.Text);
        Assert.Equal(1, _error.Text.Split("said in Daoris: from the window").Length - 1);
        _input.End();
        await UntilAsync(() => Sent("END_CHAT").Count > 0, "the finish");
        await EndedAsync();
        Assert.Equal(0, await run);
    }

    /// <summary>A reader a test types into: its lines in order, then the end of input.</summary>
    private sealed class ScriptedInput : TextReader
    {
        private readonly Channel<string?> _lines = Channel.CreateUnbounded<string?>();

        public void Say(string line) => _lines.Writer.TryWrite(line);

        public void End() => _lines.Writer.TryWrite(null);

        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            await _lines.Reader.ReadAsync(cancellationToken);

        public override string? ReadLine() => _lines.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>A writer the test reads while the verb writes.</summary>
    private sealed class Lines : TextWriter
    {
        private readonly StringBuilder _text = new();

        public Lines() => NewLine = "\n";

        public override Encoding Encoding => Encoding.UTF8;

        public string Text
        {
            get
            {
                lock (_text) return _text.ToString();
            }
        }

        public override void Write(char value)
        {
            lock (_text) _text.Append(value);
        }

        public override void Write(string? value)
        {
            lock (_text) _text.Append(value);
        }
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
