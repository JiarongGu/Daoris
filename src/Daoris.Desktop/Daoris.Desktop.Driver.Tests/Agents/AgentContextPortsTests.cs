using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("agent operation ports")]
public sealed class AgentContextPortsTests
{
    private static SessionTarget Target() => new("q1", "Title", "Body", "ask #a1", "engine", "C:/stand/engine", "http://localhost");

    [Fact]
    public async Task Installed_transcript_and_event_history_keep_flush_append_encoding_and_same_instance_watch()
    {
        var home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "agent-context", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var path = Path.Combine(home, "transcript.log");
            await using (var writer = AgentContext.Current.OpenTranscript(path, false))
            {
                writer.WriteLine("first 道");
                await Poll.Until(
                    () => StubFile.Text(path).Contains("first 道", StringComparison.Ordinal),
                    () => $"the open transcript held {StubFile.Text(path).Length} character(s)");
            }
            await using (var writer = AgentContext.Current.OpenTranscript(path, true)) writer.WriteLine("second");
            Assert.Equal($"first 道{Environment.NewLine}second{Environment.NewLine}", File.ReadAllText(path));
            Assert.False(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
            var record = new SessionEvents(home);
            ISessionEventHistory history = record;
            Assert.Same(record, UsageTurns.Watched(home, history));
            Assert.Same(history, UsageTurns.Watched(home, history));
            history.Keep("s1", new() { Kind = SessionEventKind.Usage, Used = 321 }, null);
            Assert.Equal(record.After("s1", 0).Events, history.ReadAll("s1"));
            Assert.Equal(321, RotatedOpening.ContextOf(null, history, "s1"));
            var stand = new History();
            Assert.Same(stand, UsageTurns.Watched(home, stand));
            Assert.Same(stand, UsageTurns.Watched(home, stand));
            Assert.Equal(1, stand.Subscriptions);
            Assert.Equal(456, RotatedOpening.ContextOf(null, stand, "s1"));
        }
        finally { Directory.Delete(home, recursive: true); }
    }

    [Fact]
    public void Actual_composition_and_supplied_streams_keep_the_original_engine_and_account()
    {
        var actual = Assert.IsType<AgentContextOperations>(AgentContext.Current);
        var target = Target();
        Assert.Equal(SessionPartsText.Of(target), actual.Parts(target));
        TargetPrompt.Piece[] pieces = [new(HandedSections.Quest, "exact words")];
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(InstructionAccounting.Account(target, pieces)),
            System.Text.Json.JsonSerializer.Serialize(actual.Account(target, pieces)));
        Assert.Equal(InstructionAccounting.Intake(target, "intake").Text, actual.Intake(target, "intake").Text);
        var output = new SessionOutput();
        var streams = Assert.IsType<SessionStreams>(((ISessionOutput)output).ForSession("s1"));
        streams.Open(new("task", SessionStreamKind.Task, "Task", true));
        streams.Line("task", "first");
        streams.Open(new("task", SessionStreamKind.Task, "Repeated"));
        Assert.Equal("first", Assert.Single(output.Tail(SessionOutput.Key("s1", "task")).Lines).Text);
        streams.End("task", "refused");
        Assert.False(output.Tail("s1/task").Live);
        Assert.Empty(output.Tail("s1").Lines);
    }

    [Fact]
    public async Task Independent_operations_receive_original_inputs_and_capture_keeps_write_output_event_callback_disposal_order()
    {
        var actual = AgentContext.Current;
        var calls = new List<string>();
        var port = new ContextPort(calls);
        var target = Target();
        var output = new Output(calls);
        var events = new Events(calls);
        var mapper = new Mapper(calls, output.Streams);
        try
        {
            AgentContext.Current = port;
            Assert.Same(port.Instruction, TargetPrompt.Composed(target));
            Assert.Same(target, port.Target);
            Assert.Contains(port.Pieces!, piece => piece.Text.Contains("Body", StringComparison.Ordinal));
            var intake = target with { Prompt = "verbatim intake" };
            Assert.Same(port.Instruction, TargetPrompt.Composed(intake));
            Assert.Same(intake, port.Target);
            Assert.Equal("verbatim intake", port.Prompt);
            calls.Clear();
            await HarnessCapture.CaptureStructuredAsync(new StringReader("wire\n"), new StringReader(""), "stand", "s1",
                output, events, mapper, null, CancellationToken.None,
                observed: e => { Assert.Same(mapper.Event, e); calls.Add("observed"); });
            Assert.Equal(["open:stand:False", "streams:s1", "beside", "take:wire", "read:wire", "write:rendered",
                "output:s1:rendered", "event:s1", "observed", "end", "dispose"], calls);
            Assert.Same(mapper.Event, events.Kept);
        }
        finally { AgentContext.Current = actual; }
    }

    [Fact]
    public async Task Missing_install_refuses_and_operation_failures_and_reader_cancellation_propagate_with_disposal()
    {
        var actual = AgentContext.Current;
        try
        {
            AgentContext.Current = null!;
            Assert.Throws<InvalidOperationException>(() => TargetPrompt.Composed(Target()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => HarnessCapture.CaptureAsync(new StringReader(""), new StringReader(""), "stand", "s1", null, default));
            foreach (var failure in new Exception[] { new IOException("refused"), new OperationCanceledException(new CancellationToken(true)) })
            {
                AgentContext.Current = new ContextPort([]) { Failure = failure };
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => TargetPrompt.Composed(Target())));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => TargetPrompt.Composed(Target() with { Prompt = "intake" })));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentContext.Current.Account(Target(), [])));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentContext.Current.Parts(Target())));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentContext.Current.OpenTranscript("stand", false)));
            }
            var calls = new List<string>();
            AgentContext.Current = new ContextPort(calls);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HarnessCapture.CaptureAsync(new CancelledReader(), new StringReader(""), "stand", "s1", null, new CancellationToken(true)));
            Assert.Equal(["open:stand:False", "dispose"], calls);
        }
        finally { AgentContext.Current = actual; }
    }

    [Fact]
    public async Task Independent_output_and_event_sources_keep_relay_subscription_lifetime_and_native_stream_words()
    {
        var calls = new List<string>();
        var output = new Output(calls);
        var events = new Events(calls);
        var lines = new List<ConsoleLine>();
        var kept = new List<SessionEvent>();
        using (var console = new ConsoleRelay(output, (_, batch) => { lines.AddRange(batch); return Task.CompletedTask; }, TimeSpan.FromMinutes(5)))
        using (var relay = new EventRelay(events, (_, batch) => { kept.AddRange(batch); return Task.CompletedTask; }, TimeSpan.FromMinutes(5)))
        {
            Assert.Equal(1, output.Subscriptions);
            Assert.Equal(1, events.Subscriptions);
            output.Append("s1", "word");
            var e = new SessionEvent { Kind = SessionEventKind.Note, Text = "note" };
            events.Keep("s1", e, null);
            await console.FlushAsync();
            await relay.FlushAsync();
            Assert.Equal("word", Assert.Single(lines).Text);
            Assert.Same(e, Assert.Single(kept));
        }
        Assert.Equal(0, output.Subscriptions);
        Assert.Equal(0, events.Subscriptions);
        var streamCalls = output.Streams.Calls;
        var reader = new ClaudeStreams(output.Streams, _ => { });
        Assert.True(reader.Take("{\"type\":\"system\",\"subtype\":\"task_started\",\"task_id\":\"b1\",\"description\":\"task\",\"task_type\":\"local_bash\"}"));
        Assert.Contains("open:task/b1:task", streamCalls);
        await reader.EndAllAsync();
        Assert.Contains("end:task/b1:session-ended", streamCalls);
    }

    private sealed class ContextPort(List<string> calls) : IAgentContext
    {
        public Exception? Failure;
        public SessionTarget? Target;
        public IReadOnlyList<TargetPrompt.Piece>? Pieces;
        public string? Prompt;
        public readonly ComposedInstruction Instruction = new("stand instruction", new(17, []));
        private void Check() { if (Failure is { } failure) throw failure; }
        public TextWriter OpenTranscript(string path, bool append) { Check(); calls.Add($"open:{path}:{append}"); return new Writer(calls); }
        public ComposedInstruction Account(SessionTarget target, IReadOnlyList<TargetPrompt.Piece> pieces) { Check(); Target = target; Pieces = pieces; return Instruction; }
        public ComposedInstruction Intake(SessionTarget target, string prompt) { Check(); Target = target; Prompt = prompt; return Instruction; }
        public string Parts(SessionTarget target) { Check(); Target = target; return ""; }
    }
    private sealed class Writer(List<string> calls) : StringWriter
    {
        public override void WriteLine(string? value) => calls.Add($"write:{value}");
        public override ValueTask DisposeAsync() { calls.Add("dispose"); return ValueTask.CompletedTask; }
    }
    private sealed class CancelledReader : StringReader
    {
        public CancelledReader() : base("") { }
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => ValueTask.FromCanceled<string?>(cancellationToken);
    }
    private sealed class Output(List<string> calls) : ISessionOutput
    {
        public readonly Streams Streams = new();
        private event Action<string, ConsoleLine>? Heard;
        public int Subscriptions;
        public event Action<string, ConsoleLine>? Lined { add { Subscriptions++; Heard += value; } remove { Subscriptions--; Heard -= value; } }
        public void Append(string sessionId, string text) { calls.Add($"output:{sessionId}:{text}"); Heard?.Invoke(sessionId, new(1, text)); }
        public ISessionStreams ForSession(string sessionId) { calls.Add($"streams:{sessionId}"); return Streams; }
    }
    private sealed class Streams : ISessionStreams
    {
        public readonly List<string> Calls = [];
        public void Open(SessionStream stream) => Calls.Add($"open:{stream.Id}:{stream.Name}");
        public void Line(string streamId, string text) => Calls.Add($"line:{streamId}:{text}");
        public void End(string streamId, string? state) => Calls.Add($"end:{streamId}:{state}");
    }
    private sealed class Events(List<string> calls) : ISessionEvents
    {
        public SessionEvent? Kept;
        private event Action<string, SessionEvent>? Heard;
        public int Subscriptions;
        public event Action<string, SessionEvent>? Evented { add { Subscriptions++; Heard += value; } remove { Subscriptions--; Heard -= value; } }
        public void Keep(string sessionId, SessionEvent e, Action<string>? say) { Kept = e; calls.Add($"event:{sessionId}"); Heard?.Invoke(sessionId, e); }
    }
    private sealed class History : ISessionEventHistory
    {
        public int Subscriptions;
        public event Action<string, SessionEvent>? Evented { add { Subscriptions++; } remove { Subscriptions--; } }
        public void Keep(string sessionId, SessionEvent e, Action<string>? say) => throw new IOException("refused");
        public IReadOnlyList<SessionEvent> ReadAll(string sessionId) => [new() { Kind = SessionEventKind.Usage, Used = 456 }];
    }
    private sealed class Mapper(List<string> calls, ISessionStreams streams) : IStreamMapper, IStreamsReader
    {
        public readonly SessionEvent Event = new() { Kind = SessionEventKind.Note, Text = "kept" };
        public AcpUsage? Usage => null;
        public StreamMapped Read(string line) { calls.Add($"read:{line}"); return new(["rendered"], [Event]); }
        public IStreamsReader Beside(ISessionStreams supplied, Action<string> say) { Assert.Same(streams, supplied); calls.Add("beside"); return this; }
        public bool Take(string line) { calls.Add($"take:{line}"); return false; }
        public Task EndAllAsync() { calls.Add("end"); return Task.CompletedTask; }
    }
}
