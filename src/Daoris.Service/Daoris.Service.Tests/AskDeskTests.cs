using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An ask (D65 §1a): a sentence, with links and files, entered at a WORKSPACE rather than at a
/// repository — held as a record of what was asked and what became of it. With no intake harness
/// the declarations tier proposes and says so; a named receiver is published to at once; a person
/// turns a proposal into a quest. Nothing is ever published on a guess.
/// </summary>
public sealed class AskDeskTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-asks-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private QuestFiles _files = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T10:00:00Z");

    public async Task InitializeAsync()
    {
        Repo("media-api", """{ "summary": "The media service and its config.", "owns": ["media config", "video and image field names"], "accepts": ["a media bug"] }""");
        Repo("storefront", """{ "summary": "The storefront.", "owns": ["product pages", "checkout"], "accepts": ["a UI bug"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            await service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        _files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: _files);
        _desk = new AskDesk(service, asks, _exchange, _files);
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

    private const string Sentence =
        "check the ticket and use the media config instead of hard coding the video/image field name";

    [Fact]
    public async Task With_no_harness_the_declarations_propose_and_say_so_and_nothing_is_published()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", Sentence), Now);

        Assert.Equal(AskRefusal.None, outcome.Refusal);
        var ask = outcome.Ask!;
        Assert.Equal(AskState.Proposed, ask.State);
        Assert.Equal(AskDesk.ByDeclarations, ask.Tier);
        Assert.Equal("media-api", ask.Proposal[0].Repository);
        Assert.Contains("by declarations only", outcome.Message);
        Assert.Contains("no intake agent ran", outcome.Message);
        Assert.Contains("media-api", outcome.Message);
        Assert.Null(outcome.Quest);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>
    /// ASKNAME1: the ask reaches the tier with every registration, so a repository registered here with a
    /// root and no manifest (D70) that the sentence names is proposed first, ahead of the adopted one its
    /// words overlap — said in the answer, and kept on the ask's record.
    /// </summary>
    [Fact]
    public async Task A_named_unadopted_repository_is_proposed_first_and_kept_on_the_record()
    {
        await _service.RegisterAsync(
            new Registration("release-infra", Adopted: false, null, [], [], [], 0, Root: "/trees/release-infra", Workspace: "work"),
            Now);

        var outcome = await _desk.AskAsync(new AskRequest("work", $"In release-infra: {Sentence}"), Now);

        Assert.Equal(AskState.Proposed, outcome.Ask!.State);
        Assert.Equal(["release-infra", "media-api"], outcome.Ask.Proposal.Select(match => match.Repository).ToArray());
        Assert.Contains("proposed, best first: `release-infra` (release-infra); `media-api`", outcome.Message);
        Assert.Empty(await _quests.ListAsync());

        var kept = (await _desk.FindAsync(outcome.Ask.Id))!;
        Assert.Equal(
            outcome.Ask.Proposal.Select(match => (match.Repository, match.Score, string.Join(" ", match.Matched))),
            kept.Proposal.Select(match => (match.Repository, match.Score, string.Join(" ", match.Matched))));
    }

    [Fact]
    public async Task A_sentence_nobody_declared_is_proposed_to_nobody_and_says_so()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", "rotate the office wifi password"), Now);

        Assert.Equal(AskState.Proposed, outcome.Ask!.State);
        Assert.Empty(outcome.Ask.Proposal);
        Assert.Contains("no repository's declarations share its words", outcome.Message);
    }

    /// <summary>
    /// Naming the receiver is the asker deciding, so the quest is published at once — asked BY the
    /// ask, in the ask's circle, carrying its links and files — and the ask records it.
    /// </summary>
    [Fact]
    public async Task A_named_receiver_is_published_to_at_once_carrying_the_links_and_files()
    {
        var outcome = await _desk.AskAsync(
            new AskRequest("work", Sentence)
            {
                To = "media-api",
                Links = ["https://tickets.example/MEDIA-142"],
                Uploads = [new QuestUpload("broken.png", Encoding.UTF8.GetBytes("pixels"))],
            },
            Now);

        Assert.Equal(AskRefusal.None, outcome.Refusal);
        var quest = outcome.Quest!;
        Assert.Equal($"ask #{outcome.Ask!.Id}", quest.From);
        Assert.Equal("media-api", quest.To);
        Assert.Equal("work", quest.Workspace);
        Assert.Equal(["https://tickets.example/MEDIA-142"], quest.Links);
        Assert.True(_files.Has(quest.Id, Assert.Single(quest.Attachments)));
        Assert.Contains(Sentence, quest.Body);
        Assert.Equal(AskState.Published, outcome.Ask.State);
        Assert.Equal(AskDesk.ByName, outcome.Ask.Tier);
        Assert.Equal([quest.Id], outcome.Ask.Quests);
    }

    /// <summary>
    /// A receiver that cannot be asked is refused in the exchange's own words — and the ask is KEPT,
    /// with the declarations' proposal, because the sentence is still what the person meant.
    /// </summary>
    [Fact]
    public async Task A_named_receiver_that_cannot_be_asked_keeps_the_ask_with_its_proposal()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", Sentence) { To = "nobody-here" }, Now);

        Assert.Equal(AskRefusal.QuestRefused, outcome.Refusal);
        Assert.Contains("nobody-here", outcome.Message);
        Assert.Equal(AskState.Proposed, outcome.Ask!.State);
        Assert.Equal("media-api", outcome.Ask.Proposal[0].Repository);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>A person turns a proposal into a quest — the files the ask kept travel with it.</summary>
    [Fact]
    public async Task A_person_publishes_a_proposal_and_the_asks_files_travel_with_it()
    {
        var asked = await _desk.AskAsync(
            new AskRequest("work", Sentence) { Uploads = [new QuestUpload("trace.log", Encoding.UTF8.GetBytes("stack"))] },
            Now);

        var published = await _desk.PublishAsync(asked.Ask!.Id, "media-api", Now.AddMinutes(5));

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(AskState.Published, published.Ask!.State);
        var quest = published.Quest!;
        Assert.Equal("stack", await File.ReadAllTextAsync(_files.PathOf(quest.Id, Assert.Single(quest.Attachments))));
        Assert.Equal([quest.Id], published.Ask.Quests);
    }

    [Fact]
    public async Task Asking_the_same_sentence_again_in_a_circle_is_the_same_ask()
    {
        var first = await _desk.AskAsync(new AskRequest("work", Sentence), Now);
        var second = await _desk.AskAsync(new AskRequest("work", "  " + Sentence + " "), Now.AddMinutes(1));

        Assert.Equal(first.Ask!.Id, second.Ask!.Id);
        Assert.Contains("already asked", second.Message);
        Assert.Single(await _desk.ListAsync("work"));
    }

    /// <summary>
    /// ASKAGAIN1, found asking a ticket again after its first run failed: a person who CLOSED an ask
    /// ended it, so the same words afterwards are a fresh intent, not a repeat. The door used to hand
    /// the closed record back for good, and the only way through was rewording. The closed one stays,
    /// as it was.
    /// </summary>
    [Fact]
    public async Task The_same_sentence_after_the_person_closed_the_ask_asks_anew()
    {
        var first = await _desk.AskAsync(new AskRequest("work", Sentence), Now);
        await _desk.CloseAsync(first.Ask!.Id, "Its run was cut off; asking again.", Now.AddMinutes(1));

        var again = await _desk.AskAsync(new AskRequest("work", Sentence), Now.AddMinutes(2));

        Assert.NotEqual(first.Ask.Id, again.Ask!.Id);
        Assert.NotEqual(AskState.Closed, again.Ask.State);
        Assert.DoesNotContain("already asked", again.Message);
        Assert.Equal(AskState.Closed, (await _desk.FindAsync(first.Ask.Id))!.State);

        // …and while the new one is open, the same words are that one — a third ask repeats the second.
        var repeat = await _desk.AskAsync(new AskRequest("work", Sentence), Now.AddMinutes(3));
        Assert.Equal(again.Ask.Id, repeat.Ask!.Id);
    }

    [Fact]
    public async Task An_ask_in_a_circle_this_machine_does_not_hold_is_refused_naming_the_ones_it_does()
    {
        var outcome = await _desk.AskAsync(new AskRequest("elsewhere", Sentence), Now);

        Assert.Equal(AskRefusal.UnknownWorkspace, outcome.Refusal);
        Assert.Contains("elsewhere", outcome.Message);
        Assert.Contains("work", outcome.Message);
        Assert.Null(outcome.Ask);
    }

    [Fact]
    public async Task An_empty_sentence_is_refused()
    {
        Assert.Equal(AskRefusal.Empty, (await _desk.AskAsync(new AskRequest("work", "   "), Now)).Refusal);
    }

    /// <summary>Closing is a person's answer to their own ask — it needs a reason, and it is final.</summary>
    [Fact]
    public async Task A_closed_ask_keeps_its_reason_and_publishes_nothing_more()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence), Now);

        var noReason = await _desk.CloseAsync(asked.Ask!.Id, "  ", Now);
        var closed = await _desk.CloseAsync(asked.Ask.Id, "Handled on a call.", Now);
        var late = await _desk.PublishAsync(asked.Ask.Id, "media-api", Now);

        Assert.Equal(AskRefusal.Empty, noReason.Refusal);
        Assert.Equal(AskState.Closed, closed.Ask!.State);
        Assert.Equal("Handled on a call.", closed.Ask.Note);
        Assert.Equal(AskRefusal.Closed, late.Refusal);
        Assert.Empty(await _quests.ListAsync());
    }

    [Fact]
    public async Task Publishing_an_ask_that_does_not_exist_is_refused()
    {
        Assert.Equal(AskRefusal.NotFound, (await _desk.PublishAsync("zzzzzz", "media-api", Now)).Refusal);
    }

    /// <summary>
    /// An ask's quests are appended where they are kept, never rewritten from what one caller read
    /// (REV3 service F10). The intake's connector and the person's page publish from two processes, and
    /// each used to save the whole record it had read — so of two publishes at once, one quest vanished
    /// from the ask. A close written whole the same way could drop a quest published meanwhile.
    /// </summary>
    [Fact]
    public async Task A_publish_appends_to_what_the_ask_holds_and_a_close_keeps_it()
    {
        var asks = await AskStore.OpenAsync(_connection);
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

        // Two publishers, each holding the record as it read it — the second never saw the first's.
        await asks.RecordPublishedAsync(asked.Id, "q-intake", tier: null, Now.AddMinutes(1));
        await asks.RecordPublishedAsync(asked.Id, "q-person", tier: null, Now.AddMinutes(1));
        await asks.RecordPublishedAsync(asked.Id, "q-intake", tier: null, Now.AddMinutes(2));
        await asks.RecordClosedAsync(asked.Id, "Handled.", Now.AddMinutes(3));
        await asks.RecordPublishedAsync(asked.Id, "q-late", tier: null, Now.AddMinutes(4));

        var held = (await asks.FindAsync(asked.Id))!;
        Assert.Equal(["q-intake", "q-person", "q-late"], held.Quests);
        Assert.Equal(AskState.Closed, held.State);
        Assert.Equal("Handled.", held.Note);
    }

    // ——— USE1c: an ask whose work is finished is DONE, derived from its quests rather than stored.

    /// <summary>An ask that became no quest has no work to finish — it stays what it was.</summary>
    [Fact]
    public async Task An_ask_that_became_no_quest_is_never_done()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

        Assert.Equal(AskState.Proposed, (await _desk.FindAsync(asked.Id))!.State);
        Assert.Single(await _desk.ListAsync("work"));
    }

    /// <summary>One quest still open is work in flight: the ask stays published, and listed.</summary>
    [Fact]
    public async Task An_ask_with_a_quest_still_open_stays_published_and_listed()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence) { To = "media-api" }, Now)).Ask!;
        var second = await _desk.PublishAsync(asked.Id, "storefront", Now.AddMinutes(1));
        await _quests.MoveAsync(asked.Quests[0], QuestStatus.Done, "Landed.", Now.AddMinutes(2));

        Assert.Equal(AskState.Published, (await _desk.FindAsync(asked.Id))!.State);
        Assert.Equal(QuestStatus.Open, second.Quest!.Status);
        Assert.Single(await _desk.ListAsync("work"));
    }

    /// <summary>
    /// 🔴 USE1c, the owner's list: every quest from the ask closed — done or declined, a decline is an
    /// answer too — makes the ask DONE, and the default list hides it as it hides a closed one. With
    /// closed ones included it is there, done.
    /// </summary>
    [Fact]
    public async Task An_ask_whose_quests_all_closed_is_done_and_leaves_the_default_list()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence) { To = "media-api" }, Now)).Ask!;
        var second = (await _desk.PublishAsync(asked.Id, "storefront", Now.AddMinutes(1))).Quest!;
        await _quests.MoveAsync(asked.Quests[0], QuestStatus.Taken, null, Now.AddMinutes(2));
        await _quests.MoveAsync(asked.Quests[0], QuestStatus.Done, "Landed.", Now.AddMinutes(3));
        await _quests.MoveAsync(second.Id, QuestStatus.Declined, "Not ours.", Now.AddMinutes(4));

        Assert.Equal(AskState.Done, (await _desk.FindAsync(asked.Id))!.State);
        Assert.Empty(await _desk.ListAsync("work"));
        Assert.Equal(AskState.Done, Assert.Single(await _desk.ListAsync("work", includeClosed: true)).State);
    }

    /// <summary>
    /// A chain's next step is published in the same move that closes the step before it, asked BY the
    /// ask (D65 §4) — so the ask is not done while a step is still to come.
    /// </summary>
    [Fact]
    public async Task An_ask_whose_chain_has_a_step_still_open_is_not_done()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var first = (await _desk.PublishAsync(
            asked.Id, "media-api", Now.AddMinutes(1),
            draft: new AskDraft("Read names from the media config", null)
            {
                Then = [new QuestStep("storefront", "Verify {parent} in the browser", "The fields read their names.")],
            })).Quest!;

        var closed = await _quests.MoveAsync(first.Id, QuestStatus.Done, "Landed.", Now.AddMinutes(2));

        Assert.Equal($"ask #{asked.Id}", closed.FollowUp!.From);
        Assert.Equal(AskState.Published, (await _desk.FindAsync(asked.Id))!.State);

        await _quests.MoveAsync(closed.FollowUp.Id, QuestStatus.Done, "Verified.", Now.AddMinutes(3));
        Assert.Equal(AskState.Done, (await _desk.FindAsync(asked.Id))!.State);
    }

    /// <summary>
    /// A step waiting on another repository (D79) is taken, and its work is in its taker's tree — so
    /// the ask is not done, even once the question it waits on is answered.
    /// </summary>
    [Fact]
    public async Task An_ask_whose_quest_waits_on_another_repository_is_not_done()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence) { To = "media-api" }, Now)).Ask!;
        var work = asked.Quests[0];
        await _exchange.RespondAsync(work, "take", null, Now.AddMinutes(1));
        var question = (await _exchange.PublishAsync("media-api", "storefront", "Which field names?", "why", Now.AddMinutes(2))).Quest!;
        var waited = await _exchange.RespondAsync(work, "wait", null, Now.AddMinutes(3), on: question.Id);
        await _exchange.RespondAsync(question.Id, "done", "These ones.", Now.AddMinutes(4));

        Assert.Equal(QuestRespondRefusal.None, waited.Refusal);
        Assert.Equal(AskState.Published, (await _desk.FindAsync(asked.Id))!.State);
        Assert.Single(await _desk.ListAsync("work"));
    }

    /// <summary>A person's close stands whatever its quests do — it is their word on the ask.</summary>
    [Fact]
    public async Task A_closed_ask_stays_closed_when_its_quests_close()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence) { To = "media-api" }, Now)).Ask!;
        await _desk.CloseAsync(asked.Id, "Handled on a call.", Now.AddMinutes(1));
        await _quests.MoveAsync(asked.Quests[0], QuestStatus.Done, "Landed.", Now.AddMinutes(2));

        Assert.Equal(AskState.Closed, (await _desk.FindAsync(asked.Id))!.State);
    }

    /// <summary>
    /// Derived, not stored: a quest closed on ANOTHER machine and synced in is reflected with no write
    /// to the ask — the sync knows nothing of asks, which stay on this machine (D68 §2).
    /// </summary>
    [Fact]
    public async Task A_quest_closed_on_another_machine_and_synced_in_makes_the_ask_done()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence) { To = "media-api" }, Now)).Ask!;
        var quest = asked.Quests[0];
        var published = Assert.Single(await _quests.HistoryAsync(quest));

        await _quests.IntegrateAsync(
            "work",
            [
                published with { Number = 1 },
                new QuestOperation(quest, QuestOperationKind.Done, "another-machine", 1, Now.AddHours(1), "Landed there.", Number: 2),
            ],
            through: 2);

        Assert.Equal(AskState.Done, (await _desk.FindAsync(asked.Id))!.State);
    }

    /// <summary>
    /// A finished ask ended as surely as a closed one (ASKAGAIN1's reason): the same words afterwards
    /// ask anew, rather than answering with a record the default list no longer shows.
    /// </summary>
    [Fact]
    public async Task The_same_sentence_after_its_work_is_done_asks_anew()
    {
        var first = (await _desk.AskAsync(new AskRequest("work", Sentence) { To = "media-api" }, Now)).Ask!;
        await _quests.MoveAsync(first.Quests[0], QuestStatus.Done, "Landed.", Now.AddMinutes(1));

        var again = await _desk.AskAsync(new AskRequest("work", Sentence), Now.AddMinutes(2));

        Assert.NotEqual(first.Id, again.Ask!.Id);
        Assert.DoesNotContain("already asked", again.Message);
    }
}
