using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// GOAHEAD2, the owner's case on the install (2026-10-08): a driven session asked two go-aheads on its ask and parked, the
/// person answered both on the ask, and the session stayed waiting on them until the same words were said to it. The
/// answers to the go-aheads a parked session asked are its answer: the one that answers the last still waiting keeps the
/// park's blank answer (ANSWER1b), which the driver's next look goes on with, in the same session (D131).
/// </summary>
public sealed partial class GoAheadTests
{
    /// <summary>A session on <paramref name="quest"/> that asks for each act on production, then parks waiting on them.</summary>
    private async Task<Session> ParkedAsking(Quest quest, string tree, params (string Kind, string Act)[] acts)
    {
        var session = await Working(quest, tree);
        foreach (var (kind, act) in acts)
        {
            var asked = await _ledger.AskGoAheadAsync(session.Id, kind, "production", act, "The work needs it.", Now.AddMinutes(4));
            Assert.Equal(GoAheadRefusal.None, asked.Refusal);
        }

        await _ledger.AdvanceAsync(session.Id, "awaiting-person", "It waits on the go-aheads it asked.", null, null, Now.AddMinutes(5));
        return session;
    }

    /// <summary>The person answers go-ahead <paramref name="number"/> on the ask's door, which then asks the ledger what it did to the parks.</summary>
    private async Task<(GoAheadAnswerOutcome Answer, GoAheadParks Parks)> AnsweredAsync(
        string ask, int number, bool approved, string? words, DateTimeOffset at)
    {
        var answered = await _desk.AnswerGoAheadAsync(ask, number, approved, words, at);
        Assert.Equal(GoAheadAnswerRefusal.None, answered.Refusal);
        return (answered, await _ledger.GoOnWithGoAheadsAsync(answered, at));
    }

    private async Task<Session> Record(string id) => (await _sessions.FindAsync(id))!;

    /// <summary>
    /// 🔴 The row's own case: two go-aheads asked, then a park. Answering the first keeps it parked with nothing kept, and the
    /// door says which is still open; answering the second, the last it waited on, keeps the park's blank answer, so it goes
    /// on, and the door says so.
    /// </summary>
    [Fact]
    public async Task Answering_the_last_go_ahead_a_parked_session_waited_on_is_its_answer_and_the_first_of_two_is_not()
    {
        var (ask, quest) = await Asked();
        var parked = await ParkedAsking(quest, "first", ("write", "dashboard configuration"), ("release", "comparison report"));

        var (first, waiting) = await AnsweredAsync(ask.Id, 1, approved: true, "run the put", Now.AddMinutes(6));

        Assert.True(first.WasWaiting);
        Assert.Equal(1, first.GoAhead!.Number);
        var stillParked = await Record(parked.Id);
        Assert.Equal((SessionState.AwaitingPerson, 0), (stillParked.State, stillParked.Said.Count));
        Assert.Empty(waiting.GoesOn);
        Assert.Equal([2], waiting.Waits[parked.Id]);
        Assert.Equal($" Session `{parked.Id}` is still waiting on you for go-ahead 2.", waiting.Said);

        var (_, last) = await AnsweredAsync(ask.Id, 2, approved: false, "not on production yet", Now.AddMinutes(7));

        var answered = await Record(parked.Id);
        Assert.Equal(SessionState.AwaitingPerson, answered.State);
        var word = Assert.Single(answered.Said);
        Assert.Equal(("carry on.", Now.AddMinutes(7), false), (word.Text, word.At, word.Reopens));
        Assert.EndsWith("Answered: carry on.", answered.Note);
        Assert.Equal([parked.Id], last.GoesOn);
        Assert.Empty(last.Waits);
        Assert.Equal(
            $" Session `{parked.Id}` was waiting on you for its go-aheads: it goes on with your answers at the driver's next look.",
            last.Said);
    }

    /// <summary>
    /// A go-ahead another session asked wakes nobody else: the carry-on that asked go-ahead 2 stays parked while go-ahead 1,
    /// the earlier session's, is answered, and goes on once its own is.
    /// </summary>
    [Fact]
    public async Task A_go_ahead_another_session_asked_does_not_wake_this_one()
    {
        var (ask, quest) = await Asked();
        var first = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(first.Id, "write", "production", "dashboard configuration", "The target.", Now.AddMinutes(4));
        var carryOn = await CarriedOn(first, quest);
        await _ledger.AdvanceAsync(carryOn.Id, "starting", null, null, null, Now.AddMinutes(4));
        await _ledger.AdvanceAsync(carryOn.Id, "working", null, null, null, Now.AddMinutes(4));
        await _ledger.AskGoAheadAsync(carryOn.Id, "release", "production", "comparison report", "Ship it.", Now.AddMinutes(5));
        await _ledger.AdvanceAsync(carryOn.Id, "awaiting-person", "It waits on go-ahead 2.", null, null, Now.AddMinutes(6));

        var (_, other) = await AnsweredAsync(ask.Id, 1, approved: true, null, Now.AddMinutes(7));

        Assert.Empty((await Record(carryOn.Id)).Said);
        Assert.Equal((0, 0, ""), (other.GoesOn.Count, other.Waits.Count, other.Said));

        var (_, own) = await AnsweredAsync(ask.Id, 2, approved: true, null, Now.AddMinutes(8));

        Assert.Equal("carry on.", Assert.Single((await Record(carryOn.Id)).Said).Text);
        Assert.Equal([carryOn.Id], own.GoesOn);
    }

    /// <summary>
    /// A session that is not parked behaves as before: one still working, or one that ended, keeps nothing and is not named,
    /// though its go-ahead is answered on the ask as ever.
    /// </summary>
    [Fact]
    public async Task An_answer_to_a_go_ahead_whose_session_is_not_parked_keeps_nothing_on_it()
    {
        var (ask, quest) = await Asked();
        var working = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(working.Id, "write", "production", "dashboard configuration", "The target.", Now.AddMinutes(4));

        var (answered, parks) = await AnsweredAsync(ask.Id, 1, approved: true, null, Now.AddMinutes(5));

        Assert.Equal(GoAheadState.Approved, answered.GoAhead!.State);
        var record = await Record(working.Id);
        Assert.Equal((SessionState.Working, 0), (record.State, record.Said.Count));
        Assert.Equal((0, 0, ""), (parks.GoesOn.Count, parks.Waits.Count, parks.Said));

        await _ledger.AskGoAheadAsync(working.Id, "release", "production", "comparison report", "Ship it.", Now.AddMinutes(6));
        await _ledger.AdvanceAsync(working.Id, "completed", "Done without it.", null, null, Now.AddMinutes(7));
        var (_, ended) = await AnsweredAsync(ask.Id, 2, approved: true, null, Now.AddMinutes(8));

        Assert.Equal((SessionState.Completed, 0), ((await Record(working.Id)).State, (await Record(working.Id)).Said.Count));
        Assert.Empty(ended.GoesOn);
    }

    /// <summary>
    /// The park goes on once: a park the person already answered goes on with their words, and is handed these answers too,
    /// so nothing is kept beside them; and an answer changed after its first wakes nothing, since the session was waiting
    /// on none.
    /// </summary>
    [Fact]
    public async Task A_park_already_answered_and_a_changed_answer_keep_nothing_more()
    {
        var (ask, quest) = await Asked();
        var parked = await ParkedAsking(quest, "first", ("write", "dashboard configuration"));
        await _ledger.AnswerAsync(parked.Id, "Port 8080.", Now.AddMinutes(6));

        var (_, already) = await AnsweredAsync(ask.Id, 1, approved: true, null, Now.AddMinutes(7));

        Assert.Equal("Port 8080.", Assert.Single((await Record(parked.Id)).Said).Text);
        Assert.Equal([parked.Id], already.GoesOn);

        // The words were taken by the run that went on with them, which parked again on something else.
        var taken = await _ledger.TakeSaidAsync(parked.Id, [.. (await Record(parked.Id)).Said.Select(word => word.Id)], by: null);
        Assert.Empty(taken.Session!.Said);
        var (changed, again) = await AnsweredAsync(ask.Id, 1, approved: false, "not after all", Now.AddMinutes(9));

        Assert.False(changed.WasWaiting);
        Assert.Empty((await Record(parked.Id)).Said);
        Assert.Equal((0, 0, ""), (again.GoesOn.Count, again.Waits.Count, again.Said));
    }
}
