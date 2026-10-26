using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("agent operation ports")]
public sealed class AgentCoursePortsTests
{
    private static SessionTarget Target() => new("q1", "Title", "Body", "ask #a1", "engine", "C:/stand/engine", "http://localhost");

    [Fact]
    public void Actual_views_keep_message_queue_files_callbacks_higher_state_and_serialized_properties()
    {
        Assert.IsType<CourseAgentOperations>(AgentCourse.Current);
        var file = new KeptFile("note.txt", "stand/note.txt");
        var message = new ChatMessage("exact words", [file])
        {
            Id = "w1", Key = "producer", Preface = "preface", Briefing = "briefing", Supervised = "decision",
            Process = new("process", "mode", "source"), Attempting = () => Task.FromResult<IReadOnlyList<string>>(["w1"]),
            Went = () => { },
        };
        IAgentWord word = message;
        Assert.Same(message, word);
        Assert.Same(message, ChatMessage.FromAgent(word));
        Assert.Same(message.Files, word.Files);
        Assert.Same(message.Attempting, word.Attempting);
        Assert.Same(message.Went, word.Went);
        Assert.Equal("briefing\n\nprocess\n\npreface\n\nexact words", word.Prompt);
        Assert.Equal(JsonSerializer.Serialize(message), JsonSerializer.Serialize(word));
        var queue = new ChatQueue(true, [message], DateTimeOffset.UnixEpoch, true, true);
        IAgentQueue view = queue;
        Assert.Same(message, Assert.Single(view.Queued));
        Assert.Equal(JsonSerializer.Serialize(queue), JsonSerializer.Serialize(view));
        Assert.Equal(HandOverNote.Said(" agent claim "), AgentCourse.Current.HandOverText(" agent claim "));
        Assert.Null(AgentCourse.Current.HandOverText(" "));
    }

    [Fact]
    public void Actual_registry_preserves_same_instance_close_removal_and_original_queue_notifications()
    {
        var processes = new SessionProcesses();
        var queues = new List<IAgentQueue>();
        processes.HeldChanged += (_, queue) => queues.Add(queue);
        var first = processes.OpenInbox("s1");
        var newer = processes.OpenInbox("S1");
        first.Close();
        Assert.Same(newer, processes.WordInboxOf("s1"));
        Assert.Same(newer, processes.InboxOf("s1"));
        newer.Attach(null);
        Assert.True(newer.Hold(new("original", [])));
        Assert.IsType<ChatQueue>(queues[^1]);
        Assert.Same(newer.State.Queued.Single(), queues[^1].Queued.Single());
        newer.Close();
        Assert.Null(processes.WordInboxOf("s1"));
    }

    [Fact]
    public async Task Actual_retry_binding_keeps_key_author_attempt_acknowledgement_and_take_off_fallback()
    {
        var doors = new Doors();
        using var service = new ServiceClient(doors);
        var keep = AgentCourse.Current.RetryGoOn(service, "s1");
        var episode = new TransientEpisode(DateTimeOffset.UnixEpoch,
            new(AgentErrorKind.Transient, new("protocol", -32603, "overloaded", 529, "failure", DoorWhere.Prompt), "stand"));
        var word = Assert.IsType<ChatMessage>(await keep(episode, "go on", default));
        Assert.Equal("w1", word.Id);
        Assert.Equal(DaorisKeys.TransientRetry("s1", episode.Began), word.Daoris!.Key);
        Assert.True(word.Kept);
        var said = JsonDocument.Parse(doors.Calls[0].Json!).RootElement;
        Assert.Equal(SaidFrom.Daoris, said.GetProperty("from").GetString());
        Assert.Equal(word.Daoris.Key, said.GetProperty("key").GetString());
        Assert.True(said.GetProperty("running").GetBoolean());
        Assert.Equal(["w1"], await ((IAgentWord)word).Attempting!());
        Assert.DoesNotContain(doors.Calls, call => call.Path.EndsWith("/taken", StringComparison.Ordinal));
        ((IAgentWord)word).Went!();
        await Poll.Until(() => doors.Calls.Any(call => call.Path.EndsWith("/taken", StringComparison.Ordinal)));
        doors.Calls.Clear();
        await TransientRetries.GoOnOffAsync(service, "s1", default);
        var taken = Assert.Single(doors.Calls, call => call.Path.EndsWith("/taken", StringComparison.Ordinal));
        Assert.Equal(["retry-left"], JsonDocument.Parse(taken.Json!).RootElement.GetProperty("said").EnumerateArray().Select(id => id.GetString()));
        doors.Refused = true;
        Assert.Null(await keep(episode, "go on", default));
        doors.Refused = false;
        doors.Failure = new OperationCanceledException(new CancellationToken(true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => keep(episode, "go on", new CancellationToken(true)));
    }

    [Fact]
    public async Task Independent_installed_operations_keep_registry_and_binding_inputs_and_missing_refused_cancelled_wiring_refuses()
    {
        var actual = AgentCourse.Current;
        var stand = new Course();
        var processes = new SessionProcesses();
        using var service = new ServiceClient(new Doors());
        try
        {
            AgentCourse.Current = stand;
            var queues = new List<IAgentQueue>();
            var left = new List<IAgentWord>();
            processes.HeldChanged += (_, queue) => queues.Add(queue);
            processes.KeptLeft += (_, words) => left.AddRange(words);
            var first = Assert.IsType<Inbox>(processes.OpenWordInbox("s1"));
            var next = Assert.IsType<Inbox>(processes.OpenWordInbox("S1"));
            var word = new Word();
            var queue = new Queue(word);
            next.Changed!(queue);
            next.Left!([word]);
            Assert.Same(queue, Assert.Single(queues));
            Assert.Same(word, Assert.Single(left));
            Assert.False(next.PersonStopped!());
            first.Close();
            Assert.Same(next, processes.WordInboxOf("s1"));
            next.Close();
            Assert.Null(processes.WordInboxOf("s1"));
            Assert.Contains("stand claim", TargetPrompt.Compose(Target() with { CutOff = "handed over", HandedOver = "prior", HandedNote = "claim" }));
            Assert.Equal("claim", stand.Note);
            var binding = TransientRetries.For(AdapterSet.Built(), AdapterSet.Built().Resolve("claude-code-acp"),
                new HarnessRoster(AdapterSet.Built(), "stand"), service, new Events(), null, SessionWatch.Of(new Events()), null,
                "s1", "q1", null, TimeProvider.System, _ => Task.FromResult(true));
            Assert.Same(stand.Keep, binding.KeepGoOn);
            Assert.Same(service, stand.Service);
            Assert.Equal("s1", stand.Session);
            await TransientRetries.GoOnOffAsync(service, "s1", default);
            Assert.True(stand.Off);
            AgentCourse.Current = null!;
            Assert.Throws<InvalidOperationException>(() => processes.OpenWordInbox("missing"));
            Assert.Throws<InvalidOperationException>(() => TargetPrompt.Compose(Target() with { CutOff = "handed over", HandedOver = "prior" }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => TransientRetries.GoOnOffAsync(service, "s1", default));
            foreach (var failure in new Exception[] { new IOException("refused"), new OperationCanceledException(new CancellationToken(true)) })
            {
                AgentCourse.Current = new Course { Failure = failure };
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => processes.OpenWordInbox("refused")));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => TargetPrompt.Compose(Target() with { CutOff = "handed over", HandedOver = "prior" })));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentCourse.Current.RetryGoOn(service, "s1")));
                Assert.Same(failure, await Assert.ThrowsAsync(failure.GetType(), () => TransientRetries.GoOnOffAsync(service, "s1", default)));
            }
        }
        finally { AgentCourse.Current = actual; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Independent_words_await_acknowledgement_before_wire_and_only_written_words_get_receipts_and_Went(bool acknowledged)
    {
        var calls = new List<string>();
        var marked = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var word = new Word { Attempting = () => { calls.Add("attempt"); attempting.SetResult(); return marked.Task; }, Went = () => calls.Add("went") };
        var inbox = new Inbox { Word = word };
        var wire = new Wire(calls);
        var session = new AcpSession(wire.Incoming, wire, _ => { }, onEvent: e =>
        {
            if (e.Kind == SessionEventKind.Receipt && e.Receipt == Receipts.Sent) calls.Add("receipt");
        });
        var run = session.RunAsync("stand", "opening", default, inbox: inbox, asked: taken => Assert.Same(word, taken));
        await Task.WhenAny(attempting.Task, run).WaitAsync(Poll.Patience);
        if (run.IsCompleted) await run;
        Assert.True(attempting.Task.IsCompleted, "the held word never reached its acknowledgement callback");
        Assert.Single(wire.Prompts);
        Assert.DoesNotContain("went", calls);
        marked.SetResult(acknowledged ? ["w1"] : []);
        await run.WaitAsync(Poll.Patience);
        Assert.Equal(acknowledged ? 2 : 1, wire.Prompts.Count);
        if (acknowledged)
        {
            Assert.Equal(["attempt", "wire", "receipt", "went"], calls);
            var blocks = wire.Prompts[1].GetProperty("params").GetProperty("prompt");
            Assert.Equal(word.Prompt, blocks[0].GetProperty("text").GetString());
            Assert.Equal("attached", blocks[1].GetProperty("name").GetString());
        }
        else Assert.Equal(["attempt"], calls);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("rate_limit")]
    [InlineData("fatal")]
    public async Task Independent_episode_words_cross_held_go_on_refused_and_uncertain_contracts_without_reconstruction(string result)
    {
        var calls = new List<string>();
        var word = new Word { Went = () => calls.Add("person went") };
        var goOn = new Word { Id = "go-on", IsDaoris = true, Went = () => calls.Add("go-on went") };
        var inbox = new Inbox { Word = word };
        var wire = new Wire(calls) { FirstFails = true, RetryFailure = result == "success" ? null : result };
        var refused = new List<IAgentWord>();
        var retry = new TransientRetry
        {
            Read = failure => new(failure.ErrorKind == "rate_limit" ? AgentErrorKind.Limit : failure.ErrorKind == "fatal" ? AgentErrorKind.Fatal : AgentErrorKind.Transient,
                DoorFailure.OfPrompt(failure), "stand"),
            KeepGoOn = (_, _, _) => Task.FromResult<IAgentWord?>(goOn),
            LimitRefused = words => refused.AddRange(words),
        };
        var session = new AcpSession(wire.Incoming, wire, _ => { });
        var run = session.RunAsync("stand", "opening", default, inbox: inbox, retry: retry);
        if (result == "success") await run.WaitAsync(Poll.Patience);
        else await Assert.ThrowsAsync<AcpRefusal>(() => run.WaitAsync(Poll.Patience));
        Assert.Equal(2, wire.Prompts.Count);
        Assert.Equal(1, inbox.HeldTakes);
        Assert.Contains("person went", calls);
        Assert.Contains("go-on went", calls);
        if (result == "rate_limit") Assert.Same(word, Assert.Single(refused));
        else Assert.Empty(refused);
        if (result == "fatal") Assert.Same(word, Assert.Single(retry.Uncertain));
        else Assert.Empty(retry.Uncertain);
    }

    [Fact]
    public async Task A_failed_wire_keeps_the_actual_dispatched_word_and_never_tells_it_Went()
    {
        var record = new Daoris.Driver.Tests.StandInSaid();
        var kept = await record.KeepAsync(new("keep me", []), default);
        var message = SaidDoor.Dispatched(new("keep me", []) { Id = kept.Word!.Id, Kept = true }, record, _ => Assert.Fail("an unwritten word went"));
        var calls = new List<string>();
        var failure = new IOException("wire refused");
        var wire = new Wire(calls) { WriteFailure = failure };
        var session = new AcpSession(wire.Incoming, wire, _ => { });
        await Assert.ThrowsAsync<IOException>(() => session.RunAsync("stand", "opening", default, inbox: new Inbox { Word = message }));
        Assert.Same(message, Assert.Single(session.Unreached));
        Assert.Contains(record.Said, word => word.Id == message.Id);
        Assert.Empty(record.TakenOff);
    }

    [Fact]
    public async Task Independent_drop_judgement_settles_without_wire_and_prompt_required_hands_back_the_same_word()
    {
        var calls = new List<string>();
        var word = new Word { IsDaoris = true, Goes = false, Went = () => calls.Add("settled") };
        var wire = new Wire(calls);
        var dropped = new List<SessionEvent>();
        var session = new AcpSession(wire.Incoming, wire, _ => { }, onEvent: e => dropped.Add(e));
        await session.RunAsync("stand", "opening", default, inbox: new Inbox { Word = word }).WaitAsync(Poll.Patience);
        Assert.Single(wire.Prompts);
        Assert.Equal(["settled"], calls);
        Assert.Contains(dropped, e => e.Code == SessionEventCodes.Dropped && e.Words!.Single() == word.Id);

        calls.Clear();
        wire = new Wire(calls) { Steering = true };
        session = new AcpSession(wire.Incoming, wire, _ => { });
        await session.OpenAsync("stand", default);
        try
        {
            var returned = false;
            var outcome = await session.SteeredAsync(word, default, handBack: back =>
            {
                Assert.Same(word, back);
                returned = true;
                return true;
            }).WaitAsync(Poll.Patience);
            Assert.Equal("promptRequired", outcome);
            Assert.True(returned);
            Assert.Equal(["settled"], calls);
            await session.CloseAsync(default);
        }
        finally { session.Release(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_inbox_lower_close_keeps_failed_run_words_and_only_person_stop_takes_them_off(bool stopped)
    {
        var door = new Daoris.Driver.Tests.StandInSaid();
        var left = new List<IAgentWord>();
        IAgentInbox inbox = AgentCourse.Current.OpenInbox(_ => { });
        inbox.OnClosing(() => { }, () => stopped, words => left.AddRange(words));
        var real = Assert.IsType<DrivenInbox>(inbox);
        real.Attach(null);
        await real.KeepAsync(door, new("kept", []), default);
        var waiting = Assert.Single(real.State.Queued);
        var closed = inbox.Close();
        await real.TakingOff;
        if (stopped)
        {
            Assert.Same(waiting, Assert.Single(closed));
            Assert.Empty(left);
            Assert.Empty(door.Said);
        }
        else
        {
            Assert.Empty(closed);
            Assert.Same(waiting, Assert.Single(left));
            Assert.Single(door.Said);
        }
    }

    private sealed class Word : IAgentWord
    {
        public string Text => "stand words";
        public string Prompt => "lossless stand prompt";
        public string Id { get; init; } = "w1";
        public IReadOnlyList<KeptFile> Files { get; } = [new("attached", "C:/stand/file")];
        public bool IsDaoris { get; init; }
        public Func<Task<IReadOnlyList<string>>>? Attempting { get; init; }
        public Action? Went { get; init; }
        public bool Goes { get; init; } = true;
        public Task<bool> GoesAsync(CancellationToken ct) => Task.FromResult(Goes);
        public SessionEvent Dropped() => new() { Kind = SessionEventKind.Note, Code = SessionEventCodes.Dropped, Words = [Id] };
    }
    private sealed record Queue(IAgentWord Word) : IAgentQueue
    {
        public bool Taking => true;
        public IReadOnlyList<IAgentWord> Queued => [Word];
        public DateTimeOffset? LastTurnEnded => null;
        public bool Opening => false;
        public bool Pressing => false;
    }
    private sealed class Inbox : IAgentInbox
    {
        public IAgentWord? Word;
        public Action<IAgentQueue>? Changed;
        public Action? Closing;
        public Func<bool>? PersonStopped;
        public Action<IReadOnlyList<IAgentWord>>? Left;
        public int HeldTakes;
        public bool Holds => Word is not null;
        public void Attach(Func<Task>? interrupt, Func<IAgentWord, Task<string>>? deliver, Func<IAgentWord, Task<string>>? steer) { }
        public void Flow() { }
        public void LastAnswered() { }
        public bool HandBack(IAgentWord word) { Word = word; return true; }
        public AgentNext? NextOrClose() { if (Word is not { } word) return null; Word = null; return new(word, null); }
        public IReadOnlyList<IAgentWord> Close() { Closing?.Invoke(); return []; }
        public void OnClosing(Action closing, Func<bool> personStopped, Action<IReadOnlyList<IAgentWord>> left) { Closing = closing; PersonStopped = personStopped; Left = left; }
        public void Settling() { }
        public Task Waiting() => Task.CompletedTask;
        public void Waited() { }
        public Task<string>? TakeSent() => null;
        public IReadOnlyList<IAgentWord> TakeHeld() { HeldTakes++; var word = Word; Word = null; return word is null ? [] : [word]; }
    }
    private sealed class Course : IAgentCourse
    {
        public Exception? Failure;
        public string? Note;
        public ServiceClient? Service;
        public string? Session;
        public bool Off;
        public readonly Func<TransientEpisode, string, CancellationToken, Task<IAgentWord?>> Keep = (_, _, _) => Task.FromResult<IAgentWord?>(new Word());
        private void Check() { if (Failure is { } failure) throw failure; }
        public IAgentInbox OpenInbox(Action<IAgentQueue> changed) { Check(); return new Inbox { Changed = changed }; }
        public string? HandOverText(string? note) { Check(); Note = note; return "stand claim"; }
        public Func<TransientEpisode, string, CancellationToken, Task<IAgentWord?>> RetryGoOn(ServiceClient service, string sessionId) { Check(); Service = service; Session = sessionId; return Keep; }
        public Task GoOnOffAsync(ServiceClient service, string sessionId, CancellationToken ct) { Check(); Service = service; Session = sessionId; Off = true; return Task.CompletedTask; }
    }
    private sealed class Events : ISessionEvents
    {
        public event Action<string, SessionEvent>? Evented { add { } remove { } }
        public void Keep(string sessionId, SessionEvent e, Action<string>? say) { }
    }
    private sealed class Doors : IServiceDoors
    {
        public string BaseUrl => "http://stand";
        public readonly List<(string Path, string? Json)> Calls = [];
        public bool Refused;
        public Exception? Failure;
        public void Dispose() { }
        public Task<string> GetAsync(string path, CancellationToken ct) { Calls.Add((path, null)); return Task.FromResult("""[{"id":"s1","said":[{"id":"retry-left","text":"go on","key":"transient-retry/s1/old","at":"2026-10-27T00:00:00Z"},{"id":"person-left","text":"person","at":"2026-10-27T00:00:00Z"}]}]"""); }
        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Calls.Add((path, json));
            if (Failure is { } failure) return Task.FromException<ServiceReply>(failure);
            return Task.FromResult(Refused ? new ServiceReply(403, "{\"error\":\"refused\"}") : new(200,
                path.EndsWith("/say", StringComparison.Ordinal) ? """{"said":{"id":"w1","text":"go on","at":"2026-10-27T00:00:00Z"}}""" : "{}"));
        }
    }
    private sealed class Wire(List<string> calls) : TextWriter
    {
        private readonly Channel<string> _answers = Channel.CreateUnbounded<string>();
        public readonly List<JsonElement> Prompts = [];
        public bool FirstFails;
        public string? RetryFailure;
        public Exception? WriteFailure;
        public bool Steering;
        public TextReader Incoming => new Reader(_answers);
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value)
        {
            var frame = JsonDocument.Parse(value!).RootElement;
            if (!frame.TryGetProperty("id", out var id)) return Task.CompletedTask;
            var method = frame.GetProperty("method").GetString();
            if (method == "session/prompt")
            {
                if (Prompts.Count > 0 && WriteFailure is { } failure) throw failure;
                Prompts.Add(frame);
                if (Prompts.Count > 1) calls.Add("wire");
                if (FirstFails && Prompts.Count == 1 || Prompts.Count == 2 && RetryFailure is not null)
                {
                    var kind = Prompts.Count == 1 ? "overloaded" : RetryFailure;
                    _answers.Writer.TryWrite($"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"error\":{{\"code\":-32603,\"message\":\"refused\",\"data\":{{\"errorKind\":\"{kind}\"}}}}}}");
                    return Task.CompletedTask;
                }
            }
            var result = method switch
            {
                "initialize" => Steering
                    ? "{\"protocolVersion\":1,\"_meta\":{\"steering\":{\"supported\":true}},\"agentCapabilities\":{\"_meta\":{\"claudeCode\":{\"promptQueueing\":true}}}}"
                    : "{\"protocolVersion\":1,\"agentCapabilities\":{}}",
                "session/new" => "{\"sessionId\":\"s-1\"}",
                "session/prompt" => "{\"stopReason\":\"end_turn\"}",
                "_session/steering" => "{\"outcome\":\"promptRequired\"}",
                _ => "{}",
            };
            _answers.Writer.TryWrite($"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":{result}}}");
            return Task.CompletedTask;
        }
        private sealed class Reader(Channel<string> answers) : TextReader
        {
            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await answers.Reader.WaitToReadAsync(ct) && answers.Reader.TryRead(out var line) ? line : null;
        }
    }
}
