using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1d-ii part 1 (the foundation design §3.4 rules 1 and 2, §19.12): a host with the <c>head</c> mark refuses a word said to a record that
/// is not its line's head as <c>continued</c>, naming the head, and the shell says it there through its whole judgement, once, whole: its producer's
/// key, its level, its files and its display choice. The box's record path, a stale driven inbox and the pump each land the word on the head; a
/// second <c>continued</c> follows the next head, and a planted cycle ends the walk with the word said lost. A host without the mark keeps the
/// driver's own walk to the conversation a record went on in, as today. Each case holds the answer the say door will give, by the stand-in that
/// models it (<see cref="KeyedSayService.Continue"/>).
/// </summary>
public sealed class SessionWordsContinuedTests : DriverModuleBridge
{
    private const string Key = "press/5e1d";

    private const string Words = "Port 9090, not 8080.";

    private const string Choice = "Port 9090";

    private async Task<DriverLoop> UpAsync(KeyedSayService service, Action<DriverLoop>? before = null)
    {
        var loop = Loop();
        before?.Invoke(loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        return loop;
    }

    private static string Records(params string[] records) => $"[{string.Join(",", records)}]";

    private static string Chat(string id, string state, string? from = null, int minute = 0)
    {
        var continues = from is null ? "" : $",\"from\":\"{from}\"";
        return $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","kind":"chat","created":"2026-10-03T08:{{minute:00}}:00Z"{{continues}}}""";
    }

    private static string Driven(string id, string state) =>
        $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","kind":"driven","quest":"q1","created":"2026-10-03T07:00:00Z"}""";

    /// <summary>Every say posted to a record, each as the shell sent it: its key, its files, joined, and its level.</summary>
    private static IReadOnlyList<(string? Key, string Files, string? Level)> PostsTo(KeyedSayService service, string session) =>
    [
        .. service.Said(session).Select(post =>
        {
            using var body = JsonDocument.Parse(post.Body);
            var root = body.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var files = root.TryGetProperty("files", out var named) ? string.Join(",", named.EnumerateArray().Select(name => name.GetString())) : "";
            return (Text("key"), files, Text("level"));
        }),
    ];

    /// <summary>
    /// The box's record path: a word said to an ended conversation the host names went on in <c>c2</c> is kept once on <c>c2</c>, under the press's
    /// key, with its file kept in <c>c2</c>'s folder and shown in <c>c2</c>'s conversation under its display choice; nothing is kept on or shown in
    /// <c>c1</c>, and the answer names the head. The driver's own walk stands aside for the host's judgement.
    /// </summary>
    [Fact]
    public async Task The_boxs_word_to_a_record_that_went_on_lands_once_on_the_head_with_its_key_files_and_choice()
    {
        var service = new KeyedSayService { Records = Records(Chat("c1", "completed"), Chat("c2", "completed", from: "c1", minute: 5)), HeadMark = true }
            .Continue("c1", "c2");
        var loop = await UpAsync(service);

        var said = await loop.Words.SayAsync(
            "c1", Words, [new ChatUpload("notes.md", "the notes"u8.ToArray())], preface: null, "screen", CancellationToken.None, MessageLevel.Ordinary,
            Key, Choice);

        Assert.Equal((true, "c2"), (said!.Sent, said.At));
        Assert.Equal(0, service.Kept("c1"));
        Assert.Equal(1, service.Kept("c2"));
        Assert.Equal([(Key, "notes.md", (string?)null)], PostsTo(service, "c1"));
        Assert.Equal([(Key, "notes.md", (string?)null)], PostsTo(service, "c2"));
        Assert.Equal("the notes", File.ReadAllText(ChatFiles.Find(Home, "c2", "notes.md")!.Path));
        await UntilAsync(() => loop.Events.Page("c2").Events.Count == 1);
        var shown = Assert.Single(loop.Events.Page("c2").Events);
        Assert.Equal((Words, Choice, "screen"), (shown.Text, shown.Choice, shown.Door));
        Assert.Empty(loop.Events.Page("c1").Events);
    }

    /// <summary>
    /// A host without the <c>head</c> mark: the driver's own walk to the conversation a record went on in judges it before anything is asked, as
    /// today, refused <c>continued</c> naming <c>c2</c>, and the say door is never posted to.
    /// </summary>
    [Fact]
    public async Task A_host_without_the_head_mark_keeps_the_drivers_own_walk_as_today()
    {
        var service = new KeyedSayService { Records = Records(Chat("c1", "completed"), Chat("c2", "completed", from: "c1", minute: 5)) }
            .Continue("c1", "c2");
        var loop = await UpAsync(service);

        var said = await loop.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key);

        Assert.Equal((false, WordsNever.Continued, "c2"), (said!.Sent, said.Why, said.To));
        Assert.Empty(service.Said("c1"));
        Assert.Empty(service.Said("c2"));
    }

    /// <summary>
    /// A stale driven inbox: the word kept for its run is refused <c>continued</c>, so the inbox holds, sends and queues nothing, and the shell says
    /// the word at the head under the key the inbox kept it under, where it is kept once.
    /// </summary>
    [Fact]
    public async Task A_word_through_a_stale_driven_inbox_is_held_nowhere_here_and_lands_once_on_the_head_under_its_key()
    {
        var service = new KeyedSayService { Records = Records(Driven("d1", "working"), Chat("c2", "completed")) }.Continue("d1", "c2");
        var inbox = default(DrivenInbox);
        var delivered = new List<string>();
        var loop = await UpAsync(service, up =>
        {
            inbox = up.Processes.OpenInbox("d1");
            inbox.Attach(() => Task.CompletedTask, message => { delivered.Add(message.Text); return Task.FromResult("end_turn"); });
            inbox.Flow();
        });

        var said = await loop.Words.SayAsync("d1", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Urgent, Key);

        Assert.Equal((true, "c2"), (said!.Sent, said.At));
        Assert.Empty(inbox!.State.Queued);
        Assert.False(inbox.Holds);
        Assert.Empty(delivered);
        // Kept for the run under the press's key at its level, refused, and said once at the head's record under the same key.
        Assert.Equal((0, 1), (service.Kept("d1"), service.Kept("c2")));
        Assert.Equal([(Key, "", "urgent")], PostsTo(service, "d1"));
        Assert.Equal([Key], PostsTo(service, "c2").Select(each => each.Key));
    }

    /// <summary>A word held while its conversation wound up, then tried by the pump once it ended: where its record answers.</summary>
    private async Task<(DriverLoop Loop, KeyedSayService Service)> HeldThenEndedAsync(Action<KeyedSayService> ended, params string[] records)
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records(Chat("c1", "working")), Winding = id => id == "c1" && winding };
        var before = await UpAsync(service);
        var held = await before.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, MessageLevel.Ordinary, Key, Choice);
        Assert.True(held!.Sent);
        Assert.True(File.Exists(SessionWords.HeldPath(Home)));

        before.Stop();
        winding = false;
        service.Records = Records([Chat("c1", "completed"), .. records]);
        ended(service);
        var after = await UpAsync(service);
        await after.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended.");
        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
        return (after, service);
    }

    /// <summary>
    /// The pump: a held word its record refuses <c>continued</c> once it ended is said again at the head once, under its key, off the held file,
    /// and shown in the head's conversation under its display choice. The head is named by the host alone: no record's <c>from</c> names it.
    /// </summary>
    [Fact]
    public async Task The_pump_says_a_held_word_again_at_the_head_once_under_its_key()
    {
        var (loop, service) = await HeldThenEndedAsync(service => service.Continue("c1", "c2"), Chat("c2", "completed", minute: 5));

        Assert.Equal(0, service.Kept("c1"));
        Assert.Equal(1, service.Kept("c2"));
        Assert.Equal([Key], PostsTo(service, "c2").Select(each => each.Key));
        await UntilAsync(() => loop.Events.Page("c2").Events.Count == 1);
        Assert.Equal((Words, Choice), (loop.Events.Page("c2").Events[0].Text, loop.Events.Page("c2").Events[0].Choice));
    }

    /// <summary>A chain of two: the head the pump said the word at went on again, so the word follows to the next head and is kept there once.</summary>
    [Fact]
    public async Task A_second_continued_follows_the_next_head()
    {
        var (_, service) = await HeldThenEndedAsync(
            service => service.Continue("c1", "c2").Continue("c2", "c3"), Chat("c2", "completed", minute: 5), Chat("c3", "completed", minute: 9));

        Assert.Equal((0, 0, 1), (service.Kept("c1"), service.Kept("c2"), service.Kept("c3")));
        Assert.Equal([Key], PostsTo(service, "c3").Select(each => each.Key));
    }

    /// <summary>
    /// A planted cycle, <c>c1</c>'s head <c>c2</c> and <c>c2</c>'s <c>c1</c>: the walk ends at the record it would ask again, never looping. Nothing is
    /// kept anywhere, the word leaves the held file and the conversation it was said to says it was not kept, by the code <c>continued</c>.
    /// </summary>
    [Fact]
    public async Task A_planted_cycle_ends_the_walk_and_the_word_is_said_lost()
    {
        var (loop, service) = await HeldThenEndedAsync(
            service => service.Continue("c1", "c2").Continue("c2", "c1"), Chat("c2", "completed", minute: 5));

        Assert.Equal((0, 0), (service.Kept("c1"), service.Kept("c2")));
        await UntilAsync(() => loop.Events.Page("c1").Events.Any(each => each.Why == WordsNever.Continued));
        Assert.Single(service.Said("c2"));
    }
}
