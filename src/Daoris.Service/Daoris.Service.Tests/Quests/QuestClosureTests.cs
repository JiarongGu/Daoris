using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-CLOSE1a (the fabric events design §E5, D173's part E notes): in a graph that closes its quest itself, the agent's
/// result is a submission, never a done. The driver binds the taken quest to the graph; the agent says <c>ready</c>, which keeps
/// the quest taken and hangs a done's holds on the submission; an unheld submission publishes the chain's next step and is
/// sealed by it; and the graph's close door writes every quest's Done in one transaction, fenced by the submissions it names
/// and by its key, so a stale close writes nothing and the same close twice writes once.
/// </summary>
public sealed class QuestClosureTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-closure-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T09:00:00Z");

    private const string Sentence = "write the bridge report into docs/report-bridge.md, and this will need the v3 bridge";
    private const string Graph = "sha256:0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    private const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private const string Blob = "fedcba9876543210fedcba9876543210fedcba98";

    private static readonly QuestRequirement Report =
        new("write the bridge report into docs/report-bridge.md", "The report is in docs/report-bridge.md.")
        {
            Evidence = [new QuestEvidence("docs/report-bridge.md")],
        };

    private static readonly QuestRequirement Bridge =
        new("this will need the v3 bridge", "The report opens through the bridge's route.");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);

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
        _exchange = new QuestExchange(service, _quests, files: files, asks: asks);
        _desk = new AskDesk(service, asks, _exchange, files);
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

    /// <summary>The report quest, naming its file as evidence, with a verifying step to follow when asked, and taken.</summary>
    private async Task<Quest> Taken(bool chain = true, string title = "Write the bridge report")
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft(title, "Through the bridge.")
        {
            Requirements = [Report, Bridge],
            Then = chain ? [new QuestStep("checker", "Verify {parent} in the browser", "Open it and look.")] : [],
        });
        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now)).Refusal);
        return published.Quest;
    }

    /// <summary>A taken quest bound to the closure graph, as the driver binds one at its run's first start.</summary>
    private async Task<Quest> Bound(bool chain = true, string title = "Write the bridge report")
    {
        var quest = await Taken(chain, title);
        var bound = await _exchange.BindAsync(quest.Id, Graph, Now);
        Assert.Equal(QuestRespondRefusal.None, bound.Refusal);
        return bound.Quest!;
    }

    private static QuestAnswer Met(int requirement) => new(requirement, Met: "Done as the check says.");

    private static readonly QuestAnswer Departed =
        new(1, null, Departed: "It went into the wiki instead.", Quote: "write the bridge report into docs/report-bridge.md");

    private Task<QuestRespondOutcome> Ready(Quest quest, params QuestAnswer[] answers) =>
        _exchange.RespondAsync(quest.Id, "ready", "Built.", Now, answers: answers.Length > 0 ? answers : [Met(1), Met(2)]);

    /// <summary>The report found, read for the quest's current submission, as the driver asks it.</summary>
    private async Task<QuestRespondOutcome> Found(Quest quest) =>
        await Found(quest, (await Find(quest.Id)).Submission is { } current ? new QuestOperationRef(current.Machine, current.Sequence) : null);

    /// <summary>The report found, read for the submission named (FABRIC-CLOSE1e): a verdict says which submission it read.</summary>
    private Task<QuestRespondOutcome> Found(Quest quest, QuestOperationRef? asked) =>
        _exchange.EvidenceAsync(quest.Id, new QuestEvidenceVerdict(Commit, "session-end",
            [new QuestEvidenceRead(1, "docs/report-bridge.md", null, "found") { Object = Blob, Changed = true }]) { Submission = asked }, Now);

    private static (string, QuestOperationRef) Named(Quest quest) =>
        (quest.Id, new QuestOperationRef(quest.Submission!.Machine, quest.Submission.Sequence));

    private Task<QuestCloseOutcome> Close(string key, params Quest[] quests) =>
        _exchange.CloseAsync(quests[0].Id, key, "token-" + key, [.. quests.Select(Named)], Now);

    private async Task<Quest> Find(string id) => (await _quests.FindAsync(id))!;

    // ——— Bind and ready.

    [Fact]
    public async Task Ready_keeps_the_quest_taken_with_its_submission_and_holds_it_as_a_done_would_be()
    {
        var quest = await Bound();

        var ready = await Ready(quest);

        Assert.Equal(QuestRespondRefusal.None, ready.Refusal);
        var submitted = ready.Quest!;
        Assert.Equal(QuestStatus.Taken, submitted.Status);
        Assert.Equal(Graph, submitted.Closure);
        Assert.Equal("Built.", submitted.Submission!.Note);
        Assert.Equal([Met(1), Met(2)], submitted.Answers);
        // Its met requirement names evidence nobody read yet: held, as a done would be, and nothing follows it.
        Assert.Equal(QuestHold.EvidenceUnread, submitted.Hold);
        Assert.True(submitted.AwaitsEvidence);
        Assert.Contains("ready", ready.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(await _quests.ListAsync(), q => q.Parent == quest.Id);
        Assert.Equal(QuestOperationKind.Submitted, (await _quests.HistoryAsync(quest.Id))[^1].Kind);
    }

    [Fact]
    public async Task An_agents_done_on_a_bound_quest_is_refused_naming_ready_and_the_persons_done_stays_theirs()
    {
        var quest = await Bound();

        var done = await _exchange.RespondAsync(quest.Id, "done", "Built.", Now, answers: [Met(1), Met(2)]);

        Assert.Equal(QuestRespondRefusal.ClosedByGraph, done.Refusal);
        Assert.Contains("`ready`", done.Message, StringComparison.Ordinal);
        Assert.Equal(QuestStatus.Taken, (await Find(quest.Id)).Status);

        // The person's override (§E5): their done at any time, closed as theirs.
        await Ready(quest);
        var theirs = await _exchange.PersonDoneAsync(quest.Id, "I checked it myself.", Now);
        Assert.Equal(QuestRespondRefusal.None, theirs.Refusal);
        Assert.Equal(QuestStatus.Done, theirs.Quest!.Status);
        Assert.StartsWith("The person marked this done", theirs.Quest.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ready_where_no_closure_graph_holds_the_quest_is_refused_and_moves_nothing()
    {
        var quest = await Taken();

        var ready = await Ready(quest);

        Assert.Equal(QuestRespondRefusal.NotInGraph, ready.Refusal);
        var standing = await Find(quest.Id);
        Assert.Null(standing.Submission);
        Assert.Empty(standing.Answers);
        Assert.Equal(QuestOperationKind.Taken, (await _quests.HistoryAsync(quest.Id))[^1].Kind);
    }

    [Fact]
    public async Task A_bind_applies_once_to_a_taken_quest_before_its_first_submission()
    {
        var open = (await _desk.PublishAsync(
            (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!.Id, "reports", Now,
            draft: new AskDraft("Write the bridge report", "Through the bridge.") { Requirements = [Report, Bridge] })).Quest!;
        Assert.Equal(QuestRespondRefusal.NotInGraph, (await _exchange.BindAsync(open.Id, Graph, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(open.Id, "take", null, Now)).Refusal);
        Assert.NotEqual(QuestRespondRefusal.None, (await _exchange.BindAsync(open.Id, "", Now)).Refusal);

        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(open.Id, Graph, Now)).Refusal);
        // The same graph again is the same bind; another graph is refused, since a quest follows the graph bound at its start.
        var again = await _exchange.BindAsync(open.Id, Graph, Now);
        Assert.Equal(QuestRespondRefusal.None, again.Refusal);
        Assert.Equal(QuestRespondRefusal.NotInGraph, (await _exchange.BindAsync(open.Id, "sha256:another", Now)).Refusal);
        Assert.Single(await _quests.HistoryAsync(open.Id), op => op.Kind == QuestOperationKind.Bound);
    }

    /// <summary>An open quest of the report's, as published and not yet taken.</summary>
    private async Task<Quest> Open() => (await _desk.PublishAsync(
        (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!.Id, "reports", Now,
        draft: new AskDraft("Write the bridge report", "Through the bridge.") { Requirements = [Report, Bridge] })).Quest!;

    [Fact]
    public async Task A_take_the_driver_named_a_graph_for_binds_in_its_own_write_and_its_done_is_refused_naming_ready()
    {
        // FABRIC-CLOSE1l (CLOSE1f): the driver names the graph as it opens the first session; nothing is bound until the take.
        var open = await Open();
        var named = await _exchange.BindAsync(open.Id, Graph, Now, atTake: true, session: "ses-first");
        Assert.Equal(QuestRespondRefusal.None, named.Refusal);
        Assert.Null((await Find(open.Id)).Closure);

        // FABRIC-CLOSE1o: the session it was named for takes it (QuestTakeBindTests holds every other taker).
        var take = await _exchange.RespondAsync(open.Id, "take", null, Now, session: "ses-first");

        // The take answers bound, saying `ready`, with the bind appended in the take's own write: no done fits between them.
        Assert.Equal(QuestRespondRefusal.None, take.Refusal);
        Assert.Equal(Graph, take.Quest!.Closure);
        Assert.Contains("`ready`", take.Message, StringComparison.Ordinal);
        Assert.Equal([QuestOperationKind.Taken, QuestOperationKind.Bound], (await _quests.HistoryAsync(open.Id)).TakeLast(2).Select(op => op.Kind));
        var done = await _exchange.RespondAsync(open.Id, "done", "Built.", Now, answers: [Met(1), Met(2)]);
        Assert.Equal(QuestRespondRefusal.ClosedByGraph, done.Refusal);
        Assert.Contains("`ready`", done.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_take_no_graph_was_named_for_is_unbound_and_says_nothing_of_ready_and_a_taken_quest_takes_no_bind_at_its_take()
    {
        // The owner's install: no closure graph, so no bind is named, and the take and its done are today's.
        var open = await Open();
        var take = await _exchange.RespondAsync(open.Id, "take", null, Now);
        Assert.Null(take.Quest!.Closure);
        Assert.DoesNotContain("ready", take.Message, StringComparison.OrdinalIgnoreCase);

        // Already taken: its take has passed, so a graph named for it now is refused and nothing is bound (§E5's cutover).
        Assert.NotEqual(QuestRespondRefusal.None, (await _exchange.BindAsync(open.Id, Graph, Now, atTake: true, session: "ses-first")).Refusal);
        Assert.DoesNotContain(await _quests.HistoryAsync(open.Id), op => op.Kind == QuestOperationKind.Bound);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(open.Id, "done", "Built.", Now, answers: [Met(1), Met(2)])).Refusal);
    }

    // ——— The submission's holds, its replacement and its seal.

    [Fact]
    public async Task A_later_ready_replaces_the_earlier_and_is_judged_again_from_the_start()
    {
        // No chain step: the yes releases the submission, and nothing seals it.
        var quest = await Bound(chain: false);
        var departed = (await Ready(quest, Departed, Met(2))).Quest!;
        Assert.Equal(QuestHold.Departed, departed.Hold);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(quest.Id, Now)).Refusal);

        var replaced = await Ready(quest);

        Assert.Equal(QuestRespondRefusal.None, replaced.Refusal);
        Assert.NotEqual(departed.Submission!.Sequence, replaced.Quest!.Submission!.Sequence);
        // The yes was the earlier submission's: the new one is judged again, and waits on its evidence.
        Assert.Null(replaced.Quest.Accepted);
        Assert.Equal(QuestHold.EvidenceUnread, replaced.Quest.Hold);
    }

    [Fact]
    public async Task Found_evidence_releases_the_submission_and_its_successor_seals_it_until_the_close()
    {
        var quest = await Bound();
        await Ready(quest);

        var found = await Found(quest);

        Assert.Equal(QuestRespondRefusal.None, found.Refusal);
        Assert.Equal(QuestStatus.Taken, found.Quest!.Status);
        Assert.Null(found.Quest.Hold);
        var next = Assert.Single(await _quests.ListAsync(), q => q.Parent == quest.Id);
        Assert.Equal(QuestStatus.Open, next.Status);

        // Sealed: a later ready is refused while the step it published stands, naming it.
        var later = await Ready(quest);
        Assert.Equal(QuestRespondRefusal.Sealed, later.Refusal);
        Assert.Contains($"#{next.Id}", later.Message, StringComparison.Ordinal);
        Assert.Equal(found.Quest.Submission, (await Find(quest.Id)).Submission);
    }

    [Fact]
    public async Task An_unheld_submission_publishes_its_chain_step_in_the_same_write()
    {
        var quest = await Bound();

        // No evidence named by a met answer: nothing holds it, and the chain moves on with the submission.
        var ready = await Ready(quest, new QuestAnswer(1, null, Departed: "No report this time.", Quote: Report.Quote), Met(2));
        Assert.Equal(QuestHold.Departed, ready.Quest!.Hold);
        Assert.DoesNotContain(await _quests.ListAsync(), q => q.Parent == quest.Id);

        var yes = await _exchange.AcceptAsync(quest.Id, Now);
        Assert.Equal(QuestRespondRefusal.None, yes.Refusal);
        Assert.Single(await _quests.ListAsync(), q => q.Parent == quest.Id);
    }

    /// <summary>
    /// FABRIC-CLOSE1e, finding 1: evidence read for one submission, arriving after a later ready replaced it, would release a
    /// submission nobody read, and publish its step. A verdict names the submission it was asked for, and is no verdict on another.
    /// </summary>
    [Fact]
    public async Task A_late_verdict_read_for_a_replaced_submission_never_releases_the_one_that_replaced_it()
    {
        var quest = await Bound();
        var first = (await Ready(quest)).Quest!.Submission!;
        var replaced = (await Ready(quest)).Quest!;
        Assert.NotEqual(first.Sequence, replaced.Submission!.Sequence);
        Assert.Equal(QuestHold.EvidenceUnread, replaced.Hold);

        var late = await Found(quest, new QuestOperationRef(first.Machine, first.Sequence));

        Assert.Equal(QuestRespondRefusal.NotAwaitingEvidence, late.Refusal);
        Assert.Contains("submission", late.Message, StringComparison.Ordinal);
        var standing = await Find(quest.Id);
        Assert.Equal(QuestHold.EvidenceUnread, standing.Hold);
        Assert.Null(standing.Evidence);
        Assert.DoesNotContain(await _quests.ListAsync(), q => q.Parent == quest.Id);
        Assert.DoesNotContain(await _quests.HistoryAsync(quest.Id), op => op.Kind == QuestOperationKind.Evidenced);

        // The replay judges it alike, so a verdict another machine pushed applies to no submission but its own.
        var history = await _quests.HistoryAsync(quest.Id);
        QuestOperation Read(QuestOperationRef? asked) => new(quest.Id, QuestOperationKind.Evidenced, "m2", 1, Now,
            Evidence: new QuestEvidenceVerdict(Commit, "session-end",
                [new QuestEvidenceRead(1, "docs/report-bridge.md", null, "found") { Object = Blob }]) { Submission = asked });
        Assert.False(QuestLog.Applies(QuestLog.Replay(history), Read(new QuestOperationRef(first.Machine, first.Sequence))));
        Assert.False(QuestLog.Applies(QuestLog.Replay(history), Read(null)));
        Assert.True(QuestLog.Applies(
            QuestLog.Replay(history), Read(new QuestOperationRef(replaced.Submission.Machine, replaced.Submission.Sequence))));

        // A submission's verdict names it: one naming none is not read for it.
        Assert.Equal(QuestRespondRefusal.NotAwaitingEvidence, (await Found(quest, null)).Refusal);

        // Read for the current submission, it releases it, and its step is published.
        Assert.Equal(QuestRespondRefusal.None, (await Found(quest)).Refusal);
        Assert.Single(await _quests.ListAsync(), q => q.Parent == quest.Id);
    }

    // ——— The close door.

    /// <summary>Canary 48: a two-step chain advances on the first step's unheld submission, and the close closes both in order.</summary>
    private async Task<(Quest First, Quest Second)> Chain()
    {
        var first = await Bound();
        await Ready(first);
        await Found(first);
        var second = Assert.Single(await _quests.ListAsync(), q => q.Parent == first.Id);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(second.Id, "take", null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(second.Id, Graph, Now)).Refusal);
        // The step to another repository inherits the requirements without their evidence: nothing holds its submission.
        var submitted = await Ready(second);
        Assert.Null(submitted.Quest!.Hold);
        return (await Find(first.Id), submitted.Quest);
    }

    [Fact]
    public async Task The_close_writes_every_quest_of_the_chain_done_with_its_submission_in_one_transaction()
    {
        var (first, second) = await Chain();

        var closed = await Close("k1", first, second);

        Assert.Equal(QuestCloseRefusal.None, closed.Refusal);
        Assert.All(closed.Quests, q => Assert.Equal(QuestStatus.Done, q.Status));
        foreach (var quest in new[] { first, second })
        {
            var history = await _quests.HistoryAsync(quest.Id);
            var done = Assert.Single(history, op => op.Kind == QuestOperationKind.Done);
            Assert.Equal(new QuestClose("k1", "token-k1"), done.Close);
            Assert.Equal("Built.", done.Note);
            Assert.Equal([Met(1), Met(2)], done.Answers);
            var standing = await Find(quest.Id);
            Assert.Equal(QuestStatus.Done, standing.Status);
            Assert.Null(standing.Hold);
        }
    }

    [Fact]
    public async Task The_same_close_again_answers_its_first_outcome_and_writes_once()
    {
        var (first, second) = await Chain();
        Assert.Equal(QuestCloseRefusal.None, (await Close("k1", first, second)).Refusal);

        var again = await Close("k1", first, second);

        Assert.Equal(QuestCloseRefusal.None, again.Refusal);
        Assert.All(again.Quests, q => Assert.Equal(QuestStatus.Done, q.Status));
        Assert.Single(await _quests.HistoryAsync(first.Id), op => op.Kind == QuestOperationKind.Done);
        Assert.Single(await _quests.HistoryAsync(second.Id), op => op.Kind == QuestOperationKind.Done);

        // Another key on quests already closed is a racing close: refused, naming the state.
        var racing = await Close("k2", first, second);
        Assert.Equal(QuestCloseRefusal.Closed, racing.Refusal);
    }

    /// <summary>
    /// FABRIC-CLOSE1e, finding 3: a key is one invocation. Its replay names the same token and the same submissions; anything
    /// else sent with it is another call, refused, never answered with the first close's success.
    /// </summary>
    [Fact]
    public async Task A_close_key_answers_only_its_own_close_and_refuses_another_call_sent_under_it()
    {
        var (first, second) = await Chain();
        Assert.Equal(QuestCloseRefusal.None, (await Close("k1", first)).Refusal);

        // The same key, naming a still-taken quest beside the closed one: not the close it was, so nothing is written.
        var wider = await Close("k1", first, second);

        Assert.Equal(QuestCloseRefusal.KeyReused, wider.Refusal);
        Assert.Contains("k1", wider.Message, StringComparison.Ordinal);
        Assert.Equal(QuestStatus.Taken, (await Find(second.Id)).Status);
        Assert.DoesNotContain(await _quests.HistoryAsync(second.Id), op => op.Kind == QuestOperationKind.Done);

        // Nor with another token, nor another submission.
        Assert.Equal(QuestCloseRefusal.KeyReused,
            (await _exchange.CloseAsync(first.Id, "k1", "another-token", [Named(first)], Now)).Refusal);
        Assert.Equal(QuestCloseRefusal.KeyReused,
            (await _exchange.CloseAsync(first.Id, "k1", "token-k1", [(first.Id, new QuestOperationRef("m9", 1))], Now)).Refusal);

        // The same call again is the same close.
        var again = await Close("k1", first);
        Assert.Equal(QuestCloseRefusal.None, again.Refusal);
        Assert.Single(await _quests.HistoryAsync(first.Id), op => op.Kind == QuestOperationKind.Done);
    }

    /// <summary>
    /// FABRIC-CLOSE1e, finding 4: the first outcome is the answer, a refusal as much as a close. A close refused for a hold whose
    /// answer was lost, sent again after the hold is released, answers the same refusal and writes nothing; a new attempt takes a
    /// new key. The refusal is kept in the store, beside the closes, and never as an operation that syncs.
    /// </summary>
    [Fact]
    public async Task A_refused_close_is_answered_the_same_under_its_key_after_what_refused_it_is_gone()
    {
        var quest = await Bound(chain: false);
        var held = (await Ready(quest, Departed, Met(2))).Quest!;
        Assert.Equal(QuestHold.Departed, held.Hold);
        Assert.Equal(QuestCloseRefusal.Held, (await Close("k1", held)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(quest.Id, Now)).Refusal);
        var written = (await _quests.HistoryAsync(quest.Id)).Count;

        var retried = await Close("k1", held);

        Assert.Equal(QuestCloseRefusal.Held, retried.Refusal);
        Assert.Contains("k1", retried.Message, StringComparison.Ordinal);
        Assert.Equal(QuestStatus.Taken, (await Find(quest.Id)).Status);
        Assert.Equal(written, (await _quests.HistoryAsync(quest.Id)).Count);
        // Under the key it was refused with, another call is still another call.
        Assert.Equal(QuestCloseRefusal.KeyReused,
            (await _exchange.CloseAsync(quest.Id, "k1", "another-token", [Named(held)], Now)).Refusal);
        // Kept in the store: a store opened again answers it alike.
        var reopened = await QuestStore.OpenAsync(_connection);
        var kept = await reopened.CloseAsync([Named(held)], new QuestClose("k1", "token-k1"), Now);
        Assert.Equal((QuestCloseRefusal.Held, true), (kept.Refusal, kept.Replayed));

        var fresh = await Close("k2", held);
        Assert.Equal(QuestCloseRefusal.None, fresh.Refusal);
        Assert.Equal(new QuestClose("k2", "token-k2"), Assert.Single(await _quests.HistoryAsync(quest.Id), op => op.Kind == QuestOperationKind.Done).Close);
    }

    /// <summary>
    /// FABRIC-CLOSE1u: what became of a close is answered from its key, executing nothing. A key never taken is withdrawn in that
    /// answer's transaction, so its close never runs: the same close arriving after is refused under it, the quest left taken. A
    /// key taken answers its first outcome, a close as a refusal, and another call under it is refused as one; asked again, the
    /// same answer, and nothing is written.
    /// </summary>
    [Fact]
    public async Task What_became_of_a_close_is_answered_from_its_key_and_a_key_never_taken_is_withdrawn_so_its_close_never_runs()
    {
        var quest = await Bound(chain: false);
        await Ready(quest, new QuestAnswer(1, null, Departed: "No report this time.", Quote: Report.Quote), Met(2));
        await _exchange.AcceptAsync(quest.Id, Now);
        var submitted = await Find(quest.Id);
        var written = (await _quests.HistoryAsync(quest.Id)).Count;
        Task<QuestCloseOutcome> Asked(string key, string? token = null) =>
            _exchange.CloseOutcomeAsync(quest.Id, key, token ?? "token-" + key, [Named(submitted)], Now);

        // Never taken: withdrawn, and the close it names never runs, though it would close the quest now.
        var withdrawn = await Asked("k1");
        Assert.Equal(QuestCloseRefusal.Withdrawn, withdrawn.Refusal);
        Assert.Contains("k1", withdrawn.Message, StringComparison.Ordinal);
        var late = await Close("k1", submitted);
        Assert.Equal(QuestCloseRefusal.Withdrawn, late.Refusal);
        Assert.Contains("withdrawn", late.Message, StringComparison.Ordinal);
        Assert.Equal(QuestStatus.Taken, (await Find(quest.Id)).Status);
        Assert.Equal(written, (await _quests.HistoryAsync(quest.Id)).Count);
        Assert.Equal(QuestCloseRefusal.Withdrawn, (await Asked("k1")).Refusal);

        // Taken and closed: the close is its answer, however often it is asked, and nothing more is written.
        Assert.Equal(QuestCloseRefusal.None, (await Close("k2", submitted)).Refusal);
        var closed = await Asked("k2");
        Assert.Equal(QuestCloseRefusal.None, closed.Refusal);
        Assert.Equal(QuestStatus.Done, Assert.Single(closed.Quests).Status);
        Assert.Equal(QuestCloseRefusal.None, (await Asked("k2")).Refusal);
        Assert.Single(await _quests.HistoryAsync(quest.Id), op => op.Kind == QuestOperationKind.Done);

        // Another call under a key is not that close; a refusal kept is answered as it was.
        Assert.Equal(QuestCloseRefusal.KeyReused, (await Asked("k2", "another-token")).Refusal);
        Assert.Equal(QuestCloseRefusal.Closed, (await Close("k3", submitted)).Refusal);
        Assert.Equal(QuestCloseRefusal.Closed, (await Asked("k3")).Refusal);

        // A question that is not one asks nothing and keeps no key.
        Assert.Equal(QuestCloseRefusal.Malformed, (await _exchange.CloseOutcomeAsync(quest.Id, "", "t", [Named(submitted)], Now)).Refusal);
        Assert.Equal(QuestCloseRefusal.Malformed, (await _exchange.CloseOutcomeAsync(quest.Id, "k4", "t", [], Now)).Refusal);
    }

    /// <summary>Canary 45: a close naming a submission no longer current writes nothing.</summary>
    [Fact]
    public async Task A_close_naming_a_submission_no_longer_current_writes_nothing()
    {
        var quest = await Bound(chain: false);
        var earlier = (await Ready(quest, Departed, Met(2))).Quest!;
        await Ready(quest, new QuestAnswer(1, null, Departed: "No report this time.", Quote: Report.Quote), Met(2));
        await _exchange.AcceptAsync(quest.Id, Now);

        var stale = await Close("k1", earlier);

        Assert.Equal(QuestCloseRefusal.Stale, stale.Refusal);
        Assert.Contains($"#{quest.Id}", stale.Message, StringComparison.Ordinal);
        Assert.Equal(QuestStatus.Taken, (await Find(quest.Id)).Status);
        Assert.DoesNotContain(await _quests.HistoryAsync(quest.Id), op => op.Kind == QuestOperationKind.Done);
    }

    /// <summary>Canaries 52 and 56: one quest's hold, a review's or its evidence's, refuses the whole close, and no quest is Done.</summary>
    [Fact]
    public async Task A_held_submission_refuses_the_close_and_no_quest_of_the_chain_is_done()
    {
        var first = await Bound();
        await Ready(first);
        await Found(first);
        var second = Assert.Single(await _quests.ListAsync(), q => q.Parent == first.Id);
        await _exchange.RespondAsync(second.Id, "take", null, Now);
        await _exchange.BindAsync(second.Id, Graph, Now);
        var held = (await Ready(second, new QuestAnswer(1, null, Departed: "Checked another way.", Quote: Report.Quote), Met(2))).Quest!;
        Assert.Equal(QuestHold.Departed, held.Hold);

        var refused = await Close("k1", await Find(first.Id), held);

        Assert.Equal(QuestCloseRefusal.Held, refused.Refusal);
        Assert.Contains($"#{second.Id}", refused.Message, StringComparison.Ordinal);
        Assert.Equal(QuestStatus.Taken, (await Find(first.Id)).Status);
        Assert.Equal(QuestStatus.Taken, (await Find(second.Id)).Status);
    }

    [Fact]
    public async Task The_close_refuses_a_quest_no_graph_holds_one_closed_by_the_person_and_a_call_that_is_not_one()
    {
        var quest = await Bound(chain: false);
        await Ready(quest, new QuestAnswer(1, null, Departed: "No report this time.", Quote: Report.Quote), Met(2));
        await _exchange.AcceptAsync(quest.Id, Now);
        var submitted = await Find(quest.Id);

        Assert.Equal(QuestCloseRefusal.Malformed, (await _exchange.CloseAsync(quest.Id, "", "t", [Named(submitted)], Now)).Refusal);
        Assert.Equal(QuestCloseRefusal.Malformed, (await _exchange.CloseAsync(quest.Id, "k", "t", [], Now)).Refusal);
        Assert.Equal(QuestCloseRefusal.Malformed,
            (await _exchange.CloseAsync(quest.Id, "k", "t", [Named(submitted), Named(submitted)], Now)).Refusal);
        // Each call is its own close, so each takes its own key (FABRIC-CLOSE1e): a malformed call is none, and keeps no key.
        Assert.Equal(QuestCloseRefusal.NotFound,
            (await _exchange.CloseAsync("feedfacecafe", "k", "t", [("feedfacecafe", new QuestOperationRef("m", 1))], Now)).Refusal);

        var unbound = await Taken(chain: false, title: "Another report");
        Assert.Equal(QuestCloseRefusal.NotBound,
            (await _exchange.CloseAsync(unbound.Id, "k-unbound", "t", [(unbound.Id, new QuestOperationRef("m", 1))], Now)).Refusal);

        // The person's override: closed as theirs, and the graph's close finds it closed.
        await _exchange.PersonDoneAsync(quest.Id, null, Now);
        Assert.Equal(QuestCloseRefusal.Closed, (await Close("k1", submitted)).Refusal);
    }

    /// <summary>Canaries 39 and 46: a quest this machine does not hold is never closed here.</summary>
    [Fact]
    public async Task The_close_refuses_a_quest_another_machine_took_and_submitted()
    {
        var asked = new Quest("abcdefabcdef", "ask #a1b2c3", "reports", "Write the report", "Through the bridge.", QuestStatus.Open, null, Now, Now);
        QuestOperation Op(QuestOperationKind kind, long sequence) => new("abcdefabcdef", kind, "m2", sequence, Now, Number: sequence);
        await _quests.IntegrateAsync(Workspaces.Default,
        [
            Op(QuestOperationKind.Published, 1) with { Published = asked },
            Op(QuestOperationKind.Taken, 2),
            Op(QuestOperationKind.Bound, 3) with { Note = Graph },
            Op(QuestOperationKind.Submitted, 4) with { Note = "Built." },
        ], 4);
        var theirs = await Find("abcdefabcdef");
        Assert.NotNull(theirs.Submission);

        var refused = await Close("k1", theirs);

        Assert.Equal(QuestCloseRefusal.NotHeld, refused.Refusal);
        Assert.Equal(QuestStatus.Taken, (await Find("abcdefabcdef")).Status);
    }

    /// <summary>Canary 54: a submission is one of a take's own kinds, so one made on a take this machine lost is refused.</summary>
    [Fact]
    public void A_submission_and_a_bind_on_a_take_this_machine_lost_are_made_on_a_claim_it_never_held()
    {
        var asked = new Quest("abcdefabcdef", "ask #a1b2c3", "reports", "Write the report", "Through the bridge.", QuestStatus.Open, null, Now, Now);
        IReadOnlyList<QuestOperation> history =
        [
            new("abcdefabcdef", QuestOperationKind.Published, "m2", 1, Now, Published: asked, Number: 1),
            new("abcdefabcdef", QuestOperationKind.Taken, "m2", 2, Now, Number: 2),
            new("abcdefabcdef", QuestOperationKind.Bound, "m2", 3, Now, Note: Graph, Number: 3),
            new("abcdefabcdef", QuestOperationKind.Conflict, "m1", 1, Now, Attempted: QuestStatus.Taken),
        ];
        var standing = QuestLog.Replay(history);

        Assert.True(QuestLog.TakersOwn(QuestOperationKind.Submitted));
        Assert.True(QuestLog.TakersOwn(QuestOperationKind.Bound));
        Assert.True(QuestLog.OnALostTake(standing, history, new("abcdefabcdef", QuestOperationKind.Submitted, "m1", 2, Now)));
        Assert.Equal(QuestLoss.Forgotten, QuestLog.Lost(QuestOperationKind.Submitted));
        Assert.Equal(QuestLoss.Forgotten, QuestLog.Lost(QuestOperationKind.Bound));
    }

    // ——— Quests from before.

    [Fact]
    public async Task A_quest_no_graph_binds_closes_done_as_it_always_did()
    {
        var quest = await Taken();

        var done = await _exchange.RespondAsync(quest.Id, "done", "Built.", Now, answers: [Met(1), Met(2)]);

        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        Assert.Equal(QuestStatus.Done, done.Quest!.Status);
        Assert.Null(done.Quest.Closure);
        Assert.Null(done.Quest.Submission);
        Assert.Equal(QuestHold.EvidenceUnread, done.Quest.Hold);
        Assert.Null((await _quests.HistoryAsync(quest.Id))[^1].Close);
    }
}
