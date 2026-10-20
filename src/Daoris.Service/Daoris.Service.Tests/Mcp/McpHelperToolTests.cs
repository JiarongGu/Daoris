using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// MIXAGENT2a-i (D163 point 2; the mixed agents design §3): the connector's two tools for a helper. `helper_ask` writes an ask
/// file the driver takes, for the working session its connector names; `helper_answer` writes the answer of the helper the
/// driver started for an ask. Each is an agent's, and a helper cannot ask for a helper (depth one).
/// </summary>
public sealed class McpHelperToolTests : McpToolsFixture
{
    private string HelpersHome => Path.Combine(_root, "rules-home");

    /// <summary>The helpers' box in that home, asked of the table as the box asks it (HOMELAYOUT3), and a file in it.</summary>
    private string Helpers(params string[] file) => DaorisHome.Entry(HelpersHome, HomeEntry.Helpers, file);

    /// <summary>The connector a session is handed, with a ledger to read its record and a box under the rules home.</summary>
    private async Task<(HelperTools Tools, SessionStore Sessions)> HelperConnectorAsync(Func<SessionStore, Task<string?>> session)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var named = await session(sessions);
        var tools = new HelperTools(intake: new IntakeScope(null, named), ledger: ledger, helpers: new HelperBox(HelpersHome));
        return (tools, sessions);
    }

    private Task<Session> WorkingAsync(SessionStore sessions) =>
        sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"));

    /// <summary>
    /// 🔴 The working session's `helper_ask` writes one immutable ask file under the rules home, as the agent's, and answers
    /// the asker a sentence that the driver takes it at its next look; the question and the agent are kept as said.
    /// </summary>
    [Fact]
    public async Task A_working_session_asks_a_helper_and_the_ask_is_a_file_the_driver_takes()
    {
        string asker = null!;
        var (tools, _) = await HelperConnectorAsync(async sessions => asker = (await WorkingAsync(sessions)).Id);

        var said = await tools.AskHelperAsync("Does the retry loop in Report.cs stop at five?", "codex-acp");

        Assert.Contains("driver takes it at its next look", said);
        var file = Assert.Single(Directory.GetFiles(Helpers(), "*.ask.json"));
        using var kept = JsonDocument.Parse(File.ReadAllText(file));
        var root = kept.RootElement;
        Assert.EndsWith(".ask.json", file);
        Assert.Equal(Path.GetFileName(file), root.GetProperty("id").GetString() + ".ask.json");
        Assert.Equal(
            (asker, "Does the retry loop in Report.cs stop at five?", "codex-acp", "agent"),
            (root.GetProperty("asker").GetString(), root.GetProperty("question").GetString(), root.GetProperty("agent").GetString(),
                root.GetProperty("askedBy").GetString()));
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("at").GetString(), out _));
        var left = string.Join(' ', Directory.GetFiles(Helpers()));
        Assert.DoesNotContain(".tmp", left);
        Assert.DoesNotContain(".writing", left);
        Assert.Empty(Directory.GetFiles(Helpers(), "*.state.json"));
    }

    /// <summary>
    /// 🔴 What the tool can judge itself is refused in a sentence and writes nothing: no session in its scope, a session nobody
    /// holds, a helper (its record has `serves`), a reviewer, an intake, and a question left blank.
    /// </summary>
    [Fact]
    public async Task helper_ask_refuses_what_it_can_judge_and_writes_nothing()
    {
        string helper = null!, reviewer = null!, intake = null!, working = null!;
        var (tools, sessions) = await HelperConnectorAsync(async store =>
        {
            working = (await WorkingAsync(store)).Id;
            helper = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: working)).Id;
            reviewer = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, opinion: "op1")).Id;
            intake = (await store.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat, ask: "a1")).Id;
            return null;
        });
        async Task<string> AskAs(string? id, string question = "What does this do?") =>
            await new HelperTools(
                intake: new IntakeScope(null, id), ledger: new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection)),
                helpers: new HelperBox(HelpersHome)).AskHelperAsync(question);

        Assert.Contains("speaks for no session", await tools.AskHelperAsync("What does this do?"));
        Assert.Contains("No session `nobody12`", await AskAs("nobody12"));
        Assert.Contains("is itself a helper", await AskAs(helper));
        Assert.Contains("reads another session's work for second opinion `op1`", await AskAs(reviewer));
        Assert.Contains("an intake cannot ask for a helper", await AskAs(intake));
        Assert.Contains("needs the question", await AskAs(working, "   "));
        Assert.False(Directory.Exists(Helpers()));
    }

    /// <summary>
    /// 🔴 `helper_answer` writes the answer for the ask the driver started this session for (its state names it), once: a session
    /// that is no helper, one no state names, and a second answer are refused; the answer file holds the words and when.
    /// </summary>
    [Fact]
    public async Task A_helper_answers_the_ask_its_state_names_once()
    {
        string asker = null!, helper = null!, other = null!;
        var (_, sessions) = await HelperConnectorAsync(async store =>
        {
            asker = (await WorkingAsync(store)).Id;
            helper = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: asker)).Id;
            other = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: asker)).Id;
            return null;
        });
        HelperTools As(string? id) => new(
            intake: new IntakeScope(null, id), ledger: new SessionLedger(_quests, sessions, _service, AskStore.OpenAsync(_connection).GetAwaiter().GetResult()),
            helpers: new HelperBox(HelpersHome));
        await As(asker).AskHelperAsync("Where is the cap read?");
        var askId = Path.GetFileName(Directory.GetFiles(Helpers(), "*.ask.json").Single())[..^".ask.json".Length];

        Assert.Contains("speaks for no session", await As(null).AnswerHelperAsync("x"));
        Assert.Contains("is no helper", await As(asker).AnswerHelperAsync("x"));
        Assert.Contains("No question waits", await As(helper).AnswerHelperAsync("x"));

        // The driver alone writes the state: this is its shape, read back by the box.
        var state = Helpers(askId + ".state.json");
        File.WriteAllText(state, $$"""{"id":"{{askId}}","state":"started","code":null,"helper":"{{helper}}","at":"2026-10-12T10:00:00.0000000+00:00"}""");
        Assert.Equal(("started", helper), (HelperBox.ReadState(HelpersHome, askId)!.State, HelperBox.ReadState(HelpersHome, askId)!.Helper));
        Assert.Contains("No question waits", await As(other).AnswerHelperAsync("x"));
        Assert.Contains("needs your answer", await As(helper).AnswerHelperAsync("  "));

        var said = await As(helper).AnswerHelperAsync("Line 41 of Cap.cs, once, at start.");

        Assert.Contains("answer is kept", said);
        using var kept = JsonDocument.Parse(File.ReadAllText(Helpers(askId + ".answer.json")));
        Assert.Equal("Line 41 of Cap.cs, once, at start.", kept.RootElement.GetProperty("answer").GetString());
        Assert.True(DateTimeOffset.TryParse(kept.RootElement.GetProperty("at").GetString(), out _));
        Assert.Contains("already answered", await As(helper).AnswerHelperAsync("again"));
        Assert.Contains("Line 41", File.ReadAllText(Helpers(askId + ".answer.json")));
    }

    /// <summary>With no home the ask is refused and nothing is written, as a rule proposal is.</summary>
    [Fact]
    public async Task helper_ask_with_no_home_says_so()
    {
        string asker = null!;
        var (_, sessions) = await HelperConnectorAsync(async store => asker = (await WorkingAsync(store)).Id);
        var tools = new HelperTools(
            intake: new IntakeScope(null, asker), ledger: new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection)),
            helpers: new HelperBox(null));

        Assert.Contains("Nothing was asked", await tools.AskHelperAsync("What does this do?"));
    }
}
