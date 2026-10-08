using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// REVIEWENV1b (D154; the review environment design §1.4–§1.5, §2.1, §2.5–§2.6, §3.5): the review on the record. A chain
/// carries the person's review choice, an intake's only on their quoted words; a set-up step is a chain step in the same
/// repository as the work it shows; its session says what it showed, its driver posts each set-up with the commit it read,
/// and its done waits, held, for the person's verdict. A yes accepts a departure or its evidence and never a review.
/// </summary>
public sealed class ReviewStepTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-review-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T09:00:00Z");

    private const string Sentence = "add the compare setting to the report, and run it locally against dev data";
    private const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private const string Later = "0123456789abcdef0123456789abcdef01234567";
    private const string Look = "http://localhost:4200/reports/7";

    private static readonly QuestRequirement Setting =
        new("add the compare setting to the report", "The report shows the compare setting.");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            await service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files, sessions: _sessions, asks: _asks);
        _desk = new AskDesk(service, _asks, _exchange, files);
        _ledger = new SessionLedger(_quests, _sessions, service, _asks);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    private async Task<Ask> Asked() => (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

    private static QuestStep SetUpStep(string to = "reports", string environment = "local") =>
        new(to, "Show {parent} in local for review", "Set the work up and show it.") { SetUpIn = environment };

    private Task<AskOutcome> Publish(
        Ask ask, IReadOnlyList<QuestStep> then, QuestReview? review = null, string? session = null, string title = "Add the compare setting",
        ReviewProposed? proposal = null) =>
        _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft(title, "The report gains a setting.")
        {
            Requirements = [Setting], Then = then, Review = review, ReviewProposal = proposal,
        }, session: session);

    private static QuestAnswer Met => new(1, Met: "The setting is on the report.");

    /// <summary>
    /// The build quest, taken and closed done, and the set-up step its close published, with a note step to `checker` after
    /// it. One ask holds every quest a test makes, so each is told apart by its title.
    /// </summary>
    private async Task<(Ask Ask, Quest Build, Quest SetUp)> Built(string title = "Add the compare setting", QuestReview? review = null)
    {
        var ask = await Asked();
        var published = await Publish(ask, [SetUpStep(), new QuestStep("checker", "Note {parent}", "Write it down.")], review, title: title);
        Assert.Equal(AskRefusal.None, published.Refusal);
        var build = published.Quest!;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(build.Id, "take", null, Now)).Refusal);
        var done = await _exchange.RespondAsync(build.Id, "done", "Built.", Now, answers: [Met]);
        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        var setUp = (await FollowUps(ask, build)).Single();
        return (ask, build, setUp);
    }

    /// <summary>A session working the set-up step, which took it through its own door, in a tree of its own.</summary>
    private async Task<Session> Working(Quest setUp, string tree = "setup")
    {
        var path = Path.Combine(_root, "trees", tree);
        Directory.CreateDirectory(Path.Combine(path, "dist", "app"));
        var session = (await _ledger.OpenAsync(setUp.Id, "stub", Now, tree: path)).Session!;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(setUp.Id, "take", null, Now)).Refusal);
        return session;
    }

    /// <summary>The set-up step shown: its session says a set-up and closes it done, and its driver posts it at <paramref name="commit"/>.</summary>
    private async Task<(Session Session, Quest Shown)> Shown(Quest setUp, string commit = Commit)
    {
        var session = await Working(setUp);
        Assert.Equal(ReviewSayRefusal.None,
            (await _ledger.ReadyAsync(session.Id, Look, "The report with the compare setting on.", "Open the report and turn it on.", null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now, answers: [Met])).Refusal);
        var posted = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(commit, "local", Session: session.Id), Now);
        Assert.Equal(QuestRespondRefusal.None, posted.Refusal);
        return (session, posted.Quest!);
    }

    private async Task<IReadOnlyList<Quest>> FollowUps(Ask ask, Quest parent) =>
        [.. (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Where(quest => quest.Parent == parent.Id)];

    // ——— §2.1: a set-up step is a chain step to the repository whose work it shows.

    /// <summary>
    /// A set-up step is kept on the chain as composed, and the close of the work before it publishes it as a quest that names its
    /// environment, in the same repository, carrying the chain's requirements and its review choice.
    /// </summary>
    [Fact]
    public async Task A_set_up_step_is_published_by_the_close_before_it_naming_its_environment()
    {
        var (_, build, setUp) = await Built(review: new QuestReview("local"));

        Assert.Equal("local", Assert.Single(build.Then, step => step.SetUpIn is not null).SetUpIn);
        Assert.Equal("local", setUp.SetUpIn);
        Assert.Equal("reports", setUp.To);
        Assert.Equal(build.Id, setUp.Parent);
        Assert.Equal([Setting], setUp.Requirements);
        Assert.Equal(new QuestReview("local"), setUp.Review);
        Assert.Equal("checker", Assert.Single(setUp.Then).To);
        Assert.Null(Assert.Single(setUp.Then).SetUpIn);
        var replayed = QuestLog.Replay(await _quests.HistoryAsync(setUp.Id))!;
        Assert.Equal(("local", new QuestReview("local")), (replayed.SetUpIn, replayed.Review));
    }

    /// <summary>
    /// 🔴 The exchange judges a set-up step's shape when the chain is composed, and nothing is published: an environment that is
    /// not a name, one that reads as production, a step to another repository than the one before it, a later step to the
    /// same repository after it, and a chain whose choice is `off`.
    /// </summary>
    [Theory]
    [InlineData("prod", "reports", null, "production")]
    [InlineData("pre-live", "reports", null, "production")]
    [InlineData("Local", "reports", null, "not an environment's name")]
    [InlineData("none", "reports", null, "names no environment")]
    [InlineData("local", "checker", null, "the step before it asks `reports`")]
    [InlineData("local", "reports", "reports", "step 2 asks `reports` after it")]
    [InlineData("local", "reports", "OFF", "review choice is `off`")]
    public async Task A_set_up_step_that_is_not_one_is_refused_when_the_chain_is_composed(
        string environment, string to, string? then, string said)
    {
        var ask = await Asked();
        IReadOnlyList<QuestStep> steps = then is "reports"
            ? [SetUpStep(to, environment), new QuestStep("reports", "Again {parent}", "More.")]
            : [SetUpStep(to, environment)];

        var refused = await Publish(ask, steps, review: then == "OFF" ? new QuestReview(Reviews.Off) : null);

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains(said, refused.Message);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);
    }

    // ——— §1.4–§1.5: the chain's choice, the ask's, and the intake's proposal.

    /// <summary>
    /// The person's own door sets a chain's choice with any words they give, and every step inherits it. A session sets one only
    /// on the person's words, which must stand in what they said on the ask: an unsaid quote, and none, are refused, naming why,
    /// and nothing is published.
    /// </summary>
    [Fact]
    public async Task A_chains_choice_is_the_persons_or_quotes_them_and_every_step_inherits_it()
    {
        var ask = await Asked();
        var persons = await Publish(ask, [], new QuestReview("local", "please look"), title: "Person's");
        Assert.Equal(AskRefusal.None, persons.Refusal);
        Assert.Equal(new QuestReview("local", "please look"), persons.Quest!.Review);

        var unsaid = await Publish(ask, [], new QuestReview(Reviews.Off, "it's a typo"), session: "s1a2b3c4", title: "Unsaid");
        Assert.Equal(AskRefusal.QuestRefused, unsaid.Refusal);
        Assert.Contains("never said", unsaid.Message);
        Assert.Contains("\"it's a typo\"", unsaid.Message);

        var wordless = await Publish(ask, [], new QuestReview(Reviews.On), session: "s1a2b3c4", title: "Wordless");
        Assert.Equal(AskRefusal.QuestRefused, wordless.Refusal);
        Assert.Contains("only on the person's own words", wordless.Message);

        var quoted = await Publish(
            ask, [new QuestStep("checker", "Check {parent}", "Look.")], new QuestReview("local", "run it locally against dev data"),
            session: "s1a2b3c4", title: "Quoted");
        Assert.Equal(AskRefusal.None, quoted.Refusal);
        Assert.Equal("local", quoted.Quest!.Review!.Choice);
        Assert.Contains("review choice is `local`", quoted.Message);
        await _exchange.RespondAsync(quoted.Quest.Id, "take", null, Now);
        await _exchange.RespondAsync(quoted.Quest.Id, "done", "Built.", Now, answers: [Met]);
        Assert.Equal(quoted.Quest.Review, (await FollowUps(ask, quoted.Quest)).Single().Review);

        var badChoice = await Publish(ask, [], new QuestReview("production"), title: "Bad");
        Assert.Equal(AskRefusal.QuestRefused, badChoice.Refusal);
        Assert.Contains("production", badChoice.Message);
        Assert.Equal([persons.Quest.Id, quoted.Quest.Id], (await _desk.FindAsync(ask.Id))!.Quests);
    }

    /// <summary>
    /// 🔴 A session publishing a quest a repository asks, on no ask, has no words of the person's to quote, so its choice is
    /// refused rather than kept unchecked.
    /// </summary>
    [Fact]
    public async Task A_sessions_choice_on_no_ask_is_refused()
    {
        var refused = await _exchange.PublishAsync(
            new QuestAsk("checker", "reports", "A repository's quest", "b") { Review = new QuestReview("local", "words"), PublishedBy = "s1a2b3c4" },
            Now);

        Assert.Equal(QuestPublishRefusal.BadReview, refused.Refusal);
        Assert.Contains("on no ask", refused.Message);
    }

    /// <summary>
    /// The person sets the ask's choice in the composer and again on its page, with their words; the latest stands. A choice
    /// that is not one is refused, and nothing is asked.
    /// </summary>
    [Fact]
    public async Task An_asks_choice_is_kept_with_its_words_and_the_latest_stands()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence) { Review = "local", ReviewWords = "show me first" }, Now);
        Assert.Equal(AskRefusal.None, asked.Refusal);
        Assert.Equal(new AskReviewChoice("local", Now, "show me first"), Assert.Single(asked.Ask!.ReviewChoices));

        var off = await _desk.ChooseReviewAsync(asked.Ask.Id, "off", "  it's only words  ", Now.AddMinutes(1));
        Assert.Equal(AskRefusal.None, off.Refusal);
        Assert.Contains("no review", off.Message);
        var stands = (await _desk.FindAsync(asked.Ask.Id))!;
        Assert.Equal(["local", "off"], stands.ReviewChoices.Select(choice => choice.Choice));
        Assert.Equal("it's only words", stands.ReviewChoices[^1].Words);

        Assert.Equal(AskRefusal.BadReview, (await _desk.ChooseReviewAsync(asked.Ask.Id, "live", null, Now)).Refusal);
        Assert.Equal(AskRefusal.NotFound, (await _desk.ChooseReviewAsync("ffffff", "on", null, Now)).Refusal);
        var refused = await _desk.AskAsync(new AskRequest("work", "another sentence") { Review = "prd" }, Now);
        Assert.Equal(AskRefusal.BadReview, refused.Refusal);
        Assert.Null(refused.Ask);
    }

    /// <summary>
    /// An intake proposes a choice with its reason where it has no words of the person's to set one on: kept on the ask with the
    /// quest it came with, a proposal only. One with no reason, a reason past 300 characters, one beside a choice, and one from
    /// the person's own publish are refused, and nothing is published.
    /// </summary>
    [Fact]
    public async Task An_intakes_proposal_is_kept_on_the_ask_and_only_a_proposal()
    {
        var ask = await Asked();
        var proposed = await Publish(ask, [], session: "s1a2b3c4", title: "Proposed", proposal: new ReviewProposed("off", "It changes only a document."));
        Assert.Equal(AskRefusal.None, proposed.Refusal);
        Assert.Contains("only the person's press applies", proposed.Message);
        var kept = (await _desk.FindAsync(ask.Id))!;
        Assert.Equal(new AskReviewProposal("off", "It changes only a document.", Now, "s1a2b3c4", proposed.Quest!.Id), Assert.Single(kept.ReviewProposals));
        Assert.Empty(kept.ReviewChoices);
        Assert.Null(proposed.Quest.Review);

        foreach (var (proposal, review, session, said) in new (ReviewProposed, QuestReview?, string?, string)[]
        {
            (new ReviewProposed("off", ""), null, "s1a2b3c4", "says why"),
            (new ReviewProposed("off", new string('a', Reviews.ReasonLimit + 1)), null, "s1a2b3c4", "at most 300"),
            (new ReviewProposed("off", "Why."), new QuestReview("local", "run it locally against dev data"), "s1a2b3c4", "never both"),
            (new ReviewProposed("off", "Why."), null, null, "the person sets their own choice"),
        })
        {
            var refused = await Publish(ask, [], review, session, "Refused", proposal);
            Assert.Equal(AskRefusal.BadReview, refused.Refusal);
            Assert.Contains(said, refused.Message);
        }

        Assert.Single((await _desk.FindAsync(ask.Id))!.Quests);
    }

    // ——— §2.6: what the step's session says, the commit its driver reads, and the done it holds.

    /// <summary>
    /// 🔴 A set-up step's done is refused until a set-up has been said, so a done always has one; once its session says one,
    /// it closes done and held, waiting for the person's review: its next step unpublished, the quest outstanding, and its ask
    /// not done.
    /// </summary>
    [Fact]
    public async Task A_set_up_steps_done_waits_on_a_set_up_said_and_then_holds_for_the_review()
    {
        var (ask, _, setUp) = await Built();
        var session = await Working(setUp);

        var early = await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now, answers: [Met]);
        Assert.Equal(QuestRespondRefusal.NotShown, early.Refusal);
        Assert.Contains("review_ready", early.Message);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(setUp.Id))!.Status);

        Assert.Equal(ReviewSayRefusal.None, (await _ledger.ReadyAsync(session.Id, Look, "The report.", "Open it.", null, Now)).Refusal);
        var done = await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now, answers: [Met]);

        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        Assert.Contains("waits for the person's review in `local`", done.Message);
        var held = (await _quests.FindAsync(setUp.Id))!;
        Assert.Equal((QuestStatus.Done, QuestHold.Unreviewed), (held.Status, held.Hold));
        Assert.Empty(await FollowUps(ask, held));
        Assert.Contains(await _quests.ListAsync(receiver: "reports"), quest => quest.Id == setUp.Id);
        Assert.NotEqual(AskState.Done, (await _desk.FindAsync(ask.Id))!.State);
    }

    /// <summary>
    /// The driver posts the session's said set-up with the commit it read, never the session's: a set-up operation on the
    /// quest, the session's words, its served folder and its session. Posted again it posts nothing new; a later set-up is a
    /// second one.
    /// </summary>
    [Fact]
    public async Task The_driver_posts_each_set_up_said_once_with_the_commit_it_read()
    {
        var (_, _, setUp) = await Built();
        var session = await Working(setUp);
        Assert.Equal(ReviewSayRefusal.None, (await _ledger.ServeAsync(session.Id, "dist/app", "http://localhost:4200", Now)).Refusal);
        await _ledger.ReadyAsync(session.Id, Look, "The report.", "Open it.", "npm run serve", Now);
        await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now, answers: [Met]);

        var posted = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(Commit.ToUpperInvariant(), "local", Session: session.Id), Now);

        Assert.Equal(QuestRespondRefusal.None, posted.Refusal);
        Assert.Contains("at `a1b2c3d`", posted.Message);
        var kept = Assert.Single(posted.Quest!.SetUps);
        Assert.Equal((Commit, Look, "The report.", "Open it.", "dist/app", "npm run serve", session.Id, true),
            (kept.Commit, kept.Look, kept.Shows, kept.Again, kept.Served, kept.Run, kept.Session, kept.Local));
        var history = await _quests.HistoryAsync(setUp.Id);
        Assert.Equal(QuestOperationKind.SetUp, history[^1].Kind);
        Assert.True(Assert.Single((await _sessions.ReviewOfAsync(session.Id)).Said).Posted);

        var again = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(Commit, "local", Session: session.Id), Now);
        Assert.Contains("nothing new", again.Message);
        Assert.Single((await _quests.FindAsync(setUp.Id))!.SetUps);

        await _ledger.ReadyAsync(session.Id, Look, "The report again.", "Open it.", null, Now);
        var second = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(Later, "local", Session: session.Id), Now);
        Assert.Equal(2, second.Quest!.SetUps.Count);
        Assert.Equal(Later, second.Quest.SetUps[^1].Commit);
    }

    /// <summary>
    /// The person records a set-up of their own (I set it up myself…): where to look, their words and the commit, with no
    /// session, and their done on the open step then closes it held for their review.
    /// </summary>
    [Fact]
    public async Task The_person_records_their_own_set_up_and_closes_the_step_on_it()
    {
        var (_, _, setUp) = await Built();
        Assert.Equal(QuestRespondRefusal.NotShown, (await _exchange.PersonDoneAsync(setUp.Id, null, Now)).Refusal);

        var own = await _exchange.PostSetUpAsync(
            setUp.Id, new QuestSetUpPost(Commit, "deployed", Look: "https://dev.example.test/reports/7", Shows: "I deployed it."), Now);
        Assert.Equal(QuestRespondRefusal.None, own.Refusal);
        var kept = Assert.Single(own.Quest!.SetUps);
        Assert.Null(kept.Session);
        Assert.False(kept.Local);

        var done = await _exchange.PersonDoneAsync(setUp.Id, null, Now);
        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        Assert.Equal(QuestHold.Unreviewed, done.Quest!.Hold);
        Assert.Contains("waits for the person's review", done.Message);
    }

    /// <summary>🔴 Each way a post is not one is refused naming why, and nothing is kept.</summary>
    [Theory]
    [InlineData("abc", "local", "SESSION", null, "full id")]
    [InlineData(Commit, "production", "SESSION", null, "`local` or `deployed`")]
    [InlineData(Commit, "local", null, null, "name exactly one")]
    [InlineData(Commit, "local", "SESSION", "http://localhost:4200", "name exactly one")]
    [InlineData(Commit, "deployed", null, "file:///C:/report.html", "not where to look")]
    [InlineData(Commit, "local", "nobody", null, "No session `nobody`")]
    public async Task A_post_that_is_not_one_is_refused(string commit, string kind, string? session, string? look, string said)
    {
        var (_, _, setUp) = await Built();
        var working = await Working(setUp);
        await _ledger.ReadyAsync(working.Id, Look, "The report.", "Open it.", null, Now);

        var refused = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(commit, kind, session == "SESSION" ? working.Id : session, look), Now);

        Assert.NotEqual(QuestRespondRefusal.None, refused.Refusal);
        Assert.Contains(said, refused.Message);
        Assert.Empty((await _quests.FindAsync(setUp.Id))!.SetUps);
    }

    /// <summary>
    /// 🔴 Only a set-up step's own session serves or says a set-up, once it is working the step: a session on another quest is
    /// refused, as is a folder outside its tree, an address that is not an origin, and words past their bounds.
    /// </summary>
    [Fact]
    public async Task Only_a_set_up_steps_own_session_serves_or_says_a_set_up()
    {
        var (_, build, setUp) = await Built();
        var other = await _sessions.CreateAsync(build.Id, "reports", "stub", Now, "work", tree: Path.Combine(_root, "trees", "build"));
        Assert.Equal(ReviewSayRefusal.NotASetUpStep, (await _ledger.ReadyAsync(other.Id, Look, "x", "y", null, Now)).Refusal);
        Assert.Equal(ReviewSayRefusal.NotFound, (await _ledger.ReadyAsync(null, Look, "x", "y", null, Now)).Refusal);

        var opened = (await _ledger.OpenAsync(setUp.Id, "stub", Now, tree: Path.Combine(_root, "trees", "early"))).Session!;
        Assert.Equal(ReviewSayRefusal.NotWorking, (await _ledger.ReadyAsync(opened.Id, Look, "x", "y", null, Now)).Refusal);

        var session = await Working(setUp);
        foreach (var (folder, address, said) in new[]
        {
            ("../outside", "http://localhost:4200", "`..`"),
            ("missing", "http://localhost:4200", "not a folder in your tree"),
            ("dist/app", "http://localhost:4200/reports", "an absolute http or https origin"),
            ("dist/app", "file:///C:/app", "an absolute http or https origin"),
        })
        {
            var refused = await _ledger.ServeAsync(session.Id, folder, address, Now);
            Assert.Equal(ReviewSayRefusal.BadShape, refused.Refusal);
            Assert.Contains(said, refused.Message);
        }

        foreach (var (look, shows, again, run, said) in new (string, string, string, string?, string)[]
        {
            ("not an address", "x", "y", null, "`look`"),
            (Look, "", "y", null, "`shows`"),
            (Look, new string('s', Reviews.ShowsLimit + 1), "y", null, "at most 300"),
            (Look, "x", new string('a', Reviews.AgainLimit + 1), null, "at most 600"),
            (Look, "x", "y", "two\nlines", "`run`"),
        })
        {
            Assert.Equal(ReviewSayRefusal.BadShape, (await _ledger.ReadyAsync(session.Id, look, shows, again, run, Now)).Refusal);
        }

        Assert.Empty((await _sessions.ReviewOfAsync(session.Id)).Said);
        Assert.Null((await _sessions.ReviewOfAsync(session.Id)).Serving);
    }

    // ——— §3.3–§3.5: the person's verdict, and what it lets go.

    /// <summary>
    /// The person's `reviewed` on the newest set-up lets the step go: the chain's next step is published in the same
    /// transaction, and the ask reads done once it closes. Their words are kept on the ask as a `reviewed` word. A second
    /// `reviewed` is refused.
    /// </summary>
    [Fact]
    public async Task Reviewed_on_the_newest_set_up_lets_the_step_go()
    {
        var (ask, _, setUp) = await Built();
        var (_, shown) = await Shown(setUp);

        var reviewed = await _exchange.ReviewAsync(setUp.Id, "reviewed", "That is the setting.", null, Now);

        Assert.Equal(QuestRespondRefusal.None, reviewed.Refusal);
        Assert.Contains("what its review held goes on", reviewed.Message);
        Assert.Contains("Then: published", reviewed.Message);
        Assert.Contains($"kept on ask `#{ask.Id}`", reviewed.Message);
        var verdict = Assert.Single(reviewed.Quest!.Verdicts);
        Assert.Equal((Reviews.Reviewed, shown.SetUps[0].Ref, Commit, "That is the setting."), (verdict.Said, verdict.SetUp, verdict.Commit, verdict.Words));
        Assert.Null(reviewed.Quest.Hold);
        var note = (await FollowUps(ask, setUp)).Single();
        Assert.Equal("checker", note.To);
        var word = (await _desk.FindAsync(ask.Id))!.Words[^1];
        Assert.Equal((AskWordKind.Reviewed, "That is the setting.", setUp.Id), (word.Kind, word.Text, word.Quest));
        Assert.Equal("reviewed", AskWord.Spell(word.Kind));

        Assert.Equal(QuestRespondRefusal.ReviewRefused, (await _exchange.ReviewAsync(setUp.Id, "reviewed", null, null, Now)).Refusal);

        await _exchange.RespondAsync(note.Id, "take", null, Now);
        await _exchange.RespondAsync(note.Id, "done", "Noted.", Now, answers: [Met]);
        Assert.Equal(AskState.Done, (await _desk.FindAsync(ask.Id))!.State);
    }

    /// <summary>
    /// A `not-yet` needs the person's words, keeps the step held and its words on the quest and the ask as a `not-yet`. The set-up
    /// said next is the one a `reviewed` answers: a `reviewed` of the older set-up is refused, since the newer holds the work that
    /// lands, and a newer set-up holds a reviewed step for the person again.
    /// </summary>
    [Fact]
    public async Task Not_yet_keeps_the_step_held_until_a_newer_set_up_is_reviewed()
    {
        var (ask, _, setUp) = await Built();
        var (session, shown) = await Shown(setUp);
        var first = shown.SetUps[0].Ref;

        Assert.Equal(QuestRespondRefusal.BadReviewVerdict, (await _exchange.ReviewAsync(setUp.Id, "not-yet", "  ", null, Now)).Refusal);
        var notYet = await _exchange.ReviewAsync(setUp.Id, "not-yet", "the label still reads the old name", null, Now);
        Assert.Equal(QuestRespondRefusal.None, notYet.Refusal);
        Assert.Equal(QuestHold.Unreviewed, notYet.Quest!.Hold);
        Assert.Equal("the label still reads the old name", Assert.Single(notYet.Quest.Verdicts).Words);
        Assert.Equal(("not-yet", AskWordKind.NotYet), (AskWord.Spell(AskWordKind.NotYet), (await _desk.FindAsync(ask.Id))!.Words[^1].Kind));

        await _ledger.ReadyAsync(session.Id, Look, "The label, corrected.", "Open it.", null, Now);
        await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(Later, "local", Session: session.Id), Now);

        var older = await _exchange.ReviewAsync(setUp.Id, "reviewed", null, first, Now);
        Assert.Equal(QuestRespondRefusal.ReviewRefused, older.Refusal);
        Assert.Contains("shown again since", older.Message);

        var reviewed = await _exchange.ReviewAsync(setUp.Id, "reviewed", null, null, Now);
        Assert.Equal(QuestRespondRefusal.None, reviewed.Refusal);
        Assert.Equal(Later, reviewed.Quest!.Verdicts[^1].Commit);
        Assert.Null(reviewed.Quest.Hold);

        await _ledger.ReadyAsync(session.Id, Look, "Shown once more.", "Open it.", null, Now);
        var third = "fedcba9876543210fedcba9876543210fedcba98";
        var again = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(third, "local", Session: session.Id), Now);
        Assert.Equal(QuestHold.Unreviewed, again.Quest!.Hold);
    }

    /// <summary>
    /// A skip lets a set-up step go with the person's words, kept as a `skipped` word; on the chain's last quest where no set-up
    /// step was composed, it is recorded on that quest, which nothing held. A second skip, a `reviewed` on a quest that is no
    /// set-up step, and a verdict nobody says are refused.
    /// </summary>
    [Fact]
    public async Task A_skip_lets_the_work_go_on_a_set_up_step_or_is_recorded_on_the_chains_quest()
    {
        var (ask, build, setUp) = await Built();
        await Shown(setUp);

        var skipped = await _exchange.ReviewAsync(setUp.Id, "skipped", "no need, it's a typo", null, Now);
        Assert.Equal(QuestRespondRefusal.None, skipped.Refusal);
        Assert.Null(skipped.Quest!.Hold);
        Assert.Contains("what its review held goes on", skipped.Message);
        Assert.Single(await FollowUps(ask, setUp));
        Assert.Equal(AskWordKind.Skipped, (await _desk.FindAsync(ask.Id))!.Words[^1].Kind);
        Assert.Equal(QuestRespondRefusal.ReviewRefused, (await _exchange.ReviewAsync(setUp.Id, "skipped", null, null, Now)).Refusal);

        var onBuild = await _exchange.ReviewAsync(build.Id, "skipped", null, null, Now);
        Assert.Equal(QuestRespondRefusal.None, onBuild.Refusal);
        Assert.Contains("waits for no review", onBuild.Message);
        Assert.Equal(Reviews.Skipped, Assert.Single(onBuild.Quest!.Verdicts).Said);
        Assert.Equal(QuestRespondRefusal.NotSetUpStep, (await _exchange.ReviewAsync(build.Id, "reviewed", null, null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.BadReviewVerdict, (await _exchange.ReviewAsync(build.Id, "looks fine", null, null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.NotFound, (await _exchange.ReviewAsync("ffffffffffff", "skipped", null, null, Now)).Refusal);
    }

    /// <summary>
    /// 🔴 A yes accepts a departure or its evidence and never a review (design §3.5): a set-up step that departed is held for
    /// the yes first; the yes lets the departure go and the review still holds it; a second yes is refused naming the review.
    /// </summary>
    [Fact]
    public async Task A_yes_accepts_the_departure_and_never_the_review()
    {
        var (ask, _, setUp) = await Built();
        var session = await Working(setUp);
        await _ledger.ReadyAsync(session.Id, Look, "The report.", "Open it.", null, Now);
        var departed = await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now,
            answers: [new QuestAnswer(1, null, Departed: "It shows on another page.", Quote: "add the compare setting to the report")]);
        Assert.Equal(QuestHold.Departed, departed.Quest!.Hold);

        var yes = await _exchange.AcceptAsync(setUp.Id, Now);
        Assert.Equal(QuestRespondRefusal.None, yes.Refusal);
        Assert.Equal(QuestHold.Unreviewed, yes.Quest!.Hold);
        Assert.Contains("waits for the person's review", yes.Message);
        Assert.Empty(await FollowUps(ask, setUp));

        var again = await _exchange.AcceptAsync(setUp.Id, Now);
        Assert.Equal(QuestRespondRefusal.NotHeld, again.Refusal);
        Assert.Contains("waits for your review in `local`", again.Message);
    }

    /// <summary>
    /// The hold's order is unchanged: a departure first, then evidence, then the review, so a set-up step's evidence is read
    /// before its review holds it, and a found verdict leaves it held for the person.
    /// </summary>
    [Fact]
    public async Task Evidence_holds_before_the_review_and_a_found_verdict_leaves_the_review()
    {
        var ask = await Asked();
        var report = Setting with { Evidence = [new QuestEvidence("docs/report.md")] };
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Write the report", "b")
        {
            Requirements = [report], Then = [SetUpStep()],
        });
        var build = published.Quest!;
        await _exchange.RespondAsync(build.Id, "take", null, Now);
        await _exchange.RespondAsync(build.Id, "done", "Built.", Now, answers: [Met]);
        Assert.Equal(QuestHold.EvidenceUnread, (await _quests.FindAsync(build.Id))!.Hold);
        await _exchange.EvidenceAsync(build.Id, Found(), Now);
        var setUp = (await FollowUps(ask, build)).Single();

        var session = await Working(setUp);
        await _ledger.ReadyAsync(session.Id, Look, "The report.", "Open it.", null, Now);
        await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now, answers: [Met]);
        Assert.Equal(QuestHold.EvidenceUnread, (await _quests.FindAsync(setUp.Id))!.Hold);

        var found = await _exchange.EvidenceAsync(setUp.Id, Found(), Now);
        Assert.Equal(QuestRespondRefusal.None, found.Refusal);
        Assert.Equal(QuestHold.Unreviewed, found.Quest!.Hold);
        Assert.Contains("waits for the person's review", found.Message);

        static QuestEvidenceVerdict Found() => new(Commit, "session-end",
            [new QuestEvidenceRead(1, "docs/report.md", null, "found") { Object = Later, Changed = true }]);
    }

    // ——— §2.1, §3.6: the person's *Set it up in `<environment>`*.

    /// <summary>
    /// The person's press publishes a set-up step following a done quest, in Daoris's words, to its repository, carrying its
    /// requirements and its chain's choice, held to a done; pressed again it is the same step. Work not done, a set-up step,
    /// a chain that composes one, another environment after one, and production are refused.
    /// </summary>
    [Fact]
    public async Task The_persons_set_it_up_publishes_a_set_up_step_after_done_work()
    {
        var ask = await Asked();
        var published = await Publish(ask, [], new QuestReview(Reviews.Off), title: "Plain");
        var work = published.Quest!;
        Assert.Equal(QuestRespondRefusal.ReviewRefused, (await _exchange.PublishSetUpStepAsync(work.Id, "local", Now)).Refusal);
        await _exchange.RespondAsync(work.Id, "take", null, Now);
        await _exchange.RespondAsync(work.Id, "done", "Built.", Now, answers: [Met]);

        var pressed = await _exchange.PublishSetUpStepAsync(work.Id, "local", Now);

        Assert.Equal(QuestRespondRefusal.None, pressed.Refusal);
        var step = pressed.Quest!;
        Assert.Equal(("reports", work.Id, "local", work.From), (step.To, step.Parent, step.SetUpIn, step.From));
        Assert.Equal([Setting], step.Requirements);
        Assert.Equal(new QuestReview(Reviews.Off), step.Review);
        Assert.Contains($"#{work.Id}", step.Title);
        Assert.Equal(step.Id, (await _exchange.PublishSetUpStepAsync(work.Id, "local", Now)).Quest!.Id);
        Assert.Contains("one set-up step per repository", (await _exchange.PublishSetUpStepAsync(work.Id, "dev", Now)).Message);
        Assert.Contains("set-up step itself", (await _exchange.PublishSetUpStepAsync(step.Id, "local", Now)).Message);
        Assert.Equal(QuestRespondRefusal.BadSetUp, (await _exchange.PublishSetUpStepAsync(work.Id, "production", Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.NotFound, (await _exchange.PublishSetUpStepAsync("ffffffffffff", "local", Now)).Refusal);

        var (_, build, _) = await Built(title: "Composed");
        Assert.Contains("already composes", (await _exchange.PublishSetUpStepAsync(build.Id, "dev", Now)).Message);
    }

    // ——— §3.5: what crosses machines.

    /// <summary>
    /// 🔴 A local set-up crosses without its address, which is a tab on the machine that showed it, and is read there as local;
    /// a deployed one crosses as given. The machine that made it keeps its own.
    /// </summary>
    [Fact]
    public void A_local_set_up_crosses_as_local_without_its_address()
    {
        var local = new QuestOperation("abcdefabcdef", QuestOperationKind.SetUp, "m1", 4, Now,
            SetUp: new QuestSetUp(Commit) { Look = Look, Shows = "The report.", Again = "Open it.", Session = "s1a2b3c4", Local = true });
        var deployed = local with { SetUp = local.SetUp! with { Local = false, Look = "https://dev.example.test/r/7" } };

        var crossed = QuestWire.ReadPush(QuestWire.Push(0, [local, deployed]))!.Value.Operations;

        Assert.Null(crossed[0].SetUp!.Look);
        Assert.True(crossed[0].SetUp!.Local);
        Assert.Equal("The report.", crossed[0].SetUp!.Shows);
        Assert.Equal("https://dev.example.test/r/7", crossed[1].SetUp!.Look);
        Assert.DoesNotContain("localhost", QuestWire.Push(0, [local]));
    }

    /// <summary>
    /// 🔴 The wire refuses a review on the record that is not whole: a set-up with no commit, a deployed one with nowhere to
    /// look, a verdict saying nothing it may say, a `reviewed` naming no set-up, a `not-yet` without words, and a publish whose
    /// choice or set-up step's environment reads as production.
    /// </summary>
    [Theory]
    [InlineData("""{ "kind": "setup", "setUp": { "look": "http://localhost:4200" } }""")]
    [InlineData("""{ "kind": "setup", "setUp": { "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678" } }""")]
    [InlineData("""{ "kind": "setup" }""")]
    [InlineData("""{ "kind": "verdict", "verdict": { "said": "fine" } }""")]
    [InlineData("""{ "kind": "verdict", "verdict": { "said": "reviewed", "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678" } }""")]
    [InlineData("""{ "kind": "verdict", "verdict": { "said": "not-yet", "setUp": { "machine": "m1", "sequence": 4 }, "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678" } }""")]
    [InlineData("""{ "kind": "published", "asked": { "from": "f", "to": "t", "title": "t", "body": "b", "review": { "choice": "prod" } } }""")]
    [InlineData("""{ "kind": "published", "asked": { "from": "f", "to": "t", "title": "t", "body": "b", "setUpIn": "live" } }""")]
    [InlineData("""{ "kind": "published", "asked": { "from": "f", "to": "t", "title": "t", "body": "b", "then": [{ "to": "t", "title": "t", "body": "b", "setUpIn": "prd" }] } }""")]
    public void A_review_on_the_wire_that_is_not_whole_is_refused(string fields)
    {
        var operation = """{ "machine": "m1", "sequence": 5, "quest": "abcdefabcdef", "at": "2026-10-08T09:00:00Z", """
                        + fields.TrimStart()[1..];
        Assert.Null(QuestWire.ReadPush($$"""{ "base": 0, "operations": [{{operation}}] }"""));
        Assert.Contains("never reads as production", QuestWire.Shape);

        // The same operation made whole crosses, so what refused it is the part each case leaves out.
        var whole = """{ "machine": "m1", "sequence": 5, "quest": "abcdefabcdef", "at": "2026-10-08T09:00:00Z", "kind": "setup", """
                    + "\"setUp\": { \"commit\": \"" + Commit + "\", \"local\": true } }";
        Assert.NotNull(QuestWire.ReadPush($$"""{ "base": 0, "operations": [{{whole}}] }"""));
    }

    /// <summary>
    /// A review's verdict that lost while the step is still held takes the step it published with it, as a lost yes does: a
    /// remote's newer set-up beside the one it reviewed leaves it no move.
    /// </summary>
    [Fact]
    public async Task A_verdict_that_loses_takes_the_step_it_published_with_it()
    {
        var (ask, _, setUp) = await Built();
        await Shown(setUp);
        var history = await _quests.HistoryAsync(setUp.Id);
        await _exchange.ReviewAsync(setUp.Id, "reviewed", null, null, Now);
        Assert.Single(await FollowUps(ask, setUp));

        // The remote numbered everything up to the set-up, then another machine's newer set-up, before this machine's verdict.
        var accepted = history.Select((operation, index) => operation with { Number = index + 1 }).ToList();
        var newer = new QuestOperation(setUp.Id, QuestOperationKind.SetUp, "m9", 1, Now.AddMinutes(1),
            SetUp: new QuestSetUp(Later) { Look = "https://dev.example.test/r/7", Shows = "Shown elsewhere." }, Number: accepted.Count + 1);
        await _quests.IntegrateAsync("work", [.. accepted, newer], accepted.Count + 1);

        var stands = (await _quests.FindAsync(setUp.Id))!;
        Assert.Equal(QuestHold.Unreviewed, stands.Hold);
        Assert.Empty(stands.Verdicts);
        Assert.Empty(await FollowUps(ask, setUp));
    }

    /// <summary>A store from before reviews reads every quest as it did: no choice, no set-up step, nothing shown, nothing held for it.</summary>
    [Fact]
    public async Task A_quest_from_before_reviews_reads_as_it_did()
    {
        var ask = await Asked();
        var plain = (await Publish(ask, [new QuestStep("checker", "Check {parent}", "Look.")], title: "Plain")).Quest!;

        var read = (await _quests.FindAsync(plain.Id))!;
        Assert.Null(read.Review);
        Assert.Null(read.SetUpIn);
        Assert.Empty(read.SetUps);
        Assert.Empty(read.Verdicts);
        Assert.Null(Assert.Single(read.Then).SetUpIn);
        Assert.DoesNotContain("setUpIn", QuestWire.Push(0, await _quests.HistoryAsync(plain.Id)));
        Assert.DoesNotContain("review", QuestWire.Push(0, await _quests.HistoryAsync(plain.Id)));
    }
}
