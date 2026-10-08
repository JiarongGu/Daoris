using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// XAGENT1c (D155 points 5 and 7; the second agent design §5.4, §6.1, §6.4): the connector's two tools for a second opinion.
/// `opinion_give` answers only the opinion's own reviewer, and `opinion_answer` only the working session its findings were
/// handed to; every call is an agent's, and neither settles anything for the person. On a shared host both say its sentence.
/// </summary>
public sealed partial class McpToolsTests
{
    private const string OpinionBase = "1111111111111111111111111111111111111111";
    private const string OpinionTip = "2222222222222222222222222222222222222222";

    /// <summary>A working session that ended, and a first pass on its work whose reviewer runs.</summary>
    private async Task<(OpinionDesk Desk, Session Working, Session Reviewer, string Opinion)> OpinionAskedAsync(bool local = true)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var desk = new OpinionDesk(await OpinionStore.OpenAsync(_connection), sessions);
        var working = await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"));
        await sessions.SetStateAsync(working.Id, SessionState.Completed, "landed.", null, null, DateTimeOffset.UtcNow);
        var asked = await desk.AskAsync(
            new OpinionAsk(
                Opinions.Landing, Opinions.First, working.Id, new OpinionCandidate("Owner", OpinionBase, OpinionTip, [OpinionTip]),
                new OpinionReviewer("codex-acp", Opinions.AnotherMaker) { Product = "Codex", Maker = "OpenAI" }, Opinions.CopyAlone, 20,
                Path.Combine(_root, "trees", "clone")),
            DateTimeOffset.UtcNow);
        Assert.Equal(OpinionRefusal.None, asked.Refusal);
        await sessions.SetStateAsync(asked.Session!.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        return (local ? desk : new OpinionDesk(await OpinionStore.OpenAsync(_connection), sessions, local: false), working, asked.Session, asked.Opinion!.Id);
    }

    /// <summary>The connector a session the driver started is handed: it names its session, and nothing else.</summary>
    private KnowledgeTools Connector(string? session, OpinionDesk desk) => new(
        _service, _quests, new QuestExchange(_service, _quests, files: _files), new AmbientWorkspace(Path.Combine(_root, "family", "Owner")),
        intake: new IntakeScope(null, session), opinions: desk);

    private static OpinionFindingInput Claim(string weight = "must") => new(
        weight, "src/Report.cs:42", "The workspace name is not quoted.", "A name with a space breaks the command.",
        "Ran it with `my space`: it split in two.", "likely", "Quote it.");

    /// <summary>
    /// 🔴 Only the opinion's own reviewer says it (design §5.4, §6.1): the working session's connector, one that speaks for no
    /// session, and one with no desk are each refused and keep nothing; the reviewer's keeps its findings, once.
    /// </summary>
    [Fact]
    public async Task Only_an_opinions_own_reviewer_gives_it()
    {
        var (desk, working, reviewer, opinion) = await OpinionAskedAsync();

        Assert.Contains("reads no work for a second opinion", await Connector(working.Id, desk).GiveOpinionAsync([Claim()], "src/"));
        Assert.Contains("speaks for no session", await Connector(null, desk).GiveOpinionAsync([Claim()], "src/"));
        Assert.Contains("speaks for no session", await _tools.GiveOpinionAsync([Claim()], "src/"));
        Assert.Null((await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion.Given);

        var given = await Connector(reviewer.Id, desk).GiveOpinionAsync(
            [Claim(), Claim("should") with { Where = "general", Claim = "The table is kept twice." }], "src/Report.cs and both commits",
            "I did not run the gate.");

        Assert.Contains($"Kept second opinion `{opinion}`: 2 findings (1 must, 1 should)", given);
        var kept = (await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!;
        Assert.Equal(Opinions.Given, kept.State);
        Assert.Equal(("Quote it.", "I did not run the gate."), (kept.Opinion.Given!.Findings[0].Proposal, kept.Opinion.Given.Limits));
        Assert.Contains("said at", await Connector(reviewer.Id, desk).GiveOpinionAsync([], "again"));
    }

    /// <summary>
    /// 🔴 Only the session the findings were handed to answers them (design §6.4): the reviewer, another session, and a
    /// connector for none are refused, and the working session's answers are kept, each as it said it.
    /// </summary>
    [Fact]
    public async Task Only_the_session_handed_an_opinion_answers_it()
    {
        var (desk, working, reviewer, opinion) = await OpinionAskedAsync();
        await Connector(reviewer.Id, desk).GiveOpinionAsync([Claim(), Claim("note") with { Where = "general" }], "src/");
        Assert.Contains("No second opinion was handed", await Connector(working.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "x")]));
        Assert.Equal(OpinionRefusal.None, (await desk.HandAsync(opinion, DateTimeOffset.UtcNow)).Refusal);
        var other = await (await SessionStore.OpenAsync(_connection)).CreateAsync("q2", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "other"));

        Assert.Contains("No second opinion was handed", await Connector(reviewer.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "fixed", "a1b2c3d")]));
        Assert.Contains("No second opinion was handed", await Connector(other.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "fixed", "a1b2c3d")]));
        Assert.Contains("speaks for no session", await Connector(null, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "fixed", "a1b2c3d")]));
        Assert.Contains("speaks for no session", await _tools.AnswerOpinionAsync([new OpinionAnswerInput(1, "fixed", "a1b2c3d")]));
        Assert.Contains("gave no finding 3", await Connector(working.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(3, "fixed", "a1b2c3d")]));
        Assert.Empty((await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion.Answers);

        var answered = await Connector(working.Id, desk).AnswerOpinionAsync(
            [new OpinionAnswerInput(1, "fixed", "a1b2c3d"), new OpinionAnswerInput(2, "rejected", Evidence: "The twin test holds it.")], opinion);

        Assert.Contains($"Kept 2 answers on second opinion `{opinion}`. Every finding is answered.", answered);
        Assert.Contains("it must be one this turn made", answered);
        var kept = (await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion;
        Assert.Equal((Opinions.Fixed, "a1b2c3d"), (Opinions.AnswerTo(kept, 1)!.Said, Opinions.AnswerTo(kept, 1)!.Commit));
        Assert.Equal("The twin test holds it.", Opinions.AnswerTo(kept, 2)!.Evidence);
    }

    /// <summary>🔴 A shared host keeps no opinion (design §6.2): both tools say its sentence, and nothing is kept.</summary>
    [Fact]
    public async Task Both_opinion_tools_are_refused_on_a_shared_host()
    {
        var (shared, working, reviewer, opinion) = await OpinionAskedAsync(local: false);

        Assert.Equal(Opinions.SharedSentence, await Connector(reviewer.Id, shared).GiveOpinionAsync([Claim()], "src/"));
        Assert.Equal(Opinions.SharedSentence, await Connector(working.Id, shared).AnswerOpinionAsync([new OpinionAnswerInput(1, "fixed", "a1b2c3d")]));
        Assert.Null((await (await OpinionStore.OpenAsync(_connection)).FindAsync(opinion))!.Given);
    }
}
