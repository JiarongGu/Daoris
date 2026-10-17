using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work wire on one plugin's process (FABRIC-PERFORM1a; the fabric events design §C4, §D6): the reader takes exactly one notification,
/// <c>work/step</c>, and only from a process that declared it and that the lease's loop listens to; a frame over 64 KB is dropped unread;
/// the handshake keeps the work points and the capability; and <c>work/perform</c> is acknowledged by its invocation, four in flight at most.
/// </summary>
public sealed class WorkPeerTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly DateTimeOffset At = new(2026, 10, 17, 9, 30, 0, TimeSpan.Zero);

    private static readonly string[] Declared = [HookPoints.Watch, HookPoints.Step, HookPoints.Perform];

    /// <summary>A stand-in plugin: its stdout gives characters in blocks, as a process's does, so the peer reads it under the bound.</summary>
    private sealed class Stand
    {
        private readonly Channel<string> _toHost = Channel.CreateUnbounded<string>();
        private readonly Func<JsonElement, Stand, string?> _handle;

        public Stand(Func<JsonElement, Stand, string?> handle)
        {
            _handle = handle;
            Incoming = new Blocks(_toHost);
            Outgoing = new Lines(line =>
            {
                var frame = JsonDocument.Parse(line).RootElement.Clone();
                lock (Sent) Sent.Add(frame);
                if (_handle(frame, this) is { } reply) Push(reply);
            });
        }

        public List<JsonElement> Sent { get; } = [];

        public TextReader Incoming { get; }

        public TextWriter Outgoing { get; }

        public void Push(string line) => _toHost.Writer.TryWrite(line + "\n");

        public int Count(string method)
        {
            lock (Sent) return Sent.Count(frame => Method(frame) == method);
        }

        private sealed class Blocks(Channel<string> chunks) : TextReader
        {
            private string _chunk = "";
            private int _at;

            public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken ct = default)
            {
                while (_at == _chunk.Length)
                {
                    if (!await chunks.Reader.WaitToReadAsync(ct)) return 0;
                    if (chunks.Reader.TryRead(out var next)) (_chunk, _at) = (next, 0);
                }

                var count = Math.Min(buffer.Length, _chunk.Length - _at);
                _chunk.AsSpan(_at, count).CopyTo(buffer.Span);
                _at += count;
                return count;
            }
        }

        private sealed class Lines(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    private static string? Method(JsonElement frame) => frame.TryGetProperty("method", out var m) ? m.GetString() : null;

    private static string Ok(JsonElement request, string result) =>
        $$"""{"jsonrpc":"2.0","id":{{request.GetProperty("id").GetRawText()}},"result":{{result}}}""";

    private static string? Handshake(JsonElement frame, string points, string capabilities = "[]") =>
        Method(frame) == "initialize" ? Ok(frame, $$"""{"protocolVersion":1,"points":{{points}},"capabilities":{{capabilities}}}""") : null;

    private static async Task<HookPeer> Started(Stand stand, IReadOnlyList<string>? declared = null, Action<string>? onLine = null)
    {
        var peer = new HookPeer(stand.Incoming, stand.Outgoing, "acme.deploy", onLine, frameLimit: WorkBounds.FrameBytes);
        await peer.InitializeAsync("h", "d", declared ?? Declared, CancellationToken.None);
        return peer;
    }

    private static string Event(string id) =>
        WorkWire.Step(new WorkStepFrame("deployed", new WorkOutcome(WorkStates.Reached), At, id, "engine", "quest/a", Commit)).ToJsonString();

    private static string? IdOf(JsonElement? @params) => @params is { } read && read.TryGetProperty("id", out var id) ? id.GetString() : null;

    private static async Task Until(Func<bool> done)
    {
        for (var wait = 0; wait < 400 && !done(); wait++) await Task.Delay(25);
        Assert.True(done());
    }

    /// <summary>Answers every watch with nothing: a call whose answer comes after a pushed frame shows that frame was read.</summary>
    private static string? Answering(JsonElement frame, string points) =>
        Handshake(frame, points) ?? (Method(frame) == WorkWire.WatchMethod ? Ok(frame, "{}") : null);

    private static readonly WorkWatchFrame Nothing = new(1, Resync: true, []);

    [Fact]
    public async Task The_handshake_keeps_the_work_points_and_the_capabilities_this_build_knows()
    {
        var stand = new Stand((frame, _) =>
            Handshake(frame, """["work/watch","work/step","work/perform"]""", """["work.perform@1","acme.other@2",7]"""));

        var peer = await Started(stand);

        Assert.Equal(Declared, peer.Points);
        Assert.Equal([WorkWire.Capability], peer.Capabilities);
    }

    [Fact]
    public async Task A_step_event_reaches_the_listener_and_nothing_else_from_the_plugin_does()
    {
        var heard = new List<JsonElement?>();
        var stand = new Stand((frame, _) => Answering(frame, """["work/watch","work/step","work/perform"]"""));
        var peer = await Started(stand);

        // Before anyone listens, an event goes nowhere: a landing's process is never listened to.
        stand.Push(Event("e0"));
        await peer.WatchAsync(Nothing, CancellationToken.None);
        peer.Listen(each => { lock (heard) heard.Add(each); });

        stand.Push("""{"jsonrpc":"2.0","method":"work/other","params":{"id":"x1"}}""");
        stand.Push("""{"jsonrpc":"2.0","id":99,"method":"work/step","params":{"id":"x2"}}""");
        stand.Push(Event("e1"));
        stand.Push(Event("e2"));
        await Until(() => { lock (heard) return heard.Count >= 2; });
        await peer.WatchAsync(Nothing, CancellationToken.None);

        lock (heard) Assert.Equal(["e1", "e2"], heard.Select(IdOf));
    }

    [Fact]
    public async Task A_process_that_did_not_declare_the_event_is_not_heard()
    {
        var heard = new List<JsonElement?>();
        var stand = new Stand((frame, _) => Answering(frame, """["work/watch","work/perform"]"""));
        var peer = await Started(stand, [HookPoints.Watch, HookPoints.Perform]);
        peer.Listen(each => { lock (heard) heard.Add(each); });

        stand.Push(Event("e1"));
        await peer.WatchAsync(Nothing, CancellationToken.None);

        lock (heard) Assert.Empty(heard);
    }

    [Fact]
    public async Task A_frame_over_64_KB_is_dropped_unread_told_to_the_listener_and_the_wire_goes_on()
    {
        var heard = new List<JsonElement?>();
        var stand = new Stand((frame, _) => Answering(frame, """["work/watch","work/step","work/perform"]"""));
        var peer = await Started(stand);
        peer.Listen(each => { lock (heard) heard.Add(each); });

        stand.Push($$$"""{"jsonrpc":"2.0","method":"work/step","params":{"id":"{{{new string('x', WorkBounds.FrameBytes)}}}"}}""");
        stand.Push(Event("e1"));
        await Until(() => { lock (heard) return heard.Count >= 2; });

        lock (heard) Assert.Equal([null, "e1"], heard.Select(IdOf));
        lock (heard) Assert.Null(heard[0]);
    }

    [Fact]
    public async Task A_frame_over_64_KB_on_a_process_nobody_listens_to_is_said_under_the_plugin()
    {
        var lines = new List<string>();
        var stand = new Stand((frame, _) => Answering(frame, """["work/watch"]"""));
        var peer = await Started(stand, [HookPoints.Watch], line => { lock (lines) lines.Add(line); });

        stand.Push(new string('x', WorkBounds.FrameBytes + 1));
        await peer.WatchAsync(Nothing, CancellationToken.None);

        lock (lines) Assert.Contains(lines, line => line.Contains("64 KB", StringComparison.Ordinal) && line.Contains("dropped unread", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_watch_frame_is_sent_and_its_answer_waited_for()
    {
        var stand = new Stand((frame, _) => Answering(frame, """["work/watch"]"""));
        var peer = await Started(stand, [HookPoints.Watch]);
        var watch = new WorkWatchFrame(2, Resync: false,
            [new WatchedUnit("engine", "game", "C:/fam/engine", "quest/a", Commit, "main", null, ["deploy"])]);

        await peer.WatchAsync(watch, CancellationToken.None);

        JsonElement sent;
        lock (stand.Sent) sent = stand.Sent.Single(frame => Method(frame) == WorkWire.WatchMethod);
        Assert.Equal(WorkWire.Watch(watch).ToJsonString(), WorkWire.Watch(WorkWire.ReadWatch(sent.GetProperty("params"))!).ToJsonString());
    }

    [Fact]
    public async Task A_perform_is_acknowledged_by_its_invocation_and_anything_else_is_unreadable()
    {
        var stand = new Stand((frame, _) => Handshake(frame, """["work/perform"]""") ?? (Method(frame) == WorkWire.PerformMethod
            ? Ok(frame, WorkWire.PerformAnswer(WorkWire.ReadPerform(frame.GetProperty("params"))!.Invocation == "i1" ? "i1" : "another").ToJsonString())
            : null));
        var peer = await Started(stand, [HookPoints.Perform]);
        var call = new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i1", "k1", "r1", [], At);

        await peer.PerformAsync(call, CancellationToken.None);
        var error = await Assert.ThrowsAsync<DriverException>(() => peer.PerformAsync(call with { Invocation = "i2" }, CancellationToken.None));

        Assert.Equal(PluginEvents.Unreadable, PluginFailures.KindOf(error, ""));
        Assert.Contains("acme.deploy", error.Message, StringComparison.Ordinal);
        JsonElement first;
        lock (stand.Sent) first = stand.Sent.First(frame => Method(frame) == WorkWire.PerformMethod);
        Assert.Equal(WorkWire.Perform(call).ToJsonString(), WorkWire.Perform(WorkWire.ReadPerform(first.GetProperty("params"))!).ToJsonString());
    }

    [Fact]
    public async Task At_most_four_calls_are_in_flight_and_the_fifth_goes_when_one_is_answered()
    {
        var waiting = new List<JsonElement>();
        var stand = new Stand((frame, _) =>
        {
            if (Handshake(frame, """["work/perform"]""") is { } answer) return answer;
            lock (waiting) waiting.Add(frame);
            return null;
        });
        var peer = await Started(stand, [HookPoints.Perform]);

        var calls = Enumerable.Range(1, WorkBounds.CallsInFlight + 1)
            .Select(at => peer.PerformAsync(new WorkPerformCall("u1", "acme.deploy/deploy", 1, $"i{at}", $"k{at}", "r1", [], At), CancellationToken.None))
            .ToList();
        await Until(() => stand.Count(WorkWire.PerformMethod) == WorkBounds.CallsInFlight);
        await Task.Delay(200);
        Assert.Equal(WorkBounds.CallsInFlight, stand.Count(WorkWire.PerformMethod));

        void Answer(JsonElement frame) =>
            stand.Push(Ok(frame, WorkWire.PerformAnswer(WorkWire.ReadPerform(frame.GetProperty("params"))!.Invocation).ToJsonString()));

        lock (waiting) Answer(waiting[0]);
        await Until(() => stand.Count(WorkWire.PerformMethod) == WorkBounds.CallsInFlight + 1);
        lock (waiting) foreach (var frame in waiting.Skip(1)) Answer(frame);
        await Task.WhenAll(calls);
    }

    [Fact]
    public async Task A_reconcile_answer_is_read_by_shape()
    {
        var stand = new Stand((frame, _) => Handshake(frame, """["work/perform"]""") ?? (Method(frame) == WorkWire.ReconcileMethod
            ? Ok(frame, WorkWire.ReadReconcile(frame.GetProperty("params"))!.Invocation == "i1"
                ? WorkWire.ReconcileAnswer(new WorkReconcileAnswer(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached))).ToJsonString()
                : """{"state":"finished"}""")
            : null));
        var peer = await Started(stand, [HookPoints.Perform]);
        var call = new WorkReconcileCall("u1", "acme.deploy/deploy", "i1", "k1", "r1");

        var answer = await peer.ReconcileStepAsync(call, CancellationToken.None);
        var error = await Assert.ThrowsAsync<DriverException>(() => peer.ReconcileStepAsync(call with { Invocation = "i2" }, CancellationToken.None));

        Assert.Equal((ReconcileStates.Done, WorkStates.Reached), (answer.State, answer.Outcome?.State));
        Assert.Equal(PluginEvents.Unreadable, PluginFailures.KindOf(error, ""));
    }
}
