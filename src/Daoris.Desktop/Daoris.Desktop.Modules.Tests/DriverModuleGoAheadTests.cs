using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// KNOWUSE1a2 (D135 §2, D131, D137): a go-ahead a parked session asked, answered on the session's own page, answers the
/// park too, so the same session goes on with one press where the ask's page and the box took two
/// (<c>SESSION_GO_AHEAD</c>, <c>DriverModule.Conversation.cs</c>).
/// </summary>
/// <remarks>
/// The go-ahead is answered first, on its ask, so the conversation the answer resumes is handed it (KNOWUSE1a's
/// <c>GoAheadsText.Resumed</c>, read as the driver takes the park up); only then is the park answered and the loop nudged.
/// </remarks>
public sealed class DriverModuleGoAheadTests : DriverModuleBridge
{
    /// <summary>
    /// The proof: the person's yes with their words answers the go-ahead on its ask, then the park with the same words
    /// through the box's own door, which keeps them on the record, shows them at once as the person's and nudges the loop.
    /// The order is the point: a look nudged before the go-ahead was answered would resume the session without it.
    /// </summary>
    [Fact]
    public async Task A_go_ahead_answered_on_a_parks_page_answers_it_then_the_park_with_the_persons_words()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "awaiting-person")) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD",
            new { id = "s1", ask = "a1", number = 2, approved = true, words = "only the report's menu entry" });

        Assert.Equal("Go-ahead 2 on ask `#a1` is approved.", answered.GetProperty("message").GetString());
        Assert.True(answered.GetProperty("sent").GetBoolean());
        Assert.Equal("resume", answered.GetProperty("reaches").GetString());
        Assert.Equal(JsonValueKind.Null, answered.GetProperty("why").ValueKind);
        Assert.Equal(["/api/asks/a1/go-aheads/2", "/api/sessions/s1/say"], ledger.Posts.Select(post => post.Path));
        using (var goAhead = JsonDocument.Parse(ledger.Posts[0].Body))
        {
            Assert.Equal("approved", goAhead.RootElement.GetProperty("answer").GetString());
            Assert.Equal("only the report's menu entry", goAhead.RootElement.GetProperty("words").GetString());
        }

        using (var said = JsonDocument.Parse(ledger.Posts[1].Body))
        {
            Assert.Equal("only the report's menu entry", said.RootElement.GetProperty("text").GetString());
        }

        var shown = Assert.Single(loop.Events.Page("s1").Events);
        Assert.Equal(
            (SessionEventKind.User, "person", "w1", "only the report's menu entry", "resume", "screen"),
            (shown.Kind, shown.Origin, shown.Id, shown.Text, shown.Reaches, shown.Door));
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// A yes or a no with no words of the person's: the record keeps none they did not write, so the park takes the blank
    /// answer the service has always kept for one (ANSWER1b), through the answer door, and the loop is nudged.
    /// </summary>
    [Theory]
    [InlineData(true, "approved")]
    [InlineData(false, "refused")]
    public async Task With_no_words_the_park_takes_its_blank_answer_after_the_go_ahead(bool approved, string answer)
    {
        var ledger = new Ledger { Records = Records(Record("s1", "awaiting-person")) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 1, approved });

        Assert.True(answered.GetProperty("sent").GetBoolean());
        Assert.Equal("resume", answered.GetProperty("reaches").GetString());
        Assert.Equal(["/api/asks/a1/go-aheads/1", "/api/sessions/s1/answer"], ledger.Posts.Select(post => post.Path));
        using (var goAhead = JsonDocument.Parse(ledger.Posts[0].Body))
        {
            Assert.Equal(answer, goAhead.RootElement.GetProperty("answer").GetString());
            Assert.False(goAhead.RootElement.TryGetProperty("words", out _));
        }

        using (var blank = JsonDocument.Parse(ledger.Posts[1].Body)) Assert.False(blank.RootElement.TryGetProperty("answer", out _));
        Assert.Empty(loop.Events.Page("s1").Events);
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// A park already answered (its first go-ahead's press, or the box) goes on with what it holds: a second go-ahead with no
    /// words is answered on its ask and adds no second answer, since the resumed session reads every go-ahead it asked as
    /// it goes on. Its words, given, join the first, as a second word to a park does (D137 §2.4).
    /// </summary>
    [Fact]
    public async Task A_park_already_answered_takes_no_second_blank_answer()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "awaiting-person", answer: "carry on.")) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 2, approved = false });

        Assert.True(answered.GetProperty("sent").GetBoolean());
        Assert.Equal("resume", answered.GetProperty("reaches").GetString());
        Assert.Equal(["/api/asks/a1/go-aheads/2"], ledger.Posts.Select(post => post.Path));
        Assert.Equal(1, loop.Nudges);

        await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 3, approved = true, words = "and the docs" });

        Assert.Equal(
            ["/api/asks/a1/go-aheads/2", "/api/asks/a1/go-aheads/3", "/api/sessions/s1/say"],
            ledger.Posts.Select(post => post.Path));
    }

    /// <summary>
    /// The go-ahead's door refuses (a number the ask does not hold, a host older than the door): its sentence is the
    /// answer, verbatim, and nothing else is answered, so a session never goes on believing a go-ahead was given.
    /// </summary>
    [Fact]
    public async Task A_refused_go_ahead_answers_nothing_else_and_says_the_services_sentence()
    {
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "awaiting-person")),
            GoAhead = (_, _) => (HttpStatusCode.NotFound, """{"error":"Ask `#a1` holds no go-ahead 7: it holds 1, 2."}"""),
        };
        var (loop, module) = await UpAsync(ledger);

        var refusal = await RefusalAsync(module, "SESSION_GO_AHEAD",
            new { id = "s1", ask = "a1", number = 7, approved = true, words = "go" });

        Assert.Contains("Ask `#a1` holds no go-ahead 7: it holds 1, 2.", refusal);
        Assert.Equal(["/api/asks/a1/go-aheads/7"], ledger.Posts.Select(post => post.Path));
        Assert.Empty(loop.Events.Page("s1").Events);
        Assert.Equal(0, loop.Nudges);
    }

    /// <summary>
    /// A session no longer parked by the time the press lands (the driver took it up, or another door answered it and it
    /// went on): with no words, its go-ahead is answered and nothing waits on the person to answer, so nothing else is
    /// kept, and the page is told the session was not answered.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("completed")]
    public async Task A_session_no_longer_parked_has_only_its_go_ahead_answered(string state)
    {
        var ledger = new Ledger { Records = Records(Record("s1", state)) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 1, approved = true });

        Assert.False(answered.GetProperty("sent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answered.GetProperty("why").ValueKind);
        Assert.Equal(["/api/asks/a1/go-aheads/1"], ledger.Posts.Select(post => post.Path));
        Assert.Equal(0, loop.Nudges);
    }

    /// <summary>What never goes on is said by its code, as the box's words are (MSG1d), and the go-ahead stays answered.</summary>
    [Fact]
    public async Task A_park_that_never_goes_on_is_said_by_its_code()
    {
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "failed"), Record("s2", "awaiting-person", created: "2026-10-03T09:00:00Z")),
        };
        var (_, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 1, approved = true });

        Assert.Equal((false, "superseded"), (answered.GetProperty("sent").GetBoolean(), answered.GetProperty("why").GetString()));
        Assert.Equal(["/api/asks/a1/go-aheads/1"], ledger.Posts.Select(post => post.Path));
    }

    /// <summary>Before the loop's service answers, nothing can be answered, which is a sentence the person can wait out.</summary>
    [Fact]
    public async Task A_go_ahead_answered_before_the_loop_is_up_says_so()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 1, approved = true });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>A go-ahead is named by its number: one that is not a whole number above nought is refused before anything is posted.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task A_go_ahead_with_no_number_is_refused_before_anything_is_posted(int number)
    {
        var ledger = new Ledger { Records = Records(Record("s1", "awaiting-person")) };
        var (_, module) = await UpAsync(ledger);

        var refusal = await RefusalAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number, approved = true });

        Assert.Contains("go-ahead", refusal);
        Assert.Empty(ledger.Posts);
    }

    /// <summary>A loop whose service is the stand-in, and the module over it.</summary>
    private async Task<(DriverLoop Loop, DriverModule Module)> UpAsync(Ledger ledger)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(ledger)));
        return (loop, new DriverModule(Bus, loop));
    }

    private static string Records(params string[] records) => $"[{string.Join(",", records)}]";

    private static string Record(string id, string state, string? answer = null, string created = "2026-10-03T08:00:00Z") =>
        $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","quest":"q1","kind":"driven","created":"{{created}}"{{(answer is null ? "" : $",\"answer\":\"{answer}\"")}}}""";

    /// <summary>
    /// A local host standing in: the records as given; the go-ahead door, which approves by default; the say door, which
    /// keeps the words as <c>w1</c>; the answer door, which keeps the park parked; and the state door. Each write is heard
    /// by its path and body, in the order it arrived.
    /// </summary>
    private sealed class Ledger : HttpMessageHandler
    {
        private readonly List<(string Path, string Body)> _posts = [];

        public string Records { get; init; } = "[]";

        public Func<string, int, (HttpStatusCode Status, string Body)> GoAhead { get; init; } = (ask, number) =>
            (HttpStatusCode.OK, $$"""{"ask":{"id":"{{ask}}"},"message":"Go-ahead {{number}} on ask `#{{ask}}` is approved."}""");

        /// <summary>Every write but a move, in order.</summary>
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
                ("GET", ["api", "sessions"]) => (HttpStatusCode.OK, Records),
                ("POST", ["api", "sessions", var id, "state"]) => (HttpStatusCode.OK,
                    $$"""{"session":{"id":"{{Uri.UnescapeDataString(id)}}","state":"{{JsonDocument.Parse(body).RootElement.GetProperty("state").GetString()}}"},"message":"Moved."}"""),
                ("POST", ["api", "asks", var ask, "go-aheads", var number]) =>
                    Heard(path, body, GoAhead(Uri.UnescapeDataString(ask), int.Parse(number))),
                ("POST", ["api", "sessions", var id, "say"]) => Heard(path, body, (HttpStatusCode.OK, Kept(id, body))),
                ("POST", ["api", "sessions", var id, "answer"]) => Heard(path, body, (HttpStatusCode.OK,
                    $$"""{"session":{"id":"{{id}}","state":"awaiting-person","answer":"carry on."},"message":"Answered session `{{id}}`."}""")),
                ("POST", _) => Heard(path, body, (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}""")),
                _ => (HttpStatusCode.NotFound, ""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        /// <summary>The say door's yes for a park: its answer kept as word <c>w1</c>, with the words as sent.</summary>
        private static string Kept(string id, string body)
        {
            var text = JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement.GetProperty("text").GetString());
            return $$"""
                {"session":{"id":"{{id}}","state":"awaiting-person"},"message":"Answered.",
                 "said":{"id":"w1","text":{{text}},"at":"2026-10-03T09:00:00+00:00","files":[],"reopens":false}
                }
                """;
        }

        private (HttpStatusCode, string) Heard(string path, string body, (HttpStatusCode, string) answer)
        {
            lock (_posts) _posts.Add((path, body));
            return answer;
        }
    }
}
