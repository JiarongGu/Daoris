using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// MIXAGENT1c (D163 point 3; the mixed agents design §2, §5): the connector's `handover_propose`. A working session's agent asks to
/// hand its session over to another agent with a note of its own, kept as its claim: one file per session under the home's
/// sessions folder, which the driver judges by the person's `handOver` rule. Not Ask Daoris's `hand_propose`, which hands a landed
/// branch to a plugin.
/// </summary>
public sealed class McpHandOverToolTests : McpToolsFixture
{
    private string RulesHome => Path.Combine(_root, "rules-home");

    /// <summary>The sessions folder of that home, asked of the table as the box asks it (HOMELAYOUT3), and a file in it.</summary>
    private string Sessions(params string[] file) => DaorisHome.Entry(RulesHome, HomeEntry.Sessions, file);

    private async Task<(HandOverTools Tools, SessionStore Sessions, SessionLedger Ledger)> ConnectorAsync(Func<SessionStore, Task<string?>> session)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var named = await session(sessions);
        var tools = new HandOverTools(intake: new IntakeScope(null, named), ledger: ledger, box: new HandOverAskBox(RulesHome));
        return (tools, sessions, ledger);
    }

    private Task<Session> WorkingAsync(SessionStore sessions) =>
        sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"));

    /// <summary>
    /// 🔴 The working session's `handover_propose` writes one ask under the home's sessions folder, named by the session, as the agent's,
    /// with its note as written; and answers that the person's rule settles it at the driver's next look, naming what each rule does.
    /// </summary>
    [Fact]
    public async Task A_working_session_asks_to_hand_over_and_the_ask_is_a_file_named_by_its_session()
    {
        string asker = null!;
        var (tools, _, _) = await ConnectorAsync(async sessions => asker = (await WorkingAsync(sessions)).Id);

        var said = await tools.ProposeAsync("My account is near its limit. The parser is done; the tests in ParserTests are left.");

        Assert.Contains("person's rule for hand-overs", said);
        Assert.Contains("`ask`", said);
        Assert.Contains("`on`", said);
        Assert.Contains("`off`", said);
        var file = Sessions(asker + HandOverAskBox.AskSuffix);
        using var kept = JsonDocument.Parse(File.ReadAllText(file));
        var root = kept.RootElement;
        Assert.Equal(
            (asker, "My account is near its limit. The parser is done; the tests in ParserTests are left.", "agent"),
            (root.GetProperty("session").GetString(), root.GetProperty("note").GetString(), root.GetProperty("askedBy").GetString()));
        Assert.Matches("^[0-9a-f]{8}$", root.GetProperty("id").GetString());
        Assert.Contains($"`#{root.GetProperty("id").GetString()}`", said);
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("at").GetString(), out _));
        Assert.DoesNotContain("\r", File.ReadAllText(file));
        Assert.False(File.Exists(Sessions(asker + HandOverAskBox.StateSuffix)));
        Assert.Empty(Directory.GetFiles(Sessions(), "*.hand-over.json"));
    }

    /// <summary>
    /// 🔴 What the tool can judge itself is refused in a sentence and writes nothing: no session in its scope, a session nobody holds,
    /// a helper, a reviewer, an intake, Ask Daoris's own conversation, a note left blank and a note too long.
    /// </summary>
    [Fact]
    public async Task handover_propose_refuses_what_it_can_judge_and_writes_nothing()
    {
        string helper = null!, reviewer = null!, intake = null!, help = null!, working = null!;
        var (tools, _, ledger) = await ConnectorAsync(async store =>
        {
            working = (await WorkingAsync(store)).Id;
            helper = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: working)).Id;
            reviewer = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, opinion: "op1")).Id;
            intake = (await store.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat, ask: "a1")).Id;
            help = (await store.CreateAsync(null, SessionLedger.HelpRepository, "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat)).Id;
            return null;
        });
        Task<string> AskAs(string? id, string note = "Where I am: the parser is done.") =>
            new HandOverTools(intake: new IntakeScope(null, id), ledger: ledger, box: new HandOverAskBox(RulesHome)).ProposeAsync(note);

        Assert.Contains("speaks for no session", await tools.ProposeAsync("x"));
        Assert.Contains("No session `nobody12`", await AskAs("nobody12"));
        Assert.Contains("a helper cannot hand over", await AskAs(helper));
        Assert.Contains("a reviewer cannot hand over", await AskAs(reviewer));
        Assert.Contains("an intake cannot hand over", await AskAs(intake));
        Assert.Contains("Ask Daoris's own conversation", await AskAs(help));
        Assert.Contains("needs your note", await AskAs(working, "   "));
        Assert.Contains($"at most {HandOverAskBox.MaxNote} characters", await AskAs(working, new string('x', HandOverAskBox.MaxNote + 1)));
        Assert.False(Directory.Exists(Sessions()) && Directory.GetFiles(Sessions(), "*" + HandOverAskBox.AskSuffix).Length > 0);
    }

    /// <summary>
    /// 🔴 One ask at a time: while the session's ask has no word from the driver, or waits on the person, or has their yes, a second is
    /// refused and the first is kept as written; once the driver refused it, a new ask takes its place under a new id.
    /// </summary>
    [Fact]
    public async Task A_second_ask_waits_for_the_first_to_be_settled()
    {
        string asker = null!;
        var (tools, _, _) = await ConnectorAsync(async sessions => asker = (await WorkingAsync(sessions)).Id);
        await tools.ProposeAsync("First note.");
        var first = HandOverAskBox.ReadAsk(RulesHome, asker)!;

        Assert.Contains("already waits", await tools.ProposeAsync("Second note."));
        void State(string state) => File.WriteAllText(
            Sessions(asker + HandOverAskBox.StateSuffix),
            $$"""{"id":"{{first.Id}}","state":"{{state}}","code":null,"agent":"codex-acp","at":"2026-10-20T10:00:00.0000000+00:00"}""");
        State("waiting");
        Assert.Contains("already waits", await tools.ProposeAsync("Second note."));
        State("approved");
        Assert.Contains("already waits", await tools.ProposeAsync("Second note."));
        State("taken");
        Assert.Contains("is being handed over", await tools.ProposeAsync("Second note."));
        Assert.Equal("First note.", HandOverAskBox.ReadAsk(RulesHome, asker)!.Note);

        State("refused");
        Assert.Equal(("refused", "codex-acp"), (HandOverAskBox.ReadState(RulesHome, asker)!.State, HandOverAskBox.ReadState(RulesHome, asker)!.Agent));
        var said = await tools.ProposeAsync("Second note.");

        var second = HandOverAskBox.ReadAsk(RulesHome, asker)!;
        Assert.Equal("Second note.", second.Note);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Contains($"`#{second.Id}`", said);
    }

    /// <summary>With no home the ask is refused and nothing is written, as a helper's ask is.</summary>
    [Fact]
    public async Task handover_propose_with_no_home_says_so()
    {
        string asker = null!;
        var (_, _, ledger) = await ConnectorAsync(async store => asker = (await WorkingAsync(store)).Id);
        var tools = new HandOverTools(intake: new IntakeScope(null, asker), ledger: ledger, box: new HandOverAskBox(null));

        Assert.Contains("Nothing was asked", await tools.ProposeAsync("Where I am."));
    }
}
