using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 FOUNDATION1j part iii (the foundation design §2.4's first and fourth rows, §7 row 5; D212): a conversation's go-on and a briefed move go
/// on on the agent whose conversation it is, the record's, and on its role's choice, Chat's order, only where the record names none; never on
/// the machine's <c>adapter</c> key, so a person who set Chat's order sees their conversations go on where they chose. Chat off, or its entry
/// unread, holds the words with its sentence rather than faulting.
/// </summary>
/// <remarks>
/// The agent each case reaches is read from where the judgement before any spawn lands: a tree gone is its code only for an agent this machine
/// has (<c>talk</c> here), and an agent it lacks (<c>ghost</c>) is refused as one no longer here. A judgement test spawns nothing.
/// </remarks>
public sealed class ChatChoiceTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "chat-choice", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly string _gone;

    public ChatChoiceTests()
    {
        _root = Path.Combine(_home, "engine");
        _gone = DaorisHome.Entry(_home, HomeEntry.Trees, "chat-1");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>The agent this machine has: on the protocol door, with no toolchain to ask, so nothing is probed.</summary>
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
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>The machine's <c>adapter</c> key at <paramref name="adapter"/>, and Chat's order at <paramref name="chat"/>.</summary>
    private static DriverConfig Config(string adapter, params string[] chat) =>
        ActorChoices.With(DriverConfig.Empty with { Adapter = adapter }, "chat", [.. chat.Select(agent => new ActorChoice(agent, null))]);

    /// <summary>A chat that ended with its tree gone and the words of the person's waiting, its conversation kept for <c>talk</c>.</summary>
    private ChatLedger Ended(string? adapter)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "stopped", adapter!, _gone).Say("w1", "Also log the port.");
        new HarnessConversations(_home).Keep("c1", "talk", "conv-c1");
        return ledger;
    }

    [Fact]
    public async Task A_go_on_of_a_conversation_naming_no_agent_goes_on_with_Chat_s_choice_never_the_machine_s_adapter()
    {
        var ledger = Ended(null);
        using var runner = Runner(ledger);

        var answer = await runner.GoOnAsync("c1", Config("ghost", "talk"));

        // Judged on `talk`, which is here, as far as its tree: on the machine's `ghost` it would be refused as an agent no longer here.
        Assert.Null(answer.SessionId);
        Assert.Contains("tree", answer.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ContinueWhy.Tree, new GoOnMarks(_home).Read("c1")!.Why);
    }

    [Fact]
    public async Task A_go_on_of_a_conversation_naming_its_agent_goes_on_on_it_whatever_Chat_says()
    {
        var ledger = Ended("talk");
        using var runner = Runner(ledger);

        var answer = await runner.GoOnAsync("c1", Config("ghost", "ghost"));

        Assert.Null(answer.SessionId);
        Assert.Equal(ContinueWhy.Tree, new GoOnMarks(_home).Read("c1")!.Why);
    }

    [Fact]
    public async Task A_go_on_of_a_conversation_naming_no_agent_holds_with_Chat_s_sentence_where_Chat_does_not_read()
    {
        var ledger = Ended(null);
        using var runner = Runner(ledger);
        var unread = Config("talk", "talk") with
        {
            ActorErrors = new Dictionary<string, string>(StringComparer.Ordinal) { ["chat"] = "holds a choice that is no agent" },
        };

        var answer = await runner.GoOnAsync("c1", unread);

        Assert.Null(answer.SessionId);
        Assert.Contains("`actors.chat`", answer.Message);
        Assert.Equal("stopped", ledger.State("c1"));
        Assert.Null(new GoOnMarks(_home).Read("c1"));
    }

    /// <summary>
    /// A new chat, from the page's press and from a terminal's, starts on the first agent of Chat's order, which it hands the runner whole to
    /// walk; a named agent is the person's pick alone. Chat left as before answers the machine's key as the role's own answer; Chat's unread
    /// entry holds the start in its sentence.
    /// </summary>
    [Fact]
    public void A_new_chat_starts_on_Chat_s_first_agent_never_the_machine_s_adapter()
    {
        var (agent, order) = ActorChoices.NewChat(Config("ghost", "talk", "other"));
        Assert.Equal("talk", agent);
        Assert.Equal(["talk", "other"], order.Select(entry => entry.Agent));

        var (named, picked) = ActorChoices.NewChat(Config("ghost", "talk", "other"), "other");
        Assert.Equal("other", named);
        Assert.Equal(["other"], picked.Select(entry => entry.Agent));

        Assert.Equal("ghost", ActorChoices.NewChat(DriverConfig.Empty with { Adapter = "ghost" }).Agent);

        var unread = Config("talk", "talk") with { ActorErrors = new Dictionary<string, string>(StringComparer.Ordinal) { ["chat"] = "holds no agent" } };
        Assert.Contains("`actors.chat`", Assert.Throws<ActorUnreadableException>(() => ActorChoices.NewChat(unread)).Message);
    }

    /// <summary>A conversation to go on with Daoris's brief: ended, its tree standing, the person's words waiting.</summary>
    private ChatLedger Briefable(string? adapter)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "stopped", adapter!, _root).Say("w1", "Also log the port.");
        return ledger;
    }

    [Fact]
    public async Task A_briefed_move_of_a_conversation_naming_no_agent_is_refused_on_Chat_s_choice_never_the_machine_s_adapter()
    {
        using var runner = Runner(Briefable(null));

        var moved = await runner.GoOnBriefedAsync("c1", Config("talk", "ghost"));

        Assert.Equal(("ghost", GoOnBrief.AgentGone), (moved.Message.Split('`')[1], moved.Why));
    }

    [Fact]
    public async Task A_briefed_move_of_a_conversation_naming_its_agent_goes_on_on_it_whatever_Chat_says()
    {
        using var runner = Runner(Briefable("ghost"));

        var moved = await runner.GoOnBriefedAsync("c1", Config("talk", "talk"));

        Assert.Equal(("ghost", GoOnBrief.AgentGone), (moved.Message.Split('`')[1], moved.Why));
    }

    [Fact]
    public async Task A_briefed_move_of_a_conversation_naming_no_agent_is_refused_with_Chat_s_sentence_where_Chat_does_not_read()
    {
        using var runner = Runner(Briefable(null));
        var unread = Config("talk", "talk") with
        {
            ActorErrors = new Dictionary<string, string>(StringComparer.Ordinal) { ["chat"] = "holds a choice that is no agent" },
        };

        var moved = await runner.GoOnBriefedAsync("c1", unread);

        Assert.Equal(GoOnBrief.AgentGone, moved.Why);
        Assert.Contains("`actors.chat`", moved.Message);
    }
}
