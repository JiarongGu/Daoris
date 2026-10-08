using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// KNOWUSE1a2 and GOAHEAD2b (D135 §2, D131, D137): a go-ahead a parked session asked, answered on the session's own page
/// (<c>SESSION_GO_AHEAD</c>, <c>DriverModule.Conversation.cs</c>), goes through the ask's own door with <c>goesOn</c>, so
/// the park is answered only where none of the go-aheads the session asked is still open: the service's decision, the same
/// as the ask's page and the terminal's door, read here off the record and never judged again.
/// </summary>
/// <remarks>
/// The stand-in is the service's decision, not a copy of it: its go-ahead door keeps the park's blank answer only where a
/// test says this answer was the last one open, and only when the caller lets it (<c>goesOn</c> true or absent).
/// </remarks>
public sealed class DriverModuleGoAheadTests : DriverModuleBridge
{
    /// <summary>
    /// The proof: the last go-ahead still open, answered on the park's page, goes through the go-ahead's door saying
    /// <c>goesOn: true</c>, whose decision keeps the park's blank answer; the page is told the same session goes on, and the
    /// loop is nudged. Nothing is posted to the park's own doors: the service answered it.
    /// </summary>
    [Theory]
    [InlineData(true, "approved")]
    [InlineData(false, "refused")]
    public async Task The_last_open_go_ahead_sends_the_park_on_by_the_services_decision(bool approved, string answer)
    {
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "awaiting-person")),
            AfterGoAhead = Records(Record("s1", "awaiting-person", answer: "carry on.")),
        };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 2, approved });

        Assert.Equal(
            "Go-ahead 2 on ask `#a1` is " + answer + ". Session `s1` was waiting on you for its go-aheads: it goes on.",
            answered.GetProperty("message").GetString());
        Assert.Equal((true, "resume", false), Park(answered));
        Assert.Equal(JsonValueKind.Null, answered.GetProperty("why").ValueKind);
        Assert.Equal(["/api/asks/a1/go-aheads/2"], ledger.Posts.Select(post => post.Path));
        using (var goAhead = JsonDocument.Parse(ledger.Posts[0].Body))
        {
            Assert.Equal(answer, goAhead.RootElement.GetProperty("answer").GetString());
            Assert.True(goAhead.RootElement.GetProperty("goesOn").GetBoolean());
            Assert.False(goAhead.RootElement.TryGetProperty("words", out _));
        }

        Assert.Empty(loop.Events.Page("s1").Events);
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// A go-ahead the session asked still open keeps it parked, as at the ask's door: the service keeps no answer, so the
    /// page is told the park still waits on the person, and nothing is nudged or posted beside the go-ahead.
    /// </summary>
    [Fact]
    public async Task A_go_ahead_still_open_keeps_the_park_waiting()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "awaiting-person")) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 1, approved = true });

        Assert.Equal(
            "Go-ahead 1 on ask `#a1` is approved. Session `s1` is still waiting on you for go-ahead 2.",
            answered.GetProperty("message").GetString());
        Assert.Equal((false, null, true), Park(answered));
        Assert.Equal(JsonValueKind.Null, answered.GetProperty("why").ValueKind);
        Assert.Equal(["/api/asks/a1/go-aheads/1"], ledger.Posts.Select(post => post.Path));
        using (var goAhead = JsonDocument.Parse(ledger.Posts[0].Body))
        {
            Assert.True(goAhead.RootElement.GetProperty("goesOn").GetBoolean());
        }

        Assert.Equal(0, loop.Nudges);
    }

    /// <summary>
    /// The person's words beside a go-ahead are that go-ahead's, kept on its answer as at the ask's page, and never said to
    /// the park: said there, they would send it on with a go-ahead still open, or join a <i>carry on.</i> that was never
    /// theirs. The resumed session is handed them quoted as theirs, beneath the park's answer (GOAHEAD2).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_persons_words_are_the_go_aheads_and_never_said_to_the_park(bool last)
    {
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "awaiting-person")),
            AfterGoAhead = last ? Records(Record("s1", "awaiting-person", answer: "carry on.")) : null,
        };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD",
            new { id = "s1", ask = "a1", number = 2, approved = true, words = "  only the report's menu entry " });

        Assert.Equal(last ? (true, "resume", false) : (false, null, true), Park(answered));
        Assert.Equal(["/api/asks/a1/go-aheads/2"], ledger.Posts.Select(post => post.Path));
        using (var goAhead = JsonDocument.Parse(ledger.Posts[0].Body))
        {
            Assert.Equal("only the report's menu entry", goAhead.RootElement.GetProperty("words").GetString());
            Assert.True(goAhead.RootElement.GetProperty("goesOn").GetBoolean());
        }

        Assert.Empty(loop.Events.Page("s1").Events);
        Assert.Equal(last ? 1 : 0, loop.Nudges);
    }

    /// <summary>
    /// A park already answered (in the box, or by an earlier door) goes on with what it holds, whatever is still open: the
    /// service keeps nothing more on it, and the page is told it goes on.
    /// </summary>
    [Fact]
    public async Task A_park_already_answered_goes_on_with_what_it_holds()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "awaiting-person", answer: "carry on.")) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 2, approved = false });

        Assert.Equal((true, "resume", false), Park(answered));
        Assert.Equal(["/api/asks/a1/go-aheads/2"], ledger.Posts.Select(post => post.Path));
        Assert.Equal(1, loop.Nudges);
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
    /// went on): its go-ahead is answered and nothing waits on the person, so the page is told the session was not answered
    /// and does not wait.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("completed")]
    public async Task A_session_no_longer_parked_has_only_its_go_ahead_answered(string state)
    {
        var ledger = new Ledger { Records = Records(Record("s1", state)) };
        var (loop, module) = await UpAsync(ledger);

        var answered = await AnswerAsync(module, "SESSION_GO_AHEAD", new { id = "s1", ask = "a1", number = 1, approved = true });

        Assert.Equal((false, null, false), Park(answered));
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

        Assert.Equal((false, "superseded", false),
            (answered.GetProperty("sent").GetBoolean(), answered.GetProperty("why").GetString(), answered.GetProperty("waits").GetBoolean()));
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

    /// <summary>What became of the park, as the page reads it: whether it goes on, when, and whether it still waits on the person.</summary>
    private static (bool Sent, string? Reaches, bool Waits) Park(JsonElement answered) => (
        answered.GetProperty("sent").GetBoolean(),
        answered.GetProperty("reaches").ValueKind == JsonValueKind.Null ? null : answered.GetProperty("reaches").GetString(),
        answered.GetProperty("waits").GetBoolean());

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
    /// A local host standing in: the records as given; the go-ahead door, which approves or refuses as asked by default and,
    /// where a test says this answer leaves none of the park's go-aheads open (<see cref="AfterGoAhead"/>), keeps the park's
    /// blank answer, as the service decides, unless the caller says <c>goesOn: false</c>; and every other write, heard. Each
    /// write is heard by its path and body, in the order it arrived.
    /// </summary>
    private sealed class Ledger : HttpMessageHandler
    {
        private readonly List<(string Path, string Body)> _posts = [];

        public string Records { get; set; } = "[]";

        /// <summary>
        /// The records once the go-ahead's door sent a park on: this answer was the last one open of its go-aheads. Null where
        /// one is still open, so the door keeps nothing.
        /// </summary>
        public string? AfterGoAhead { get; init; }

        /// <summary>The go-ahead door's answer in place of the service's decision: a refusal, by the ask and the number.</summary>
        public Func<string, int, (HttpStatusCode Status, string Body)>? GoAhead { get; init; }

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
                    Heard(path, body, GoAhead is { } refused
                        ? refused(Uri.UnescapeDataString(ask), int.Parse(number))
                        : Decided(Uri.UnescapeDataString(ask), int.Parse(number), body)),
                ("POST", _) => Heard(path, body, (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}""")),
                _ => (HttpStatusCode.NotFound, ""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        /// <summary>
        /// The go-ahead door as the service answers it (GOAHEAD2): the answer kept, and, unless the caller answers the park
        /// itself, the park's blank answer where this was the last one open, its sentence saying which.
        /// </summary>
        private (HttpStatusCode, string) Decided(string ask, int number, string body)
        {
            using var sent = JsonDocument.Parse(body);
            var said = sent.RootElement.GetProperty("answer").GetString();
            var goesOn = !sent.RootElement.TryGetProperty("goesOn", out var flag) || flag.ValueKind != JsonValueKind.False;
            var parks = !goesOn ? "" : AfterGoAhead is null
                ? " Session `s1` is still waiting on you for go-ahead 2."
                : " Session `s1` was waiting on you for its go-aheads: it goes on.";
            if (goesOn && AfterGoAhead is { } after) Records = after;
            return (HttpStatusCode.OK, $$"""{"ask":{"id":"{{ask}}"},"message":"Go-ahead {{number}} on ask `#{{ask}}` is {{said}}.{{parks}}"}""");
        }

        private (HttpStatusCode, string) Heard(string path, string body, (HttpStatusCode, string) answer)
        {
            lock (_posts) _posts.Add((path, body));
            return answer;
        }
    }
}
