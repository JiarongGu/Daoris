using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A failed check's words reach the work's session through the one door that reaches a session that is working, its inbox, as
/// <c>session say</c> does, and never through the record's say door, which refuses a running record; a session this loop's inbox does not reach
/// is told once it is not running, and the step says which of these happened.
/// </summary>
public sealed class CheckTellTests
{
    private const string Key = "changes/run-1/check/1";
    private const string Words = "Daoris: `check` failed, so your work goes back to you.";

    private readonly StandInSaid _record = new();
    private readonly List<string> _sayAtRecord = [];

    private static DrivenInbox Opened(bool sends)
    {
        var inbox = new DrivenInbox(_ => { });
        if (sends)
        {
            inbox.Attach(() => Task.CompletedTask, _ => Task.FromResult("end_turn"));
            inbox.Flow();
        }
        else
        {
            inbox.Attach(interrupt: null);
        }

        return inbox;
    }

    private WorkSay Door(DrivenInbox? inbox) => new(
        _ => inbox,
        _ => _record,
        (session, _, _, _) =>
        {
            _sayAtRecord.Add(session);
            return Task.FromResult(new SayAnswer(false, "Session is working: words to a running session reach it through the driver that runs it, not its record.")
            {
                Refusal = "running",
            });
        });

    [Fact]
    public async Task A_session_that_is_working_is_told_at_its_next_step_through_its_inbox_as_Daoris_s_word_under_the_key()
    {
        var sent = new List<ChatMessage>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message =>
        {
            sent.Add(message);
            return Task.FromResult("end_turn");
        });
        inbox.Flow();

        var answer = await Door(inbox).SayAsync("s1", Words, Key, CancellationToken.None);

        Assert.True(answer.Kept);
        Assert.Equal(SayAnswer.NextStep, answer.Reaches);
        Assert.Empty(_sayAtRecord);
        var word = Assert.Single(_record.Said);
        Assert.Equal((Key, DaorisWords.From, Words), (word.Key, word.From, word.Text));
        var delivered = Assert.Single(sent);
        Assert.Equal(Words, delivered.Text);
        Assert.NotNull(delivered.Daoris);
    }

    [Fact]
    public async Task A_session_whose_door_takes_words_only_at_its_turn_s_end_holds_them_for_it_and_the_answer_says_so()
    {
        var inbox = Opened(sends: false);

        var answer = await Door(inbox).SayAsync("s1", Words, Key, CancellationToken.None);

        Assert.True(answer.Kept);
        Assert.Equal(SayAnswer.TurnEnd, answer.Reaches);
        Assert.Equal(Words, inbox.NextOrClose()?.Held?.Text);
        Assert.Empty(_sayAtRecord);
    }

    [Fact]
    public async Task Words_said_again_under_the_key_are_held_once()
    {
        var inbox = Opened(sends: false);
        var door = Door(inbox);

        await door.SayAsync("s1", Words, Key, CancellationToken.None);
        await door.SayAsync("s1", Words, Key, CancellationToken.None);

        Assert.Single(_record.Said);
        Assert.Equal(Words, inbox.NextOrClose()?.Held?.Text);
        Assert.Null(inbox.NextOrClose()?.Held);
    }

    [Fact]
    public async Task A_session_no_inbox_here_reaches_is_said_to_at_the_record_which_refuses_a_running_one_and_the_answer_says_it_is_running()
    {
        var answer = await Door(null).SayAsync("s1", Words, Key, CancellationToken.None);

        Assert.False(answer.Kept);
        Assert.Equal("running", answer.Refusal);
        Assert.Equal(["s1"], _sayAtRecord);
    }

    [Fact]
    public async Task An_inbox_that_closed_as_the_session_wound_up_falls_to_the_record_so_the_words_are_never_dropped_on_the_floor()
    {
        var inbox = Opened(sends: false);
        inbox.Close();

        var answer = await Door(inbox).SayAsync("s1", Words, Key, CancellationToken.None);

        Assert.Equal(["s1"], _sayAtRecord);
        Assert.False(answer.Kept);
        Assert.Empty(_record.Said);
    }

    [Fact]
    public async Task A_session_whose_record_went_on_in_its_line_s_head_is_told_there()
    {
        var head = new StandInSaid();
        var behind = new StandInSaid { Continues = "s2" };
        var inboxes = new Dictionary<string, DrivenInbox> { ["s1"] = Opened(sends: false), ["s2"] = Opened(sends: false) };
        var door = new WorkSay(session => inboxes.GetValueOrDefault(session), session => session == "s1" ? behind : head,
            (_, _, _, _) => Task.FromResult(new SayAnswer(false, "no")));

        var answer = await door.SayAsync("s1", Words, Key, CancellationToken.None);

        Assert.True(answer.Kept);
        Assert.Single(head.Said);
        Assert.Empty(behind.Said);
    }

    [Fact]
    public void A_failed_check_step_says_when_its_words_reach_the_session_that_was_working()
    {
        Assert.Equal("its words went to session `s1`, which was working, and reach it at its next step.",
            new WorkflowRunTold("s1", null) { Reaches = SayAnswer.NextStep }.Said);
        Assert.Equal("its words went to session `s1`, which was working, and are held for its turn's end.",
            new WorkflowRunTold("s1", null) { Reaches = SayAnswer.TurnEnd }.Said);
        Assert.Equal("its rounds back to the work are spent, so it waits on you, and its words went to session `s1`, which was working, and reach it at its next step.",
            new WorkflowRunTold("s1", null) { Spent = true, Reaches = SayAnswer.NextStep }.Said);
        Assert.Equal("its words went to session `s1` as its next turn.", new WorkflowRunTold("s1", null).Said);
    }

    [Fact]
    public void Where_the_words_reach_a_working_session_is_kept_on_the_run_s_record_and_read_back_by_the_step()
    {
        var at = DateTimeOffset.UnixEpoch;
        var record = new NodeRecord("run:r") { Told = [new NodeTold("check", 1, "s1", null, at) { Reaches = SayAnswer.TurnEnd }] };

        Assert.Equal(SayAnswer.TurnEnd, WorkflowRunTold.Of(record, "check", 1).Reaches);
        Assert.Equal(record.Told, NodeRecordFiles.Parse(NodeRecordFiles.ToJson(record))!.Told);
    }
}
