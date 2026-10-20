using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TURNFAIL1 and USERSHOWN1 (D137 §2.2): the person's words a driven session held when its door failed never reached the
/// agent, and its run ends with them. They are kept on its ended record through the say door, to go again with the next run
/// that goes on, and its conversation says why where they were shown, then shows them waiting at its foot.
/// </summary>
public sealed class WordsLeftTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-words-left-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Words_a_failed_door_never_delivered_are_kept_on_the_record_to_go_again()
    {
        var standIn = new StandIn(HttpStatusCode.OK, """
            {"session":{"id":"s1","state":"failed"},"message":"Kept for session `s1` to go on with.",
             "said":{"id":"w9","text":"stuck?","at":"2026-10-14T06:53:37+00:00","files":[],"reopens":true}}
            """);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Keep("s1", ChatMessage.Words(new ChatMessage("stuck?", []) { Id = "said-1" }) with { Reaches = "next-step", Door = "screen" }, say: null);

        var kept = await WordsLeft.KeepAsync(service, events, "s1", [new ChatMessage("stuck?", []) { Id = "said-1" }], CancellationToken.None);

        Assert.Equal(1, kept);
        var (method, path, body) = Assert.Single(standIn.Heard);
        Assert.Equal(("POST", "/api/sessions/s1/say"), (method, path));
        using (var sent = JsonDocument.Parse(body)) Assert.Equal("stuck?", sent.RootElement.GetProperty("text").GetString());

        var record = events.After("s1", 1).Events;
        // Why they wait, naming the word as it was shown, so the page settles it there; then the word waiting at the foot.
        Assert.Equal(2, record.Count);
        Assert.Equal(SessionEventKind.Note, record[0].Kind);
        Assert.Equal(["said-1"], record[0].Words);
        Assert.Equal(["ended.words-wait"], NoteAssert.Codes(record[0].Parts));
        Assert.Equal((SessionEventKind.User, "w9", "stuck?", "resume", "screen"), (record[1].Kind, record[1].Id, record[1].Text, record[1].Reaches, record[1].Door));
    }

    /// <summary>
    /// COWORK1b: Daoris's own word a failed door left is kept on the record as Daoris's, under its fact's key, and shown waiting
    /// as Daoris's, never as the person's.
    /// </summary>
    [Fact]
    public async Task Daoris_word_a_failed_door_left_is_kept_and_shown_as_daoris()
    {
        var standIn = new StandIn(HttpStatusCode.OK, """
            {"session":{"id":"s1","state":"failed"},"message":"Kept for session `s1`.",
             "said":{"id":"w9","text":"Daoris: reopened.","at":"2026-10-14T06:53:37+00:00","files":[],"reopens":true,"from":"daoris","key":"reopen/q1/desk/44"}}
            """);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var word = new ChatMessage("Daoris: reopened.", []) { Id = "said-1", Daoris = new DaorisWord("reopen/q1/desk/44") };

        Assert.Equal(1, await WordsLeft.KeepAsync(service, events, "s1", [word], CancellationToken.None));

        using (var sent = JsonDocument.Parse(Assert.Single(standIn.Heard).Body))
        {
            Assert.Equal(("daoris", "reopen/q1/desk/44"), (sent.RootElement.GetProperty("from").GetString(), sent.RootElement.GetProperty("key").GetString()));
        }

        var shown = events.After("s1", 0).Events.Last();
        Assert.Equal(("target", "daoris", "w9", "resume"), (shown.Origin, shown.From, shown.Id, shown.Reaches));
    }

    [Fact]
    public async Task Words_the_record_would_not_keep_are_said_never_to_have_reached_it()
    {
        var standIn = new StandIn(HttpStatusCode.Conflict, """{"error":"Session `s1` stood down: quest `#q1` is someone else's.","refusal":"stood-down"}""");
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));

        var kept = await WordsLeft.KeepAsync(service, events, "s1", [new ChatMessage("stuck?", []) { Id = "said-1" }], CancellationToken.None);

        Assert.Equal(0, kept);
        var note = Assert.Single(events.After("s1", 0).Events);
        Assert.Equal(["words.unreached"], NoteAssert.Codes(note.Parts));
        Assert.Null(note.Words);
    }

    /// <summary>A service standing in: each request recorded, then answered with the one status and body it was given.</summary>
    private sealed class StandIn(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(string Method, string Path, string Body)> Heard { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sent = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (Heard) Heard.Add((request.Method.Method, request.RequestUri!.AbsolutePath, sent));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
