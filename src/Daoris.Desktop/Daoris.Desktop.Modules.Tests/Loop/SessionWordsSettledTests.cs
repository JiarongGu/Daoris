using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1 row p (D212's note of the row; the foundation design §3.4 rule 5, §19.2): a keyed say the service answers <b>settled</b>, a retry of a
/// word kept and taken whose answer was lost, is handled by the shell before it reads a word. Its record path shows no <c>User</c> event and
/// nudges nothing; its held words go off the held file with nothing shown; the forward of a held word to a live successor's inbox and a say to a
/// driven session's inbox hold, send and keep nothing on the ask. Each case holds the answer the service's say door gives to a client that asks,
/// by the stand-in that models it (<see cref="KeyedSayService.Settle"/>).
/// </summary>
public sealed class SessionWordsSettledTests : DriverModuleBridge
{
    private const string Key = "press/3f1c";

    private const string Words = "Port 9090, not 8080.";

    private async Task<DriverLoop> UpAsync(KeyedSayService service, Action<DriverLoop>? before = null)
    {
        var loop = Loop();
        before?.Invoke(loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        return loop;
    }

    private static string Records(params string[] records) => $"[{string.Join(",", records)}]";

    private static string Chat(string id, string state, string? from = null)
    {
        var continues = from is null ? "" : $",\"from\":\"{from}\"";
        return $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","kind":"chat","created":"2026-10-03T08:00:00Z"{{continues}}}""";
    }

    /// <summary>
    /// The record path: a retry of a word the line settled, said to a record that ended, is answered sent with no word and no reach, shows no
    /// <c>User</c> event in the conversation, keeps nothing on the ask and asks the service once; the same key said to a record on which it was
    /// never settled is kept and shown as ever.
    /// </summary>
    [Fact]
    public async Task A_settled_retry_on_the_record_path_shows_nothing_and_keeps_nothing_on_the_ask()
    {
        var service = new KeyedSayService { Records = Records(Chat("c1", "completed"), Chat("c2", "completed")) }.Settle("c1", Key);
        var loop = await UpAsync(service);

        var settled = await loop.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key);

        Assert.True(settled!.Sent);
        Assert.Null(settled.Word);
        Assert.False(settled.Running);
        Assert.Equal(0, service.Kept("c1"));
        Assert.Single(service.Said("c1"));
        Assert.Empty(loop.Events.Page("c1").Events);
        Assert.Empty(service.Added());

        // A key this line never settled is kept and shown, as before.
        var kept = await loop.Words.SayAsync("c2", Words, [], preface: null, "screen", CancellationToken.None, key: "another/key-2");
        Assert.True(kept!.Sent);
        Assert.NotNull(kept.Word);
        Assert.Equal(1, service.Kept("c2"));
        await UntilAsync(() => loop.Events.Page("c2").Events.Count == 1);
    }

    /// <summary>
    /// The pump: a word held while its record wound up, then tried when it ended, is answered settled (its first say's answer was lost, the word
    /// kept and taken meanwhile): off the held file with nothing shown, never a second word on the record and nothing said to the person.
    /// </summary>
    [Fact]
    public async Task A_held_word_the_pump_finds_settled_goes_off_the_held_file_and_shows_nothing()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = _ => winding };
        var before = await UpAsync(service);
        var held = await before.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key);
        Assert.True(held!.Sent);
        Assert.True(File.Exists(SessionWords.HeldPath(Home)));

        before.Stop();
        winding = false;
        service.Records = Records(Chat("c1", "completed"));
        service.Settle("c1", Key);
        var after = await UpAsync(service);
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended.");

        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
        Assert.Equal(0, service.Kept("c1"));
        Assert.Empty(after.Events.Page("c1").Events);
        Assert.Empty(service.Added());
    }

    /// <summary>
    /// The forward: a held word passed to the live conversation its record went on in, whose record the service answers settled, is held,
    /// queued and sent nowhere: the successor's inbox stays empty and nothing is kept on its ask.
    /// </summary>
    [Fact]
    public async Task A_held_word_forwarded_to_a_live_successor_that_the_service_answers_settled_reaches_nothing()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = id => id == "c1" && winding };
        var before = await UpAsync(service);
        await before.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Ordinary, Key);

        before.Stop();
        winding = false;
        service.Records = Records(Chat("c1", "completed"), Chat("c2", "working", from: "c1"));
        service.Settle("c1", Key);
        var successor = default(DrivenInbox);
        var delivered = new List<string>();
        var after = await UpAsync(service, loop =>
        {
            successor = loop.Processes.OpenInbox("c2");
            successor.Attach(() => Task.CompletedTask, message => { delivered.Add(message.Text); return Task.FromResult("end_turn"); });
        });
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation went on in c2.");

        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
        await UntilAsync(() => service.Said("c2").Count == 1);
        Assert.Empty(successor!.State.Queued);
        Assert.False(successor.Holds);
        Assert.Empty(delivered);
        Assert.Equal(0, service.Kept("c2"));
        Assert.Empty(after.Events.Page("c2").Events);
        Assert.Empty(service.Added());
    }

    /// <summary>
    /// A say to a driven session's inbox the record answers settled: sent, with no word, not running, so nothing is kept on its ask again; the
    /// inbox holds and sends nothing.
    /// </summary>
    [Fact]
    public async Task A_say_to_a_driven_inbox_the_record_answers_settled_holds_sends_and_keeps_nothing()
    {
        var service = new KeyedSayService { Records = Records(Chat("c2", "working")) }.Settle("c1", Key);
        var inbox = default(DrivenInbox);
        var delivered = new List<string>();
        var loop = await UpAsync(service, up =>
        {
            inbox = up.Processes.OpenInbox("c2");
            inbox.Attach(() => Task.CompletedTask, message => { delivered.Add(message.Text); return Task.FromResult("end_turn"); });
            inbox.Flow();
        });

        var said = await loop.Words.SayAsync("c2", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Urgent, Key);

        Assert.True(said!.Sent);
        Assert.Null(said.Word);
        Assert.False(said.Running);
        Assert.Empty(inbox!.State.Queued);
        Assert.False(inbox.Holds);
        Assert.Empty(delivered);
        Assert.Equal(0, service.Kept("c2"));
        Assert.Empty(service.Added());
    }
}
