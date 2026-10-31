using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LINESTOGETHER1b (the review-flow design §2): <c>SESSION_QUEUE_MOVE</c> moves a word held on a part's record to the head through the
/// service's move door, answering <c>moved</c>, a retry's <c>again</c>, a refusal as data with its code, and <c>not-known</c> where the
/// service's answer never came.
/// </summary>
public sealed class DriverModuleQueueMoveTests : DriverModuleBridge
{
    private const string Kept = """
        {"moved":true,"message":"Moved word `w1` from session `s1` to session `h1`.","to":"h1","session":{"id":"h1","state":"completed"},
         "said":{"id":"w9","text":"use the blue one","at":"2026-10-31T09:00:00+00:00","files":[],"reopens":true,"key":"move:s1:w1"}}
        """;

    [Fact]
    public async Task A_held_word_moves_to_the_head_its_part_settled_and_the_head_showing_it()
    {
        var service = new MoveService { Move = _ => (HttpStatusCode.OK, Kept) };
        var (loop, module) = await UpAsync(service);
        var nudged = loop.Nudges;

        var moved = await AnswerAsync(module, "SESSION_QUEUE_MOVE", new { session = "s1", id = "w1", to = "h0" });

        Assert.Equal((true, "h1", "w9"), (moved.GetProperty("moved").GetBoolean(), moved.GetProperty("to").GetString(), moved.GetProperty("word").GetString()));
        var (path, body) = Assert.Single(service.Posts);
        Assert.Equal("/api/sessions/s1/move", path);
        using (var asked = JsonDocument.Parse(body)) Assert.Equal(("w1", "h0"), (asked.RootElement.GetProperty("word").GetString(), asked.RootElement.GetProperty("to").GetString()));
        var left = Assert.Single(loop.Events.Page("s1").Events);
        Assert.Equal((SessionEventCodes.Withdrawn, "w1"), (left.Code, Assert.Single(left.Words!)));
        var shown = Assert.Single(loop.Events.Page("h1").Events);
        Assert.Equal((SessionEventKind.User, "w9", "use the blue one"), (shown.Kind, shown.Id, shown.Text));
        Assert.True(loop.Nudges > nudged);
    }

    [Fact]
    public async Task A_retry_the_service_answers_moved_again_shows_nothing_twice()
    {
        var service = new MoveService { Move = _ => (HttpStatusCode.OK, """{"moved":true,"again":true,"to":"h1","message":"Already moved."}""") };
        var (loop, module) = await UpAsync(service);

        var again = await AnswerAsync(module, "SESSION_QUEUE_MOVE", new { session = "s1", id = "w1", to = "h0" });

        Assert.Equal((true, true, "h1"), (again.GetProperty("moved").GetBoolean(), again.GetProperty("again").GetBoolean(), again.GetProperty("to").GetString()));
        Assert.Empty(loop.Events.Page("s1").Events);
        Assert.Empty(loop.Events.Page("h1").Events);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, """{"error":"Session `h1`, the newest part of the line, is working.","refusal":"running","head":"h1"}""", "running", "h1")]
    [InlineData(HttpStatusCode.Conflict, """{"error":"Session `s1` is working: word `w1` waits for its run.","refusal":"not-held"}""", "not-held", null)]
    [InlineData(HttpStatusCode.OK, """{"moved":false,"why":"taken","message":"Word `w1` no longer waits on session `s1`."}""", "taken", null)]
    public async Task A_move_the_service_refuses_is_answered_with_its_code_and_sentence(HttpStatusCode status, string answer, string why, string? head)
    {
        var (loop, module) = await UpAsync(new MoveService { Move = _ => (status, answer) });

        var refused = await AnswerAsync(module, "SESSION_QUEUE_MOVE", new { session = "s1", id = "w1", to = "h0" });

        Assert.Equal((false, why), (refused.GetProperty("moved").GetBoolean(), refused.GetProperty("why").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(refused.GetProperty("message").GetString()));
        Assert.Equal(head, refused.TryGetProperty("to", out var to) ? to.GetString() : null);
        Assert.Empty(loop.Events.Page("s1").Events);
    }

    /// <summary>A move whose answer never came may have happened: never answered moved, nor refused, but <c>not-known</c>, to be asked again.</summary>
    [Fact]
    public async Task A_move_whose_answer_never_came_is_not_known()
    {
        var (_, module) = await UpAsync(new MoveService { Move = _ => throw new HttpRequestException("the connection was reset") });

        var unknown = await AnswerAsync(module, "SESSION_QUEUE_MOVE", new { session = "s1", id = "w1", to = "h0" });

        Assert.Equal((false, "not-known"), (unknown.GetProperty("moved").GetBoolean(), unknown.GetProperty("why").GetString()));
    }

    private async Task<(DriverLoop Loop, DriverModule Module)> UpAsync(MoveService service)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        return (loop, new DriverModule(Bus, loop));
    }

    /// <summary>A local host standing in: no records, and the move door as a case answers it, each post heard by its path and body.</summary>
    private sealed class MoveService : HttpMessageHandler
    {
        private readonly List<(string Path, string Body)> _posts = [];

        public Func<string, (HttpStatusCode Status, string Body)> Move { get; init; } = _ => (HttpStatusCode.NotFound, "");

        public IReadOnlyList<(string Path, string Body)> Posts
        {
            get { lock (_posts) return [.. _posts]; }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var (status, answer) = (request.Method.Method, parts) switch
            {
                ("GET", ["api", "sessions"]) => (HttpStatusCode.OK, "[]"),
                ("POST", ["api", "sessions", _, "move"]) => Heard(path, body),
                _ => (HttpStatusCode.NotFound, ""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        private (HttpStatusCode, string) Heard(string path, string body)
        {
            lock (_posts) _posts.Add((path, body));
            return Move(body);
        }
    }
}
