using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Every word kept on a record carries a key, so a keep that went unanswered after the record stored the word finds that stored
/// copy again by the key and holds it once, instead of holding a second copy in this process beside the stored one.
/// </summary>
public sealed class DrivenInboxKeyTests
{
    private static DrivenInbox Inbox()
    {
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        return inbox;
    }

    [Fact]
    public async Task A_keyless_keep_that_timed_out_after_the_record_stored_it_is_held_once_under_the_stored_copy()
    {
        var calls = 0;
        var record = new StandInSaid { Answering = _ => ++calls == 1 ? throw new HttpRequestException("timed out") : Task.CompletedTask };
        var inbox = Inbox();

        var kept = await inbox.KeepOrHoldAsync(record, new ChatMessage("use the older bridge", []), default);

        Assert.Equal(InboxKeep.Held, kept.Outcome);
        Assert.Single(record.Said);
        var held = Assert.IsType<ChatMessage>(inbox.NextOrClose()?.Held);
        Assert.True(held.Kept);
        Assert.Equal(record.Said[0].Id, held.Id);
        Assert.Null(inbox.NextOrClose()?.Held);
    }

    [Fact]
    public async Task A_producers_key_is_the_one_the_record_keeps_and_a_record_that_never_answers_still_holds_the_word_here_under_it()
    {
        var record = new StandInSaid { Answering = _ => Task.CompletedTask };
        Assert.Equal(InboxKeep.Held, (await Inbox().KeepOrHoldAsync(record, new ChatMessage("one", []) { Key = "request/1" }, default)).Outcome);
        Assert.Equal("request/1", Assert.Single(record.Said).Key);

        var inbox = Inbox();
        var kept = await inbox.KeepOrHoldAsync(new Silent(), new ChatMessage("two", []), default);

        Assert.Equal(InboxKeep.Held, kept.Outcome);
        var held = Assert.IsType<ChatMessage>(inbox.NextOrClose()?.Held);
        Assert.False(held.Kept);
        Assert.False(string.IsNullOrEmpty(held.Key));
    }

    [Fact]
    public void A_steered_word_the_agent_hands_back_is_held_here_without_the_key_it_left_the_record_under()
    {
        var inbox = Inbox();

        Assert.True(inbox.HandBack(new ChatMessage("go first", []) { Id = "said-1", Key = "keep/abc", Kept = true }));

        var held = Assert.IsType<ChatMessage>(inbox.NextOrClose()?.Held);
        Assert.False(held.Kept);
        Assert.Null(held.Key);
    }

    private sealed class Silent : ISaidDoor
    {
        public Task<SayAnswer> KeepAsync(ChatMessage word, CancellationToken ct) => throw new HttpRequestException("no answer");

        public Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct) => Task.FromResult(false);

        public Task<IReadOnlyList<string>> AttemptAsync(IReadOnlyList<string> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
