using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MSG1e2 (D137's MSG1e note, design §5.2): the shell's loop answers a terminal's <c>daoris-driver sessions say</c> with the
/// screen's judge, <see cref="SessionWords"/>, as the box's words are judged: kept on a parked or ended record and shown at
/// once as the terminal's, the loop nudged; held at a running session's door; held in the shell while a session winds up,
/// never asked again; and refused by the one table of what never goes on. A conversation the window runs hearing a
/// terminal needs a real process, so that row is <see cref="DriverModuleConversationProcessTests"/>'.
/// </summary>
public sealed class SessionWordsTerminalTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    /// <summary>
    /// Words to a session of this machine's that parked or ended go to the say door as the box's do; the conversation shows
    /// them at once under the id the record gave them, said at the terminal; the loop is nudged; and the answer carries the
    /// id, which the asker follows to say whether the same session took them.
    /// </summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("stopped")]
    [InlineData("awaiting-person")]
    public async Task A_terminals_words_to_a_parked_or_ended_session_are_kept_shown_as_the_terminals_and_the_loop_nudged(string state)
    {
        var service = new StandIn { Records = Records(Record("s1", state)) };
        var loop = await UpAsync(service);

        var held = await loop.Words.HoldAsync(Say("s1", "also the changelog"), CancellationToken.None);

        Assert.Equal((true, "resume", (string?)null, "w1"), (held.Sent, held.Reaches, held.Why, held.Word));
        Assert.Equal(["/api/sessions/s1/say"], service.Posts.Select(post => post.Path));
        var shown = Assert.Single(loop.Events.Page("s1").Events);
        Assert.Equal(
            (SessionEventKind.User, "person", "w1", "also the changelog", "resume", "terminal"),
            (shown.Kind, shown.Origin, shown.Id, shown.Text, shown.Reaches, shown.Door));
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// 🔴 Words said as a session winds up (the say door answers <c>running</c>) are held in the shell, as the box's are, and
    /// the asker is told they are held: never <c>running</c>, which had it ask again for ten seconds and give up. Once the
    /// record moves through this machine's client, they are kept, shown as the terminal's, and the loop nudged.
    /// </summary>
    [Fact]
    public async Task A_terminals_words_said_as_a_session_winds_up_are_held_rather_than_asked_again()
    {
        var ended = false;
        var service = new StandIn
        {
            Records = Records(Record("s1", "working")),
            Said = id => ended
                ? (HttpStatusCode.OK, Kept(id, "w7", "and the readme"))
                : (HttpStatusCode.Conflict, """{"error":"Session `s1` is working.","refusal":"running"}"""),
        };
        var loop = await UpAsync(service);

        var held = await loop.Words.HoldAsync(Say("s1", "and the readme"), CancellationToken.None);

        Assert.Equal((true, "resume", (string?)null), (held.Sent, held.Reaches, held.Why));
        Assert.Null(held.Word);
        Assert.Empty(loop.Events.Page("s1").Events);

        ended = true;
        await loop.Service!.AdvanceAsync("s1", "completed", note: "the quest reached done.");

        await UntilAsync(() => loop.Events.Page("s1").Events.Count == 1);
        var shown = Assert.Single(loop.Events.Page("s1").Events);
        Assert.Equal(("w7", "resume", "terminal"), (shown.Id, shown.Reaches, shown.Door));
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// A driven session running here hears a terminal's words at its door, as the box's (D136): its inbox holds them, the
    /// answer says when they reach it, and once it took them they are kept on its ask as the box's are (DRIFT1a2).
    /// </summary>
    [Fact]
    public async Task A_terminals_words_to_a_driven_session_running_here_are_held_at_its_door_and_kept_on_its_ask()
    {
        var service = new StandIn();
        var loop = await UpAsync(service);
        var inbox = loop.Processes.OpenInbox("s1");
        inbox.Attach(() => Task.CompletedTask, _ => Task.FromResult("end_turn"));

        var held = await loop.Words.HoldAsync(Say("s1", "the level file moved"), CancellationToken.None);

        Assert.Equal((true, "next-step", (string?)null), (held.Sent, held.Reaches, held.Why));
        Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);
        await UntilAsync(() => service.Posts.Count > 0);
        var (path, body) = Assert.Single(service.Posts);
        Assert.Equal("/api/sessions/s1/added", path);
        using var words = JsonDocument.Parse(body);
        Assert.Equal("the level file moved", words.RootElement.GetProperty("text").GetString());
    }

    /// <summary>
    /// What never goes on is refused by the one table the terminal judges by (<see cref="WordsNever"/>), before anything is
    /// posted: a session that stood down, Ask Daoris's own conversation, an intake, and a session whose quest went on later.
    /// </summary>
    [Theory]
    [InlineData("""{"id":"s1","repository":"engine","state":"stood-down","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}""", "stood-down")]
    [InlineData("""{"id":"s1","repository":"daoris:help","state":"completed","kind":"chat","created":"2026-10-03T08:00:00Z"}""", "help")]
    [InlineData("""{"id":"s1","repository":"ask #a1","state":"completed","ask":"a1","kind":"driven","created":"2026-10-03T08:00:00Z"}""", "intake")]
    [InlineData("""{"id":"s1","repository":"engine","state":"failed","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"},{"id":"s2","repository":"engine","state":"completed","quest":"q1","kind":"driven","created":"2026-10-03T09:00:00Z"}""", "superseded")]
    public async Task What_never_goes_on_is_refused_by_the_one_table_and_nothing_is_kept(string records, string why)
    {
        var service = new StandIn { Records = $"[{records}]" };
        var loop = await UpAsync(service);

        var held = await loop.Words.HoldAsync(Say("s1", "one more thing"), CancellationToken.None);

        Assert.Equal((false, (string?)null, why), (held.Sent, held.Reaches, held.Why));
        Assert.Empty(service.Posts);
        Assert.Empty(loop.Events.Page("s1").Events);
        Assert.Equal(0, loop.Nudges);
    }

    /// <summary>
    /// The say door's refusal reaches the terminal by its code and with its sentence, which the asker prints where the code
    /// is none it words itself; its word for a teammate's record is the page's <c>teammate</c>.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "no-words", "no-words")]
    [InlineData(HttpStatusCode.Conflict, "not-ours", "teammate")]
    public async Task The_say_doors_refusal_reaches_the_terminal_with_its_sentence(HttpStatusCode status, string refusal, string why)
    {
        var service = new StandIn
        {
            Records = Records(Record("s1", "completed")),
            Said = _ => (status, $$"""{"error":"Nothing to keep for `s1`.","refusal":"{{refusal}}"}"""),
        };
        var loop = await UpAsync(service);

        var held = await loop.Words.HoldAsync(Say("s1", " "), CancellationToken.None);

        Assert.Equal((false, why, "Nothing to keep for `s1`."), (held.Sent, held.Why, held.Message));
    }

    /// <summary>
    /// A terminal's files are kept by the verb before it asks, and named on the request by the names they are kept under:
    /// the shell reads them from where they lie, so the record names them as it names the box's, and a name nothing keeps
    /// is a sentence, never words sent without what they named.
    /// </summary>
    [Fact]
    public async Task A_terminals_files_are_read_from_where_the_verb_kept_them()
    {
        var service = new StandIn { Records = Records(Record("s1", "completed")) };
        var loop = await UpAsync(service);
        ChatFiles.Keep(loop.Home, "s1", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]);

        var held = await loop.Words.HoldAsync(Say("s1", "see the trace", "trace.txt"), CancellationToken.None);

        Assert.True(held.Sent);
        using (var said = JsonDocument.Parse(Assert.Single(service.Posts).Body))
        {
            Assert.Equal(["trace.txt"], said.RootElement.GetProperty("files").EnumerateArray().Select(name => name.GetString()));
        }

        var missing = await Assert.ThrowsAsync<DriverException>(
            () => loop.Words.HoldAsync(Say("s1", "and this", "gone.png"), CancellationToken.None));
        Assert.Contains("gone.png", missing.Message);
        Assert.Single(service.Posts);
    }

    /// <summary>
    /// Before the loop's service answers, a say for a session nothing here runs is a sentence the asker prints, as the box's
    /// is: nothing could keep the words yet.
    /// </summary>
    [Fact]
    public async Task A_terminals_words_before_the_loop_is_up_say_so()
    {
        var held = await Loop().Words.HoldAsync(Say("s1", "hello"), CancellationToken.None);

        Assert.Equal((false, (string?)null), (held.Sent, held.Why));
        Assert.Contains("still coming up", held.Message);
    }

    /// <summary>
    /// The watch a terminal's request reaches answers it through the screen's judge: a say written to the request folder is
    /// taken, its words kept and shown as the terminal's, and its answer written beside it with the kept word's id.
    /// </summary>
    [Fact]
    public async Task A_watch_handed_the_screens_judge_answers_a_terminals_say_beside_its_request()
    {
        var service = new StandIn { Records = Records(Record("s1", "completed")) };
        var loop = await UpAsync(service);
        await using var watch = new SessionRequestWatch(Home, loop.Processes, () => loop.Service, every: TimeSpan.FromHours(1))
        {
            Say = loop.Words.HoldAsync,
        };
        var requests = new SessionRequests(Home);
        var request = Say("s1", "also the changelog");
        requests.Write(request);

        var honoured = await watch.HonourAsync();

        Assert.Equal("s1", Assert.Single(honoured).Session);
        var answer = requests.AnswerOf(request)!;
        Assert.Equal((true, "resume", "w1"), (answer.Sent, answer.Reaches, answer.Word));
        Assert.Equal("terminal", Assert.Single(loop.Events.Page("s1").Events).Door);
    }

    /// <summary>
    /// The shell's loop hands its watch the screen's judge (MSG1e2): read from the loop's source, since the watch is built
    /// only once the loop's host is up. <c>DriverModuleSessionsTests</c> holds the watch's construction itself.
    /// </summary>
    [Fact]
    public void The_shells_watch_takes_a_terminals_words_with_the_screens_judge()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root!.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Modules", "DriverLoop.cs"));

        var watch = source[source.IndexOf("new SessionRequestWatch(homeDirectory", StringComparison.Ordinal)..];
        watch = watch[..watch.IndexOf(';')];
        Assert.Contains("Say = Words.HoldAsync", watch);
    }

    private async Task<DriverLoop> UpAsync(StandIn service)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        return loop;
    }

    /// <summary>A terminal's say as the verb writes it: the words, the names its files are kept under, and its own key.</summary>
    private static SessionRequest Say(string id, string text, params string[] files) =>
        new(id, SessionMove.Say, Now) { Text = text, Files = files, Key = SessionRequests.NewKey() };

    private static string Records(params string[] records) => $"[{string.Join(",", records)}]";

    private static string Record(string id, string state, string quest = "q1") =>
        $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","quest":"{{quest}}","kind":"driven","created":"2026-10-03T08:00:00Z"}""";

    /// <summary>The say door's yes: the record, its sentence, and the word as kept, with the id the record gave it.</summary>
    private static string Kept(string id, string word = "w1", string text = "also the changelog") => $$"""
        {"session":{"id":"{{id}}","state":"completed"},"message":"Kept for session `{{id}}` to go on with.",
         "said":{"id":"{{word}}","text":"{{text}}","at":"2026-10-03T09:00:00+00:00","files":[],"reopens":true}
        }
        """;

    /// <summary>
    /// A local host standing in: the records as given, the say door as given (it keeps the words it is sent by default),
    /// the state door, which moves a record as asked, and the added door. Each write but a move is heard by its path and body.
    /// </summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private readonly List<(string Path, string Body)> _posts = [];

        public string Records { get; init; } = "[]";

        public Func<string, (HttpStatusCode Status, string Body)> Said { get; init; } = id => (HttpStatusCode.OK, Kept(id));

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
                ("POST", ["api", "sessions", var id, "say"]) => Heard(path, body, Said(Uri.UnescapeDataString(id))),
                ("POST", _) => Heard(path, body, (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}""")),
                _ => (HttpStatusCode.NotFound, ""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        private (HttpStatusCode, string) Heard(string path, string body, (HttpStatusCode, string) answer)
        {
            lock (_posts) _posts.Add((path, body));
            return answer;
        }
    }
}
