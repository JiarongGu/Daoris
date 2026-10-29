using System.Net;
using System.Text;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row p (D212's note of the row; the foundation design §3.4 rule 5): an attempt is acknowledged before the wire. The mark that failed
/// open is mandatory, and its answer names each word it marked: a word it does not name is not attempted and does not go. A commit that failed
/// sends nothing and says so; an answer naming fewer words than asked sends only those; a commit whose answer is lost sends nothing, the word
/// waiting marked on its record, which the next come-up settles as uncertain and says (never sent again by itself). The dispatcher is the real
/// <see cref="ChatTurns"/> over a stand-in record (<see cref="StandInSaid"/>) that marks as the service does.
/// </summary>
public sealed class AttemptsAcknowledgedTests
{
    private sealed class Wire
    {
        public List<string> Took { get; } = [];

        public List<int> Unsent { get; } = [];

        public List<string> Delivered { get; } = [];

        public ChatTurns Turns() => new(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                lock (Took) Took.Add(message.Text);
                sent();
                return Task.CompletedTask;
            },
            interrupt: () => Task.CompletedTask,
            changed: _ => { })
        {
            Unsent = count => { lock (Unsent) Unsent.Add(count); },
        };
    }

    /// <summary>
    /// The attempt's commit failing sends nothing and says so: the record would not mark the word, so the turn is never written, the word waits on
    /// its record unmarked and untaken, and the conversation is told once how many it left there; the next word, marked, goes as ever.
    /// </summary>
    [Fact]
    public async Task A_word_whose_attempt_the_record_would_not_mark_is_never_written_and_is_said()
    {
        var record = new StandInSaid { RefusingAttempts = true };
        var wire = new Wire();
        var turns = wire.Turns();

        var kept = await turns.KeepAsync(record, new ChatMessage("make the change", []), default);
        await Poll.Until(() => !turns.Running, () => "the turn never ended");

        Assert.Equal("w1", kept!.Word);
        Assert.Empty(wire.Took);
        Assert.Equal([1], wire.Unsent);
        var waiting = Assert.Single(record.Said);
        Assert.Equal(("w1", null), (waiting.Id, waiting.Attempt));
        Assert.Empty(record.TakenOff);

        record.RefusingAttempts = false;
        await turns.KeepAsync(record, new ChatMessage("and the readme", []), default);
        await Poll.Until(() => wire.Took.Count == 1, () => "the marked word never went");
        Assert.Equal(["and the readme"], wire.Took);
        await Poll.Until(() => record.TakenOff.Count == 1, () => "the word that went never left its record");
        Assert.Equal(["w2"], record.TakenOff);
        Assert.Equal([1], wire.Unsent);
    }

    /// <summary>
    /// A commit whose answer is lost sends nothing: the record holds the attempt, so the word waits marked on it, and the dispatcher, which cannot
    /// tell a commit that landed from one that did not, writes nothing. The next come-up settles it as uncertain and never sends it by itself.
    /// </summary>
    [Fact]
    public async Task A_commit_whose_answer_is_lost_sends_nothing_and_leaves_the_word_marked_on_its_record()
    {
        var record = new StandInSaid { AnswerLost = true };
        var wire = new Wire();
        var turns = wire.Turns();

        await turns.KeepAsync(record, new ChatMessage("make the change", []), default);
        await Poll.Until(() => !turns.Running, () => "the turn never ended");

        Assert.Empty(wire.Took);
        Assert.Equal([1], wire.Unsent);
        var waiting = Assert.Single(record.Said);
        Assert.NotNull(waiting.Attempt);
        Assert.Empty(record.TakenOff);
    }

    /// <summary>
    /// An answer naming fewer words than asked sends only those: a go-on carrying three of the record's words, the second no longer waiting, is
    /// written with two, each its own place in the prompt, and only those two leave the record once it is written. The third is the record's,
    /// unmarked and untaken, and the conversation is told one word was left.
    /// </summary>
    [Fact]
    public async Task A_go_on_whose_mark_names_fewer_words_than_asked_is_written_with_only_those()
    {
        var record = new StandInSaid { Naming = marked => [.. marked.Where(id => id != "w2")] };
        foreach (var text in new[] { "Port 8080.", "No, 9090.", "And log it." }) await record.KeepAsync(new ChatMessage(text, []), default);
        var ask = new ResumeAsk("conv-1", [.. record.Said], "the person said:");
        var wire = new Wire();
        var turns = wire.Turns();

        turns.Take(new ChatMessage(ask.Prompt, [])
        {
            GoesOn = ask,
            Attempting = () => record.AttemptAsync(ask.Ids, default),
            Delivered = () => { lock (wire.Delivered) wire.Delivered.AddRange(ask.Ids); },
        });
        await Poll.Until(() => wire.Took.Count == 1, () => "the go-on never reached the door");

        var prompt = Assert.Single(wire.Took);
        Assert.Contains("Port 8080.", prompt);
        Assert.DoesNotContain("No, 9090.", prompt);
        Assert.Contains("And log it.", prompt);
        Assert.Equal(["w1", "w3"], wire.Delivered);
        Assert.Equal([1], wire.Unsent);
        Assert.Equal(["w1", "w3"], record.Attempted);
        Assert.Null(record.Said.Single(word => word.Id == "w2").Attempt);
    }

    /// <summary>A go-on whose mark names none of its words is not written at all: the words wait on their record, and the conversation says how many.</summary>
    [Fact]
    public async Task A_go_on_whose_mark_names_none_of_its_words_is_never_written()
    {
        var record = new StandInSaid { RefusingAttempts = true };
        foreach (var text in new[] { "Port 8080.", "No, 9090." }) await record.KeepAsync(new ChatMessage(text, []), default);
        var ask = new ResumeAsk("conv-1", [.. record.Said], "the person said:");
        var wire = new Wire();
        var turns = wire.Turns();

        turns.Take(new ChatMessage(ask.Prompt, [])
        {
            GoesOn = ask,
            Attempting = () => record.AttemptAsync(ask.Ids, default),
            Delivered = () => { lock (wire.Delivered) wire.Delivered.AddRange(ask.Ids); },
        });
        await Poll.Until(() => wire.Unsent.Count == 1, () => "nothing said the go-on was left");
        await Poll.Until(() => !turns.Running, () => "the turn never ended");

        Assert.Empty(wire.Took);
        Assert.Empty(wire.Delivered);
        Assert.Equal([2], wire.Unsent);
        Assert.Equal(2, record.Said.Count);
    }

    // ---- the client's answer ----

    private sealed class Canned(HttpStatusCode status, string answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") });
    }

    private static ServiceClient Client(HttpStatusCode status, string answer) => new("http://stand-in", null, new HttpClient(new Canned(status, answer)));

    /// <summary>
    /// The service's answer names the words that no longer wait (<c>unmarked</c>); the client reads the words it marked as the words asked less
    /// those, in the order asked, where it once ignored the list and answered success.
    /// </summary>
    [Fact]
    public async Task The_client_reads_each_word_the_answer_marked_and_none_where_the_mark_was_refused()
    {
        var partial = await Client(HttpStatusCode.OK, """{"session":{"id":"c1","state":"completed"},"message":"Marked 2 word(s).","unmarked":["b2"]}""")
            .AttemptAsync("c1", ["a1", "b2", "c3"]);
        var whole = await Client(HttpStatusCode.OK, """{"session":{"id":"c1","state":"completed"},"message":"Marked 2 word(s).","unmarked":[]}""")
            .AttemptAsync("c1", ["a1", "c3"]);
        var refused = await Client(HttpStatusCode.NotFound, """{"error":"No session `c1` of this machine's.","refusal":"not-found"}""")
            .AttemptAsync("c1", ["a1"]);
        var noDoor = await Client(HttpStatusCode.NotFound, "").AttemptAsync("c1", ["a1"]);

        Assert.True(partial.Ok);
        Assert.Equal(["a1", "c3"], partial.Marked);
        Assert.Equal(["b2"], partial.Unmarked);
        Assert.True(whole.Ok);
        Assert.Equal(["a1", "c3"], whole.Marked);
        Assert.False(refused.Ok);
        Assert.Empty(refused.Marked);
        Assert.False(noDoor.Ok);
        Assert.Empty(noDoor.Marked);
        Assert.Contains("no door", noDoor.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The attempt through the real door of one record names its own word: a refusal or a lost answer is no acknowledgement, whatever the words
    /// say, and the dispatcher's mark reads that as none marked.
    /// </summary>
    [Fact]
    public async Task The_said_doors_attempt_names_the_ids_the_answer_marked()
    {
        var door = new SaidDoor(Client(HttpStatusCode.OK, """{"session":{"id":"c1","state":"completed"},"message":"x","unmarked":["b2"]}"""), "c1");

        Assert.Equal(["a1"], await door.AttemptAsync(["a1", "b2"], default));
        Assert.Empty(await new SaidDoor(Client(HttpStatusCode.Conflict, """{"error":"no"}"""), "c1").AttemptAsync(["a1"], default));
    }
}
