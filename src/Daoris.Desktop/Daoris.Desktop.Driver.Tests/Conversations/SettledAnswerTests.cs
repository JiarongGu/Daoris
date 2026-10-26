using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row p (D212; the foundation design §3.4 rule 5, §19.2): a keyed say or hand the service answers <b>settled</b> is its own terminal kind,
/// <see cref="SayAnswer.Settled"/>, read as kept and carrying no word, and every reader of a keyed answer handles it before it reads a word. The two
/// dispatchers answer it as a retry of a word they hold already, holding, queueing, steering, attempting and taking nothing, Codex's reused id
/// among the cases: a successor holding a word whose id is the settled word's. The service's own answer is its tests'; here the door is a
/// stand-in that answers as the say door does to a client that asks.
/// </summary>
public sealed class SettledAnswerTests
{
    private const string Key = "press/3f1c";

    /// <summary>A word waiting on the successor, whose id the test sets to the settled word's own (Codex's reused id), unattempted.</summary>
    private static SaidWordView Waiting() => new("c0def00d", "the successor's own words", DateTimeOffset.UnixEpoch, [], false);

    private static ChatMessage Retry() => new("the first record's words, said again", []) { Key = Key, Level = MessageLevel.Urgent };

    // ---- the client's say ----

    /// <summary>A canned service, which hears what the client sent.</summary>
    private sealed class Canned(string answer) : HttpMessageHandler
    {
        public List<string> Heard { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Heard.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>
    /// The client asks for a settled answer on every keyed say but the supervisor's release, whose door reads no settlement, and on no keyless
    /// say; it reads a success naming the record as <see cref="SayAnswer.Settled"/>, kept, with no word, whatever else the answer carries.
    /// </summary>
    [Fact]
    public async Task The_client_asks_settled_on_a_keyed_say_and_reads_it_kept_with_no_word()
    {
        var handler = new Canned("""{"message":"Already settled under key `press/3f1c`.","settled":"c1"}""");
        var client = new ServiceClient("http://stand-in", null, new HttpClient(handler));

        var keyed = await client.SayAsync("c2", "words", key: Key);
        var keyless = await client.SayAsync("c2", "words");
        var released = await client.SayAsync("c2", "", key: "release/1", supervised: ("d1", "Which?", "A"));

        Assert.True(keyed.Kept);
        Assert.Equal("c1", keyed.Settled);
        Assert.Null(keyed.Word);
        Assert.True(JsonDocument.Parse(handler.Heard[0]).RootElement.GetProperty("settled").GetBoolean());
        Assert.False(JsonDocument.Parse(handler.Heard[1]).RootElement.TryGetProperty("settled", out _));
        Assert.False(JsonDocument.Parse(handler.Heard[2]).RootElement.TryGetProperty("settled", out _));
        Assert.True(keyless.Kept);
        Assert.True(released.Kept);
    }

    /// <summary>A kept word's answer, as every host before the row gave it, is read as it always was: its word, and no settlement.</summary>
    [Fact]
    public async Task A_kept_words_answer_carries_its_word_and_no_settlement()
    {
        var client = new ServiceClient("http://stand-in", null, new HttpClient(new Canned(
            """{"session":{"id":"c1","state":"completed"},"message":"Kept.","said":{"id":"w1","text":"words","at":"2026-10-03T09:00:00+00:00","files":[],"reopens":true}}""")));

        var said = await client.SayAsync("c1", "words", key: Key);

        Assert.True(said.Kept);
        Assert.Null(said.Settled);
        Assert.Equal("w1", said.Word!.Id);
    }

    /// <summary>The helper's and the partner's doors ask settled too, and read a success naming the record as their own kind, with no word.</summary>
    [Fact]
    public async Task The_helper_and_partner_doors_ask_settled_and_read_it_with_no_word()
    {
        var handler = new Canned("""{"message":"Settled.","settled":"s1"}""");
        var client = new ServiceClient("http://stand-in", null, new HttpClient(handler));

        var helper = await client.Helpers.HandAsync("h1", "the answer");
        var partner = await client.Partners.HandAsync("pt1", "the piece");

        Assert.Equal(HelperHanded.Settled, helper.Outcome);
        Assert.Null(helper.Word);
        Assert.True(partner.Handed);
        Assert.Equal("s1", partner.Settled);
        Assert.Null(partner.Word);
        Assert.All(handler.Heard, body => Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("settled").GetBoolean()));
    }

    // ---- the dispatchers ----

    /// <summary>
    /// Codex's reused id through a fresh <see cref="ChatTurns"/>: the retry of a word the line settled, said to a successor that holds another word under
    /// the same id, is answered settled and nothing is queued, steered, attempted or taken: the successor's word waits unattempted and the retry's
    /// text never reaches the door. The same retry from an older client keeps a second word under a new id as today, the successor's own untouched.
    /// </summary>
    [Fact]
    public async Task A_settled_retry_through_a_fresh_chat_turns_queues_and_sends_nothing_and_an_older_clients_keeps_a_second_word()
    {
        var took = new List<string>();
        ChatTurns Fresh() => new(
            ready: () => Task.FromResult(true), take: (message, sent) => { took.Add(message.Text); sent(); return Task.CompletedTask; },
            interrupt: () => Task.CompletedTask, changed: _ => { });
        var record = new StandInSaid();
        record.Said.Add(Waiting());
        record.Settled[Key] = "c1";
        var turns = Fresh();

        var kept = await turns.KeepAsync(record, Retry(), default);

        Assert.True(kept!.Settled);
        Assert.Null(kept.Word);
        Assert.Empty(turns.State.Queued);
        Assert.False(turns.Running);
        Assert.Empty(took);
        Assert.Empty(record.Attempted);
        Assert.Empty(record.TakenOff);
        var word = Assert.Single(record.Said);
        Assert.Equal(("c0def00d", null), (word.Id, word.Attempt));

        record.AsksSettled = false;
        var older = Fresh();
        var second = await older.KeepAsync(record, Retry(), default);

        Assert.False(second!.Settled);
        Assert.Equal("w1", second.Word);
        await Poll.Until(() => took.Count == 1, () => "the older client's second word never reached the door");
        Assert.Equal("the first record's words, said again", took[0]);
        // Its second word went and left the record under its own new id; the successor's, kept under the id the settled word had, is untouched.
        await Poll.Until(() => record.TakenOff.Count == 1, () => "the second word was never taken off");
        Assert.Equal(["w1"], record.TakenOff);
        var remaining = Assert.Single(record.Said);
        Assert.Equal(("c0def00d", null), (remaining.Id, remaining.Attempt));
    }

    /// <summary>
    /// Codex's reused id through a fresh <see cref="DrivenInbox"/> of the successor: answered settled, nothing held, sent, steered, attempted or
    /// taken, the successor's word waiting unattempted and no word handed to the run; an older client's retry is held under a new id.
    /// </summary>
    [Fact]
    public async Task A_settled_retry_through_a_fresh_driven_inbox_holds_and_sends_nothing_and_an_older_clients_is_held_under_a_new_id()
    {
        var sent = new List<string>();
        DrivenInbox Fresh()
        {
            var inbox = new DrivenInbox(_ => { });
            inbox.Attach(interrupt: null, deliver: message => { sent.Add(message.Text); return Task.FromResult("end_turn"); });
            inbox.Flow();
            return inbox;
        }

        var record = new StandInSaid();
        record.Said.Add(Waiting());
        record.Settled[Key] = "c1";
        var inbox = Fresh();

        var kept = await inbox.KeepAsync(record, Retry(), default);
        var heldOrHold = await Fresh().KeepOrHoldAsync(record, Retry(), default);

        Assert.Equal(InboxKeep.Held, kept.Outcome);
        Assert.True(kept.Settled);
        Assert.Null(kept.Said!.Word);
        Assert.True(heldOrHold.Settled);
        Assert.Empty(inbox.State.Queued);
        Assert.False(inbox.Holds);
        Assert.Empty(sent);
        Assert.Empty(record.Attempted);
        Assert.Empty(record.TakenOff);
        var word = Assert.Single(record.Said);
        Assert.Equal(("c0def00d", null), (word.Id, word.Attempt));

        record.AsksSettled = false;
        var older = await Fresh().KeepAsync(record, Retry(), default);

        Assert.False(older.Settled);
        Assert.Equal(InboxKeep.Held, older.Outcome);
        Assert.Equal("w1", older.Said!.Word!.Id);
        Assert.Equal(2, record.Said.Count);
        Assert.Equal(("c0def00d", null), (record.Said[0].Id, record.Said[0].Attempt));
    }

    /// <summary>
    /// The conversation runner's answer to a settled say is its own: <see cref="ChatSaid.Settled"/>, with no reach and no word, so the shell keeps
    /// nothing on the ask and shows nothing for it.
    /// </summary>
    [Fact]
    public void The_chat_said_and_turn_kept_carry_settled_apart_from_a_word()
    {
        var kept = new TurnKept(TurnReach.TurnEnd, null) { Settled = true };
        var said = new ChatSaid(null, null) { Settled = true };

        Assert.True(kept.Settled);
        Assert.Null(kept.Word);
        Assert.True(said.Settled);
        Assert.Null(said.Word);
        Assert.False(new TurnKept(TurnReach.TurnEnd, "w1").Settled);
        Assert.False(new ChatSaid(null, "w1").Settled);
    }
}
