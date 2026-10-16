using System.Net;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1j (D126 §9): the service answers each quest's short title, its publisher's or the name it reads from the quest's
/// words, and the driver names a quest by it where a name is shown: the session listing, and a landing's <c>{slug}</c>
/// (LANDNAME1's naming half), so a branch is named for the work rather than cut from a note on the quest's first line.
/// A host from before the field answers none, and the title names the quest as before.
/// </summary>
public sealed class QuestNameTests
{
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request) is { } said
                ? new HttpResponseMessage(said.Status) { Content = new StringContent(said.Body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private const string Refiled = "(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)";

    [Fact]
    public async Task The_clients_read_of_a_quest_carries_its_short_title_and_names_it_by_it()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/quests"
            ? (HttpStatusCode.OK, $$"""
                [{"id":"q1","from":"ask #a1","to":"report","title":"{{Refiled}}","body":"b","status":"Open",
                  "short":"TK-2203 continue the prod half"},
                 {"id":"q2","from":"report","to":"backend","title":"Read the field names","body":"b","status":"Open"}]
                """)
            : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var named = (await service.FindQuestAsync("q1"))!;
        var older = (await service.FindQuestAsync("q2"))!;

        Assert.Equal("TK-2203 continue the prod half", named.Short);
        Assert.Equal("TK-2203 continue the prod half", named.Name);
        Assert.Null(older.Short);
        Assert.Equal("Read the field names", older.Name);
    }

    /// <summary>
    /// LANDNAME1: the install's two landings came out <c>feature/re-filed-from-ask-39c495-whose-quest-was-…</c>. The slug is
    /// the quest's name in git's words, while the landing keeps the quest's whole title for its record and its plugin.
    /// </summary>
    [Fact]
    public async Task A_landing_is_named_for_the_quests_name_and_keeps_its_title()
    {
        var quest = new QuestView("q1", "ask #a1", "portal-ui", Refiled, "b", "Done") { Short = "TK-2203 continue the prod half" };
        Task<QuestView?> Find(string id) => Task.FromResult<QuestView?>(id == "q1" ? quest : null);

        var subject = await LandingRules.SubjectAsync("s1", "q1", Find, opening: null);

        Assert.Equal(Refiled, subject.Title);
        Assert.Equal("tk-2203-continue-the-prod-half", subject.Slug);
        Assert.Equal("feature/tk-2203-continue-the-prod-half-q1",
            LandingRules.Expand("feature/{slug}-{quest}", new LandingNames(subject.Quest!, subject.Slug, "portal-ui", "s1")));
    }

    /// <summary>A start's landing is told the same name, and a quest whose name is its title is named as it always was.</summary>
    [Fact]
    public async Task A_starts_landing_is_named_for_the_quests_name_too()
    {
        var named = new QuestView("q1", "ask #a1", "engine", Refiled, "b", "Open") { Short = "Frame cap" };
        var plain = new QuestView("q2", "game", "engine", "Fix the gap", "b", "Open") { Short = "Fix the gap" };

        var told = await Daoris.Driver.Driver.LandsAsAsync("s1", named, _ => throw new InvalidOperationException("never asked"), default);
        var same = await Daoris.Driver.Driver.LandsAsAsync("s2", plain, _ => throw new InvalidOperationException("never asked"), default);

        Assert.Equal("frame-cap", told.Slug);
        // The request's words ride the subject, a ticket's key searched in them (PROCESS1b-i).
        Assert.Equal(new LandingSubject("s2", "q2", "Fix the gap") { Body = "b" }, same);
        Assert.Equal("fix-the-gap", same.Slug);
    }

    // ——— PROCESS1b-i (D164's amendment point 2): the branch named as the repository names its own

    /// <summary>
    /// <c>{ticket}</c> is the first match of the rule's key (else <see cref="LandingRules.DefaultKey"/>) in the title, then the
    /// request's body, then its links, as written; none found, the session's id stands in. The slug leaves out the ticket the
    /// pattern already says, and every web address (LANDNAME2: <c>feature/https-…-browse-a267d192</c>), and an empty one takes its
    /// separator with it. A pattern without <c>{ticket}</c> names the branch as it always did.
    /// </summary>
    [Theory]
    [InlineData("feature/{ticket}-{slug}", null, "TK-2202: fix the gap", "", null, "feature/TK-2202-fix-the-gap")]
    [InlineData("feature/{ticket}-{slug}", null, "https://acme.atlassian.net/browse/ABC-123", "", null, "feature/ABC-123")]
    [InlineData("feature/{ticket}-{slug}", null, "Saving fails, see https://acme.atlassian.net/browse/ABC-123 and www.acme.test/x", "", null, "feature/ABC-123-saving-fails-see-and")]
    [InlineData("feature/{ticket}-{slug}", null, "Fix the gap", "The trace is in TK-77, and TK-78 follows it.", null, "feature/TK-77-fix-the-gap")]
    [InlineData("feature/{ticket}-{slug}", null, "TK-1 first", "TK-2 second", null, "feature/TK-1-first")]
    [InlineData("feature/{ticket}-{slug}", @"\d{4,}", "Fix the gap", "no number here", "https://dev.azure.test/org/_workitems/edit/4521", "feature/4521-fix-the-gap")]
    [InlineData("feature/{ticket}-{slug}", @"[A-Z]+-\d+", "abc-1 is not DEF-22", "", null, "feature/DEF-22-abc-1-is-not")]
    [InlineData("feature/{ticket}-{slug}", null, "Fix the gap", "", null, "feature/fd12f1bc-fix-the-gap")]
    [InlineData("feature/{ticket}-{slug}", null, "修复 登录", "", null, "feature/fd12f1bc-work")]
    [InlineData("feature/{ticket}", null, "TK-9 alone", "", null, "feature/TK-9")]
    [InlineData("feature/{quest}-{slug}", null, "TK-2202: fix the gap", "", null, "feature/q1-tk-2202-fix-the-gap")]
    [InlineData("feature/{quest}-{slug}", null, "https://acme.atlassian.net/browse/ABC-123", "", null, "feature/q1")]
    [InlineData("feature/{slug}-{quest}", null, "https://acme.atlassian.net/browse/ABC-123", "", null, "feature/q1")]
    public void A_branch_is_named_by_the_ticket_the_rules_key_finds_and_never_by_a_web_address(
        string pattern, string? key, string title, string body, string? link, string branch)
    {
        var subject = new LandingSubject("fd12f1bc", "q1", title) { Body = body, Links = link is null ? null : [link] };

        Assert.Equal(branch, LandingRules.Branch(subject, new LandingRule(LandingForm.Branch, pattern, Key: key), "portal-ui"));
    }

    // ——— ACCEPTFOOT2 (D164's 2026-10-16 note): a conversation's branch is named for its work, never for its opening

    /// <summary>
    /// A conversation has no title of its own: its opening is the person's first words, which the page wears as its name (RAIL1), and
    /// its branch was a slug of them (seen on the install, 2026-10-16). It is named for its work's oldest commit subject beyond the
    /// line, as PROCESS1b-ii names a squash with no title; the opening stays its title and is searched for a ticket's key after that
    /// subject. No subject read names it `work`, never the opening.
    /// </summary>
    [Theory]
    [InlineData("feature/{slug}-{session}", "ok so that is merged now, what about the rest", "Cap the chunk budget per frame", "feature/cap-the-chunk-budget-per-frame-s-1a2b3c4d")]
    [InlineData("feature/{ticket}-{slug}", "ok so that is merged now, what about the rest", "TK-31: cap the chunk budget", "feature/TK-31-cap-the-chunk-budget")]
    [InlineData("feature/{ticket}-{slug}", "please pick up TK-40 from the board", "Cap the chunk budget", "feature/TK-40-cap-the-chunk-budget")]
    [InlineData("feature/{ticket}-{slug}", "ok so that is merged now, what about the rest", "Cap the chunk budget", "feature/s-1a2b3c4d-cap-the-chunk-budget")]
    [InlineData("feature/{slug}-{session}", "ok so that is merged now, what about the rest", null, "feature/work-s-1a2b3c4d")]
    public void A_conversations_branch_is_named_for_its_work_never_for_its_opening(string pattern, string opening, string? oldest, string branch)
    {
        var subject = new LandingSubject("s-1a2b3c4d", null, opening);
        Assert.True(subject.NamedByOpening);

        var named = subject.ForWork(oldest);

        Assert.Equal(branch, LandingRules.Branch(named, new LandingRule(LandingForm.Branch, pattern), "engine"));
        Assert.Equal(opening, named.Title);
    }

    /// <summary>
    /// The oldest commit's subject is the first commit's, kept by its boundary (`%x1f%s`), never the first line with words in it: an
    /// oldest commit with an empty message names the work `work`, not the commit after it.
    /// </summary>
    [Theory]
    [InlineData("\u001fCap the chunk budget\n\u001fAdd tests\n", "Cap the chunk budget")]
    [InlineData("\u001f\n\u001fAdd tests\n", "")]
    [InlineData("\u001f  Cap it  \r\n", "Cap it")]
    [InlineData("", null)]
    public void The_oldest_subject_is_the_first_commits_even_where_it_is_empty(string log, string? oldest) =>
        Assert.Equal(oldest, LandingRules.OldestSubject(log));

    /// <summary>Only a pattern that reads the subject's words (`{slug}`, `{ticket}`) is worth a git read for them.</summary>
    [Theory]
    [InlineData("feature/{slug}-{session}", true)]
    [InlineData("feature/{ticket}", true)]
    [InlineData("feature/{session}", false)]
    [InlineData("review/{quest}", false)]
    public void Only_a_pattern_that_says_the_words_reads_them(string pattern, bool reads) =>
        Assert.Equal(reads, LandingRules.ReadsWords(new LandingRule(LandingForm.Branch, pattern)));

    /// <summary>A quest's subject, one already named, and one with no opening are named as they always were.</summary>
    [Fact]
    public void Only_a_conversations_opening_is_set_aside_for_its_work()
    {
        var quest = new LandingSubject("s1", "q1", "Fix the gap");
        var named = new LandingSubject("s1", null, "ok so") { Name = "Frame cap" };
        var bare = new LandingSubject("s1", null, null);

        Assert.Equal(quest, quest.ForWork("Cap the chunk budget"));
        Assert.Equal(named, named.ForWork("Cap the chunk budget"));
        Assert.Equal(bare, bare.ForWork("Cap the chunk budget"));
        Assert.False(quest.NamedByOpening || named.NamedByOpening || bare.NamedByOpening);
    }

    /// <summary>A chain is named for its first quest (WSR5), so its key is searched in that quest's words, not the last step's.</summary>
    [Fact]
    public async Task A_chains_ticket_is_its_first_quests()
    {
        var first = new QuestView("q1", "ask #a1", "portal-ui", "The common report", "Reported in TK-2203.", "Done") { Links = ["https://tickets.test/TK-2203"] };
        var step = new QuestView("q2", "ask #a1", "portal-ui", "Verify TK-9999 in the browser", "", "Open") { Parent = "q1" };
        Task<QuestView?> Find(string id) => Task.FromResult<QuestView?>(id switch { "q1" => first, "q2" => step, _ => null });

        var subject = await LandingRules.SubjectAsync("s2", "q2", Find, opening: null);

        Assert.Equal("Reported in TK-2203.", subject.Body);
        Assert.Equal(new[] { "https://tickets.test/TK-2203" }, subject.Links!);
        Assert.Equal("feature/TK-2203-the-common-report",
            LandingRules.Branch(subject, new LandingRule(LandingForm.Branch, "feature/{ticket}-{slug}"), "portal-ui"));
        // A conversation has its opening words, and they are searched.
        Assert.Equal("feature/TK-5-tidy-the-docs", LandingRules.Branch(
            await LandingRules.SubjectAsync("c1", null, Find, "TK-5 tidy the docs"), new LandingRule(LandingForm.Branch, "feature/{ticket}-{slug}"), "portal-ui"));
    }

    /// <summary>An empty slug takes the separator it would leave doubled, leading or trailing with it; the ticket stands in for nothing.</summary>
    [Theory]
    [InlineData("feature/{ticket}-{slug}", "", "ABC-1", "feature/ABC-1")]
    [InlineData("feature/{slug}-{quest}", "", "ABC-1", "feature/0fda18")]
    [InlineData("team/{slug}/{quest}", "", null, "team/0fda18")]
    [InlineData("{slug}_{quest}", "", null, "0fda18")]
    [InlineData("feature/{ticket}-{slug}", "fix-the-gap", null, "feature/s1a2b3c4-fix-the-gap")]
    [InlineData("feature/{quest}-{slug}", "fix-the-gap", "ABC-1", "feature/0fda18-fix-the-gap")]
    public void A_pattern_is_expanded_with_its_ticket_and_an_empty_slug_takes_its_separator(string pattern, string slug, string? ticket, string branch) =>
        Assert.Equal(branch, LandingRules.Expand(pattern, new LandingNames("0fda18", slug, "engine", "s1a2b3c4", ticket)));

    /// <summary>The slug is the title's words with every web address left out: a title that was only one leaves nothing.</summary>
    [Theory]
    [InlineData("https://acme.atlassian.net/browse/ABC-123", "")]
    [InlineData("Saving fails, see HTTP://acme.test/x and www.acme.test", "saving-fails-see-and")]
    [InlineData("Fix: API gap (#12)", "fix-api-gap-12")]
    [InlineData("修复 登录", "work")]
    [InlineData(null, "work")]
    public void A_slug_is_never_made_from_a_web_address(string? title, string slug) =>
        Assert.Equal(slug, LandingRules.Slug(title));

    /// <summary>
    /// The key's shape (PROCESS1b-i): a regular expression both readers of a rule take the same way, at most 64 characters, no
    /// <c>(?</c> group, no back-reference, a letter escaped only as one of the classes and controls both engines share, and no
    /// empty class; only on a branch rule whose pattern says <c>{ticket}</c>. The CLI's <c>driverconfig.test.ts</c> holds these
    /// cases, answer for answer (<c>KEY_SHAPES</c>).
    /// </summary>
    [Theory]
    [InlineData("branch", "feature/{ticket}-{slug}", null, null)]
    [InlineData("branch", "feature/{ticket}-{slug}", @"[A-Z]+-\d+", null)]
    [InlineData("branch", "feature/{ticket}", @"\d{4,}", null)]
    [InlineData("branch", "feature/{ticket}", @"TK\\1", null)]
    [InlineData("branch", "feature/{ticket}-{slug}", @"TK\-\d+", null)]
    [InlineData("branch", "feature/{ticket}-{slug}", @"[A-Z]{2,5}-\d+", null)]
    [InlineData("branch", "feature/{ticket}-{slug}", @"TK\_\d+", "may escape only")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"\x", "may escape only")]
    [InlineData("branch", "feature/{ticket}-{slug}", "[^]", "empty class")]
    [InlineData("branch", "feature/{ticket}-{slug}", "[]", "empty class")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"[a-z-[aeiou]]+-\d+", "subtract a class")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"[a-z\-[]+-\d+", null)]
    [InlineData("branch", "feature/{quest}-{slug}", @"[A-Z]+-\d+", "only for `{ticket}`")]
    [InlineData("merge", null, @"[A-Z]+-\d+", "only a branch rule reads a ticket's key")]
    [InlineData("branch", "feature/{ticket}-{slug}", "  ", "needs a shape")]
    [InlineData("branch", "feature/{ticket}-{slug}", "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDE", "at most 64 characters")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"(?<key>[A-Z]+-\d+)", "may not use `(?`")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"[A-Z]+(?=-)", "may not use `(?`")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"(?i)tk-\d+", "may not use `(?`")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"([A-Z]+)-\1", "may not refer back")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"(?<k>A)\k<k>", "may not use `(?`")]
    [InlineData("branch", "feature/{ticket}-{slug}", @"A\k<k>", "may not refer back")]
    [InlineData("branch", "feature/{ticket}-{slug}", "[A-Z+", "is not a regular expression")]
    public void A_tickets_key_is_a_shape_both_readers_take_and_only_for_ticket(string form, string? pattern, string? key, string? problem)
    {
        var said = LandingRules.Problem(new LandingRule(form, pattern, Key: key));

        if (problem is null) Assert.Null(said);
        else Assert.Contains(problem, said);
    }

    /// <summary>The key rides the rule in <c>driver.json</c>, after its pattern, as the CLI writes it; an empty one is none.</summary>
    [Fact]
    public void A_tickets_key_is_kept_in_driver_json_after_the_pattern()
    {
        var keyed = DriverConfig.Empty.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{ticket}-{slug}", Key: @"[A-Z]+-\d+"));

        Assert.Equal(@"[A-Z]+-\d+", DriverConfig.Parse(keyed.ToJson()).WorkspaceLandings["aurora"].Key);
        Assert.Matches("\"pattern\": \"feature/\\{ticket}-\\{slug}\",\\s*\"key\": ", keyed.ToJson());
        Assert.DoesNotContain("\"key\"",
            DriverConfig.Empty.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{ticket}-{slug}")).ToJson(), StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge, Key: @"[A-Z]+-\d+")));

        var read = DriverConfig.Parse("""
            {"landings":{"game":{"form":"branch","pattern":"feature/{ticket}","key":""}}}
            """);
        Assert.Equal(new LandingRule(LandingForm.Branch, "feature/{ticket}"), read.Landings["game"]);
    }

    /// <summary>
    /// A key the driver refuses in the file is ignored, and the rest of its rule stands (PROCESS1b-i): a key written by a reader
    /// that takes more never drops the rule, so the repository never lands by its workspace's rule or a merge unasked. A door that
    /// sets one still refuses it. The CLI's <c>driverconfig.test.ts</c> reads the same file the same way.
    /// </summary>
    [Fact]
    public void A_rule_whose_key_is_refused_is_read_without_it()
    {
        var read = DriverConfig.Parse("""
            {"landings":{
              "engine":{"form":"branch","pattern":"feature/{ticket}","key":"(?i)tk-\\d+","plugin":"example.github-pull-request","tidy":true,"autoAccept":true},
              "parity":{"form":"branch","pattern":"feature/{ticket}-{slug}","key":"TK\\_\\d+"},
              "unasked":{"form":"branch","pattern":"feature/{quest}","key":"[A-Z]+-\\d+"},
              "line":{"form":"merge","key":"[A-Z]+-\\d+"},
              "bad":{"form":"branch","pattern":"fixed","key":"[A-Z]+-\\d+"}}}
            """);

        Assert.Equal(new LandingRule(LandingForm.Branch, "feature/{ticket}", Tidy: true, Plugin: "example.github-pull-request", AutoAccept: true),
            read.Landings["engine"]);
        Assert.Equal(new LandingRule(LandingForm.Branch, "feature/{ticket}-{slug}"), read.Landings["parity"]);
        Assert.Equal(new LandingRule(LandingForm.Branch, "feature/{quest}"), read.Landings["unasked"]);
        Assert.Equal(LandingRule.Merge, read.Landings["line"]);
        // A rule that could not land work without its key is still not read.
        Assert.False(read.Landings.ContainsKey("bad"));
        // The door that sets one still refuses it, in the CLI's words.
        Assert.Contains(
            @"may escape only a punctuation mark or one of `\d \D \w \W \s \S \b \B \n \r \t \f \v`, which the driver and the CLI read alike, and `\_` is neither.",
            Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{ticket}", Key: @"TK\_\d+"))).Message);
    }

    /// <summary>
    /// <c>squash</c> on a landing rule (PROCESS1b-ii, D164's amendment point 2): Accept makes the pull request's branch one commit.
    /// Only a branch rule takes it, as only a branch rule accepts automatically. The driver's half of a TWIN with the CLI's
    /// <c>driverconfig.ts</c>, whose <c>driverconfig.test.ts</c> parses these two theories to hold its own tables to them.
    /// 🔴 Keep each row on one line, its cells literals: the CLI's test reads them.
    /// </summary>
    [Theory]
    [InlineData("branch", "feature/{ticket}-{slug}", true, null)]
    [InlineData("branch", "feature/{quest}-{slug}", false, null)]
    [InlineData("merge", null, true, "only a branch rule squashes")]
    [InlineData("merge", null, false, null)]
    public void Only_a_branch_rule_squashes(string form, string? pattern, bool squash, string? problem)
    {
        var said = LandingRules.Problem(new LandingRule(form, pattern, Squash: squash));

        if (problem is null) Assert.Null(said);
        else Assert.Contains(problem, said);
    }

    /// <summary>What the file says of <c>squash</c>, read: absent is off, only JSON <c>true</c> is on, and a merge carrying it is not read.</summary>
    [Theory]
    [InlineData("absent is off", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}"}}}""", "landings", "engine", "off")]
    [InlineData("true on a branch rule is on", """{"landings":{"engine":{"form":"branch","pattern":"feature/{ticket}-{slug}","squash":true}}}""", "landings", "engine", "on")]
    [InlineData("text is not true", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","squash":"true"}}}""", "landings", "engine", "off")]
    [InlineData("a merge carrying it is not read", """{"landings":{"engine":{"form":"merge","squash":true}}}""", "landings", "engine", "none")]
    [InlineData("a workspace's rule carries it", """{"workspaceLandings":{"aurora":{"form":"branch","pattern":"review/{session}","autoAccept":true,"squash":true}}}""", "workspaceLandings", "aurora", "on")]
    public void Squash_reads_as_the_cli_reads_it(string name, string file, string map, string key, string expected)
    {
        var config = DriverConfig.Parse(file);
        var rules = map == "workspaceLandings" ? config.WorkspaceLandings : config.Landings;
        var read = rules.TryGetValue(key, out var rule) ? rule.Squash ? "on" : "off" : "none";

        Assert.True(expected == read, $"{name}: {read}");
    }

    /// <summary>Written only when on, after the automatic acceptance, and kept through the file: the CLI writes it in the same place.</summary>
    [Fact]
    public void Squash_is_written_only_when_on_and_survives_the_file()
    {
        var on = DriverConfig.Empty.WithWorkspaceLanding(
            "aurora", new LandingRule(LandingForm.Branch, "feature/{ticket}-{slug}", Tidy: true, AutoAccept: true, Squash: true));

        Assert.True(DriverConfig.Parse(on.ToJson()).WorkspaceLandings["aurora"].Squash);
        Assert.Matches("\"autoAccept\": true,\\s*\"squash\": true", on.ToJson());
        Assert.DoesNotContain("squash",
            DriverConfig.Empty.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{quest}")).ToJson(), StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge, Squash: true)));
    }

    /// <summary>A named workflow's landing step on a branch carries the rule's squash; a merge step makes no branch to squash into.</summary>
    [Fact]
    public void A_named_workflows_branch_landing_carries_the_rules_squash()
    {
        var config = DriverConfig.Parse("""{"landings":{"web-app":{"form":"branch","pattern":"feature/{quest}","squash":true}}}""");
        WorkflowProcess Of(string landing)
        {
            using var document = System.Text.Json.JsonDocument.Parse($$"""[{"id":"work","kind":"work"},{{landing}}]""");
            var (steps, problem) = WorkflowNamed.ReadSteps(document.RootElement.Clone(), 2);
            Assert.True(problem is null, problem);
            return WorkflowProcess.Of(config, "web-app", "work",
                new WorkflowRunBinding("q1", "web-app", "work", "docs-to-pr", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 2 }, steps!);
        }

        Assert.True(Of("""{"id":"land","kind":"landing","form":"branch","accept":"you"}""").Landing.Rule.Squash);
        Assert.False(Of("""{"id":"land","kind":"landing","form":"merge","accept":"you"}""").Landing.Rule.Squash);
    }

    /// <summary>
    /// The one commit's message where the rule squashes (PROCESS1b-ii, D164's PROCESS1b-ii note point 2): the quest's title as its
    /// subject, the session's commits' subjects oldest first as its body, one line each, and the line naming the session last.
    /// </summary>
    [Fact]
    public void A_squash_message_is_the_title_then_each_commits_subject_then_the_session()
    {
        var message = SquashMessage.Of("Fix the gap", [new("a1", "the first step"), new("b2", "the verify step")], "s1a2b3c4");

        Assert.Equal("Fix the gap\n\n- the first step\n- the verify step\n\nSquashed by Daoris from session `s1a2b3c4`", message);
    }

    /// <summary>
    /// The subject is one line of at most 72 characters: a conversation's opening is its title, and may run to paragraphs. A blank
    /// title takes the oldest commit's subject, and with none the session's.
    /// </summary>
    [Fact]
    public void A_squash_subject_is_one_line_of_at_most_72_characters()
    {
        var opening = "Please look at why the review says nothing\nwhen the work sits uncommitted on the session branch, and fix it properly.";

        var subject = SquashMessage.Subject(opening, [], "s1");

        Assert.True(subject.Length <= SquashMessage.SubjectLimit, subject);
        Assert.EndsWith("…", subject);
        Assert.DoesNotContain('\n', subject);
        Assert.StartsWith("Please look at why the review says nothing when the work", subject);
        Assert.Equal("the first step", SquashMessage.Subject("  ", [new("a1", "the first step")], "s1"));
        Assert.Equal("Work from session s1", SquashMessage.Subject(null, [], "s1"));
        Assert.Equal("修复 登录", SquashMessage.Subject("修复 登录", [], "s1"));
    }

    /// <summary>
    /// The body is bounded at 50 lines and 4,000 characters: what is left out is counted on its last line, so the commit never
    /// claims to carry every subject when it does not.
    /// </summary>
    [Fact]
    public void A_squash_body_is_bounded_and_counts_what_it_left_out()
    {
        var many = Enumerable.Range(1, 60).Select(n => new LandingCommit($"c{n}", $"step {n}")).ToList();

        var lines = SquashMessage.Body(many).Split('\n');

        Assert.Equal(SquashMessage.BodyLines, lines.Length);
        Assert.Equal("- step 1", lines[0]);
        Assert.Equal("- step 49", lines[^2]);
        Assert.Equal("… and 11 more commits", lines[^1]);

        var long_ = Enumerable.Range(1, 40).Select(n => new LandingCommit($"c{n}", new string('x', 150))).ToList();
        var bounded = SquashMessage.Body(long_);
        Assert.True(bounded.Length <= SquashMessage.BodyLimit, $"{bounded.Length}");
        Assert.EndsWith("more commits", bounded);
        Assert.Equal("… and 1 more commit", SquashMessage.Body([new("a1", new string('x', 5000))]));
        Assert.Equal("", SquashMessage.Body([]));
    }
}
