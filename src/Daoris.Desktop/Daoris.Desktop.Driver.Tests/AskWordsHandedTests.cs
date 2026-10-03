using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DRIFT1b (D133 §2): every session on an ask is handed all of the person's words on it, oldest first and newest last,
/// beneath its quest, read from the ask at its start (<c>GET /api/asks/{id}</c>, DRIFT1a). The drift's own chain is the
/// proof: the person answered a session, the carry-on was cut off by an account's limit, so was the next, and the carry-on
/// after that ran on another account, handed only the last cut-off. It is handed the answer now.
/// </summary>
public sealed class AskWordsHandedTests
{
    private const string Sentence = "complete the ticket I logged, and this will need the v3 bridge";

    private const string Added = "I think we should be using the shared report module so there is no need for a new backend api?";

    private const string Answer = "the new report has its calculation in the ticket, and we should be using the shared report module: "
        + "if any feature is missing, add it into that module instead";

    private const string Limit = "the agent's turn failed: the account reached its spend limit.";

    /// <summary>The ask as the service answers it, its words oldest first (DRIFT1a).</summary>
    private static string AskJson(string words) =>
        $$"""
        {"id":"a1","workspace":"work","sentence":"{{Sentence}}","state":"Published","tier":"intake","quests":["q1","q2"],
         "words":[{{words}}]}
        """;

    private static readonly string Words =
        $$"""
        {"kind":"asked","text":"{{Sentence}}","at":"2026-10-01T02:26:00+00:00"},
        {"kind":"added","text":"{{Added}}","at":"2026-10-01T03:18:00+00:00","session":"s1","quest":"q1"},
        {"kind":"answered","text":"{{Answer}}","at":"2026-10-01T08:14:00+00:00","session":"s3","quest":"q2"}
        """;

    /// <summary>The verification step the answer was given on, a `then` step of the build, asked by the ask itself.</summary>
    private static QuestView Quest(string from = "ask #a1") =>
        new("q2", from, "report", "Verify the new report in the browser", "Check each line's figures against its daily report.", "Taken")
        {
            Parent = "q1",
        };

    private static SessionTarget Target(AskWords? words = null) => SessionTarget.ForQuest(Quest(), "C:/somewhere/report", "http://stand-in") with
    {
        Words = words,
    };

    private static AskWordView Word(string kind, string text, int minute, string? session = null, string? quest = null) =>
        new(kind, text, new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).AddMinutes(minute), session, quest);

    private static AskWords Read(params AskWordView[] words) => new("a1", words);

    // ——— The chain the drift took.

    /// <summary>
    /// 🔴 The evidence's §2, S3 to S8: the person answered session s3 on the verification quest; s4 carried it on and was
    /// cut off by the account's limit before it kept a plan; s5 was cut off the same way; s6 carries it on on another
    /// account. The driver reads the last run (s5, which holds no answer) and the ask's words, and composes the carry-on
    /// through its own reading: the answer is quoted, though the record the carry-on follows says only the limit.
    /// </summary>
    [Fact]
    public async Task A_carry_on_on_another_account_after_two_cut_offs_still_quotes_the_answer()
    {
        var runs = ServiceClient.ReadLastRun(
            $$"""
            [{"id":"s3","quest":"q2","state":"completed","answer":"{{Answer}}","profile":"account-1","created":"2026-10-01T04:08:00Z"},
             {"id":"s4","quest":"q2","state":"failed","note":"{{Limit}}","limit":true,"profile":"account-1","created":"2026-10-01T08:14:00Z"},
             {"id":"s5","quest":"q2","state":"failed","note":"{{Limit}}","limit":true,"profile":"account-1","created":"2026-10-01T08:20:00Z"}]
            """);
        var prior = runs["q2"];
        Assert.Equal(("s5", (string?)null), (prior.Session, prior.Answer));

        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/asks/a1" ? (HttpStatusCode.OK, AskJson(Words)) : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        // The carry-on as the driver composes it (Driver.RunAsync): the cut-off's note and last words, the last record's
        // answer (none), the account changed, and the ask's words read for the start.
        var carryOn = SessionTarget.ForQuest(Quest(), "C:/somewhere/report", service.BaseUrl) with
        {
            CutOff = prior.Note,
            PersonSaid = prior.Answer,
            LastWords = prior.Note,
            AccountChanged = prior.Limit && Daoris.Driver.Driver.OnAnotherAccount(prior, "account-2"),
        };

        var handed = TargetPrompt.Compose(await Daoris.Driver.Driver.WithAskWordsAsync(carryOn, service, CancellationToken.None));

        // One hop, as it was: the answer is nowhere in what the carry-on was handed.
        Assert.DoesNotContain(Answer, TargetPrompt.Compose(carryOn));
        Assert.Contains("  > " + Answer, handed);
        Assert.Contains("  > " + Added, handed);
        Assert.Contains("It ran on another account", handed);
        Assert.Equal(["/api/asks/a1"], standIn.Paths);
    }

    // ——— Every start on the ask, beneath its quest, newest last.

    [Fact]
    public void A_first_start_a_resume_and_a_carry_on_are_each_handed_every_word_beneath_the_quest_newest_last()
    {
        var words = Read(Word("asked", Sentence, 0), Word("added", Added, 10, "s1", "q1"), Word("answered", Answer, 20, "s3", "q2"));
        var question = new QuestView("q9", "report", "backend", "What does the endpoint take?", "We need the contract.", "Done") { Note = "{ text }" };

        foreach (var prompt in new[]
                 {
                     TargetPrompt.Compose(Target(words)),
                     TargetPrompt.Compose(Target(words) with { Answered = question }),
                     TargetPrompt.Compose(Target(words) with { CutOff = Limit }),
                 })
        {
            var body = prompt.IndexOf("Check each line's figures", StringComparison.Ordinal);
            var asked = prompt.IndexOf("  > " + Sentence, StringComparison.Ordinal);
            var added = prompt.IndexOf("  > " + Added, StringComparison.Ordinal);
            var answered = prompt.IndexOf("  > " + Answer, StringComparison.Ordinal);
            var boundary = prompt.IndexOf("Never write outside this repository", StringComparison.Ordinal);
            Assert.True(body >= 0 && body < asked && asked < added && added < answered && answered < boundary, prompt);
            Assert.Contains("The person's own words on ask `#a1`, oldest first and newest last.", prompt);
        }
    }

    /// <summary>
    /// Words said to a session after it ended, kept on the ask once a session took them (MSG1a's <c>reopened</c>), are said as
    /// added after it ended (D137 §2.4), so a session handed them on knows they came after that session's work.
    /// </summary>
    [Fact]
    public void A_word_said_to_a_session_after_it_ended_is_said_so()
    {
        var prompt = TargetPrompt.Compose(Target(Read(Word("asked", Sentence, 0), Word("reopened", "Also log the port.", 40, "s5", "q1"))));

        Assert.Contains("- They added, after a session on quest `#q1` ended, 2026-10-01 09:40 UTC:\n\n  > Also log the port.", prompt);
    }

    [Fact]
    public void Each_word_says_how_it_was_given_when_and_on_which_quest()
    {
        var prompt = TargetPrompt.Compose(Target(Read(
            Word("asked", Sentence, 0),
            Word("added", Added, 10, "s1", "q1"),
            Word("answered", Answer, 20, "s3", "q2"),
            Word("murmured", "a kind this build does not know", 30, "s4", "q2"))));

        Assert.Contains("- They asked, 2026-10-01 09:00 UTC:\n\n  > " + Sentence, prompt);
        Assert.Contains("- They added, while a session on quest `#q1` ran, 2026-10-01 09:10 UTC:\n\n  > " + Added, prompt);
        Assert.Contains("- They answered a session on this quest, 2026-10-01 09:20 UTC:\n\n  > " + Answer, prompt);
        Assert.Contains("- They said, on this quest, 2026-10-01 09:30 UTC:\n\n  > a kind this build does not know", prompt);
        // The person's words verbatim; a reading of them is someone else's.
        Assert.Contains("a plan or an earlier session's note is someone else's reading of them", prompt.ReplaceLineEndings(" "));
    }

    [Fact]
    public void A_word_of_several_lines_is_quoted_line_by_line()
    {
        var prompt = TargetPrompt.Compose(Target(Read(Word("asked", Sentence, 0), Word("answered", "first line\r\n\r\nthird line", 5, "s3", "q2"))));

        Assert.Contains("  > first line\n  >\n  > third line", prompt);
    }

    [Fact]
    public void A_quest_no_ask_asked_reads_exactly_as_it_did()
    {
        var before = TargetPrompt.Compose(Target());

        Assert.DoesNotContain("own words", before);
        Assert.DoesNotContain("could not be read", before);
    }

    [Fact]
    public void An_ask_from_before_its_words_were_kept_says_from_when_they_are()
    {
        var prompt = TargetPrompt.Compose(Target(new AskWords("a1", [Word("asked", Sentence, 0)], new DateTimeOffset(2026, 10, 2, 11, 30, 0, TimeSpan.Zero))));

        Assert.Contains(
            "Daoris has kept their words on this ask since 2026-10-02 11:30 UTC: anything they said before then is not among them.",
            prompt.ReplaceLineEndings(" "));
    }

    // ——— When the words could not be read, the instruction is today's and says so.

    [Fact]
    public void Words_that_could_not_be_read_leave_today_s_instruction_and_say_so()
    {
        var today = TargetPrompt.Compose(Target() with { CutOff = Limit, PersonSaid = Answer });

        var prompt = TargetPrompt.Compose(Target(AskWords.Unread("a1")) with { CutOff = Limit, PersonSaid = Answer });

        var said = AskWordsText.UnreadSentence("a1");
        Assert.Contains(said, prompt);
        Assert.Equal(today, prompt.Replace("\n" + said + "\n", "", StringComparison.Ordinal));
        // The one hop still stands where nothing better was read.
        Assert.Contains("> " + Answer, prompt);
    }

    // ——— A carry-on's answer is one of the words, quoted once.

    [Fact]
    public void A_carry_on_whose_answer_is_among_the_words_points_to_it_rather_than_quoting_it_twice()
    {
        var words = Read(Word("asked", Sentence, 0), Word("answered", Answer, 20, "s3", "q2"));

        var prompt = TargetPrompt.Compose(Target(words) with { CutOff = "It stopped to ask which report.", PersonSaid = Answer });

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(prompt, System.Text.RegularExpressions.Regex.Escape(Answer)));
        Assert.Contains("stopped to ask the person, and they answered: their answer is quoted among their words above.", prompt.ReplaceLineEndings(" "));
    }

    [Fact]
    public void A_carry_on_whose_answer_is_not_among_the_words_still_quotes_it()
    {
        // Kept before the words were (DRIFT1a keeps nothing back), so the ask holds only its sentence.
        var words = new AskWords("a1", [Word("asked", Sentence, 0)], DateTimeOffset.Parse("2026-10-02T11:30:00Z"));

        var prompt = TargetPrompt.Compose(Target(words) with { CutOff = "It stopped to ask which report.", PersonSaid = Answer });

        Assert.Contains("stopped to ask the person, and they answered:\n\n> " + Answer, prompt);
    }

    // ——— Bounded.

    /// <summary>
    /// The ask's own sentence is kept whole, as the quest's body already carries it; of the rest, the newest that fit in
    /// <see cref="AskWordsText.WordsLimit"/> are kept, and the older are left out as one line saying how many and when.
    /// </summary>
    [Fact]
    public void Past_the_bound_the_oldest_words_after_the_ask_are_left_out_and_said_to_be()
    {
        var words = new List<AskWordView> { Word("asked", Sentence, 0) };
        for (var i = 1; i <= 12; i++) words.Add(Word("added", $"word {i:00} " + new string('x', 1500), i, $"s{i}", "q2"));

        var prompt = TargetPrompt.Compose(Target(new AskWords("a1", words)));

        Assert.Contains("  > " + Sentence, prompt);
        Assert.Contains("  > word 12 ", prompt);
        var kept = Enumerable.Range(1, 12).Count(i => prompt.Contains($"  > word {i:00} ", StringComparison.Ordinal));
        Assert.True(kept * 1508 <= AskWordsText.WordsLimit, $"{kept} words kept");
        var left = 12 - kept;
        Assert.True(left > 0);
        Assert.Contains($"  > word {left + 1:00} ", prompt);
        Assert.DoesNotContain($"  > word {left:00} ", prompt);
        Assert.Contains(
            $"- … {left} of their words, said from 2026-10-01 09:01 UTC to 2026-10-01 09:{left:00} UTC, are left out here to keep "
            + "this instruction bounded; the ask's record on this machine keeps every one.",
            prompt.ReplaceLineEndings(" "));
        // Left out after the sentence and before the newest, so the order still reads oldest first.
        Assert.True(prompt.IndexOf("  > " + Sentence, StringComparison.Ordinal) < prompt.IndexOf("- … ", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("- … ", StringComparison.Ordinal) < prompt.IndexOf($"  > word {left + 1:00} ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_word_longer_than_its_bound_is_cut_and_said_to_be()
    {
        var huge = "start " + new string('y', AskWordsText.WordLimit + 500);

        var prompt = TargetPrompt.Compose(Target(Read(Word("asked", Sentence, 0), Word("added", huge, 5, "s1", "q2"))));

        Assert.Contains("  > start yyy", prompt);
        Assert.DoesNotContain(huge, prompt);
        Assert.Contains($"  (… and {huge.Length - AskWordsText.WordLimit} more characters of this one, left out here to keep this instruction bounded.)", prompt);
    }

    // ——— Read from the ask, at the start.

    [Fact]
    public async Task The_words_are_read_from_the_ask_the_quest_was_asked_by()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/asks/a1" ? (HttpStatusCode.OK, AskJson(Words)) : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var words = await AskWords.ReadAsync(service, "ask #a1", CancellationToken.None);

        Assert.Equal("a1", words!.Ask);
        Assert.Equal(["asked", "added", "answered"], words.Said!.Select(word => word.Kind));
        Assert.Equal([null, "s1", "s3"], words.Said!.Select(word => word.Session));
        Assert.Equal([null, "q1", "q2"], words.Said!.Select(word => word.Quest));
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 14, 0, TimeSpan.Zero), words.Said![2].At);
        Assert.Null(words.KeptFrom);
    }

    [Theory]
    [InlineData("report")]
    [InlineData("ask #")]
    [InlineData("ask a1")]
    [InlineData("")]
    public async Task A_quest_a_repository_asked_reads_nothing_and_is_on_no_ask(string from)
    {
        var standIn = new StandIn(_ => (HttpStatusCode.OK, AskJson(Words)));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        Assert.Null(await AskWords.ReadAsync(service, from, CancellationToken.None));
        Assert.Empty(standIn.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"error":"no ask"}""")]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"broken"}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    // A host from before the words were kept answers the ask without them.
    [InlineData(HttpStatusCode.OK, """{"id":"a1","workspace":"work","sentence":"s","state":"Published","tier":"intake"}""")]
    public async Task An_ask_that_does_not_answer_its_words_reads_as_could_not_be_read(HttpStatusCode status, string body)
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(_ => (status, body))));

        var words = await AskWords.ReadAsync(service, "ask #a1", CancellationToken.None);

        Assert.Equal(AskWords.Unread("a1"), words);
    }

    [Fact]
    public async Task A_service_that_cannot_be_reached_reads_as_could_not_be_read()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(_ => throw new HttpRequestException("refused"))));

        Assert.Equal(AskWords.Unread("a1"), await AskWords.ReadAsync(service, "ask #a1", CancellationToken.None));
    }

    [Fact]
    public async Task A_read_the_driver_is_closing_on_is_cancelled_not_read_as_unread()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(_ => (HttpStatusCode.OK, AskJson(Words)))));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AskWords.ReadAsync(service, "ask #a1", cancelled.Token));
    }

    [Fact]
    public void A_word_that_is_not_one_is_passed_over_and_an_older_ask_says_from_when()
    {
        var ask = ServiceClient.ReadAskJson(
            """
            {"id":"a1","workspace":"work","sentence":"s","state":"Published","tier":"intake","wordsKeptFrom":"2026-10-02T11:30:00+00:00",
             "words":[{"kind":"asked","text":"s","at":"2026-10-01T02:26:00+00:00"},
                      {"kind":"answered","at":"2026-10-01T03:00:00+00:00"},
                      {"kind":"answered","text":"no time"},
                      "not a word",
                      {"kind":"added","text":"kept","at":"2026-10-02T12:00:00+00:00","session":"s2"}]}
            """);

        Assert.Equal(["s", "kept"], ask.Words!.Select(word => word.Text));
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 11, 30, 0, TimeSpan.Zero), ask.WordsKeptFrom);
        Assert.Null(ServiceClient.ReadAskJson("""{"id":"a1","workspace":"w","sentence":"s","state":"Open","tier":"named"}""").Words);
    }

    // ——— The ask's sender, read back (a twin of the service's `AskDesk.SenderOf` and `AskOf`).

    [Theory]
    [InlineData("ask #a1", "a1")]
    [InlineData("ask #9f45c2", "9f45c2")]
    [InlineData("ask #", null)]
    [InlineData("Ask #a1", null)]
    [InlineData("ask a1", null)]
    [InlineData("report", null)]
    [InlineData(null, null)]
    public void The_ask_a_quest_was_asked_by_is_read_from_its_sender(string? sender, string? ask)
    {
        Assert.Equal(ask, AskWords.AskOf(sender));
    }

    // ——— An intake.

    [Fact]
    public void An_intake_is_handed_what_the_person_said_on_its_ask_since_the_ask()
    {
        var ask = new AskView("a1", "work", Sentence, "Proposed", "declarations")
        {
            Words = [Word("asked", Sentence, 0), Word("added", Added, 10, "s1")],
        };

        var prompt = IntakePrompt.Compose(ask);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(prompt, System.Text.RegularExpressions.Regex.Escape(Sentence)));
        Assert.Contains("What they said on it since, oldest first and newest last:", prompt);
        Assert.Contains("- They added, while a session ran, 2026-10-01 09:10 UTC:\n\n  > " + Added, prompt);
        Assert.True(prompt.IndexOf("> " + Sentence, StringComparison.Ordinal) < prompt.IndexOf("  > " + Added, StringComparison.Ordinal));
    }

    [Fact]
    public void An_intake_on_an_ask_with_only_its_sentence_reads_as_it_did_and_one_whose_words_were_not_read_says_so()
    {
        var bare = new AskView("a1", "work", Sentence, "Proposed", "declarations");

        var asked = IntakePrompt.Compose(bare with { Words = [Word("asked", Sentence, 0)] });
        var unread = IntakePrompt.Compose(bare);

        Assert.DoesNotContain("What they said on it since", asked);
        Assert.Contains("Whether they said more on it since the ask could not be read just now.", unread);
        Assert.Equal(asked, unread.Replace("Whether they said more on it since the ask could not be read just now.\n\n", "", StringComparison.Ordinal));
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
