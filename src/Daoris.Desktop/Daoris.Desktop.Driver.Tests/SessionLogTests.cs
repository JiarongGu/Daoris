using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the person runs, and how long each part of it takes, in the machine log (LOG1b, D94): the service
/// client says when a session record opens and moves, the conversation record says when its events land,
/// and the watcher turns the two into lines, never with anyone's words.
/// </summary>
/// <remarks>
/// Driven through the real client over a stand-in ledger and the real conversation record, so what is
/// asserted is what the shell's loop would write; the clock is the test's.
/// </remarks>
public sealed class SessionLogTests : IDisposable
{
    private const string Ledger = "http://ledger.test";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-sessionlog-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 9, 30, 7, 10, 0, TimeSpan.Zero);

    private readonly StandInLedger _ledger = new();

    public SessionLogTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed record Watching(ServiceClient Client, SessionEvents Events, MachineLog Log, SessionLog Watch) : IDisposable
    {
        public void Dispose()
        {
            Watch.Dispose();
            Log.Dispose();
            Client.Dispose();
        }
    }

    private Watching Watch()
    {
        var client = new ServiceClient(Ledger, null, new HttpClient(_ledger));
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var log = new MachineLog(_home, "desktop", () => _now);
        return new Watching(client, events, log, new SessionLog(log, client, events, () => _now));
    }

    /// <summary>Every line written so far, parsed.</summary>
    private List<JsonElement> Lines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone()),
        ];
    }

    private string Raw() => string.Join("\n", Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).Select(StubFile.Text));

    private List<JsonElement> Named(string @event) =>
        [.. Lines().Where(line => line.GetProperty("event").GetString() == @event)];

    private static JsonElement Data(JsonElement line) => line.GetProperty("data");

    private void Said(SessionEvents events, string session, SessionEvent e, double atSeconds) =>
        events.Append(session, e with { At = _now.AddSeconds(atSeconds) });

    [Fact]
    public async Task A_session_opened_and_ended_is_two_lines_and_the_end_says_how_long_it_ran()
    {
        using var w = Watch();

        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code-acp");
        await w.Client.AdvanceAsync(id!, "starting");
        await w.Client.AdvanceAsync(id!, "working");
        _now = _now.AddSeconds(90.7);
        await w.Client.AdvanceAsync(id!, "completed", note: "the conversation ended");

        var started = Data(Assert.Single(Named("session.started")));
        Assert.Equal(id, started.GetProperty("session").GetString());
        Assert.Equal("chat", started.GetProperty("kind").GetString());
        Assert.Equal("claude-code-acp", started.GetProperty("adapter").GetString());
        Assert.Equal("engine", started.GetProperty("repository").GetString());

        // A move that is not an ending is no line: `starting` and `working` are the ledger's business.
        var ended = Data(Assert.Single(Named("session.ended")));
        Assert.Equal(id, ended.GetProperty("session").GetString());
        Assert.Equal("completed", ended.GetProperty("state").GetString());
        Assert.Equal(90, ended.GetProperty("seconds").GetInt64());
        Assert.DoesNotContain("the conversation ended", Raw());
    }

    /// <summary>Which door opened it is the kind: the ledger's record calls an intake and Ask Daoris a chat.</summary>
    [Theory]
    [InlineData("driven", "engine")]
    [InlineData("chat", "game")]
    [InlineData("intake", "daoris:intake")]
    [InlineData("help", "daoris:help")]
    public async Task Each_door_names_its_kind_and_the_repository_the_ledger_put_it_in(string kind, string repository)
    {
        using var w = Watch();

        var (id, _) = kind switch
        {
            // A driven session names its quest; the ledger answers with the quest's receiver.
            "driven" => await w.Client.OpenSessionAsync("q1", "claude-code"),
            "chat" => await w.Client.OpenChatAsync("game", "claude-code"),
            "intake" => await w.Client.OpenIntakeAsync("a1", "claude-code", "room"),
            _ => await w.Client.OpenHelpAsync("claude-code", "room"),
        };

        var started = Data(Assert.Single(Named("session.started")));
        Assert.Equal(id, started.GetProperty("session").GetString());
        Assert.Equal(kind, started.GetProperty("kind").GetString());
        Assert.Equal(repository, started.GetProperty("repository").GetString());
    }

    /// <summary>
    /// The three waits a person sits through, each timed from the conversation record's own stamps: the
    /// open to the first prompt handed over, a prompt to the first thing back, and a prompt to its turn's end.
    /// </summary>
    [Fact]
    public async Task The_open_the_first_answer_and_each_turn_are_timed_and_nobody_s_words_are_written()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "rename the secret module" }, 5.84);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Usage, Used = 10, Size = 100 }, 6);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Thought, Text = "the agent's private reasoning" }, 7);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "the agent's answer" }, 8);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }, 20);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "and the second question" }, 30);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "t1", Title = "Read a private file", Input = "{\"path\":\"secret.txt\"}" }, 31.5);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled" }, 35);

        var opened = Data(Assert.Single(Named("session.opened")));
        Assert.Equal(s, opened.GetProperty("session").GetString());
        Assert.Equal("claude-code-acp", opened.GetProperty("adapter").GetString());
        Assert.Equal(5840, opened.GetProperty("openMs").GetInt64());

        var answered = Named("turn.answered").Select(Data).ToList();
        Assert.Equal([1160L, 1500L], answered.Select(line => line.GetProperty("firstAnswerMs").GetInt64()));
        Assert.All(answered, line => Assert.Equal(s, line.GetProperty("session").GetString()));

        var ended = Named("turn.ended").Select(Data).ToList();
        Assert.Equal(["end_turn", "cancelled"], ended.Select(line => line.GetProperty("stopReason").GetString()));
        Assert.Equal([14160L, 5000L], ended.Select(line => line.GetProperty("turnMs").GetInt64()));

        var raw = Raw();
        foreach (var words in new[] { "rename the secret module", "private reasoning", "the agent's answer", "second question", "Read a private file", "secret.txt" })
        {
            Assert.DoesNotContain(words, raw);
        }
    }

    /// <summary>
    /// A prompt the driver sends as two lines of the record (its target, then the person's answer to a
    /// parked session) is one turn, timed from the first.
    /// </summary>
    [Fact]
    public async Task A_turn_that_opens_with_two_prompts_is_timed_from_the_first()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the composed target" }, 2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "carry on" }, 2.5);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Message, Text = "on it" }, 4);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" }, 12);

        Assert.Equal(2000, Data(Assert.Single(Named("session.opened"))).GetProperty("openMs").GetInt64());
        Assert.Equal(2000, Data(Assert.Single(Named("turn.answered"))).GetProperty("firstAnswerMs").GetInt64());
        Assert.Equal(10000, Data(Assert.Single(Named("turn.ended"))).GetProperty("turnMs").GetInt64());
    }

    /// <summary>
    /// A session this process never saw open (one a previous run left, swept as an orphan) still ends in
    /// the log, and says it cannot say how long it ran: absent is never zero.
    /// </summary>
    [Fact]
    public async Task A_session_whose_open_was_not_seen_ends_with_no_seconds_and_opens_no_wait()
    {
        using var w = Watch();

        Said(w.Events, "orphan1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "hello" }, 1);
        Said(w.Events, "orphan1", new SessionEvent { Kind = SessionEventKind.Message, Text = "hi" }, 3);
        await w.Client.AdvanceAsync("orphan1", "stopped", note: "swept");

        Assert.Empty(Named("session.opened"));
        Assert.Equal(2000, Data(Assert.Single(Named("turn.answered"))).GetProperty("firstAnswerMs").GetInt64());
        var ended = Data(Assert.Single(Named("session.ended")));
        Assert.Equal("stopped", ended.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("seconds").ValueKind);
    }

    /// <summary>A turn's end with no prompt seen before it says how it ended, and not how long it took.</summary>
    [Fact]
    public async Task A_turn_end_with_no_prompt_before_it_has_no_duration()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code");

        Said(w.Events, id!, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" }, 4);

        var ended = Data(Assert.Single(Named("turn.ended")));
        Assert.Equal("error", ended.GetProperty("stopReason").GetString());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("turnMs").ValueKind);
    }

    /// <summary>
    /// UNBLOCK5 (D122 §3.10): a call refused on the protocol door is one ask, as the record says it. The
    /// refusal arrives as an update to the call (HELP4's <c>refused</c>), carrying no kind, so the kind is the
    /// one the call was announced with. The same call refused again is still one ask, a call that merely
    /// failed is none, and neither the call's title nor its input reaches the log.
    /// </summary>
    [Fact]
    public async Task A_refused_call_is_one_ask_with_its_kind_and_never_its_words()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenChatAsync("engine", "claude-code-acp");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "ship it" }, 1);
        Said(w.Events, s, new SessionEvent
        {
            Kind = SessionEventKind.Tool, Id = "c9", Title = "git push origin release", ToolKind = "execute",
            Status = "pending", Input = "{\"command\":\"git push origin release\"}",
        }, 2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Note, Id = "c9", Text = "permission refused: `git push origin release`" }, 2.1);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c9", Status = "refused" }, 2.2);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c9", Status = "refused", Output = "denied" }, 2.3);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c10", Title = "cargo build", ToolKind = "execute", Status = "pending" }, 3);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c10", Status = "failed" }, 4);

        var refused = Data(Assert.Single(Named("permission.refused")));
        Assert.Equal(s, refused.GetProperty("session").GetString());
        Assert.Equal("claude-code-acp", refused.GetProperty("adapter").GetString());
        Assert.Equal("execute", refused.GetProperty("kind").GetString());
        // The protocol door's wire names a call's kind and title, never its tool, nor what decided it.
        Assert.Equal(JsonValueKind.Null, refused.GetProperty("tool").ValueKind);
        Assert.Equal(JsonValueKind.Null, refused.GetProperty("by").ValueKind);

        var raw = Raw();
        foreach (var words in new[] { "git push", "origin release", "cargo build", "ship it", "denied" })
        {
            Assert.DoesNotContain(words, raw);
        }
    }

    /// <summary>
    /// The native door's refusal names its tool and what decided it, in the wire's own words (UNBLOCK5), and a
    /// refused call the record never announced is still an ask, of a kind nobody said.
    /// </summary>
    [Fact]
    public async Task The_native_door_s_refusal_names_its_tool_and_what_decided_it()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code");
        var s = id!;

        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_7", Status = "refused", ToolKind = "execute", ToolName = "Bash", RefusedBy = "rule" }, 1);
        Said(w.Events, s, new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_8", Status = "refused", ToolName = "mcp__daoris-knowledge__quest_publish", RefusedBy = "classifier" }, 2);

        var lines = Named("permission.refused").Select(Data).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("claude-code", "Bash", "execute", "rule"), (
            lines[0].GetProperty("adapter").GetString(), lines[0].GetProperty("tool").GetString(),
            lines[0].GetProperty("kind").GetString(), lines[0].GetProperty("by").GetString()));
        Assert.Equal("mcp__daoris-knowledge__quest_publish", lines[1].GetProperty("tool").GetString());
        Assert.Equal(JsonValueKind.Null, lines[1].GetProperty("kind").ValueKind);
        Assert.Equal("classifier", lines[1].GetProperty("by").GetString());
    }

    /// <summary>
    /// 🔴 The line never carries words (D94 §5): a tool's name or a decider that is not an identifier is
    /// written as null, whatever a wire put there. A session whose open this process never saw has no adapter.
    /// </summary>
    [Fact]
    public void A_name_that_is_not_an_identifier_is_written_as_null()
    {
        using var w = Watch();

        Said(w.Events, "orphan2", new SessionEvent
        {
            Kind = SessionEventKind.Tool, Id = "t1", Status = "refused",
            ToolName = "Bash(git push --force)", RefusedBy = "the person said no", ToolKind = "run a command",
        }, 1);

        var refused = Data(Assert.Single(Named("permission.refused")));
        Assert.Equal("orphan2", refused.GetProperty("session").GetString());
        foreach (var field in new[] { "adapter", "tool", "kind", "by" })
        {
            Assert.Equal(JsonValueKind.Null, refused.GetProperty(field).ValueKind);
        }

        var raw = Raw();
        Assert.DoesNotContain("git push", raw);
        Assert.DoesNotContain("said no", raw);
    }

    /// <summary>A parked session the person answered ends `completed`, through the answer door.</summary>
    [Fact]
    public async Task An_answered_session_ends_in_the_log()
    {
        using var w = Watch();
        var (id, _) = await w.Client.OpenSessionAsync("q1", "claude-code");
        _now = _now.AddSeconds(42);

        var (ok, _) = await w.Client.AnswerSessionAsync(id!, "yes, the second option");

        Assert.True(ok);
        var ended = Data(Assert.Single(Named("session.ended")));
        Assert.Equal("completed", ended.GetProperty("state").GetString());
        Assert.Equal(42, ended.GetProperty("seconds").GetInt64());
        Assert.DoesNotContain("second option", Raw());
    }

    /// <summary>What the ledger refused did not happen, so nothing is written about it.</summary>
    [Fact]
    public async Task A_refused_open_or_move_writes_nothing()
    {
        using var w = Watch();
        _ledger.Refusing = true;

        var (id, message) = await w.Client.OpenChatAsync("engine", "claude-code");
        await Assert.ThrowsAsync<DriverException>(() => w.Client.AdvanceAsync("s9", "completed"));
        var (answered, _) = await w.Client.AnswerSessionAsync("s9", "words");

        Assert.Null(id);
        Assert.Equal("the repository is busy", message);
        Assert.False(answered);
        Assert.Empty(Lines());
    }

    /// <summary>A watcher that fails costs its own line, never the call the ledger already answered.</summary>
    [Fact]
    public async Task A_failing_watcher_never_fails_the_ledger_call()
    {
        using var client = new ServiceClient(Ledger, null, new HttpClient(_ledger));
        client.Opened += _ => throw new InvalidOperationException("a broken watcher");
        client.Moved += _ => throw new InvalidOperationException("a broken watcher");

        var (id, _) = await client.OpenChatAsync("engine", "claude-code");
        var moved = await client.AdvanceAsync(id!, "completed");

        Assert.NotNull(id);
        Assert.Equal("moved", moved);
    }

    /// <summary>A watcher let go of writes nothing more.</summary>
    [Fact]
    public async Task A_disposed_watcher_writes_nothing_more()
    {
        var w = Watch();
        await w.Client.OpenChatAsync("engine", "claude-code");
        w.Watch.Dispose();

        await w.Client.OpenChatAsync("game", "claude-code");
        w.Events.Append("s2", new SessionEvent { Kind = SessionEventKind.User, Text = "later" });

        Assert.Single(Lines());
        w.Dispose();
    }

    /// <summary>
    /// The ledger's doors, standing in: an open answers a record, a move answers the state it moved to,
    /// and a refusal answers 409 with the ledger's sentence.
    /// </summary>
    private sealed class StandInLedger : HttpMessageHandler
    {
        private int _next;

        public bool Refusing { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            using var body = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct));
            string? Field(string name) => body.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null;

            if (Refusing) return Answer(HttpStatusCode.Conflict, new { error = "the repository is busy" });

            var moved = System.Text.RegularExpressions.Regex.Match(path, "^/api/sessions/([^/]+)/(state|answer)$");
            if (moved.Success)
            {
                var state = moved.Groups[2].Value == "answer" ? "completed" : Field("state");
                return Answer(HttpStatusCode.OK, new { session = new { id = moved.Groups[1].Value, state }, message = "moved" });
            }

            var id = $"s{Interlocked.Increment(ref _next)}";
            var repository = path switch
            {
                "/api/sessions" => "engine",
                "/api/sessions/chat" => Field("repository"),
                "/api/sessions/intake" => "daoris:intake",
                "/api/sessions/help" => "daoris:help",
                _ => null,
            };
            return repository is null
                ? Answer(HttpStatusCode.NotFound, new { error = "no such door" })
                : Answer(HttpStatusCode.OK, new
                {
                    session = new { id, repository, adapter = Field("adapter"), state = "queued" },
                    message = "queued",
                });
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, object payload) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }
}
