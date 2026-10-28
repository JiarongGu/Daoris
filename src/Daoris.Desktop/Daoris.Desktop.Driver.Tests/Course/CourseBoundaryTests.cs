using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xiv, the Course cuts: what they keep. Each case reads the surface that is there before the cuts and after, so it runs
/// on the move commit as well as on the cuts: the channel claim and the acknowledgments' words, the lines a conversation's record keeps
/// and ends by, the lost and withdrawn words, Ask Daoris's room spelling and where a kept conversation goes on, its modes as a record
/// keeps them, the quest's take judged after a session ended and the transient end, the start's codes, and the stop's answer. The seams
/// the cuts add are <c>CoursePortsTests</c>'.
/// </summary>
public sealed class CourseBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "course-boundary", Guid.NewGuid().ToString("N")[..8]);

    public CourseBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void A_channel_claim_is_told_as_the_agents_not_the_persons_and_passed_on_when_the_person_sent_it()
    {
        var message = new ChannelItemView(ChannelItemView.Message, At)
        {
            Id = "m1", Person = false, Text = "The table is `orders`.\nCheck it.", Session = "s9", Repository = "console-api", Agent = "codex",
        };
        var claim = ChannelText.Claim(message);
        Assert.StartsWith(ChannelText.ClaimOpening, claim, StringComparison.Ordinal);
        Assert.Contains("`console-api`'s session `s9` on codex wrote to everyone (message `m1`)", claim, StringComparison.Ordinal);
        Assert.Contains("2026-10-29 09:00 UTC", claim, StringComparison.Ordinal);
        Assert.Contains("The table is `orders`.", claim, StringComparison.Ordinal);
        Assert.Contains("This is that agent's claim, not the person's words", claim, StringComparison.Ordinal);
        Assert.EndsWith(", `re` `m1`.", claim, StringComparison.Ordinal);
        Assert.DoesNotContain("The person passed this on to you.", claim, StringComparison.Ordinal);
        Assert.Contains("\nThe person passed this on to you.", ChannelText.Claim(message, passedOn: true), StringComparison.Ordinal);
    }

    [Fact]
    public void The_acknowledgments_and_the_agents_author_are_the_services_words()
    {
        Assert.Equal(["taking", "sent", "taken", "read", "partly", "released"],
            [ChannelAcks.Taking, ChannelAcks.Sent, ChannelAcks.Taken, ChannelAcks.Read, ChannelAcks.Partly, ChannelAcks.Released]);
        Assert.Equal("agent", ChannelFrom.Agent);
    }

    [Fact]
    public void A_turns_message_is_recorded_as_it_is_sent_and_a_conversation_that_ended_says_how()
    {
        var recorded = Assert.Single(ChatRunner.Recorded(new ChatMessage("hello", [])));
        Assert.Equal(SessionEventKind.User, recorded.Kind);
        Assert.Equal("person", recorded.Origin);
        Assert.Equal("hello", recorded.Text);

        var told = ChatRunner.Recorded(new ChatMessage("hello", []) { Preface = "Where the person is." }).ToList();
        Assert.Equal([SessionEventKind.Note, SessionEventKind.User], told.Select(each => each.Kind));
        Assert.Equal(ChatRunner.Told(new ChatMessage("hello", []) { Preface = "Where the person is." })!.Text, told[0].Text);

        Assert.Equal("the person ended the conversation.", ChatRunner.EndedByPersonNote);
        Assert.Equal("Daoris kept no id for its conversation, so words written to it cannot go on in it.", ChatRunner.NotKept);
        var stopped = ChatRunner.EndedNote(stopped: true, notKept: false);
        Assert.Equal(ChatRunner.EndedByPersonNote, stopped.Note);
        Assert.Equal(NoteCodes.ChatEndedByPerson.Code, stopped.Parts![0].Code);
        var ended = ChatRunner.EndedNote(stopped: false, notKept: true);
        Assert.Equal($"the conversation ended; its commits are its record. {ChatRunner.NotKept}", ended.Note);
        Assert.Equal([NoteCodes.ChatEnded.Code, NoteCodes.ChatNotKept.Code], ended.Parts!.Select(part => part.Code).OfType<string>());
    }

    [Fact]
    public void The_words_a_record_keeps_of_a_word_it_may_have_read_or_one_taken_back_are_coded_and_in_the_drivers_english()
    {
        Assert.Equal("— it may have read what you added; its answer was not kept.", ChatTurns.LostLine);
        var withdrawn = ChatTurns.Withdrawn("said-1");
        Assert.Equal(SessionEventKind.Note, withdrawn.Kind);
        Assert.Equal(["said-1"], withdrawn.Words);
        Assert.Equal(SessionEventCodes.Withdrawn, withdrawn.Code);
        var lost = ChatTurns.Lost("w1", stoppedUnder: true, "cancelled")!;
        Assert.Equal(ChatTurns.LostLine, lost.Text);
        Assert.Equal(SessionEventCodes.Lost, lost.Code);
        Assert.Null(ChatTurns.Lost("w1", stoppedUnder: true, "end_turn"));
    }

    [Fact]
    public void Ask_Daoriss_room_is_spelled_and_a_kept_conversation_goes_on_in_its_workspaces_as_before()
    {
        Assert.Equal("daoris:help", HelpRoom.Repository);
        Assert.Null(HelpRoom.GoesOnIn(_home, null, null));
        Assert.Null(HelpRoom.GoesOnIn(_home, "default", "D:/elsewhere"));
        Assert.Equal("atlas", HelpRoom.GoesOnIn(_home, "atlas", null));
        Assert.Equal("atlas", HelpRoom.GoesOnIn(_home, "atlas", Path.Combine(_home, "elsewhere")));
        // started in default's room before each workspace had its own: it goes on there, however its path is spelled
        Assert.Null(HelpRoom.GoesOnIn(_home, "atlas", HelpRoom.PathOf(_home)));
        Assert.Null(HelpRoom.GoesOnIn(_home, "atlas", HelpRoom.PathOf(_home).ToUpperInvariant() + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void A_record_keeps_its_access_mode_as_the_driver_reads_it()
    {
        Assert.Equal("restricted", HelpModes.Restricted);
        Assert.Equal("full", HelpModes.Recorded(" FULL "));
        Assert.Equal("restricted", HelpModes.Recorded(null));
        Assert.Equal("restricted", HelpModes.Recorded("somewhere-else"));
        Assert.Equal(["restricted", "full"], HelpModes.All);
        Assert.Equal(HelpModes.Restricted, new HelpConversation("c0nv0001", "completed").Mode);
    }

    [Fact]
    public void A_quests_take_is_judged_after_a_session_ended_and_a_transient_end_is_told_by_its_code()
    {
        var ended = new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero);
        var last = new PriorSession("s1", "D:/trees/s1", "failed", "timed out.") { Updated = ended };
        var quest = new QuestView("q1", "Asker", "Game", "Ask q1", "Why.", "Taken") { Updated = ended.AddHours(1) };

        Assert.True(Planner.TakenAway(quest, last, new QuestTake("lost")));
        Assert.False(Planner.TakenAway(quest with { Updated = ended.AddMinutes(-1) }, last, new QuestTake("held")));
        Assert.True(Planner.TakenAway(quest, last, new QuestTake("held")));
        Assert.False(Planner.TakenAway(quest, last, new QuestTake("held", Took: true)));
        Assert.Equal(ended.AddHours(1), Planner.TakenAt(quest, new QuestTake("held")));
        Assert.Equal(ended.AddHours(2), Planner.TakenAt(quest, new QuestTake("held", At: ended.AddHours(2))));
        Assert.False(Planner.BroughtForward(last, new QuestTake("held")));

        Assert.False(Planner.EndedTransient(last));
        var transient = last with { NoteParts = [NoteCodes.EndedTransient.Part("it kept failing.")] };
        Assert.True(Planner.EndedTransient(transient));
        Assert.False(Planner.EndedTransient(transient with { State = "completed" }));
    }

    [Fact]
    public void The_start_from_codes_and_the_stop_answer_are_what_the_page_and_the_loops_read()
    {
        Assert.Equal(["running", "no-words", "carried"], [StartFrom.Running, StartFrom.NoWords, StartFrom.Carried]);
        Assert.Contains("`c0nv0001`", StartFrom.Preface("c0nv0001"), StringComparison.Ordinal);
        var answer = new StopAnswer(true, false, true);
        Assert.True(answer.Stopped);
        Assert.False(answer.Orphan);
        Assert.True(answer.Elsewhere);
        var none = new ChatStart(null, "Daoris is closing.");
        Assert.Null(none.SessionId);
        var walk = new ConversationWalk(null, null, null, [], "no choice can run.");
        Assert.Equal("no choice can run.", walk.Refusal);
    }
}
