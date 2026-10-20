using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSCTX1c part B (D189 point 3; the session-context design §2, §8's D137): *Go on with Daoris's brief*, the person's press beside *Start
/// a conversation with these words* where a conversation, or a closed quest's session, waits for them. It opens a new part of the session
/// in its tree, on its agent and account, its record naming the one it continues, why <c>resume</c>, by the person. Refused before
/// anything opens, by a code. The real client over the in-process ledger and an adapter that spawns nothing, so the fast half: the runs
/// that take the brief and the words are <c>ChatGoesOnProcessTests</c>'.
/// </summary>
public sealed class GoOnBriefedTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-go-on-briefed-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public GoOnBriefedTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, which spawns nothing.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");
    }

    private ChatRunner Runner(ChatLedger ledger)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            events: new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// 🔴 A closed quest's session pressed goes on as a conversation in its own tree (the steward's choice: a closed quest opens no driven
    /// record), on its agent, its record naming the session it continues, why <c>resume</c>, by the person. Here the new part's spawn
    /// fails, so the words stay where they were.
    /// </summary>
    [Fact]
    public async Task A_closed_quests_session_goes_on_as_a_conversation_in_its_tree_named_from_it()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Quest("q1", "Done");
        ledger.Chat("s1", "completed", "talk", _root, kind: "driven", quest: "q1").Say("w1", "Also log the port.");
        using var runner = Runner(ledger);

        var answer = await runner.GoOnBriefedAsync("s1", DriverConfig.Empty);

        Assert.Equal((null, false, (string?)null), (answer.SessionId, answer.Sent, answer.Why));
        Assert.Equal(2, ledger.Count);
        var part = ledger.Record("c2");
        Assert.Equal(
            ("chat", "s1", HandOverWhy.Resume, HandOverBy.Person, _root),
            (part["kind"]?.GetValue<string>(), part["from"]?.GetValue<string>(), part["fromWhy"]?.GetValue<string>(),
             part["fromBy"]?.GetValue<string>(), part["tree"]?.GetValue<string>()));
        Assert.Equal(["w1"], ledger.Said("s1"));
    }

    /// <summary>
    /// What the press is refused by, before anything opens: a session that still runs, no words waiting, only another agent's answer
    /// waiting, a quest the driver carries on by itself, a tree that is gone, an agent this machine no longer has, a teammate's record and
    /// a conversation that went on in another.
    /// </summary>
    [Theory]
    [InlineData("running", StartFrom.Running)]
    [InlineData("no-words", StartFrom.NoWords)]
    [InlineData("helper", StartFrom.NoWords)]
    [InlineData("open", StartFrom.Carried)]
    [InlineData("tree", HandOver.TreeGone)]
    [InlineData("agent", BriefedMove.AgentGone)]
    [InlineData("teammate", WordsNever.Teammate)]
    [InlineData("continued", WordsNever.Continued)]
    public async Task What_cannot_go_on_with_the_brief_is_refused_by_its_code_and_opens_nothing(string what, string code)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var id = what == "teammate" ? "laptop/c1" : "c1";
        var record = what switch
        {
            "running" => ledger.Chat(id, "working", "talk", _root),
            "open" => ledger.Quest("q1", "Taken").Chat(id, "failed", "talk", _root, kind: "driven", quest: "q1"),
            "tree" => ledger.Chat(id, "completed", "talk", Path.Combine(_home, "gone")),
            "agent" => ledger.Chat(id, "completed", "gone-agent", _root),
            _ => ledger.Chat(id, "completed", "talk", _root),
        };
        if (what == "helper") record.Helped("w1", "Another agent says five.");
        else if (what != "no-words") record.Say("w1", "Also log the port.");
        if (what == "continued") ledger.Chat("c9", "working", "talk", _root, from: new ContinuedFrom(id, HandOverWhy.Press, HandOverBy.Person));
        var before = ledger.Count;
        using var runner = Runner(ledger);

        var answer = await runner.GoOnBriefedAsync(id, DriverConfig.Empty);

        Assert.Equal((null, false, code), (answer.SessionId, answer.Sent, answer.Why));
        Assert.Equal(before, ledger.Count);
    }
}
