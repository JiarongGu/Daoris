using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation over the bridge where a real process holds the session (MOD8): what a tracked session that takes no
/// person's line answers, and a driven session's inbox beside its process. Apart from
/// <see cref="DriverModuleConversationTests"/> so that class, which starts none, runs in the fast half (MSG1d).
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleConversationProcessTests : DriverModuleBridge
{
    /// <summary>
    /// 🔴 A session that takes no input — an intake, one turn, whose stdin on the protocol door
    /// carries the driver's own frames (INT4h) — is REFUSED in the driver's words, never answered
    /// false. False means "it ended while you were typing", and a page that read it so would tell the
    /// person something untrue about a session that is running fine. Finishing it is refused too:
    /// closing that stream would end the protocol's turn, not a conversation.
    /// </summary>
    [Theory]
    [InlineData("SESSION_INPUT")]
    [InlineData("END_CHAT")]
    [InlineData("CANCEL_TURN")]
    [InlineData("SESSION_QUEUE_EDIT")]
    public async Task A_session_that_takes_no_input_is_refused_in_the_drivers_words(string type)
    {
        var loop = Loop();
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track(
            "i1", process, refusesInput: "ask #a1b2c3's intake takes no messages.");

        try
        {
            var refusal = await RefusalAsync(new DriverModule(Bus, loop), type, new { id = "i1", session = "i1", text = "hello" });

            Assert.Contains(Refusals.DriverRefused, refusal);
            Assert.Contains("ask #a1b2c3's intake takes no messages.", refusal);
        }
        finally
        {
            loop.Processes.Stop("i1");
        }
    }

    /// <summary>
    /// SESS3: a driven session on the protocol door hears what the person adds — its inbox holds the
    /// words ahead of INT4i's refusal, the queue says it is listening and what waits, and the stop sends
    /// what is held now, withdrawing nothing. A finish is still refused: its stream is the driver's.
    /// </summary>
    [Fact]
    public async Task A_driven_session_that_listens_holds_what_the_person_adds_and_sends_it_on_a_stop()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track("s1", process, refusesInput: "the session on quest #q1 takes no line in its stream.");
        var inbox = loop.Processes.OpenInbox("s1");
        var stops = 0;
        inbox.Attach(() => { stops++; return Task.CompletedTask; });

        try
        {
            var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "the level file moved" });
            Assert.True(sent.GetProperty("sent").GetBoolean());

            var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
            Assert.True(queue.GetProperty("listening").GetBoolean());
            Assert.True(queue.GetProperty("taking").GetBoolean());
            Assert.Equal("the level file moved", Assert.Single(queue.GetProperty("queued").EnumerateArray()).GetProperty("text").GetString());

            var stop = await AnswerAsync(module, "CANCEL_TURN", new { id = "s1" });
            Assert.True(stop.GetProperty("cancelled").GetBoolean());
            Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
            Assert.Equal(1, stops);
            Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);

            Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "END_CHAT", new { id = "s1" }));

            // Closed, it hears nothing more — and the page is told it is not listening.
            inbox.TakeOrClose();
            var closed = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
            Assert.False(closed.GetProperty("listening").GetBoolean());
        }
        finally
        {
            loop.Processes.Stop("s1");
        }
    }

    /// <summary>
    /// MSG1e2 (D137 §5.2): a terminal's words reach a conversation the window runs, as the box's do. Its turns take them
    /// through the loop's chat runner, here a text door's process taking the person's line, and the asker is told it takes
    /// them as its next turn; nothing is posted to the say door, since the conversation is running.
    /// </summary>
    [Fact]
    public async Task A_terminals_words_reach_a_conversation_the_window_runs()
    {
        var heard = Path.Combine(Home, "heard.txt");
        var loop = Loop();
        var posts = new List<string>();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Answering(posts)));
        using var chat = new ChatRunner(service, loop.Harnesses.Adapters, loop.Home, loop.Processes, loop.Output, loop.Harnesses, loop.Events);
        await loop.ComeUpAsync(service, chat);
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList =
            {
                "-e",
                "require('readline').createInterface({ input: process.stdin }).on('line', (line) => require('fs').appendFileSync(process.argv[1], line + '\\n'));",
                heard,
            },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track("c1", process);

        try
        {
            var held = await loop.Words.HoldAsync(
                new SessionRequest("c1", SessionMove.Say, DateTimeOffset.UtcNow) { Text = "and the readme", Key = SessionRequests.NewKey() },
                CancellationToken.None);

            Assert.Equal((true, (string?)null, (string?)null), (held.Sent, held.Reaches, held.Why));
            await UntilAsync(() => File.Exists(heard) && File.ReadAllText(heard).Contains("and the readme", StringComparison.Ordinal));
            lock (posts) Assert.DoesNotContain(posts, path => path.EndsWith("/say", StringComparison.Ordinal));
        }
        finally
        {
            loop.Processes.Stop("c1");
        }
    }

    /// <summary>
    /// FOUNDATION1b (D137, D188; the foundation design §10): the shell restarts while a record winds up, before the take. A real process
    /// holds the session whose record the service still answers as working, so the person's word is refused <c>running</c> and held with
    /// its key; the process ends, the shell is replaced on the same home, the record ends, and the word is delivered once, under its key:
    /// the producer's retry afterwards is answered with it. After the take it is row p's settlement, not this case's.
    /// </summary>
    [Fact]
    public async Task A_word_held_as_a_record_winds_up_is_delivered_once_across_a_shell_restart()
    {
        var winding = true;
        var service = new KeyedSayService
        {
            Records = """[{"id":"s1","repository":"engine","state":"working","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}]""",
            Winding = _ => winding,
        };
        var before = Loop();
        await before.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = before.Processes.Track("s1", process);

        try
        {
            var said = await before.Words.SayAsync(
                "s1", "and the readme", [], preface: null, "screen", CancellationToken.None, key: "press/9d2e");
            Assert.True(said!.Sent);
            Assert.Single(service.Said("s1"), post => post.Refused);
        }
        finally
        {
            before.Processes.Stop("s1");
        }

        before.Stop();
        winding = false;
        service.Records = """[{"id":"s1","repository":"engine","state":"completed","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}]""";
        var after = Loop();
        await after.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        await after.Service!.AdvanceAsync("s1", "completed", note: "the quest reached done.");
        await UntilAsync(() => service.Kept("s1") == 1);
        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));

        var retried = await after.Words.SayAsync(
            "s1", "and the readme", [], preface: null, "screen", CancellationToken.None, key: "press/9d2e");

        Assert.True(retried!.Sent);
        Assert.Equal(1, service.Kept("s1"));
        after.Stop();
    }

    /// <summary>A service standing in that answers no record and keeps whatever is posted, each post heard by its path.</summary>
    private sealed class Answering(List<string> posts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post) lock (posts) posts.Add(request.RequestUri!.AbsolutePath);
            var body = request.Method == HttpMethod.Get ? "[]" : """{"kept":true,"message":"Kept."}""";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
