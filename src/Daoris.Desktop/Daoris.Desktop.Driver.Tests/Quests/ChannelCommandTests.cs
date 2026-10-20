using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE2c (D167 points 6 to 8; the co-working design §2.4): the person's side of an ask's channel at a terminal,
/// <c>daoris-driver ask channel &lt;id&gt; [--to &lt;repository&gt;] [--re &lt;message&gt;] ["words"]</c>, the twin of the ask page's
/// <i>Channel</i> (D50). With no words it reads the channel and moves the person's mark; with words it posts them as the person's,
/// to everyone or one repository. A service stood in: the fast half (MOD8).
/// </summary>
public sealed class ChannelCommandTests
{
    private const string Channel = """
        {"ask":"a1","entry":"person","items":[
         {"kind":"line","at":"2026-10-14T09:00:00Z","code":"published","quest":"q1","name":"Read the figure","from":"ask #a1","to":"db"},
         {"kind":"message","at":"2026-10-14T09:02:00Z","id":"m1","person":false,"session":"s1","repository":"db","agent":"claude-code",
          "entry":"ask:a1/db","question":true,"open":true,"text":"Which column holds the figure?"},
         {"kind":"message","at":"2026-10-14T09:05:00Z","id":"m2","person":true,"to":"db","re":"m1","text":"The v3 one."}],
         "unread":2,"mark":"2026-10-14T09:00:00Z"}
        """;

    /// <summary>An ask's channel at a terminal reads as a read with its id, or a post with the words after the flags.</summary>
    [Fact]
    public void The_words_after_the_ask_are_a_read_or_a_post()
    {
        Assert.Equal(new ChannelAsk("a1", null, null, null), ChannelCommand.Read(["a1"], out var none));
        Assert.Null(none);
        Assert.Equal(new ChannelAsk("a1", "db", null, "use the v3 one"), ChannelCommand.Read(["#a1", "--to", "db", "use", "the", "v3", "one"], out _));
        Assert.Equal(new ChannelAsk("a1", null, "m1", "yes"), ChannelCommand.Read(["a1", "--re", "m1", "yes"], out _));
    }

    /// <summary>A flag with no words, or no ask at all, is a problem the host prints with the usage.</summary>
    [Theory]
    [InlineData("--to")]
    [InlineData("--re")]
    public void A_flag_with_no_value_or_no_words_or_no_ask_is_a_problem(string flag)
    {
        Assert.Null(ChannelCommand.Read([], out var noAsk));
        Assert.Contains("ask's id", noAsk);
        Assert.Null(ChannelCommand.Read(["a1", flag], out var noValue));
        Assert.Contains(flag, noValue);
        Assert.Null(ChannelCommand.Read(["a1", flag, "db"], out var noWords));
        Assert.Contains("words", noWords);
    }

    /// <summary>
    /// Reading prints each item as the page shows it, the person's own as *You*, a session's by repository and agent, Daoris's lines
    /// by their code, an open question marked; then moves the person's mark through the newest and says how many were unread.
    /// </summary>
    [Fact]
    public async Task Reading_prints_the_channel_and_moves_the_persons_mark()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath is "/api/asks/a1/channel" or "/api/asks/a1/channel/read"
            ? (HttpStatusCode.OK, Channel)
            : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        var exit = await ChannelCommand.RunAsync(new ChannelAsk("a1", null, null, null), service, output);

        Assert.Equal(0, exit);
        var text = output.ToString();
        Assert.Contains("Ask #a1's channel, 2 unread", text);
        Assert.Contains("Daoris: published `Read the figure` (quest q1) for db", text);
        Assert.Contains("db · claude-code → everyone (open question): Which column holds the figure?", text);
        Assert.Contains("You → db (re m1): The v3 one.", text);
        Assert.Equal(["GET /api/asks/a1/channel", "POST /api/asks/a1/channel/read"], standIn.Requests);
        Assert.Contains("\"through\":\"2026-10-14T09:05:00", standIn.Bodies[^1], StringComparison.Ordinal);
    }

    /// <summary>A channel nothing is on says so, and moves no mark.</summary>
    [Fact]
    public async Task An_empty_channel_says_so_and_moves_no_mark()
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK, """{"ask":"a1","entry":"person","items":[],"unread":0}"""));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        Assert.Equal(0, await ChannelCommand.RunAsync(new ChannelAsk("a1", null, null, null), service, output));

        Assert.Contains("nothing on ask #a1's channel yet", output.ToString());
        Assert.Equal(["GET /api/asks/a1/channel"], standIn.Requests);
    }

    /// <summary>Posting sends the words, who they are for and what they answer to the person's door, and says where they went.</summary>
    [Fact]
    public async Task Posting_sends_the_persons_words_to_the_channel_door()
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK,
            """{"message":{"kind":"message","at":"2026-10-14T09:06:00Z","id":"m3","person":true,"to":"db","re":"m1","text":"The v3 one."}}"""));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        var exit = await ChannelCommand.RunAsync(new ChannelAsk("a1", "db", "m1", "The v3 one."), service, output);

        Assert.Equal(0, exit);
        Assert.Equal(["POST /api/asks/a1/channel"], standIn.Requests);
        Assert.Contains("\"text\":\"The v3 one.\"", standIn.Bodies[0]);
        Assert.Contains("\"to\":\"db\"", standIn.Bodies[0]);
        Assert.Contains("\"re\":\"m1\"", standIn.Bodies[0]);
        Assert.Contains("Posted to ask #a1's channel, for db.", output.ToString());
    }

    /// <summary>
    /// 🔴 COWORK1e (D188 point 2; the message-urgency design §8.6): the person's post is urgent unless <c>--ordinary</c> asked it to
    /// wait for each turn's end; <c>--urgent</c> says the default aloud; the two together, or a level with no words, is a problem.
    /// </summary>
    [Fact]
    public void A_post_is_urgent_unless_asked_ordinary()
    {
        Assert.False(ChannelCommand.Read(["a1", "hello"], out _)!.Ordinary);
        Assert.Equal(new ChannelAsk("a1", "db", null, "when you can", Ordinary: true), ChannelCommand.Read(["a1", "--ordinary", "--to", "db", "when", "you", "can"], out _));
        Assert.Equal(new ChannelAsk("a1", null, null, "now please"), ChannelCommand.Read(["a1", "--urgent", "now", "please"], out _));

        Assert.Null(ChannelCommand.Read(["a1", "--ordinary", "--urgent", "hello"], out var both));
        Assert.Contains("two levels", both);
        Assert.Null(ChannelCommand.Read(["a1", "--ordinary"], out var noWords));
        Assert.Contains("words", noWords);
    }

    /// <summary>
    /// 🔴 COWORK1e: the post carries its level to the person's door, <c>urgent</c> false for <c>--ordinary</c> and true by default, and
    /// says the level the service kept; a host from before that keeps none is told nothing of it.
    /// </summary>
    [Theory]
    [InlineData(true, "false", ""","urgent":false""", "for everyone, ordinary: each session it reaches reads it when its turn ends.")]
    [InlineData(false, "true", ""","urgent":true""", "for everyone, urgent: each session it reaches reads it at its next step.")]
    [InlineData(false, "true", "", "Posted to ask #a1's channel, for everyone.")]
    public async Task A_post_carries_its_level_and_says_the_one_kept(bool ordinary, string sent, string kept, string said)
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK,
            $$$"""{"message":{"kind":"message","at":"2026-10-14T09:06:00Z","id":"m3","person":true,"text":"hello"{{{kept}}}}}"""));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        Assert.Equal(0, await ChannelCommand.RunAsync(new ChannelAsk("a1", null, null, "hello", ordinary), service, output));

        Assert.Contains($"\"urgent\":{sent}", standIn.Bodies[0]);
        Assert.Contains(said, output.ToString());
    }

    /// <summary>
    /// 🔴 COWORK1e (the design §8.4): a read names an urgent message's level, never an ordinary one's or one's from before, and under
    /// each message says what it reached in each repository it addresses, worded as the page's receipts; a line carries none.
    /// </summary>
    [Fact]
    public async Task Reading_names_the_urgent_level_and_what_each_message_reached()
    {
        const string channel = """
            {"ask":"a1","entry":"person","items":[
             {"kind":"line","at":"2026-10-14T09:00:00Z","code":"published","quest":"q1","name":"Read the figure","from":"ask #a1","to":"db"},
             {"kind":"message","at":"2026-10-14T09:02:00Z","id":"m1","person":true,"urgent":true,"text":"Stop: the column moved.",
              "receipts":[{"repository":"db","state":"taken"},{"repository":"web","state":"waiting"},{"repository":"api","state":"not-reached"}]},
             {"kind":"message","at":"2026-10-14T09:03:00Z","id":"m2","person":true,"to":"db","urgent":false,"text":"When you can.",
              "receipts":[{"repository":"db","state":"partly"}]},
             {"kind":"message","at":"2026-10-14T09:04:00Z","id":"m3","person":true,"text":"From before."}],
             "unread":0}
            """;
        var standIn = new StandIn(_ => (HttpStatusCode.OK, channel));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        Assert.Equal(0, await ChannelCommand.RunAsync(new ChannelAsk("a1", null, null, null), service, output));

        var lines = output.ToString().Split(Environment.NewLine);
        Assert.Contains("You → everyone (urgent): Stop: the column moved.", lines[2]);
        Assert.Equal("      What it reached: taken by db; waiting for web; it never reached api's session: the ask's work ended first.", lines[3]);
        Assert.Contains("You → db: When you can.", lines[4]);
        Assert.Equal("      What it reached: partly handed to db.", lines[5]);
        Assert.Contains("You → everyone: From before.", lines[6]);
        Assert.Equal(8, lines.Length);
        Assert.Null(ChannelCommand.Reached(new ChannelItemView(ChannelItemView.Line, DateTimeOffset.UnixEpoch)));
    }

    /// <summary>Each refusal is a code the service answers, said in a sentence, exit 1; a post to everyone says so.</summary>
    [Theory]
    [InlineData("empty", "words are empty")]
    [InlineData("too-long", "at most 4,000 characters")]
    [InlineData("bad-to", "not one of the ask's repositories")]
    [InlineData("bad-re", "no message to answer")]
    [InlineData("not-found", "No ask #a1")]
    public async Task A_refusal_is_said_in_a_sentence_by_its_code(string code, string said)
    {
        var standIn = new StandIn(_ => (code == "not-found" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, $$"""{"error":"{{code}}","code":"{{code}}"}"""));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var output = new StringWriter();

        var exit = await ChannelCommand.RunAsync(new ChannelAsk("a1", "nowhere", null, "hello"), service, output);

        Assert.Equal(1, exit);
        Assert.Contains(said, output.ToString());
    }

    /// <summary>The post's answer and the person's read, as the page's route reads them: the open mark, the question and the person's flag.</summary>
    [Fact]
    public async Task The_channel_is_read_with_its_open_questions_and_the_mark()
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK, Channel));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var read = await service.ChannelAsync("#a1", null, since: DateTimeOffset.Parse("2026-10-14T09:01:00Z"));

        Assert.StartsWith("GET /api/asks/a1/channel?since=2026-10-14T09%3A01%3A00", Assert.Single(standIn.Requests));
        Assert.Equal(2, read!.Unread);
        Assert.True(read.Items[1].Open);
        Assert.True(read.Items[2].Person);
        Assert.Equal(DateTimeOffset.Parse("2026-10-14T09:00:00Z"), read.Mark);
    }

    /// <summary>
    /// The page names another session's claim by the opening of Daoris's words around it (<c>CHANNEL_CLAIM_OPENING</c> in
    /// <c>work/channel.ts</c>), since the record keeps them as the driver's own, so the two are one string: a twin read as text.
    /// </summary>
    [Fact]
    public void The_claim_opens_with_the_words_the_page_names_it_by()
    {
        var claim = ChannelText.Claim(new ChannelItemView(ChannelItemView.Message, DateTimeOffset.Parse("2026-10-14T09:02:00Z"))
        {
            Id = "m1", Repository = "db", Agent = "claude-code", Text = "Which column holds the figure?",
        });
        var page = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Web", "src", "work", "channel.ts"));

        Assert.StartsWith("Another session says, on the ask's channel (", claim);
        Assert.Contains("CHANNEL_CLAIM_OPENING = \"Another session says, on the ask's channel\"", page);
    }

    /// <summary>
    /// 🔴 SESSCTX1b (the session-context design §7): a channel's message is marked as one on its <c>user</c> event by a field of its
    /// own, the message's id, as an opinion's is, so the page and the brief tell it by the field and never by Daoris's English. Another
    /// session's is Daoris's (<c>target</c>), the person's theirs; a word that came by no channel carries none.
    /// </summary>
    [Fact]
    public void A_channel_s_message_is_marked_on_its_user_event_by_its_own_field()
    {
        var at = DateTimeOffset.Parse("2026-10-14T09:02:00Z");
        var claim = new ChannelItemView(ChannelItemView.Message, at) { Id = "m1", Repository = "db", Agent = "claude-code", Text = "Which column?" };
        var persons = new ChannelItemView(ChannelItemView.Message, at) { Id = "m2", Person = true, Text = "Use the second." };

        var theirs = ChatMessage.Words(new ChatMessage(ChannelText.Claim(claim), []) { Channel = claim, Id = "w1" });
        var person = ChatMessage.Words(new ChatMessage(persons.Text!, []) { Channel = persons, Id = "w2" });
        var plain = ChatMessage.Words(new ChatMessage("Go on.", []) { Id = "w3" });

        Assert.Equal(("target", "m1"), (theirs.Origin, theirs.Channel));
        Assert.Equal(("person", "m2"), (person.Origin, person.Channel));
        Assert.Null(plain.Channel);
        // Kept on the record by its own name, and a record from before reads none.
        var line = System.Text.Json.JsonSerializer.Serialize(theirs, SessionEventText.Json);
        Assert.Contains("\"channel\":\"m1\"", line);
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<SessionEvent>("""{"kind":"user","origin":"target","text":"x"}""", SessionEventText.Json)!.Channel);
    }

    /// <summary>The usage the host prints, and the one the bare invocation prints, spell the verb.</summary>
    [Fact]
    public void The_usage_spells_the_verb()
    {
        Assert.Contains("ask channel <id> [--to <repository>] [--re <message>] [--ordinary] [\"…\"]", DriverCommand.Usage);
        Assert.Contains("ask channel <id>", ChannelCommand.Usage);
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, """{"error":"none"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
