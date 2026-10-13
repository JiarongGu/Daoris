using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1d (D137 §5.3): the person's words to a parked or ended session go to the service's say door (MSG1a), which keeps
/// them on its record for it to go on with. The word comes back as kept, with the id the record's events say again where
/// the session took it; a refusal is an answer by the service's word beside its sentence, and an older host has no door.
/// </summary>
public sealed class SessionSayTests
{
    [Fact]
    public async Task The_words_and_their_files_names_are_posted_to_the_say_door_and_the_kept_word_read_back()
    {
        var standIn = new StandIn(HttpStatusCode.OK, """
            {"session":{"id":"s1","state":"completed"},"message":"Kept for session `s1` to go on with.",
             "said":{"id":"w1","text":"also the changelog","at":"2026-10-03T09:00:00+00:00","files":["notes.md"],"reopens":true}}
            """);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var said = await service.SayAsync("s1", "also the changelog", ["notes.md"]);

        Assert.True(said.Kept);
        Assert.Equal("Kept for session `s1` to go on with.", said.Message);
        Assert.Null(said.Refusal);
        var word = said.Word!;
        Assert.Equal(("w1", "also the changelog", true), (word.Id, word.Text, word.Reopens));
        Assert.Equal(["notes.md"], word.Files);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero), word.At);
        var (method, path, body) = Assert.Single(standIn.Heard);
        Assert.Equal(("POST", "/api/sessions/s1/say"), (method, path));
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("also the changelog", sent.RootElement.GetProperty("text").GetString());
        Assert.Equal(["notes.md"], sent.RootElement.GetProperty("files").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public async Task Words_with_no_files_send_none()
    {
        var standIn = new StandIn(HttpStatusCode.OK, """{"session":{"id":"s1","state":"awaiting-person"},"message":"Kept."}""");
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var said = await service.SayAsync("s1", "port 8080");

        Assert.True(said.Kept);
        Assert.Null(said.Word);
        using var sent = JsonDocument.Parse(Assert.Single(standIn.Heard).Body);
        Assert.False(sent.RootElement.TryGetProperty("files", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "running", "Session `s1` is working; it hears words through its driver.")]
    [InlineData(HttpStatusCode.Conflict, "stood-down", "Session `s1` stood down: quest `#q1` is someone else's.")]
    [InlineData(HttpStatusCode.NotFound, "not-found", "No session `s1` of this machine's.")]
    public async Task A_refusal_is_answered_by_its_word_and_its_sentence(HttpStatusCode status, string refusal, string error)
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(status, $$"""{"error":"{{error}}","refusal":"{{refusal}}"}""")));

        var said = await service.SayAsync("s1", "hello");

        Assert.Equal((false, error, refusal), (said.Kept, said.Message, said.Refusal));
        Assert.Null(said.Word);
    }

    [Fact]
    public async Task A_host_without_the_door_says_so()
    {
        using var older = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(HttpStatusCode.NotFound, "")));

        var said = await older.SayAsync("s1", "hello");

        Assert.False(said.Kept);
        Assert.Null(said.Refusal);
        Assert.Contains("has no door for words to a session that is not running (404)", said.Message);
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
