using System.Text.Json;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The edges of a conversation's and a driven session's keeps on their record: a hold refused while a send-back is reserved, and a reword
/// whose keep fails in any way the first keep's does is held here alone, never thrown at the person's press.
/// </summary>
public sealed class TurnKeepEdgeTests
{
    private static ChatTurns Waiting() => new(
        ready: () => new TaskCompletionSource<bool>().Task, take: (_, _) => Task.CompletedTask, interrupt: () => Task.CompletedTask,
        changed: _ => { });

    /// <summary>
    /// A hand-over's hold finds a send-back reserved as it finds any turn: refused. Held under it, the send-back's end dropped the findings
    /// the service already kept and answered them refused.
    /// </summary>
    [Fact]
    public async Task A_hold_is_refused_while_a_send_back_is_reserved_and_its_findings_are_kept()
    {
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true), take: (_, sent) => { sent(); return Task.CompletedTask; }, interrupt: () => Task.CompletedTask,
            changed: _ => { });
        var handing = new TaskCompletionSource<ChatMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var handed = false;
        var sending = turns.SendBackAsync(0, () => 0, _ =>
        {
            handed = true;
            return handing.Task;
        });
        await Poll.Until(() => handed, () => "the send-back never asked the service's hand");

        Assert.False(turns.Hold());

        handing.SetResult(new ChatMessage("the findings", []) { Id = "f1", Kept = true });
        Assert.Equal(SendBack.Reserved, await sending);
        turns.Gone();
    }

    /// <summary>
    /// A reword of a kept waiting word whose keep fails as a closed client or an unreadable answer does is reworded here alone, as one the
    /// record does not answer is, never thrown.
    /// </summary>
    [Theory]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(JsonException))]
    public async Task A_reword_whose_keep_fails_is_held_in_the_turns_alone(Type failure)
    {
        var turns = Waiting();
        var record = new StandInSaid { Answering = word => word.Text == "reworded" ? Task.FromException(Failed(failure)) : Task.CompletedTask };
        var kept = await turns.KeepAsync(record, new ChatMessage("first", []), default);

        var edit = await turns.EditQueuedAsync(kept!.Word!, "reworded");

        Assert.Equal(QueueEdit.Done, edit);
        Assert.Equal(["reworded"], turns.State.Queued.Select(message => message.Text));
        turns.Gone();
    }

    /// <summary>
    /// A keep the record stored but never answered is asked again under its key, so the stored copy is the word held and taken once:
    /// held with no id beside a stored copy, a restart or a go-on carried the word a second time.
    /// </summary>
    [Fact]
    public async Task A_keyless_keep_unanswered_after_the_record_stored_it_is_taken_once_under_the_stored_copy()
    {
        var taken = new List<ChatMessage>();
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true), take: (message, sent) => { taken.Add(message); sent(); return Task.CompletedTask; },
            interrupt: () => Task.CompletedTask, changed: _ => { });
        var calls = 0;
        var record = new StandInSaid { Answering = _ => ++calls == 1 ? throw new HttpRequestException("timed out") : Task.CompletedTask };

        var kept = await turns.KeepAsync(record, new ChatMessage("use the older bridge", []), default);

        Assert.Equal("w1", kept!.Word);
        await Poll.Until(() => taken.Count > 0, () => "the word was never taken");
        Assert.Equal("w1", taken[0].Id);
        Assert.True(taken[0].Kept);
        Assert.DoesNotContain("w2", record.Ids.Concat(record.TakenOff));
        turns.Gone();
    }

    /// <summary>A reword whose keep the record stored and never answered is asked again, so the new words wait once.</summary>
    [Fact]
    public async Task A_reword_unanswered_after_the_record_stored_it_waits_once_under_the_stored_copy()
    {
        var turns = Waiting();
        var calls = 0;
        var record = new StandInSaid { Answering = word => word.Text == "reworded" && ++calls == 1 ? throw new HttpRequestException("timed out") : Task.CompletedTask };
        var kept = await turns.KeepAsync(record, new ChatMessage("first", []), default);

        Assert.Equal(QueueEdit.Done, await turns.EditQueuedAsync(kept!.Word!, "reworded"));

        var waiting = Assert.Single(turns.State.Queued);
        Assert.True(waiting.Kept);
        Assert.Equal(["reworded"], record.Said.Where(word => word.Text == "reworded").Select(word => word.Text));
        Assert.Equal(waiting.Id, record.Said.Single(word => word.Text == "reworded").Id);
        turns.Gone();
    }

    /// <summary>The same reword on a driven session's inbox.</summary>
    [Theory]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(JsonException))]
    public async Task A_reword_whose_keep_fails_is_held_in_the_inbox_alone(Type failure)
    {
        var inbox = new DrivenInbox(_ => { });
        var record = new StandInSaid { Answering = word => word.Text == "reworded" ? Task.FromException(Failed(failure)) : Task.CompletedTask };
        var kept = await inbox.KeepAsync(record, new ChatMessage("first", []), default);

        var edit = await inbox.EditAsync(kept.Said!.Word!.Id!, "reworded");

        Assert.Equal(QueueEdit.Done, edit);
        Assert.Equal(["reworded"], inbox.State.Queued.Select(message => message.Text));
    }

    private static Exception Failed(Type failure) =>
        failure == typeof(ObjectDisposedException) ? new ObjectDisposedException("client") : new JsonException("unreadable");
}
