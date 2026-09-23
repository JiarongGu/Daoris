using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The publish/respond judgement — who may be addressed, what a refusal says, what declining
/// requires — lives in ONE place, because there are two hosts. Written per host it would drift, and
/// one host would end up refusing what the other accepts, which for a quest system is the worst
/// available bug: the same ask deliverable or not depending on which door it came through.
/// </summary>
public sealed class QuestExchangeTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-exchange-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestExchange _exchange = null!;
    private KnowledgeService _service = null!;
    private QuestStore _quests = null!;
    private QuestFiles _files = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-18T10:00:00Z");

    public async Task InitializeAsync()
    {
        // The family this exchange sees: one declared adopter, one adopter that has said nothing,
        // and one repository that has not adopted at all. Written as manifests and IMPORTED, because
        // the registry is an explicit list now (D48 §3) — and because reading a real manifest is what
        // makes "adopted" and "declared" the same two facts the running system distinguishes.
        Repo("Declared", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Owns the engine.", "owns": ["rendering"], "accepts": ["a bug"] }
            }
            """);
        Repo("Quiet", """{ "source": "s", "packs": [] }""");
        Repo("Stranger", null);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, Now);

        // The home is beside the family, never inside a repository: a quest's files are the
        // machine's, and a repository is exactly where they must not land (D32).
        _files = new QuestFiles(Path.Combine(_root + "-home"));
        _exchange = new QuestExchange(_service, _quests, files: _files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        if (Directory.Exists(_root + "-home")) Directory.Delete(_root + "-home", recursive: true);
    }

    private void Repo(string name, string? manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        if (manifest is not null) File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private Task<QuestPublishOutcome> Publish(string to, string from = "Asker") =>
        _exchange.PublishAsync(from, to, "Do the thing", "Here is why, with evidence.", Now);

    [Fact]
    public async Task Publishing_to_yourself_is_refused()
    {
        var outcome = await Publish("Asker");

        Assert.Equal(QuestPublishRefusal.SelfAddressed, outcome.Refusal);
        Assert.Null(outcome.Quest);
    }

    /// <summary>
    /// A quest for a repository with no client has nobody to read it — and the refusal must name who
    /// CAN be asked, because "no" with no alternative leaves the asker exactly where they started.
    /// </summary>
    [Fact]
    public async Task Publishing_to_a_non_adopter_is_refused_and_names_the_addressable()
    {
        var outcome = await Publish("Stranger");

        Assert.Equal(QuestPublishRefusal.NotAddressable, outcome.Refusal);
        Assert.Contains("Declared", outcome.Addressable);
        Assert.Contains("Quiet", outcome.Addressable);
        Assert.DoesNotContain("Stranger", outcome.Addressable);
        Assert.Contains("has not adopted", outcome.Message);
    }

    /// <summary>Adoption gates addressing; declaration does not — but the asker is warned (D34).</summary>
    [Fact]
    public async Task Publishing_to_an_undeclared_adopter_succeeds_with_a_caution()
    {
        var outcome = await Publish("Quiet");

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal(QuestStatus.Open, outcome.Quest!.Status);
        Assert.Contains("has not declared", outcome.Message);
    }

    [Fact]
    public async Task Publishing_to_a_declared_adopter_is_clean()
    {
        var outcome = await Publish("Declared");

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.DoesNotContain("has not declared", outcome.Message);
        Assert.Contains(outcome.Quest!.Id, outcome.Message);
    }

    [Fact]
    public async Task Declining_without_a_reason_is_refused()
    {
        var published = await Publish("Declared");

        var outcome = await _exchange.RespondAsync(published.Quest!.Id, "decline", null, Now);

        Assert.Equal(QuestRespondRefusal.MissingReason, outcome.Refusal);
    }

    [Fact]
    public async Task An_unknown_action_is_refused()
    {
        var outcome = await _exchange.RespondAsync("abc123", "postpone", null, Now);

        Assert.Equal(QuestRespondRefusal.UnknownAction, outcome.Refusal);
    }

    [Fact]
    public async Task An_unknown_id_is_not_found()
    {
        var outcome = await _exchange.RespondAsync("zzzzzz", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.NotFound, outcome.Refusal);
    }

    /// <summary>A leading `#` is how ids are printed, so responding with one pasted back must work.</summary>
    [Fact]
    public async Task Take_then_done_moves_the_status()
    {
        var published = await Publish("Declared");

        var taken = await _exchange.RespondAsync($"#{published.Quest!.Id}", "take", null, Now);
        var done = await _exchange.RespondAsync(published.Quest.Id, "done", "Landed.", Now.AddDays(1));

        Assert.Equal(QuestStatus.Taken, taken.Quest!.Status);
        Assert.Equal(QuestStatus.Done, done.Quest!.Status);
    }

    /// <summary>
    /// The cross-machine race, at the layer where it resolves (D47 §5): the second take is refused
    /// naming the state, and the message tells the losing session what to do — stand down.
    /// </summary>
    [Fact]
    public async Task Taking_a_taken_quest_is_refused_toward_standing_down()
    {
        var published = await Publish("Declared");
        await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now);

        var second = await _exchange.RespondAsync(published.Quest.Id, "take", null, Now.AddMinutes(1));

        Assert.Equal(QuestRespondRefusal.AlreadyTaken, second.Refusal);
        Assert.Contains("already taken", second.Message);
        Assert.Contains("stand down", second.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One title is one quest forever (D46 §3): closed means immovable, in both directions.</summary>
    [Fact]
    public async Task A_closed_quest_refuses_every_move()
    {
        var published = await Publish("Declared");
        await _exchange.RespondAsync(published.Quest!.Id, "done", "Landed.", Now);

        var retake = await _exchange.RespondAsync(published.Quest.Id, "take", null, Now.AddDays(1));
        var redecline = await _exchange.RespondAsync(published.Quest.Id, "decline", "second thoughts", Now.AddDays(1));

        Assert.Equal(QuestRespondRefusal.Closed, retake.Refusal);
        Assert.Equal(QuestRespondRefusal.Closed, redecline.Refusal);
        Assert.Contains("does not move", retake.Message);
    }

    /// <summary>
    /// Outside work is first-class (D46): someone who just did the thing closes the quest without
    /// ever having taken it, and the table must allow that.
    /// </summary>
    [Fact]
    public async Task Done_straight_from_open_is_outside_work_and_fine()
    {
        var published = await Publish("Declared");

        var done = await _exchange.RespondAsync(published.Quest!.Id, "done", "Already had it.", Now);

        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        Assert.Equal(QuestStatus.Done, done.Quest!.Status);
    }

    // ——— An ask asks (D65 §1a): a sender that is not a repository names the circle it asks from.

    /// <summary>
    /// An ask has no registry row, so nothing but the ask can say which circle it was made in — and
    /// the workspace is still the unit of sharing (D48 §4): named, its quest lands there; unnamed, the
    /// sender is placed where any unregistered sender always was.
    /// </summary>
    [Fact]
    public async Task A_sender_that_is_not_a_repository_asks_from_the_circle_it_names()
    {
        var quiet = (await _service.RegistryAsync()).First(r => r.Repository == "Quiet");
        await _service.RegisterAsync(quiet with { Workspace = "tools" }, Now);

        var named = await _exchange.PublishAsync(
            new QuestAsk("ask #a1b2c3", "Quiet", "Use the media config", "Asked at the workspace.") { Workspace = "tools" },
            Now);
        var unnamed = await _exchange.PublishAsync(
            new QuestAsk("ask #d4e5f6", "Quiet", "Another ask", "No circle named."), Now);

        Assert.Equal(QuestPublishRefusal.None, named.Refusal);
        Assert.Equal("tools", named.Quest!.Workspace);
        Assert.Equal(QuestPublishRefusal.CrossWorkspace, unnamed.Refusal);
    }

    // ——— A chain (D65 §4): judged when composed, published at the moment the quest closes done.

    private Task<QuestPublishOutcome> Chain(params QuestStep[] then) =>
        _exchange.PublishAsync(
            new QuestAsk("Asker", "Declared", "Develop the media config", "Field names are hard-coded.") { Then = then },
            Now);

    [Fact]
    public async Task A_chain_moves_on_when_its_quest_closes_done_and_the_answer_says_so()
    {
        var published = await Chain(new QuestStep("Quiet", "Verify {parent} in the browser", "Check it."));
        Assert.Equal(QuestPublishRefusal.None, published.Refusal);
        Assert.Equal("Quiet", Assert.Single(published.Quest!.Then).To);

        var done = await _exchange.RespondAsync(published.Quest.Id, "done", "Landed.", Now.AddHours(1));

        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        var next = (await _quests.ListAsync(receiver: "Quiet")).Single();
        Assert.Equal("Asker", next.From);
        Assert.Equal(published.Quest.Id, next.Parent);
        Assert.Contains($"`#{next.Id}`", done.Message);
        Assert.Contains("Quiet", done.Message);
    }

    /// <summary>
    /// Judged where the person or the intake can still act on the answer — when composing — rather
    /// than at a close nobody is watching. A step nobody can see is refused naming the step.
    /// </summary>
    [Fact]
    public async Task A_step_to_a_repository_that_cannot_be_asked_is_refused_when_composed()
    {
        var outcome = await Chain(new QuestStep("Stranger", "Verify", "b"));

        Assert.Equal(QuestPublishRefusal.BadChain, outcome.Refusal);
        Assert.Contains("Stranger", outcome.Message);
        Assert.Contains("Declared", outcome.Message);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>Every step is asked on behalf of the chain's asker, so a step to the asker is a self-ask.</summary>
    [Fact]
    public async Task A_step_back_to_the_asker_is_refused()
    {
        var outcome = await Chain(new QuestStep("Asker", "Report", "b"));

        Assert.Equal(QuestPublishRefusal.BadChain, outcome.Refusal);
        Assert.Empty(await _quests.ListAsync());
    }

    [Fact]
    public async Task A_step_without_its_words_is_refused()
    {
        var outcome = await Chain(new QuestStep("Quiet", "  ", "b"));

        Assert.Equal(QuestPublishRefusal.BadChain, outcome.Refusal);
    }

    [Fact]
    public async Task A_chain_longer_than_a_quest_carries_is_refused_naming_the_limit()
    {
        var steps = Enumerable.Range(0, QuestExchange.MaxChain + 1)
            .Select(i => new QuestStep("Quiet", $"Step {i}", "b"))
            .ToArray();

        var outcome = await Chain(steps);

        Assert.Equal(QuestPublishRefusal.BadChain, outcome.Refusal);
        Assert.Contains($"{QuestExchange.MaxChain}", outcome.Message);
    }

    /// <summary>A decline answers the chain too: nothing follows it, and the reason is the answer.</summary>
    [Fact]
    public async Task A_declined_quest_publishes_no_next_step()
    {
        var published = await Chain(new QuestStep("Quiet", "Verify", "b"));

        await _exchange.RespondAsync(published.Quest!.Id, "decline", "Not ours.", Now);

        Assert.Empty(await _quests.ListAsync(receiver: "Quiet", includeClosed: true));
    }

    // ——— What a quest carries (D65 §2): links travel with it; files are kept here, by content.

    private static QuestUpload Upload(string name, string content) =>
        new(name, System.Text.Encoding.UTF8.GetBytes(content));

    private Task<QuestPublishOutcome> Carry(
        IReadOnlyList<string>? links = null, IReadOnlyList<QuestUpload>? uploads = null,
        QuestExchange? exchange = null, string title = "Use the media config") =>
        (exchange ?? _exchange).PublishAsync(
            new QuestAsk("Asker", "Declared", title, "The field names are hard-coded.")
            {
                Links = links ?? [],
                Uploads = uploads ?? [],
            },
            Now);

    [Fact]
    public async Task A_quest_carries_its_links_and_keeps_its_files_under_the_home()
    {
        var outcome = await Carry(
            links: ["https://tickets.example/T-1"],
            uploads: [Upload("before.png", "pixels"), Upload("notes.txt", "remember")]);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        var quest = outcome.Quest!;
        Assert.Equal(["https://tickets.example/T-1"], quest.Links);
        Assert.Equal(["before.png", "notes.txt"], quest.Attachments.Select(a => a.Name));
        Assert.All(quest.Attachments, a => Assert.True(_files.Has(quest.Id, a)));
        Assert.Equal("pixels", await File.ReadAllTextAsync(_files.PathOf(quest.Id, quest.Attachments[0])));

        // And the record the store holds is the one the outcome named — not a copy that forgot.
        Assert.Equal(2, (await _quests.FindAsync(quest.Id))!.Attachments.Count);
    }

    /// <summary>
    /// A link is shown AS a link, so it has to be an address: anything that is not an absolute http
    /// or https URL is refused naming it — a <c>javascript:</c> "link" in a drawer is a script.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example/a")]
    [InlineData("tickets.example/T-1")]
    [InlineData("file:///C:/Users/someone/notes.txt")]
    public async Task A_link_that_is_not_an_address_is_refused_naming_it(string link)
    {
        var outcome = await Carry(links: [link]);

        Assert.Equal(QuestPublishRefusal.BadLink, outcome.Refusal);
        Assert.Contains(link, outcome.Message);
        Assert.Empty(await _quests.ListAsync());
    }

    [Fact]
    public async Task Blank_and_repeated_links_are_carried_once_and_blanks_not_at_all()
    {
        var outcome = await Carry(links: ["https://a.example", "  ", "https://a.example", " https://b.example "]);

        Assert.Equal(["https://a.example", "https://b.example"], outcome.Quest!.Links);
    }

    /// <summary>The same file dropped twice is one file — its content is its identity.</summary>
    [Fact]
    public async Task The_same_file_twice_is_carried_once()
    {
        var outcome = await Carry(uploads: [Upload("a.txt", "same"), Upload("b.txt", "same")]);

        Assert.Equal("a.txt", Assert.Single(outcome.Quest!.Attachments).Name);
    }

    /// <summary>
    /// Limits are refused before anything is kept — a refused ask must leave nothing behind on disk,
    /// or the home fills with files no record names.
    /// </summary>
    [Fact]
    public async Task Too_many_files_are_refused_and_nothing_is_kept()
    {
        var uploads = Enumerable.Range(0, QuestExchange.MaxAttachments + 1)
            .Select(i => Upload($"f{i}.txt", $"content {i}"))
            .ToList();

        var outcome = await Carry(uploads: uploads);

        Assert.Equal(QuestPublishRefusal.BadAttachment, outcome.Refusal);
        Assert.Contains($"at most {QuestExchange.MaxAttachments}", outcome.Message);
        Assert.False(Directory.Exists(Path.Combine(_root + "-home", QuestFiles.Folder)));
    }

    [Fact]
    public async Task Files_larger_than_a_quest_carries_are_refused_naming_the_limit()
    {
        var big = new QuestUpload("huge.bin", new byte[QuestExchange.MaxAttachmentBytes + 1]);

        var outcome = await Carry(uploads: [big]);

        Assert.Equal(QuestPublishRefusal.BadAttachment, outcome.Refusal);
        Assert.Contains("MB", outcome.Message);
        Assert.Contains("link", outcome.Message);
    }

    /// <summary>
    /// 🔴 No home, no files (D63): a host with nowhere of Daoris's to keep bytes refuses them with the
    /// home's own sentence rather than writing somewhere nobody pointed it. Links need no home.
    /// </summary>
    [Fact]
    public async Task Files_with_no_home_to_keep_them_are_refused_with_the_home_sentence()
    {
        var homeless = new QuestExchange(_service, _quests);

        var files = await Carry(uploads: [Upload("a.txt", "x")], exchange: homeless);
        var links = await Carry(links: ["https://a.example"], exchange: homeless, title: "Links only");

        Assert.Equal(QuestPublishRefusal.NoHome, files.Refusal);
        Assert.Contains(DaorisHome.Variable, files.Message);
        Assert.Equal(QuestPublishRefusal.None, links.Refusal);
    }

    /// <summary>
    /// One title is one quest (D46 §3): publishing it again answers the quest as it stands, and must
    /// SAY that what this call carried was not added — an ignored attachment is otherwise invisible.
    /// </summary>
    [Fact]
    public async Task Publishing_again_says_what_it_did_not_add()
    {
        await Carry();

        var again = await Carry(links: ["https://a.example"], uploads: [Upload("late.png", "late")]);

        Assert.Equal(QuestPublishRefusal.None, again.Refusal);
        Assert.Empty(again.Quest!.Attachments);
        Assert.Contains("already published", again.Message);
        Assert.Contains("late.png", again.Message);
        Assert.Contains("https://a.example", again.Message);
        Assert.False(Directory.Exists(_files.DirectoryOf(again.Quest.Id)));
    }
}
