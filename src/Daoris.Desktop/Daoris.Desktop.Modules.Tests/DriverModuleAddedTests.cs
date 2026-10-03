using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// DRIFT1a2 (D133 §1): a message the person types into a running session reaches its ask. Once the session takes it (a
/// driven session's inbox holds it, or a conversation is told it), <c>SESSION_INPUT</c> posts its words to the service's
/// <c>/api/sessions/{id}/added</c> with the session's own id, and the service keeps them on the ask that session's work is
/// for. Whatever the service answers, the person's message has reached its session: <c>kept: false</c>, a refusal and a
/// host that does not answer change nothing the page is told.
/// </summary>
public sealed class DriverModuleAddedTests : DriverModuleBridge
{
    [Fact]
    public async Task A_message_a_driven_sessions_inbox_holds_is_posted_once_with_its_sessions_id()
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK, """{"kept":true,"message":"Kept on ask `#a1`."}"""));
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        var inbox = loop.Processes.OpenInbox("s1");

        var sent = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_INPUT", new { id = "s1", text = "use the shared report module" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.Equal("use the shared report module", inbox.TakeOrClose()?.Text);
        await UntilAsync(() => standIn.Count > 0);
        await Task.Delay(200);
        var (path, body) = Assert.Single(standIn.Heard);
        Assert.Equal("/api/sessions/s1/added", path);
        using var words = JsonDocument.Parse(body);
        Assert.Equal("use the shared report module", words.RootElement.GetProperty("text").GetString());
    }

    /// <summary>
    /// Its inbox closes as the session winds up. What the person says then is held for its record to end (MSG1d, D137
    /// §2.1), no longer refused, and nothing is posted as added: the session never took it during its run, and the record
    /// keeps it on the ask once the session that goes on takes it.
    /// </summary>
    [Fact]
    public async Task A_message_the_session_no_longer_takes_is_held_and_never_posted_as_added()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/sessions" => (HttpStatusCode.OK,
                """[{"id":"s1","repository":"engine","state":"working","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}]"""),
            "/api/sessions/s1/say" => (HttpStatusCode.Conflict, """{"error":"Session `s1` is working.","refusal":"running"}"""),
            _ => (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}"""),
        });
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        loop.Processes.OpenInbox("s1").TakeOrClose();

        var sent = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_INPUT", new { id = "s1", text = "too late?" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.Equal("resume", sent.GetProperty("reaches").GetString());
        await Task.Delay(200);
        Assert.DoesNotContain(standIn.Heard, heard => heard.Path.EndsWith("/added", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"kept":false,"message":"Session `s1` works quest `#q1`, which `engine` asked rather than an ask."}""")]
    [InlineData(HttpStatusCode.NotFound, """{"error":"No session `s1` of this machine's."}""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    public async Task Whatever_the_service_answers_the_message_still_reached_its_session(HttpStatusCode status, string body)
    {
        var standIn = new StandIn(_ => (status, body));
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        var inbox = loop.Processes.OpenInbox("s1");

        var sent = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_INPUT", new { id = "s1", text = "the level file moved" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        await UntilAsync(() => standIn.Count > 0);
        Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);
    }

    [Fact]
    public async Task A_service_that_cannot_be_reached_leaves_the_message_with_its_session()
    {
        var standIn = new StandIn(_ => throw new HttpRequestException("refused"));
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        var inbox = loop.Processes.OpenInbox("s1");

        var sent = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_INPUT", new { id = "s1", text = "the level file moved" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        await UntilAsync(() => standIn.Count > 0);
        Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);
    }

    /// <summary>
    /// MSG1d (D137 §2.4): words to a session that ended go to its say door and never to the added one. The service keeps
    /// them on the ask once a session takes them (as <c>reopened</c>), so posting them as added would keep them twice.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_that_ended_go_to_its_say_door_and_never_to_the_added_one()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/sessions" => (HttpStatusCode.OK,
                """[{"id":"s1","repository":"engine","state":"completed","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}]"""),
            "/api/sessions/s1/say" => (HttpStatusCode.OK, """
                {"session":{"id":"s1","state":"completed"},"message":"Kept.",
                 "said":{"id":"w1","text":"also the changelog","at":"2026-10-03T09:00:00+00:00","files":[],"reopens":true}}
                """),
            _ => (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}"""),
        });
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));

        var sent = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_INPUT", new { id = "s1", text = "also the changelog" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        await Task.Delay(200);
        Assert.Equal(["/api/sessions/s1/say"], standIn.Heard.Select(heard => heard.Path).Where(path => path != "/api/sessions"));
    }

    /// <summary>A service standing in: each request recorded by its path and body, then answered.</summary>
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        private readonly List<(string Path, string Body)> _heard = [];

        public int Count { get { lock (_heard) return _heard.Count; } }

        public IReadOnlyList<(string Path, string Body)> Heard { get { lock (_heard) return [.. _heard]; } }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (_heard) _heard.Add((request.RequestUri!.AbsolutePath, body));
            var (status, said) = answer(request);
            return new HttpResponseMessage(status) { Content = new StringContent(said, Encoding.UTF8, "application/json") };
        }
    }
}
