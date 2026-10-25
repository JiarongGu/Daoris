using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1b (D212's note of 2026-10-25, the foundation design §10): a word keeps its identity wherever it is held. The shell's
/// held word carries its producer's key, level, files and display choice across a restart; the record path and the forward to the
/// conversation its record went on in pass the key, so a producer's retry keeps one word (D137, D188); and an ordinary display
/// choice never reads as the supervisor's, the record path passing no <c>choice</c>.
/// </summary>
/// <remarks>
/// A restart is a second loop on the same scratch home; the service is a stand-in that keeps one word per producer's key on a
/// record, as the say door does (<c>SessionLedger.SayAsync</c>), so a word kept twice is a count.
/// </remarks>
public sealed class SessionWordsKeptIdentityTests : DriverModuleBridge
{
    private const string Key = "press/3f1c";

    private const string Words = "Port 9090, not 8080.";

    /// <summary>
    /// A restart keeps what the word was said with: its key, level, files and display choice are in the held file beside its text and
    /// door, and the record keeps the word under that key and those files once it ends, the display choice shown with it. The record
    /// path says no <c>choice</c> and no level: the say door refuses a choice from any author but the supervisor and keeps nothing.
    /// </summary>
    [Fact]
    public async Task A_restart_keeps_the_held_words_key_level_files_and_display_choice()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = _ => winding };
        var before = await UpAsync(service);

        var said = await before.Words.SayAsync(
            "c1", Words, [new ChatUpload("note.txt", Encoding.UTF8.GetBytes("the port"))], preface: null, "screen", CancellationToken.None,
            MessageLevel.Ordinary, Key, choice: "Port 9090");
        Assert.True(said!.Sent);

        using (var held = JsonDocument.Parse(File.ReadAllText(SessionWords.HeldPath(Home))))
        {
            var entry = Assert.Single(held.RootElement.GetProperty("held").EnumerateArray());
            Assert.Equal(
                ("c1", Words, "screen", Key, "ordinary", "Port 9090", "note.txt"),
                (entry.GetProperty("session").GetString(), entry.GetProperty("text").GetString(), entry.GetProperty("door").GetString(),
                    entry.GetProperty("key").GetString(), entry.GetProperty("level").GetString(), entry.GetProperty("choice").GetString(),
                    string.Join(",", entry.GetProperty("files").EnumerateArray().Select(name => name.GetString()))));
        }

        before.Stop();
        winding = false;
        service.Records = Records(Chat("c1", "completed"));
        var after = await UpAsync(service);
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended.");

        await UntilAsync(() => after.Events.Page("c1").Events.Count == 1);
        using var kept = JsonDocument.Parse(service.Said("c1").Last().Body);
        Assert.Equal((Key, "note.txt"), (kept.RootElement.GetProperty("key").GetString(),
            string.Join(",", kept.RootElement.GetProperty("files").EnumerateArray().Select(name => name.GetString()))));
        Assert.False(kept.RootElement.TryGetProperty("choice", out _), "the record path says no choice: the say door keeps nothing from one");
        Assert.False(kept.RootElement.TryGetProperty("level", out _), "the record path says no level");
        var shown = Assert.Single(after.Events.Page("c1").Events);
        Assert.Equal(("screen", "Port 9090"), (shown.Door, shown.Choice));
    }

    /// <summary>
    /// A producer's retry after a shell restart keeps one word where it kept two: the word held across the restart is kept under the
    /// key it was said with, so the producer's own retry under that key is answered with it. (D137, D188: the guarantee, kept.)
    /// </summary>
    [Fact]
    public async Task A_producers_retry_after_a_restart_keeps_one_word()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = _ => winding };
        var before = await UpAsync(service);
        await before.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key);

        before.Stop();
        winding = false;
        service.Records = Records(Chat("c1", "completed"));
        var after = await UpAsync(service);
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended.");
        await UntilAsync(() => after.Events.Page("c1").Events.Count == 1);

        var retried = await after.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key);

        Assert.True(retried!.Sent);
        Assert.Equal(1, service.Kept("c1"));
        Assert.Equal(2, service.Said("c1").Count(post => !post.Refused));
    }

    /// <summary>
    /// A word said on a record that ended keeps no <c>choice</c> on the wire: an ordinary display choice never reads as the
    /// supervisor's, and the word is still kept, shown under the choice.
    /// </summary>
    [Fact]
    public async Task An_ordinary_display_choice_is_kept_and_never_sent_as_a_choice()
    {
        var service = new KeyedSayService { Records = Records(Chat("c1", "completed")) };
        var loop = await UpAsync(service);

        var said = await loop.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key, choice: "Port 9090");

        Assert.True(said!.Sent);
        using var body = JsonDocument.Parse(Assert.Single(service.Said("c1")).Body);
        Assert.False(body.RootElement.TryGetProperty("choice", out _));
        Assert.Equal(Key, body.RootElement.GetProperty("key").GetString());
        Assert.Equal("Port 9090", Assert.Single(loop.Events.Page("c1").Events).Choice);
    }

    /// <summary>
    /// A hold restarted and then forwarded to a live successor keeps its key and level there (X4-1.2): the successor's inbox is handed
    /// the word at the level it was said at, and its record keeps it under the producer's key, so a producer's retry said to the successor
    /// while the word waits there keeps one word.
    /// </summary>
    [Fact]
    public async Task A_hold_restarted_and_forwarded_to_a_live_successor_keeps_its_key_and_level_and_a_retry_there_keeps_one_word()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = id => id == "c1" && winding };
        var before = await UpAsync(service);
        await before.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Ordinary, Key, "Port 9090");

        before.Stop();
        winding = false;
        service.Records = Records(Chat("c1", "completed"), Chat("c2", "working", from: "c1"));
        var successor = default(DrivenInbox);
        var after = await UpAsync(service, loop =>
        {
            successor = loop.Processes.OpenInbox("c2");
            successor.Attach(() => Task.CompletedTask, _ => Task.FromResult("end_turn"));
        });
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation went on in c2.");

        await UntilAsync(() => service.Said("c2").Count == 1);
        using (var forwarded = JsonDocument.Parse(service.Said("c2")[0].Body))
        {
            Assert.Equal(
                (Words, Key, "ordinary", true),
                (forwarded.RootElement.GetProperty("text").GetString(), forwarded.RootElement.GetProperty("key").GetString(),
                    forwarded.RootElement.GetProperty("level").GetString(), forwarded.RootElement.GetProperty("running").GetBoolean()));
        }

        var queued = Assert.Single(successor!.State.Queued);
        Assert.Equal((Key, MessageLevel.Ordinary), (queued.Key, queued.Level));
        Assert.DoesNotContain(service.Said("c1"), post => !post.Refused);

        var retried = await after.Words.SayAsync("c2", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Ordinary, Key);

        Assert.True(retried!.Sent);
        Assert.Equal(1, service.Kept("c2"));
    }

    /// <summary>
    /// The forward to a successor whose record keeps the word (it runs elsewhere, or nothing here runs it) passes the key, so its record
    /// keeps one word for a producer's retry, and the display choice, which shows the word under it there, as the first say did.
    /// </summary>
    [Fact]
    public async Task A_hold_forwarded_to_a_successor_the_record_keeps_carries_its_key_and_display_choice()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = id => id == "c1" && winding };
        var before = await UpAsync(service);
        await before.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Ordinary, Key, "Port 9090");

        before.Stop();
        winding = false;
        service.Records = Records(Chat("c1", "completed"), Chat("c2", "completed", from: "c1"));
        var after = await UpAsync(service);
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation went on in c2.");

        await UntilAsync(() => after.Events.Page("c2").Events.Count == 1);
        using (var forwarded = JsonDocument.Parse(Assert.Single(service.Said("c2")).Body))
        {
            Assert.Equal(Key, forwarded.RootElement.GetProperty("key").GetString());
            Assert.False(forwarded.RootElement.TryGetProperty("choice", out _));
        }

        Assert.Equal("Port 9090", Assert.Single(after.Events.Page("c2").Events).Choice);

        await after.Words.SayAsync("c2", Words, [], preface: null, "screen", CancellationToken.None, key: Key);

        Assert.Equal(1, service.Kept("c2"));
    }

    /// <summary>
    /// A held file written before this row, which holds a word's text, files and door alone, is read as it was: the word is tried
    /// keyless at the default level, as today (the legacy case the census names). An older shell reads a file this one wrote by the
    /// same three fields.
    /// </summary>
    [Fact]
    public async Task A_held_file_from_before_is_read_as_a_keyless_word_at_the_default_level()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionWords.HeldPath(Home))!);
        File.WriteAllText(
            SessionWords.HeldPath(Home),
            """{"held":[{"session":"c1","text":"and the readme","files":[],"door":"screen"}]}""");
        var service = new KeyedSayService { Records = Records(Chat("c1", "completed")) };
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        await loop.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended.");

        await UntilAsync(() => service.Said("c1").Count == 1);
        using var body = JsonDocument.Parse(service.Said("c1")[0].Body);
        Assert.Equal("and the readme", body.RootElement.GetProperty("text").GetString());
        Assert.False(body.RootElement.TryGetProperty("key", out _));
        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
    }

    private async Task<DriverLoop> UpAsync(KeyedSayService service, Action<DriverLoop>? before = null)
    {
        var loop = Loop();
        before?.Invoke(loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        return loop;
    }

    private static string Records(params string[] records) => $"[{string.Join(",", records)}]";

    /// <summary>A conversation's record, continuing <paramref name="from"/> where one is named.</summary>
    private static string Chat(string id, string state, string? from = null)
    {
        var continues = from is null ? "" : $",\"from\":\"{from}\"";
        return $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","kind":"chat","created":"2026-10-03T08:00:00Z"{{continues}}}""";
    }
}
