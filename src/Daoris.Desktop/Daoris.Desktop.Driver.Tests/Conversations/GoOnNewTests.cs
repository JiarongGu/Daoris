using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1g's door (D137 §2.2, D50): *Go on in a new session*. Words a resume holds while the account its session ran on cools
/// go on now in a new session, handed them, without that conversation. The person's choice is kept by the words' ids with when
/// it was given (D143 point 4), and the driver's next look carries the words on; what cannot is refused by a code, before
/// anything is kept. The library's door, which the modules' route and <c>daoris-driver sessions go-on-new</c> both call.
/// </summary>
/// <remarks>In-process: the service is a stand-in reached through the real client, and the cool-off is handed in. The fast half.</remarks>
public sealed class GoOnNewTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-go-on-new-" + Guid.NewGuid().ToString("N")[..8]);

    public GoOnNewTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly CoolingEntry Cooling = new("claude-code", "account-1", T0.AddHours(3), true, "session", T0.AddHours(-1), "s0");

    private static JsonObject Word(string id) => new()
    {
        ["id"] = id, ["text"] = "Also log the port.", ["at"] = T0.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
    };

    private static JsonObject Record(
        string id, string state = "completed", string? quest = "q1", string kind = "driven", string? ask = null,
        string repository = "engine", int minutes = 0, params string[] said) => new()
        {
            ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest, ["ask"] = ask,
            ["adapter"] = "claude-code", ["profile"] = "account-1", ["created"] = T0.AddMinutes(minutes).ToString("O"),
            ["updated"] = T0.AddMinutes(minutes + 5).ToString("O"), ["said"] = new JsonArray([.. said.Select(Word)]),
        };

    private static JsonObject Quest(string id, string status) => new()
    {
        ["id"] = id, ["from"] = "ask #a1", ["to"] = "engine", ["title"] = $"Quest {id}", ["body"] = "A body.", ["status"] = status,
    };

    private Task<GoOnNewAnswer> AskAsync(Room room, string id, CoolingEntry? cooling = null)
    {
        var client = room.Client();
        return GoOnNew.AskAsync(client, _home, (_, _) => cooling ?? Cooling, id, T0.AddMinutes(30));
    }

    /// <summary>
    /// 🔴 The words a resume holds while its account cools go on in a new session: the choice is kept by the ids of the words
    /// waiting, with when it was made, and the answer says what comes of it and what it costs.
    /// </summary>
    [Fact]
    public async Task Words_held_while_their_account_cools_are_chosen_to_go_on_in_a_new_session()
    {
        var room = new Room([Record("s1", said: ["w1", "w2"])], [Quest("q1", "Taken")]);

        var answer = await AskAsync(room, "s1");

        Assert.Equal((true, (string?)null), (answer.Sent, answer.Why));
        Assert.Equal(
            "it goes on in a new session at the driver's next look, handed your words; that session starts without this one's conversation.",
            answer.Message);
        var choice = new NewSessionChoices(_home).Read("s1");
        Assert.Equal(["w1", "w2"], choice!.Said);
        Assert.Equal(T0.AddMinutes(30), choice.At);
    }

    /// <summary>A park answered while its account cools may go on in a new session too: its answer is its words (D83).</summary>
    [Fact]
    public async Task An_answered_park_may_go_on_in_a_new_session()
    {
        var room = new Room([Record("s1", state: "awaiting-person", said: ["w1"])], [Quest("q1", "Taken")]);

        Assert.True((await AskAsync(room, "s1")).Sent);
    }

    public static TheoryData<string, string, string> Refusals => new()
    {
        { "nobody00", WordsNever.NotFound, "no session here is nobody00." },
        { "laptop/s9", WordsNever.Teammate, "laptop/s9 ran on laptop, where its conversation is; nothing said here reaches it." },
        { "1ntake00", WordsNever.Intake, "1ntake00 is an intake; answer its ask #a1 instead: publish it or close it." },
        { "st00d000", WordsNever.StoodDown, "it stood down: #q3 is someone else's, so it has nothing to go on with." },
        { "e4rl1er0", WordsNever.Superseded, "#q2 went on in a later session here, l4t3r000, and its words go there." },
        { "qu1et000", GoOnNew.NoWords, "no words wait on qu1et000 to go on with: `daoris-driver sessions say qu1et000 \"…\"` says some." },
        { "w0rk1ng0", GoOnNew.Running, "w0rk1ng0 is working: words reach it there, so it needs no new session." },
        { "ch4t0000", GoOnNew.Conversation,
            "ch4t0000 is a conversation, which nothing carries on by itself: start a conversation with these words instead: "
            + "`daoris-driver sessions start-from ch4t0000`." },
        { "cl0sed00", GoOnNew.Closed,
            "#q4 has closed, so nothing carries its session's words on by itself: start a conversation with them instead: "
            + "`daoris-driver sessions start-from cl0sed00`." },
    };

    /// <summary>
    /// What cannot go on in a new session is refused by its code before anything is kept: D137 §2.2's nevers, a session with
    /// nothing waiting or still running, and what nothing carries on by itself, a conversation and a closed quest's session,
    /// which are pointed at *Start a conversation with these words* instead.
    /// </summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task What_cannot_go_on_in_a_new_session_is_refused_by_its_code_and_nothing_is_kept(string id, string why, string message)
    {
        var room = new Room(
            [
                Record("laptop/s9", state: "completed", said: ["w1"]),
                Record("1ntake00", state: "awaiting-person", quest: null, kind: "chat", ask: "a1", repository: "ask #a1", said: ["w1"]),
                Record("st00d000", state: "stood-down", quest: "q3", said: ["w1"]),
                Record("e4rl1er0", state: "failed", quest: "q2", minutes: 0, said: ["w1"]),
                Record("l4t3r000", state: "completed", quest: "q2", minutes: 10),
                Record("qu1et000", state: "completed", quest: "q5"),
                Record("w0rk1ng0", state: "working", quest: "q6", said: ["w1"]),
                Record("ch4t0000", state: "completed", quest: null, kind: "chat", said: ["w1"]),
                Record("cl0sed00", state: "completed", quest: "q4", said: ["w1"]),
            ],
            [Quest("q2", "Taken"), Quest("q3", "Taken"), Quest("q4", "Done"), Quest("q5", "Taken"), Quest("q6", "Taken")]);

        var answer = await AskAsync(room, id);

        Assert.Equal((false, why, message), (answer.Sent, answer.Why, answer.Message));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_home, "sessions"), "*" + NewSessionChoices.Suffix));
    }

    /// <summary>
    /// An account that is not cooling holds nothing a new session would leave: the same session goes on with the words at the
    /// driver's next look, which is better than any new one, so nothing is kept.
    /// </summary>
    [Fact]
    public async Task Words_whose_account_is_not_cooling_are_not_moved_to_a_new_session()
    {
        var room = new Room([Record("s1", said: ["w1"])], [Quest("q1", "Taken")]);
        var client = room.Client();

        var answer = await GoOnNew.AskAsync(client, _home, (_, _) => null, "s1", T0);

        Assert.Equal((false, GoOnNew.NotCooling), (answer.Sent, answer.Why));
        Assert.Equal(
            "the account s1 ran on is not cooling, so the same session goes on with your words at the driver's next look.",
            answer.Message);
        Assert.Null(new NewSessionChoices(_home).Read("s1"));
    }

    // ——— The kept choice.

    private static PriorSession Waiting(params string[] ids) =>
        new("s1", "/trees/s-1", "completed", "The quest reached done.", "engine")
        {
            Said = [.. ids.Select(id => new SaidWordView(id, "Also log the port.", T0, [], true))],
        };

    /// <summary>
    /// The choice covers the record while any word it named still waits, a word said since among them, since a carry-on takes
    /// every word waiting; once they are all taken it covers nothing, and a word said later waits for the account again.
    /// </summary>
    [Fact]
    public void The_choice_covers_the_record_while_a_word_it_named_still_waits()
    {
        var choices = new NewSessionChoices(_home);
        choices.Choose("s1", ["w1"], T0);

        Assert.True(choices.Covers(Waiting("w1")));
        Assert.True(choices.Covers(Waiting("w1", "w2")));
        Assert.False(choices.Covers(Waiting("w3")));
        Assert.False(choices.Covers(Waiting()));

        choices.Clear("s1");
        Assert.False(choices.Covers(Waiting("w1")));
    }

    /// <summary>A file that does not read is no choice, as a mark that does not read is no mark: the words wait for the account.</summary>
    [Fact]
    public void A_choice_that_does_not_read_is_none()
    {
        File.WriteAllText(new NewSessionChoices(_home).PathOf("s1"), "{ not json");

        Assert.Null(new NewSessionChoices(_home).Read("s1"));
        Assert.False(new NewSessionChoices(_home).Covers(Waiting("w1")));
    }

    // ——— The terminal's door (D50): `daoris-driver sessions go-on-new <id>`.

    [Fact]
    public void The_terminal_reads_one_session_s_id_and_its_usage_says_the_verb()
    {
        var ask = SessionsCommand.Read(["go-on-new", "s1"], out var problem);
        Assert.Null(problem);
        Assert.Equal(("go-on-new", "s1"), (ask!.Verb, ask.Ids[0]));

        Assert.Null(SessionsCommand.Read(["go-on-new"], out problem));
        Assert.Equal("`go-on-new` takes one session's id.", problem);
        Assert.Null(SessionsCommand.Read(["go-on-new", "s1", "s2"], out _));
        Assert.Contains("sessions go-on-new <id>", SessionsCommand.Usage);
        Assert.Contains("sessions go-on-new <id>", DriverCommand.Usage);
    }

    /// <summary>One line says what comes of it: 0 kept for the next look, 1 refused with its sentence.</summary>
    [Fact]
    public async Task The_terminal_says_what_comes_of_it_in_one_line()
    {
        var room = new Room([Record("s1", said: ["w1"]), Record("cl0sed00", quest: "q4", said: ["w1"])], [Quest("q1", "Taken"), Quest("q4", "Done")]);
        var world = new SessionsWorld(room.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null) { CoolingOf = (_, _) => Cooling };

        var output = new StringWriter();
        Assert.Equal(0, await SessionsCommand.RunAsync(SessionsCommand.Read(["go-on-new", "s1"], out _)!, world, output));
        Assert.Equal(
            "sessions: it goes on in a new session at the driver's next look, handed your words; that session starts without this one's conversation.\n",
            output.ToString().ReplaceLineEndings("\n"));

        output = new StringWriter();
        Assert.Equal(1, await SessionsCommand.RunAsync(SessionsCommand.Read(["go-on-new", "cl0sed00"], out _)!, world, output));
        Assert.StartsWith("sessions: #q4 has closed, so nothing carries its session's words on by itself", output.ToString());
    }

    /// <summary>The service's half of what the door reads: this machine's records, closed ones included, and every quest.</summary>
    private sealed class Room(IEnumerable<JsonObject> records, IEnumerable<JsonObject> quests) : HttpMessageHandler
    {
        private readonly List<JsonObject> _records = [.. records];
        private readonly List<JsonObject> _quests = [.. quests];

        public ServiceClient Client() => new("http://room.test", null, new HttpClient(this, disposeHandler: false));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var all = request.RequestUri.Query.Contains("includeClosed=true", StringComparison.Ordinal);
            static bool Live(JsonObject record) => (string)record["state"]! is "queued" or "starting" or "working" or "awaiting-person";
            static bool Open(JsonObject quest) => (string)quest["status"]! is "Open" or "Taken";
            return Task.FromResult(request.Method == HttpMethod.Get && path == "/api/sessions"
                ? Answer(new JsonArray([.. _records.Where(each => all || Live(each)).Select(each => each.DeepClone())]))
                : request.Method == HttpMethod.Get && path == "/api/quests"
                    ? Answer(new JsonArray([.. _quests.Where(each => all || Open(each)).Select(each => each.DeepClone())]))
                    : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"no such door"}""") });
        }

        private static HttpResponseMessage Answer(JsonNode body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
