using System.Net;
using System.Text;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1d-ii part 1 (the foundation design §3.4 rule 2, §19.12's row for the head): a host with the <c>head</c> mark refuses a word said to a
/// record that is not its line's head as <c>continued</c>, its <c>head</c> member naming the head, at the say door's refusals' status. The client reads
/// the head beside the word and the sentence, and a fresh dispatcher answered so holds, queues, steers, attempts and writes nothing on its old door:
/// it answers the head to its caller, which says the word there. The forwards themselves are their callers' tests; here the door is a stand-in that
/// answers as part 2's say door will.
/// </summary>
public sealed class ContinuedAnswerTests
{
    private const string Key = "press/7c0d";

    private const string Sentence = "Session `c1` went on in `c2`, its line's head: say it to that one.";

    private static ChatMessage Word(MessageLevel level = MessageLevel.Urgent) => new("port 9090, not 8080", []) { Key = Key, Level = level };

    /// <summary>A canned service at a status, which hears what the client sent.</summary>
    private sealed class Canned(HttpStatusCode status, string answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") });
    }

    private static Task<SayAnswer> SayAsync(string answer) =>
        new ServiceClient("http://stand-in", null, new HttpClient(new Canned(HttpStatusCode.Conflict, answer))).SayAsync("c1", "words", key: Key);

    // ---- the client's say ----

    /// <summary>
    /// The client reads the refusal <c>continued</c> with its head: not kept, no word, the refusal's word and the service's sentence as they come, and
    /// the head the answer names. A refusal of another word names no head whatever its answer carries, and a <c>continued</c> naming none names none.
    /// </summary>
    [Fact]
    public async Task The_client_reads_continued_with_the_head_it_names_beside_its_word_and_sentence()
    {
        var continued = await SayAsync($$"""{"error":"{{Sentence}}","refusal":"continued","head":"c2"}""");
        var other = await SayAsync($$"""{"error":"It stood down.","refusal":"stood-down","head":"c2"}""");
        var headless = await SayAsync($$"""{"error":"{{Sentence}}","refusal":"continued"}""");
        var blank = await SayAsync($$"""{"error":"{{Sentence}}","refusal":"continued","head":""}""");

        Assert.Equal((false, "continued", Sentence, "c2"), (continued.Kept, continued.Refusal, continued.Message, continued.Head));
        Assert.Null(continued.Word);
        Assert.Null(continued.Settled);
        Assert.Equal(("stood-down", (string?)null), (other.Refusal, other.Head));
        Assert.Equal(("continued", (string?)null), (headless.Refusal, headless.Head));
        Assert.Null(blank.Head);
        Assert.Equal(WordsNever.Continued, SayAnswer.Continued);
    }

    // ---- the dispatchers ----

    private static ChatTurns Turns(List<string> took, Func<Task<bool>>? ready = null) => new(
        ready: ready ?? (() => Task.FromResult(true)), take: (message, sent) => { took.Add(message.Text); sent(); return Task.CompletedTask; },
        interrupt: () => Task.CompletedTask, changed: _ => { });

    /// <summary>
    /// The steward's case on <see cref="ChatTurns.KeepAsync"/>: a keep the record answers <c>continued</c> never falls back to the word as it was said
    /// (a refusal from a host before COWORK1c does): nothing is queued, steered, attempted or written, and the answer names the head, with no word,
    /// at each level, <c>now</c> among them.
    /// </summary>
    [Theory]
    [InlineData(MessageLevel.Urgent)]
    [InlineData(MessageLevel.Ordinary)]
    [InlineData(MessageLevel.Now)]
    public async Task A_fresh_chat_turns_answered_continued_queues_steers_attempts_and_writes_nothing(MessageLevel level)
    {
        var took = new List<string>();
        var turns = Turns(took);
        var record = new StandInSaid { Continues = "c2" };

        var kept = await turns.KeepAsync(record, Word(level), default);

        Assert.Equal(("c2", (string?)null, false), (kept!.Head, kept.Word, kept.Settled));
        Assert.Null(kept.Now);
        Assert.Empty(turns.State.Queued);
        Assert.False(turns.Running);
        Assert.Empty(took);
        Assert.Empty(record.Said);
        Assert.Empty(record.Attempted);
        Assert.Empty(record.TakenOff);
    }

    /// <summary>
    /// The steward's case on <see cref="DrivenInbox.KeepOrHoldAsync"/>: a keep the record answers <c>continued</c> is its own outcome, naming the head,
    /// never the in-process hold a refusal falls back to: nothing held, sent, steered, attempted or taken.
    /// </summary>
    [Theory]
    [InlineData(MessageLevel.Urgent)]
    [InlineData(MessageLevel.Now)]
    public async Task A_fresh_driven_inbox_answered_continued_holds_nothing_and_never_falls_back_to_holding_it_here(MessageLevel level)
    {
        var sent = new List<string>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message => { sent.Add(message.Text); return Task.FromResult("end_turn"); });
        inbox.Flow();
        var record = new StandInSaid { Continues = "c2" };

        var kept = await inbox.KeepAsync(record, Word(level), default);
        var keptOrHeld = await inbox.KeepOrHoldAsync(record, Word(level), default);

        Assert.Equal((InboxKeep.Continued, "c2"), (kept.Outcome, kept.Head));
        Assert.Equal((InboxKeep.Continued, "c2"), (keptOrHeld.Outcome, keptOrHeld.Head));
        Assert.Null(keptOrHeld.Now);
        Assert.False(keptOrHeld.Settled);
        Assert.Empty(inbox.State.Queued);
        Assert.False(inbox.Holds);
        Assert.Empty(sent);
        Assert.Empty(record.Said);
        Assert.Empty(record.Attempted);
    }

    /// <summary>
    /// A reword of a word a dispatcher holds, kept on a record that went on meanwhile: the record answers <c>continued</c>, so nothing is reworded,
    /// held or queued here, the word kept stays as it was and the edit answers that nothing changed.
    /// </summary>
    [Fact]
    public async Task A_reword_answered_continued_changes_nothing_in_either_dispatcher()
    {
        var record = new StandInSaid();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: _ => Task.FromResult("end_turn"));
        var held = await inbox.KeepAsync(record, Word(MessageLevel.Ordinary), default);
        var took = new List<string>();
        var turns = Turns(took, ready: () => new TaskCompletionSource<bool>().Task);
        var queued = await turns.KeepAsync(record, Word() with { Key = "press/2" }, default);

        record.Continues = "c2";
        var inboxEdit = await inbox.EditAsync(held.Said!.Word!.Id!, "port 9091");
        var turnsEdit = await turns.EditQueuedAsync(queued!.Word!, "port 9091");

        Assert.Equal((QueueEdit.Taken, QueueEdit.Taken), (inboxEdit, turnsEdit));
        Assert.Equal(["port 9090, not 8080"], inbox.State.Queued.Select(word => word.Text));
        Assert.Equal(["port 9090, not 8080"], turns.State.Queued.Select(word => word.Text));
        Assert.Equal(["w1", "w2"], record.Ids);
        Assert.Empty(record.TakenOff);
        Assert.Empty(took);
    }

    /// <summary>The runner's and the turns' answers carry the head apart from a word, so the shell says the word there and shows nothing here.</summary>
    [Fact]
    public void The_turn_kept_and_chat_said_carry_the_head_apart_from_a_word()
    {
        var kept = new TurnKept(TurnReach.TurnEnd, null) { Head = "c2" };
        var said = new ChatSaid(null, null) { Head = "c2" };

        Assert.Equal(("c2", (string?)null), (kept.Head, kept.Word));
        Assert.Equal(("c2", (string?)null), (said.Head, said.Word));
        Assert.Null(new TurnKept(TurnReach.TurnEnd, "w1").Head);
        Assert.Null(new ChatSaid(null, "w1").Head);
        Assert.Null(new InboxKept(InboxKeep.Held).Head);
    }
}
