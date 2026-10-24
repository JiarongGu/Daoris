using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// CHATFLOW1e (the chat-flow design §6.1 (a) and (e), the Opus check's finding 7): the service's hand of a chat run's findings, beside the
/// connector's <c>opinion_answer</c> (<see cref="McpOpinionToolTests"/>). It admits a conversation of the working session's line in its tree,
/// live between turns or ended and the line's newest, records that record as the one handed to, and so lets only it answer; every other
/// active record is refused as before. Findings handed before a hand-over are handed again once, to the newest record.
/// </summary>
public sealed class McpOpinionChatHandTests : McpToolsFixture
{
    private const string OpinionBase = "1111111111111111111111111111111111111111";
    private const string OpinionTip = "2222222222222222222222222222222222222222";

    private string Tree => Path.Combine(_root, "trees", "s-chat");

    /// <summary>A conversation in a tree of its own that pressed Accept, and a first pass on its work, given with two findings.</summary>
    private async Task<(SessionStore Sessions, OpinionDesk Desk, Session Chat, string Opinion)> GivenAsync()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var desk = new OpinionDesk(await OpinionStore.OpenAsync(_connection), sessions);
        var chat = await sessions.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat, tree: Tree, ownEntry: true);
        await sessions.SetStateAsync(chat.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        var asked = await desk.AskAsync(
            new OpinionAsk(
                Opinions.Landing, Opinions.First, chat.Id, new OpinionCandidate("Owner", OpinionBase, OpinionTip, [OpinionTip]),
                new OpinionReviewer("codex-acp", Opinions.AnotherMaker) { Product = "Codex", Maker = "OpenAI" }, Opinions.CopyAlone, 20,
                Path.Combine(_root, "trees", "o-clone")),
            DateTimeOffset.UtcNow);
        Assert.Equal(OpinionRefusal.None, asked.Refusal);
        await sessions.SetStateAsync(asked.Session!.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        var given = await Connector(asked.Session.Id, desk).GiveOpinionAsync([Claim(), Claim("should")], "src/");
        Assert.Contains("Kept second opinion", given);
        return (sessions, desk, (await sessions.FindAsync(chat.Id))!, asked.Opinion!.Id);
    }

    /// <summary>The line's next record in the same tree, as a hand-over opens it; the one before ended.</summary>
    private async Task<Session> HandedOverAsync(SessionStore sessions, Session before, SessionState state = SessionState.Working)
    {
        await sessions.SetStateAsync(before.Id, SessionState.Completed, "handed over.", null, null, DateTimeOffset.UtcNow);
        var next = await sessions.CreateAsync(
            null, "Owner", "codex-acp", DateTimeOffset.UtcNow.AddSeconds(1), kind: SessionKind.Chat, tree: Tree, from: before.Id,
            fromWhy: "person", fromBy: "person", entryOrigin: before.Entry!.Split('/')[0]);
        await sessions.SetStateAsync(next.Id, state == SessionState.Completed ? SessionState.Working : state, null, null, null, DateTimeOffset.UtcNow);
        if (state == SessionState.Completed) await sessions.SetStateAsync(next.Id, SessionState.Completed, "ended.", null, null, DateTimeOffset.UtcNow);
        return (await sessions.FindAsync(next.Id))!;
    }

    private static OpinionTools Connector(string? session, OpinionDesk desk) => new(intake: new IntakeScope(null, session), opinions: desk);

    private static OpinionFindingInput Claim(string weight = "must") => new(
        weight, "src/Report.cs:42", "The workspace name is not quoted.", "A name with a space breaks the command.",
        "Ran it with `my space`: it split in two.", "likely");

    /// <summary>
    /// 🔴 (a): a chat run's live record between turns takes the findings as an ordinary word carrying <c>by</c>, the hand records it as the
    /// record handed to, and only it answers. Without <c>to</c> a live conversation is refused as before, and so is every other active record
    /// named: a driven session at work, a conversation waiting on the person, another tree's conversation.
    /// </summary>
    [Fact]
    public async Task The_hand_admits_a_chat_runs_working_record_and_refuses_every_other_active_one()
    {
        var (sessions, desk, chat, opinion) = await GivenAsync();
        var driven = await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Tree);
        await sessions.SetStateAsync(driven.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        var elsewhere = await sessions.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat,
            tree: Path.Combine(_root, "trees", "s-other"), ownEntry: true);
        await sessions.SetStateAsync(elsewhere.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);

        Assert.Equal(OpinionRefusal.CannotTake, (await desk.HandAsync(opinion, DateTimeOffset.UtcNow)).Refusal);
        Assert.Equal(OpinionRefusal.CannotTake, (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: driven.Id)).Refusal);
        Assert.Equal(OpinionRefusal.CannotTake, (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: elsewhere.Id)).Refusal);
        await sessions.SetStateAsync(chat.Id, SessionState.AwaitingPerson, "asked.", null, null, DateTimeOffset.UtcNow);
        Assert.Contains("only between its turns", (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: chat.Id)).Message);
        await sessions.SetStateAsync(chat.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        Assert.Null((await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion.Handed);

        var handed = await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: chat.Id);

        Assert.Equal(OpinionRefusal.None, handed.Refusal);
        Assert.Equal((opinion, SaidLevel.Ordinary), (handed.Word!.By, handed.Word.Level));
        Assert.Equal(chat.Id, (await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion.Handed!.Session);
        Assert.Contains(handed.Word.Id, (await sessions.FindAsync(chat.Id))!.Said.Select(word => word.Id));
        Assert.Contains("No second opinion was handed", await Connector(driven.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "x")]));
        Assert.Contains("Kept 1 answer", await Connector(chat.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "cannot reproduce")]));
        Assert.Contains("they go once", (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: chat.Id)).Message);
    }

    /// <summary>
    /// 🔴 (a), the Opus check's finding 7: an ended conversation is admitted only as the newest record of the working session's line in its
    /// tree, which after a hand-over is not the working session itself; the record the line went on from is refused, and so is a stand-down.
    /// </summary>
    [Fact]
    public async Task The_hand_admits_the_ended_newest_record_of_the_line_after_a_hand_over_and_refuses_one_that_is_not()
    {
        var (sessions, desk, chat, opinion) = await GivenAsync();
        var next = await HandedOverAsync(sessions, chat, SessionState.Completed);

        var older = await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: chat.Id);
        Assert.Equal(OpinionRefusal.CannotTake, older.Refusal);
        Assert.Contains($"its line went on in session `{next.Id}`", older.Message);

        var handed = await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: next.Id);

        Assert.Equal(OpinionRefusal.None, handed.Refusal);
        Assert.Equal(next.Id, (await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion.Handed!.Session);
        Assert.Contains("No second opinion was handed", await Connector(chat.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "x")]));
        Assert.Contains("Kept 1 answer", await Connector(next.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "x")]));
    }

    /// <summary>
    /// 🔴 (e): findings handed to a record a hand-over then ended, with no answer kept, are handed again once to the line's newest record,
    /// the hand moving to it, so only it answers; never while the first record still runs, never twice, and never once answered.
    /// </summary>
    [Fact]
    public async Task Findings_handed_before_a_hand_over_are_handed_again_once_to_the_newest_record()
    {
        var (sessions, desk, chat, opinion) = await GivenAsync();
        Assert.Equal(OpinionRefusal.None, (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: chat.Id)).Refusal);
        var next = await HandedOverAsync(sessions, chat);
        await sessions.SetStateAsync(chat.Id, SessionState.Working, null, null, null, DateTimeOffset.UtcNow);
        Assert.Contains("only once it ended", (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: next.Id)).Message);
        await sessions.SetStateAsync(chat.Id, SessionState.Completed, "handed over.", null, null, DateTimeOffset.UtcNow);

        var again = await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: next.Id);

        Assert.Equal(OpinionRefusal.None, again.Refusal);
        Assert.Contains($"again, since session `{chat.Id}` ended before it answered them", again.Message);
        var handed = (await desk.ReadAsync(opinion, DateTimeOffset.UtcNow))!.Opinion.Handed!;
        Assert.Equal((next.Id, (string?)chat.Id), (handed.Session, handed.From));
        Assert.Contains("No second opinion was handed", await Connector(chat.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "x")]));
        Assert.Contains("Kept 1 answer", await Connector(next.Id, desk).AnswerOpinionAsync([new OpinionAnswerInput(1, "unresolved", Why: "x")]));

        var third = await HandedOverAsync(sessions, next);
        Assert.Contains("handed again already", (await desk.HandAsync(opinion, DateTimeOffset.UtcNow, to: third.Id)).Message);
    }
}
