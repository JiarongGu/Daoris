using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DRIFT1a2 (D133 §1): what the person adds to a running session is told to the service's door for it
/// (<c>POST /api/sessions/{id}/added</c>, DRIFT1a), which keeps it on the ask the session's work is for. Whether it was
/// kept is an answer, never an error: a session on no ask is answered <c>kept: false</c>, and an older host has no door.
/// </summary>
public sealed class SessionAddedTests
{
    [Fact]
    public async Task What_the_person_added_is_posted_verbatim_to_its_sessions_door_and_kept()
    {
        var standIn = new StandIn(HttpStatusCode.OK, """{"kept":true,"message":"Kept on ask `#a1`, as said to session `s1` on quest `#q1`."}""");
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var (kept, message) = await service.AddedToSessionAsync("s1", "the level file moved\nto assets/");

        Assert.True(kept);
        Assert.Equal("Kept on ask `#a1`, as said to session `s1` on quest `#q1`.", message);
        var (method, path, body) = Assert.Single(standIn.Heard);
        Assert.Equal(("POST", "/api/sessions/s1/added"), (method, path));
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("the level file moved\nto assets/", sent.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task A_session_on_no_ask_is_answered_not_kept_with_the_services_sentence()
    {
        var said = "Session `s1` is a conversation on no ask, so nothing was kept on one; its own record holds what was said.";
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(HttpStatusCode.OK, $$"""{"kept":false,"message":"{{said}}"}""")));

        Assert.Equal((false, said), await service.AddedToSessionAsync("s1", "hello"));
    }

    [Fact]
    public async Task A_refusal_is_answered_with_its_sentence_and_a_host_without_the_door_says_so()
    {
        using var refusing = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(HttpStatusCode.NotFound, """{"error":"No session `s9` of this machine's."}""")));
        using var older = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(HttpStatusCode.NotFound, "")));

        Assert.Equal((false, "No session `s9` of this machine's."), await refusing.AddedToSessionAsync("s9", "hello"));
        var (kept, message) = await older.AddedToSessionAsync("s1", "hello");
        Assert.False(kept);
        Assert.Contains("has no door for what the person adds (404)", message);
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
