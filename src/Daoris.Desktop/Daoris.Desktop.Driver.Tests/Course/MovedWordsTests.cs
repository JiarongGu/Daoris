using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1d-ii part 4 (the foundation design §3.4 rule 3): the moves that open a part and hand it words, over the real client and the
/// in-process ledger at its <c>opens</c> mark, with a start that opens the record through the keyed door and spawns nothing, so no part here
/// holds the new conversation and its first hand fails. What the moved words come to then, and what a come-up does with a move's box once the
/// open moved them, are read here; the delivered paths, which need an agent, are the Process classes'.
/// </summary>
public sealed class MovedWordsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "moved-words", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public MovedWordsTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>The runner's port with only its start: the record opened as the chat runner's open point opens it, nothing spawned.</summary>
    private sealed class Opening(ServiceClient client) : IChatRunning
    {
        public List<MoveWords?> Asked { get; } = [];

        public async Task<ChatStart> StartAsync(
            string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, string? profile = null,
            bool? ownTree = null, CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null,
            IReadOnlyList<ActorEffective>? order = null, EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false,
            bool ownFirst = false, MoveWords? moves = null)
        {
            Asked.Add(moves);
            if (moves is null)
            {
                var (id, message) = await client.OpenChatAsync(repository, adapter, tree: tree, ct: ct, from: from);
                return new ChatStart(id, message);
            }

            var opened = await client.OpenChatMovingAsync(repository, adapter, null, profile, tree, null, from, moves, "default", ct);
            return new ChatStart(opened.SessionId, opened.Message) { Moves = opened.Moves, Moved = opened.Moved };
        }

        public Task<HelpPlaced> PlaceHelpAsync(
            string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default, string? carries = null,
            IReadOnlyList<ActorEffective>? order = null) => throw new NotSupportedException();

        public Task<ChatStart> RunHelpAsync(
            HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine, Func<string, string, Task>? onEnded = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ChatStart> RunAsync(
            string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
            ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null, ActorEffective? chosen = null,
            ChatPending? pending = null) => throw new NotSupportedException();

        public ChatPlace RepositoryPlace(
            DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree,
            IReadOnlyList<string>? continued = null, bool answers = false) => throw new NotSupportedException();

        public ChatPlace HelpPlace(string room, IReadOnlyList<HelpRead> reads, string? workspace = null) => throw new NotSupportedException();

        public Task<bool> AnswersFindingsAsync(string sessionId, string tree, DriverConfig config) => Task.FromResult(false);

        public bool Taking(string sessionId) => false;

        public ChatMessage WithProcess(string sessionId, ChatMessage message) => message;

        public Task<ConversationWalk> WalkAsync(
            string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
            (ActorEffective Entry, string? Account)? refused = null, bool counts = true, long except = 0) => throw new NotSupportedException();
    }

    /// <summary>Ask Daoris's port, never asked here: every case is a repository's conversation.</summary>
    private sealed class NoHelp : IChatHelping
    {
        public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp => null;

        public Task<string?> SetAsideHelpAsync(string? goingOn, string? workspace, CancellationToken ct) => throw new NotSupportedException();

        public Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed or spawned.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

    private (ChatContext Context, Opening Runner, ChatHandOver HandOver, ChatLedger Ledger, ServiceClient Client) World(int opens)
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        var client = ledger.Client();
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var context = new ChatContext(
            client, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)), Events);
        var runner = new Opening(client);
        return (context, runner, new ChatHandOver(context, runner), ledger, client);
    }

    /// <summary>A word kept on <paramref name="session"/> the first time a chat's open is asked, after the move read its words.</summary>
    private static void LateWord(ChatLedger ledger, string session, string id, string text)
    {
        var late = 0;
        ledger.BeforeRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat" && Interlocked.Exchange(ref late, 1) == 0)
            {
                ledger.For(session).Say(id, text);
            }

            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// 🔴 The briefed move and its not-offered path with a late word, their part's first hand failing (nothing here holds it): the open moved
    /// the person's words and the late one there, so they wait on the new part, never marked attempted, and the sentence says so; nothing is
    /// taken off either record, and the old record holds none of them.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_moved_words_wait_on_the_new_part_when_its_first_hand_fails(bool notOffered)
    {
        var (context, runner, handOver, ledger, client) = World(opens: 2);
        using var c = client;
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        LateWord(ledger, "c1", "w2", "And add a test.");
        var config = ActorChoices.With(DriverConfig.Empty, "chat", [new ActorChoice("talk")]);
        var move = new BriefedMove(context, runner, handOver, new NoHelp());

        StartedFrom answer;
        if (notOffered)
        {
            var json = await client.SessionRecordsJsonAsync();
            answer = await move.NotOfferedAsync(ServiceClient.ReadRecord(json, "c1")!, json, config, null, CancellationToken.None);
        }
        else
        {
            answer = await move.MovingAsync("c1", null, HandOverBy.Person, config, null, CancellationToken.None);
        }

        Assert.Equal(("c2", false), (answer.SessionId, answer.Sent));
        Assert.Contains("wait on `c2`", answer.Message);
        Assert.DoesNotContain("stay on `c1`", answer.Message);
        var moves = Assert.Single(runner.Asked)!;
        Assert.Equal(("c1", "w1", "w1"), (moves.From, string.Join(',', moves.Words), string.Join(',', moves.Read)));
        Assert.Empty(ledger.Said("c1"));
        Assert.Equal(["w1", "w2"], ledger.Said("c2"));
        Assert.All(ledger.Record("c2")["said"]!.AsArray(), word => Assert.Null(word!["attempt"]));
        Assert.Empty(ledger.TakenBy);
    }

    /// <summary>
    /// An ended conversation its limit held, on c1, with the refused turn's words in its record, and the new part c2 its limit's move opened,
    /// the open having moved <paramref name="moved"/> there; the move's box names them on c2.
    /// </summary>
    private void MovedBox(ChatLedger ledger, params (string Id, string Text)[] moved)
    {
        ledger.Chat("c1", "stopped", "talk", _root, profile: "account-1");
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable." });
        events.Append("c1", new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = "its account is cooling", Parts = [new NotePart { Code = NoteCodes.AccountCoolingWindow.Code }],
        });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        var words = ledger.Chat("c2", "stopped", "talk", _root, profile: "account-2", from: new ContinuedFrom("c1", HandOverWhy.Limit, HandOverBy.Daoris));
        foreach (var (id, text) in moved) words.Say(id, text);
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 2, events.After("c1", 0).Events.Count, [.. moved.Select(word => word.Id)], "c2", Moved: true));
    }

    private static IEnumerable<System.Text.Json.Nodes.JsonNode?> Said(ChatLedger ledger, string session) => ledger.Record(session)["said"]!.AsArray();

    /// <summary>
    /// 🔴 FOUNDATION1d-ii part 4's interim recovery of the move's box (the foundation design §3.4 rules 3 and 5, O6-F13, O7-F4): a shell cut off
    /// after the new part showed each moved word under its id and before it wrote the prompt. Its words wait there marked attempted, so the
    /// display is no evidence that they went, and nothing says they did not reach the agent: at the come-up, and at the one after, the box is
    /// neither delivered, which would take the words as taken, nor said again as a joined copy under the move's key; each attempted word is
    /// taken back off the new part, never sent again, with one <c>lost</c> line naming them there whatever its record shows, and the box goes.
    /// </summary>
    [Fact]
    public async Task A_moved_boxs_delivery_cut_off_after_its_words_were_shown_is_neither_delivered_nor_said_again()
    {
        var (context, runner, handOver, ledger, client) = World(opens: 2);
        using var c = client;
        MovedBox(ledger, ("w1", "Make it fast."), ("w2", "And add a test."));
        ledger.Edit("c2", record =>
        {
            foreach (var word in record["said"]!.AsArray()) word!["attempt"] = "2026-10-03T09:59:30Z";
        });
        foreach (var (id, text) in new[] { ("w1", "Make it fast."), ("w2", "And add a test.") })
        {
            Events.Append("c2", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = id, Text = text });
        }

        var limit = new LimitMove(context, runner, handOver, new NoHelp());
        await limit.TakeUpBoxesAsync(CancellationToken.None);
        await limit.TakeUpBoxesAsync(CancellationToken.None);

        Assert.Empty(ledger.TakenBy);
        Assert.Equal([("c2", "w1"), ("c2", "w2")], ledger.TakenBack);
        Assert.Empty(Said(ledger, "c2"));
        Assert.DoesNotContain(Said(ledger, "c1"), word => word!["key"]?.GetValue<string>() == "limit-move/c1");
        var lost = Assert.Single(Events.After("c2", 0).Events, e => e.Code == SessionEventCodes.Lost);
        Assert.Equal(["w1", "w2"], lost.Words!);
        Assert.Null(new LimitBoxes(_home).Read("c1"));
    }

    /// <summary>
    /// 🔴 A shell cut off after the move's open and before its hand: the moved words wait on the new part never attempted, so nothing of the
    /// move reached the agent. The come-up makes the delivery once from the box, the brief, the refused turn's words and the moved words read on
    /// the new part, as Daoris's word at its head under the move's key, the moved words leaving the new part into it, so its go-on hands each
    /// once; the box goes, and the next come-up says nothing again.
    /// </summary>
    [Fact]
    public async Task A_moved_boxs_delivery_never_attempted_is_made_once_from_the_new_part_at_the_come_up()
    {
        var (context, runner, handOver, ledger, client) = World(opens: 2);
        using var c = client;
        MovedBox(ledger, ("w1", "Make it fast."));
        var limit = new LimitMove(context, runner, handOver, new NoHelp());

        await limit.TakeUpBoxesAsync(CancellationToken.None);
        await limit.TakeUpBoxesAsync(CancellationToken.None);

        var copy = Assert.Single(Said(ledger, "c2"));
        Assert.Equal(("limit-move/c1", DaorisWords.From), (copy!["key"]!.GetValue<string>(), copy["from"]!.GetValue<string>()));
        var text = copy["text"]!.GetValue<string>();
        Assert.Contains("You continue session `c1`", text);
        Assert.True(text.IndexOf("Make the port configurable.", StringComparison.Ordinal) < text.IndexOf("Make it fast.", StringComparison.Ordinal), text);
        Assert.Equal([("c2", "w1", (string?)null)], ledger.TakenBy);
        Assert.Empty(ledger.TakenBack);
        Assert.Null(new LimitBoxes(_home).Read("c1"));
    }
}
