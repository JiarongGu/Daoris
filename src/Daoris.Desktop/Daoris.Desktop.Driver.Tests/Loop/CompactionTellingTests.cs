using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The wiring from a harness's compaction to the word its session or conversation is told: a protocol session's event reaching a quest
/// session's <see cref="CompactWord"/>, a native door's boundary reading doing the same, which door reads which signal, and a
/// conversation's open requests read from the service's records. Stand-in streams and a stand-in service: no process, no model.
/// </summary>
public sealed class CompactionTellingTests
{
    private const string Finished =
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"c1","status":"completed","_meta":{"claudeCode":{"toolName":"compact"}}}}}""";

    private static readonly DateTimeOffset Noon = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class Doors(string records) : IServiceDoors
    {
        public string BaseUrl => "http://stand";
        public void Dispose() { }
        public Task<string> GetAsync(string path, CancellationToken ct) => Task.FromResult(records);
        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct) =>
            Task.FromResult(new ServiceReply(200, records));
    }

    private static string Chat(string id, string updated) =>
        $$"""{"id":"{{id}}","quest":null,"state":"working","repository":"engine","door":"stub","workspace":"default","kind":"chat","created":"{{updated}}","updated":"{{updated}}"}""";

    private static ServiceClient Service() => new(new SessionChangesDoors(new Doors($"[{Chat("c1", "2026-11-13T10:00:00Z")},{Chat("c2", "2026-11-13T11:00:00Z")}]")));

    private static Task<IReadOnlyList<BriefWait>> Waiting(string owner) =>
        Task.FromResult<IReadOnlyList<BriefWait>>([new BriefWait("q7", owner, "engine", "kept", "pending", "add the cap")]);

    [Fact]
    public async Task A_quest_sessions_protocol_event_reaches_its_word_through_the_session()
    {
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            self.Push(Finished);
            return AcpTransientTests.Answer(frame, "end_turn");
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { CompactionNamed = true };
        var said = new StandInSaid();
        using var word = CompactWord.Tell(
            session, "s1", new DrivenInbox(_ => { }), said, _ => Task.FromResult("the requirements and the brief"), failed: null);

        await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);
        await Poll.Until(() => said.Said.Count == 1);

        Assert.Equal("the requirements and the brief", said.Said[0].Text);
        Assert.StartsWith("compact/s1/", said.Said[0].Key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_native_boundary_reading_reaches_the_word_through_the_sink_the_run_hands()
    {
        var said = new StandInSaid();
        using var word = new CompactWord("s1", new DrivenInbox(_ => { }), said, _ => Task.FromResult("brief"), failed: null);
        var sink = ReadingSinks.On<WireReading.Compacted>(_ => word.Compacted());

        sink.Read(NativeCompaction.Read(NativeFrame.Of(
            """{"type":"system","subtype":"compact_boundary","compact_metadata":{"trigger":"auto","pre_tokens":150000}}"""))!);
        await Poll.Until(() => said.Said.Count == 1);

        Assert.Equal("brief", said.Said[0].Text);
    }

    [Fact]
    public void Claude_codes_protocol_door_names_its_compactions_and_codex_keeps_the_counter()
    {
        Assert.True(((ISpawn)new ClaudeAcpSpawn()).NamesCompaction);
        Assert.False(((ISpawn)new CodexAcpSpawn()).NamesCompaction);
    }

    [Fact]
    public async Task A_workspaces_new_conversation_reads_the_last_ones_open_requests_from_the_service()
    {
        using var service = Service();
        var read = new List<string>();

        var told = await ChatOpenTold.ToldAsync(
            service, "home", "default", "new", CancellationToken.None,
            (records, owner) =>
            {
                read.Add(owner);
                return Waiting(owner);
            });

        Assert.Equal(["c2"], read);
        Assert.Contains("`c2`, left a request open", told, StringComparison.Ordinal);
        Assert.Contains("`q7` to engine", told, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_compacted_conversation_is_told_its_own_open_requests_and_one_with_none_is_told_nothing()
    {
        using var service = Service();
        var taken = new List<ChatMessage>();
        var told = new ChatCompactTold(
            "c2", service, "home", word => { taken.Add(word); return true; }, failed: _ => { },
            waits: records => Waiting("c2"));
        var none = new ChatCompactTold("c2", service, "home", word => { taken.Add(word); return true; }, _ => { }, records => Task.FromResult<IReadOnlyList<BriefWait>>([]));

        Assert.False(await none.TellAsync());
        Assert.Empty(taken);
        Assert.True(await told.TellAsync());

        var word = Assert.Single(taken);
        Assert.Contains("Your context was compacted", word.Text, StringComparison.Ordinal);
        Assert.Contains("`q7` to engine", word.Text, StringComparison.Ordinal);
        Assert.StartsWith("compact/c2/", word.Daoris!.Key, StringComparison.Ordinal);
    }
}
