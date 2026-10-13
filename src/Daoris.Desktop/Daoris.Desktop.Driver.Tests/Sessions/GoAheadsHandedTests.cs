using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KNOWUSE1a (D135 §2): a go-ahead is asked once and held on the ask, and every session on the ask is handed what was
/// approved, what was refused and what still waits, read from the ask at its start (<c>GET /api/asks/{id}</c>) beside the
/// person's words: a first start, a resume, a carry-on on any account and a follow-up step alike. Three production acts
/// drew thirteen asks because each session re-listed the go-aheads still open (`docs/2026-10-03-knowledge-use-evidence.md`
/// §5.1).
/// </summary>
public sealed class GoAheadsHandedTests
{
    private const string Sentence = "the dashboard figure reads zero";

    private const string GoAheads =
        """
        [{"number":1,"kind":"write","on":"production","act":"dashboard configuration","state":"approved",
          "asked":[{"session":"s1","quest":"q2","at":"2026-10-03T09:01:00+00:00","why":"The tile's target."}],
          "answer":{"approved":true,"words":"run the put","at":"2026-10-03T09:06:00+00:00"}},
         {"number":2,"kind":"release","on":"production","act":"comparison report","state":"refused",
          "asked":[{"session":"s1","quest":"q2","at":"2026-10-03T09:02:00+00:00","why":"Ship it."}],
          "answer":{"approved":false,"words":"test it on dev first","at":"2026-10-03T09:07:00+00:00"}},
         {"number":3,"kind":"write","on":"production","act":"menu entries","state":"asked",
          "asked":[{"session":"s2","quest":"q2","at":"2026-10-03T09:08:00+00:00","why":"The entries."}],"near":1}]
        """;

    private static string AskJson(string goAheads = GoAheads) =>
        $$"""
        {"id":"a1","workspace":"work","sentence":"{{Sentence}}","state":"Published","tier":"intake","quests":["q1","q2"],
         "words":[{"kind":"asked","text":"{{Sentence}}","at":"2026-10-03T09:00:00+00:00"}],"goAheads":{{goAheads}}}
        """;

    /// <summary>The verification step, a `then` step of the build, asked by the ask itself.</summary>
    private static QuestView Quest(string from = "ask #a1") =>
        new("q2", from, "dashboards", "Verify the figure", "Look at the tile in production.", "Taken") { Parent = "q1" };

    private static GoAheadView Held(int number, string kind, string act, bool? approved, string? words = null, int minute = 0, string session = "s1") =>
        new(number, kind, "production", act,
            [new GoAheadRequestView(session, "q2", new DateTimeOffset(2026, 10, 3, 9, minute, 0, TimeSpan.Zero), "why")])
        {
            Answer = approved is { } yes ? new GoAheadAnswerView(yes, words, new DateTimeOffset(2026, 10, 3, 9, 30 + minute, 0, TimeSpan.Zero)) : null,
        };

    private static AskWords Read(params GoAheadView[] goAheads) =>
        new("a1", [new AskWordView("asked", Sentence, new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero))]) { GoAheads = goAheads };

    private static SessionTarget Target(AskWords? words) =>
        SessionTarget.ForQuest(Quest(), "C:/somewhere/dashboards", "http://stand-in") with { Words = words };

    /// <summary>
    /// The proof: a carry-on is handed the go-aheads. The session before asked for the production write and parked; the
    /// person approved it on the ask; the carry-on, on another account, is composed through the driver's own reading of the
    /// ask and is handed the approval in the person's words, the refusal, and the one still waiting, by number.
    /// </summary>
    [Fact]
    public async Task A_carry_on_is_handed_what_was_approved_refused_and_still_waits()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/asks/a1" ? (HttpStatusCode.OK, AskJson()) : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var carryOn = SessionTarget.ForQuest(Quest(), "C:/somewhere/dashboards", service.BaseUrl) with
        {
            CutOff = "the agent's turn failed: the account reached its spend limit.",
            AccountChanged = true,
        };

        var handed = TargetPrompt.Compose(await Daoris.Driver.Driver.WithAskWordsAsync(carryOn, service, CancellationToken.None));

        Assert.DoesNotContain("Go-ahead 1", TargetPrompt.Compose(carryOn));
        Assert.Contains("- Go-ahead 1, write on production: \"dashboard configuration\" — approved 2026-10-03 09:06 UTC, saying:\n\n  > run the put", handed);
        Assert.Contains("- Go-ahead 2, release on production: \"comparison report\" — refused 2026-10-03 09:07 UTC, saying:\n\n  > test it on dev first", handed);
        Assert.Contains("- Go-ahead 3, write on production: \"menu entries\" — still waiting on the person, first asked 2026-10-03 09:08 UTC.", handed);
        Assert.Contains("do not ask for one of these again", handed);
        Assert.Equal(["/api/asks/a1"], standIn.Paths);
    }

    /// <summary>A first start, a resume and a carry-on each carry them beneath the quest and the person's words, before the look.</summary>
    [Fact]
    public void A_first_start_a_resume_and_a_carry_on_are_each_handed_the_go_aheads_beneath_the_quest()
    {
        var words = Read(Held(1, "write", "dashboard configuration", true, "run the put"));
        var question = new QuestView("q9", "dashboards", "engine", "What does it take?", "The contract.", "Done") { Note = "{ text }" };

        foreach (var prompt in new[]
                 {
                     TargetPrompt.Compose(Target(words)),
                     TargetPrompt.Compose(Target(words) with { Answered = question }),
                     TargetPrompt.Compose(Target(words) with { CutOff = "it was cut off." }),
                 })
        {
            var body = prompt.IndexOf("Look at the tile in production.", StringComparison.Ordinal);
            var said = prompt.IndexOf("  > " + Sentence, StringComparison.Ordinal);
            var goAhead = prompt.IndexOf("- Go-ahead 1, write on production", StringComparison.Ordinal);
            var look = prompt.IndexOf("Look before you ask.", StringComparison.Ordinal);
            Assert.True(body >= 0 && body < said && said < goAhead && goAhead < look, prompt);
        }
    }

    /// <summary>
    /// A follow-up step is asked by the ask too (D65 §4): its session, in whichever repository, is handed the go-aheads its
    /// parent's sessions asked, read from the same ask.
    /// </summary>
    [Fact]
    public async Task A_follow_up_steps_session_is_handed_the_go_aheads_its_parents_sessions_asked()
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK, AskJson()));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var step = new QuestView("q3", "ask #a1", "checker", "Check the figure", "Open it.", "Open") { Parent = "q2" };

        var handed = TargetPrompt.Compose(await Daoris.Driver.Driver.WithAskWordsAsync(
            SessionTarget.ForQuest(step, "C:/somewhere/checker", service.BaseUrl), service, CancellationToken.None));

        Assert.Contains("- Go-ahead 1, write on production", handed);
    }

    /// <summary>
    /// A session on an ask is told to ask a go-ahead once, through its connector; a quest no ask asked reads exactly as it
    /// did, since there is no ask to hold one, and nothing is read for it.
    /// </summary>
    [Fact]
    public void A_session_on_an_ask_is_told_to_ask_a_go_ahead_once_and_one_on_none_reads_as_it_did()
    {
        var onAsk = TargetPrompt.Compose(Target(Read()));
        var onNone = TargetPrompt.Compose(SessionTarget.ForQuest(Quest("checker"), "C:/somewhere/dashboards", "http://stand-in"));

        Assert.Contains("`go_ahead_ask`", onAsk);
        Assert.Contains("an act already asked joins the first", onAsk.ReplaceLineEndings(" "));
        Assert.DoesNotContain("go_ahead_ask", onNone);
        Assert.DoesNotContain("Go-ahead", onNone);
    }

    /// <summary>An ask holding none, and a host from before go-aheads that answers none, add no section.</summary>
    [Fact]
    public void An_ask_holding_none_and_a_host_from_before_add_nothing()
    {
        var none = TargetPrompt.Compose(Target(Read()));
        var before = TargetPrompt.Compose(Target(new AskWords("a1", [new AskWordView("asked", Sentence, DateTimeOffset.UnixEpoch)])));

        Assert.DoesNotContain("Go-aheads the person was asked for", none);
        Assert.DoesNotContain("Go-aheads the person was asked for", before);
    }

    /// <summary>Bounded, and said: past the bound the rest are named by number, with where each is whole.</summary>
    [Fact]
    public void The_go_aheads_are_bounded_and_say_which_were_left_out()
    {
        var many = Enumerable.Range(1, 12)
            .Select(n => Held(n, "write", $"entry {n} " + new string('x', GoAheadsText.ActLimit - 10), true, new string('y', 900), n % 20))
            .ToArray();

        var prompt = TargetPrompt.Compose(Target(Read(many)));

        Assert.Contains("- Go-ahead 1, write on production", prompt);
        Assert.DoesNotContain("- Go-ahead 12, write on production", prompt);
        Assert.Matches(@"- Go-aheads \d+ to 12 are left out here to keep this instruction bounded", prompt);
    }

    /// <summary>The client reads them as the service answers them, passing over one it cannot read, as the service does.</summary>
    [Fact]
    public async Task The_client_reads_the_go_aheads_and_passes_over_one_it_cannot_read()
    {
        var withBad = GoAheads.TrimEnd().TrimEnd(']') + """,{"number":"four","kind":"write"}]""";
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(_ => (HttpStatusCode.OK, AskJson(withBad)))));

        var ask = (await service.FindAskAsync("a1"))!;

        Assert.Equal([1, 2, 3], ask.GoAheads!.Select(goAhead => goAhead.Number));
        Assert.Equal(("write", "production", "dashboard configuration"), (ask.GoAheads![0].Kind, ask.GoAheads[0].On, ask.GoAheads[0].Act));
        Assert.Equal(new GoAheadAnswerView(true, "run the put", DateTimeOffset.Parse("2026-10-03T09:06:00+00:00")), ask.GoAheads[0].Answer);
        Assert.Null(ask.GoAheads[2].Answer);
        Assert.Equal(1, ask.GoAheads[2].Near);
        Assert.Equal("s2", Assert.Single(ask.GoAheads[2].Asked).Session);
    }

    /// <summary>A host from before go-aheads answers none, read as none held rather than as a failed read.</summary>
    [Fact]
    public async Task A_host_from_before_go_aheads_is_read_as_answering_none()
    {
        var before = $$"""{"id":"a1","workspace":"work","sentence":"{{Sentence}}","state":"Published","tier":"intake","quests":[]}""";
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(_ => (HttpStatusCode.OK, before))));

        Assert.Null((await service.FindAskAsync("a1"))!.GoAheads);
    }

    /// <summary>
    /// An answer continues its own session (ANSWER1a), whose conversation was handed the go-aheads at its start but not the
    /// person's answer to the ones it asked since: the resumed prompt is the person's answer, verbatim, then what was
    /// answered on the go-aheads this session asked. The record keeps the person's words as theirs, and only those.
    /// </summary>
    [Fact]
    public void A_resumed_conversation_is_told_the_answers_to_the_go_aheads_it_asked()
    {
        var words = Read(
            Held(1, "write", "dashboard configuration", true, "run the put", session: "s9"),
            Held(2, "release", "comparison report", null, session: "s9"),
            Held(3, "push", "report branch", false, "not yet", session: "s1"));

        var prompt = GoAheadsText.Resumed("carry on", words, "s9");

        Assert.StartsWith("carry on\n\n", prompt, StringComparison.Ordinal);
        Assert.Contains("Go-ahead 1, write on production: \"dashboard configuration\" — approved", prompt);
        Assert.DoesNotContain("Go-ahead 2", prompt);
        Assert.DoesNotContain("Go-ahead 3", prompt);
        Assert.Equal("carry on", GoAheadsText.Resumed("carry on", Read(Held(2, "release", "comparison report", null, session: "s9")), "s9"));
        Assert.Equal("carry on", GoAheadsText.Resumed("carry on", null, "s9"));
    }

    /// <summary>
    /// The resumed run prompts with the answer and the go-aheads after it, while its record keeps the person's answer as
    /// theirs, alone: Daoris's line is not the person's words.
    /// </summary>
    [Fact]
    public void A_resumed_run_prompts_with_the_go_aheads_and_its_record_keeps_only_the_persons_answer()
    {
        var words = Read(Held(1, "write", "dashboard configuration", true, "run the put", session: "s9"));
        var prompt = GoAheadsText.Resumed("carry on", words, "s9");
        IReadOnlyList<SaidWordView> said = [new("w1", "carry on", DateTimeOffset.UnixEpoch, [], Reopens: false)];
        var resume = new ResumeAsk("conversation-1", said, "It goes on.", GoAheadsText.Resumed("", words, "s9").TrimStart());

        Assert.Equal(prompt, resume.Prompt);
        // The protocol door's blocks (MSG1b): the person's words, then what Daoris adds, as its own block.
        Assert.Equal(2, resume.Blocks.Count);
        Assert.Equal("carry on", resume.Blocks[0]);
        var opening = resume.Opening();
        Assert.Equal(("person", "carry on", "w1"), (opening[1].Origin, opening[1].Text, opening[1].Id));
        Assert.Single(opening, e => e.Origin == "person");
        Assert.Equal("carry on", new ResumeAsk("conversation-1", said, "It goes on.").Prompt);
    }

    /// <summary>
    /// The terminal's door (`daoris-driver ask --go-ahead`): the person's yes or no posted to the ask's go-ahead door with
    /// their words, and the service's sentence back verbatim; a refusal is an answer too.
    /// </summary>
    [Fact]
    public async Task The_terminals_door_posts_the_answer_and_says_the_services_sentence()
    {
        string? body = null;
        var standIn = new StandIn(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return request.RequestUri!.AbsolutePath == "/api/asks/a1/go-aheads/2"
                ? (HttpStatusCode.OK, """{"ask":{"id":"a1"},"message":"Go-ahead 2 on ask `#a1` refused."}""")
                : (HttpStatusCode.NotFound, """{"error":"Ask `#a1` holds no go-ahead 7: it holds 1, 2."}""");
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var refused = await service.AnswerGoAheadAsync("#a1", 2, approved: false, "test it on dev first");
        var none = await service.AnswerGoAheadAsync("a1", 7, approved: true, null);

        Assert.Equal((true, "Go-ahead 2 on ask `#a1` refused."), refused);
        Assert.Equal((false, "Ask `#a1` holds no go-ahead 7: it holds 1, 2."), none);
        Assert.Contains("\"answer\":\"approved\"", body);
        // GOAHEAD2: the park is the caller's unless it says otherwise, as the terminal's door does.
        Assert.Contains("\"goesOn\":false", body);
        await service.AnswerGoAheadAsync("a1", 2, approved: true, null, goesOn: true);
        Assert.Contains("\"goesOn\":true", body);
    }

    /// <summary>
    /// The headless host's usage spells the door, as its ask verb takes it; and the door is the ask page's twin, so the answer
    /// that leaves none of a park's go-aheads waiting sends it on (GOAHEAD2).
    /// </summary>
    [Fact]
    public void The_hosts_usage_spells_the_go_ahead_door()
    {
        Assert.Contains("ask --go-ahead <id> <n> approve|refuse [\"…\"]", DriverCommand.Usage);
        var host = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "AskConsole.cs"));
        Assert.Contains("case \"--go-ahead\":", host);
        Assert.Contains("answer.Words, goesOn: true)", host);
    }

    /// <summary>The terminal's words after `--go-ahead <id>`: the number, approve or refuse, then the person's words as one sentence.</summary>
    [Theory]
    [InlineData("2 approve", 2, true, null)]
    [InlineData("1 refuse test it on dev first", 1, false, "test it on dev first")]
    public void The_terminals_words_are_read_as_a_number_a_yes_or_no_and_the_persons_words(
        string said, int number, bool approved, string? words)
    {
        Assert.Equal((number, approved, words), GoAheadCommand.Read(said.Split(' '), out var problem));
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("", "--go-ahead <id> <n> approve|refuse")]
    [InlineData("two approve", "--go-ahead <id> <n> approve|refuse")]
    [InlineData("0 approve", "--go-ahead <id> <n> approve|refuse")]
    [InlineData("2 maybe", "`approve` or `refuse`, not `maybe`")]
    public void The_terminals_words_that_do_not_read_say_why(string said, string why)
    {
        Assert.Null(GoAheadCommand.Read(said.Split(' ', StringSplitOptions.RemoveEmptyEntries), out var problem));
        Assert.Contains(why, problem);
    }

    /// <summary>A service standing in: each request recorded by its path, then answered, or 404 where the answer is null.</summary>
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (Paths) Paths.Add(request.RequestUri!.AbsolutePath);
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, """{"error":"none"}""");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
