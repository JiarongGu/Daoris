using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// PARKVIEW1 (the park's ask design §2): the connector's `question_ask`. A driven session's agent records what only the person can
/// decide before it ends its turn on it: the question, the answers it offers with what it does on each, the one it recommends and
/// by when, as its own words. One file per session under the home's sessions folder, which the driver binds to the park and the
/// page draws at the conversation's end, each answer a press.
/// </summary>
public sealed class McpQuestionToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "question-home");

    /// <summary>The sessions folder of that home, asked of the table as the box asks it (HOMELAYOUT3), and a file in it.</summary>
    private string Sessions(params string[] file) => DaorisHome.Entry(Home, HomeEntry.Sessions, file);

    private static QuestionChoiceInput Choice(string? label, string? does) => new(label, does);

    private static readonly QuestionChoiceInput[] TwoReadings =
    [
        Choice("1,200", "Write 1,200, the planning sheet's value, which the ticket links."),
        Choice("1,350", "Write 1,350, the last stand-up note's value."),
    ];

    private async Task<(Func<string?, QuestionTools> As, string Driven, SessionStore Store)> ConnectorAsync()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var driven = (await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"))).Id;
        return (id => new QuestionTools(intake: new IntakeScope(null, id), ledger: ledger, box: new QuestionBox(Home)), driven, sessions);
    }

    /// <summary>
    /// 🔴 A driven session's question is one file under the home's sessions folder named by the session, its words as written, each
    /// answer with what the agent does on it, the one it recommends and by when; and the agent is told the person answers it with a
    /// press at the conversation's end, the answer coming back as its next prompt, and to end its turn with its quest taken.
    /// </summary>
    [Fact]
    public async Task A_driven_session_asks_and_its_question_is_a_file_named_by_its_session()
    {
        var (connector, driven, _) = await ConnectorAsync();

        var said = await connector(driven).AskAsync(
            "Which quarterly target should the dashboard tile show?", TwoReadings, recommend: 1, by: "before Friday's release",
            why: "  The ticket links the planning sheet.  ");

        var file = Sessions(driven + QuestionBox.Suffix);
        var text = File.ReadAllText(file);
        Assert.DoesNotContain("\r", text);
        using var kept = JsonDocument.Parse(text);
        var root = kept.RootElement;
        Assert.Equal(
            (driven, "Which quarterly target should the dashboard tile show?", 1, "before Friday's release"),
            (root.GetProperty("session").GetString(), root.GetProperty("question").GetString(), root.GetProperty("recommend").GetInt32(),
                root.GetProperty("by").GetString()));
        Assert.Equal(
            [("1,200", "Write 1,200, the planning sheet's value, which the ticket links."), ("1,350", "Write 1,350, the last stand-up note's value.")],
            root.GetProperty("choices").EnumerateArray().Select(choice => (choice.GetProperty("label").GetString(), choice.GetProperty("does").GetString())));
        Assert.Equal("The ticket links the planning sheet.", root.GetProperty("why").GetString());
        Assert.Equal("The ticket links the planning sheet.", QuestionBox.Read(Home, driven)!.Why);
        Assert.Matches("^[0-9a-f]{8}$", root.GetProperty("id").GetString());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("at").GetString(), out _));
        Assert.Contains("press", said);
        Assert.Contains("end your turn", said);
        Assert.Contains("label", said);
        Assert.Contains("go_ahead_ask", said);
    }

    /// <summary>A question with no answers offered is a free answer's, and keeps no recommendation nor deadline it was not given.</summary>
    [Fact]
    public async Task A_question_with_no_answers_offered_keeps_none()
    {
        var (connector, driven, _) = await ConnectorAsync();

        await connector(driven).AskAsync("  Which environment should I test the migration against?  ", choices: null);

        using var kept = JsonDocument.Parse(File.ReadAllText(Sessions(driven + QuestionBox.Suffix)));
        var root = kept.RootElement;
        Assert.Equal("Which environment should I test the migration against?", root.GetProperty("question").GetString());
        Assert.Empty(root.GetProperty("choices").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("recommend").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("by").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("why").ValueKind);
    }

    /// <summary>The latest is what it asks now: a second question replaces the first, under a new id.</summary>
    [Fact]
    public async Task A_later_question_replaces_the_first()
    {
        var (connector, driven, _) = await ConnectorAsync();
        await connector(driven).AskAsync("First?", TwoReadings);
        var first = QuestionBox.Read(Home, driven)!;

        await connector(driven).AskAsync("Second?", choices: null);

        var second = QuestionBox.Read(Home, driven)!;
        Assert.Equal("Second?", second.Question);
        Assert.Empty(second.Choices);
        Assert.NotEqual(first.Id, second.Id);
    }

    /// <summary>
    /// 🔴 What the tool can judge itself is refused in a sentence and writes nothing: no session in its scope, a session nobody holds, a
    /// conversation (its person reads it as it comes), a helper, a reviewer, an intake, Ask Daoris's own conversation, a driven record
    /// on no quest, and every bound: the question blank or too long, too many answers, an answer with no label or no words for what it
    /// does or either too long, a recommendation that names no answer, a deadline too long, and a reason with no recommendation or too
    /// long (QUESTIONUI1a).
    /// </summary>
    [Fact]
    public async Task question_ask_refuses_what_it_can_judge_and_writes_nothing()
    {
        var (connector, driven, store) = await ConnectorAsync();
        var chat = (await store.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat)).Id;
        var helper = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: driven)).Id;
        var reviewer = (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, opinion: "op1")).Id;
        var intake = (await store.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat, ask: "a1")).Id;
        var help = (await store.CreateAsync(null, SessionLedger.HelpRepository, "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat)).Id;
        var questless = (await store.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow)).Id;
        var ended = (await store.CreateAsync("q2", "Owner", "claude-code", DateTimeOffset.UtcNow)).Id;
        await store.SetStateAsync(ended, SessionState.Completed, "done.", null, null, DateTimeOffset.UtcNow);
        Task<string> Ask(
            string? id, string question = "Which one?", QuestionChoiceInput[]? choices = null, int? recommend = null, string? by = null,
            string? why = null) =>
            connector(id).AskAsync(question, choices, recommend, by, why);

        Assert.Contains("speaks for no session", await Ask(null));
        Assert.Contains("No session `nobody12`", await Ask("nobody12"));
        Assert.Contains("conversation", await Ask(chat));
        Assert.Contains("a helper", await Ask(helper));
        Assert.Contains("a reviewer", await Ask(reviewer));
        Assert.Contains("an intake", await Ask(intake));
        Assert.Contains("Ask Daoris's own conversation", await Ask(help));
        Assert.Contains("holds no quest", await Ask(questless));
        Assert.Contains("no turn to end", await Ask(ended));
        Assert.Contains("needs the question", await Ask(driven, "   "));
        Assert.Contains($"at most {QuestionBox.MaxQuestion} characters", await Ask(driven, new string('x', QuestionBox.MaxQuestion + 1)));
        Assert.Contains($"at most {QuestionBox.MaxChoices} answers",
            await Ask(driven, choices: [.. Enumerable.Range(1, QuestionBox.MaxChoices + 1).Select(n => Choice($"{n}", "Do it."))]));
        Assert.Contains("answer 2 needs its label", await Ask(driven, choices: [Choice("Yes", "Do it."), Choice(" ", "Do it.")]));
        Assert.Contains("answer 1 needs what you will do", await Ask(driven, choices: [Choice("Yes", null)]));
        Assert.Contains($"at most {QuestionBox.MaxLabel} characters",
            await Ask(driven, choices: [Choice(new string('x', QuestionBox.MaxLabel + 1), "Do it.")]));
        Assert.Contains($"at most {QuestionBox.MaxDoes} characters",
            await Ask(driven, choices: [Choice("Yes", new string('x', QuestionBox.MaxDoes + 1))]));
        Assert.Contains("names no answer", await Ask(driven, choices: TwoReadings, recommend: 3));
        Assert.Contains("names no answer", await Ask(driven, recommend: 1));
        Assert.Contains($"at most {QuestionBox.MaxBy} characters", await Ask(driven, by: new string('x', QuestionBox.MaxBy + 1)));
        Assert.Contains("A reason goes with a recommendation", await Ask(driven, choices: TwoReadings, why: "Because."));
        Assert.Contains($"at most {QuestionBox.MaxWhy} characters",
            await Ask(driven, choices: TwoReadings, recommend: 1, why: new string('x', QuestionBox.MaxWhy + 1)));
        Assert.Contains("the same answer twice", await Ask(driven, choices: [Choice("Yes", "Do it."), Choice("yes", "Do it again.")]));
        Assert.False(Directory.Exists(Sessions()) && Directory.GetFiles(Sessions(), "*" + QuestionBox.Suffix).Length > 0);
    }

    /// <summary>With no home the question is refused and nothing is written, as a hand-over's ask is.</summary>
    [Fact]
    public async Task question_ask_with_no_home_says_so()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var driven = (await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow)).Id;
        var tools = new QuestionTools(intake: new IntakeScope(null, driven), ledger: ledger, box: new QuestionBox(null));

        Assert.Contains("Nothing was asked", await tools.AskAsync("Which one?", choices: null));
    }
}
