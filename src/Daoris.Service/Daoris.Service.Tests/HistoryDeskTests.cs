using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// HIST1b (D153, the history-clearing design §1, §2.1, §3): the records' half of clearing finished work from this machine,
/// judged by <see cref="HistoryDesk"/>. A closed quest's work, an ask's work, or a closed quest's failed sessions is listed
/// with what it takes, and goes whole or stays whole, its refusal naming the piece that keeps it. A record that never left
/// the machine simply goes, row and log together. What a remote numbered is forgotten, which the sync tests hold.
/// </summary>
public sealed class HistoryDeskTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-history-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private AskStore _asks = null!;
    private KnowledgeService _service = null!;
    private QuestFiles _files = null!;
    private RuleProposalBox _proposals = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T10:00:00Z");

    private string Home => _root + "-home";

    public async Task InitializeAsync()
    {
        // One joined receiver and one local-only one: whether a record went up follows the receiver (design §3).
        Repo("Federated", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Shared with the team.", "owns": ["its area"], "accepts": ["a quest"] },
              "remote": { "join": true, "knowledge": false }
            }
            """);
        Repo("Homebody", """
            { "source": "s", "packs": [], "domain": { "summary": "Stays local.", "owns": ["itself"], "accepts": ["a quest"] } }
            """);
        Repo("Asker", """
            { "source": "s", "packs": [], "domain": { "summary": "Asks for things.", "owns": ["its own tree"], "accepts": ["a question"] } }
            """);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);

        _files = new QuestFiles(Home);
        _proposals = new RuleProposalBox(Home);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        foreach (var folder in new[] { _root, Home })
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private QuestExchange Exchange() => new(_service, _quests, remotes: null, _files, _sessions, _asks);

    private AskDesk Asking() => new(_service, _asks, Exchange(), _files);

    /// <summary>The desk, on a machine whose every circle is wired to <paramref name="remote"/>; null is a machine with none.</summary>
    private HistoryDesk Desk(IRemote? remote = null) =>
        new(_quests, _sessions, _asks, _service, remote is null ? null : new OneRemote(remote), _files, _proposals);

    private static HistoryUnitRef QuestUnit(string id) => new(HistoryUnitKind.Quest, id);

    private static HistoryUnitRef AskUnit(string id) => new(HistoryUnitKind.Ask, id);

    private static HistoryUnitRef FailedUnit(string id) => new(HistoryUnitKind.Failed, id);

    /// <summary>A quest <paramref name="from"/> asks, taken and closed as <paramref name="status"/>, with a file kept for it.</summary>
    private async Task<Quest> Closed(
        string title, string to = "Homebody", QuestStatus status = QuestStatus.Done, string? publishedBy = null, string from = "Asker")
    {
        var quest = (await Exchange().PublishAsync(
            new QuestAsk(from, to, title, "why")
            {
                Uploads = [new QuestUpload("trace.log", Encoding.UTF8.GetBytes("stack"))],
                PublishedBy = publishedBy,
            },
            Now)).Quest!;
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.MoveAsync(quest.Id, status, "An answer.", Now.AddMinutes(2));
        return (await _quests.FindAsync(quest.Id))!;
    }

    /// <summary>This machine's session of <paramref name="quest"/>, ended as <paramref name="state"/>.</summary>
    private async Task<Session> Served(Quest quest, SessionState state = SessionState.Completed)
    {
        var session = await _sessions.CreateAsync(quest.Id, quest.To, "stub", Now, workspace: quest.Workspace);
        await _sessions.SetStateAsync(session.Id, state, null, null, null, Now.AddMinutes(3));
        return (await _sessions.FindAsync(session.Id))!;
    }

    /// <summary>A teammate's record of <paramref name="quest"/>, as a fetch brings it down (SYNC4).</summary>
    private async Task<Session> Theirs(Quest quest, SessionState state = SessionState.Completed, string origin = "b@two")
    {
        var record = new Session(
            $"{origin}/{Guid.NewGuid():N}"[..(origin.Length + 9)], quest.Id, quest.To, "stub", state, null, null, null, Now, Now,
            quest.Workspace)
        {
            Origin = origin,
        };
        await _sessions.MirrorAsync(record);
        return (await _sessions.FindAsync(record.Id))!;
    }

    // ——— A closed quest's work (design §1.1).

    /// <summary>
    /// 🔴 A closed quest that never left the machine simply goes (design §3.1): its row and its whole log together (H3), every
    /// session that served it, and the files the service kept for it. Nothing is marked forgotten, since no remote numbered it.
    /// </summary>
    [Fact]
    public async Task A_closed_quest_that_never_left_the_machine_goes_with_its_log_its_sessions_and_its_files()
    {
        var quest = await Closed("Finished work");
        var first = await Served(quest, SessionState.Failed);
        var second = await Served(quest);
        Assert.True(_files.Has(quest.Id, quest.Attachments[0]));

        var plan = await Desk().PlanAsync(QuestUnit(quest.Id));

        Assert.True(plan.Clearable);
        Assert.Null(plan.Refusal);
        Assert.Equal([quest.Id], plan.Quests);
        Assert.Empty(plan.Forgotten);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), plan.Sessions.Order());
        Assert.NotNull(await _quests.FindAsync(quest.Id));

        var cleared = Assert.Single(await Desk().ClearAsync([QuestUnit(quest.Id)], Now.AddHours(1)));

        Assert.True(cleared.Cleared);
        Assert.Contains($"Cleared `#{quest.Id}` from this machine", cleared.Message);
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Empty(await _quests.HistoryAsync(quest.Id));
        Assert.False(await _quests.ForgottenAsync(quest.Id));
        Assert.Null(await _sessions.FindAsync(first.Id));
        Assert.Null(await _sessions.FindAsync(second.Id));
        Assert.False(Directory.Exists(Path.GetDirectoryName(_files.DirectoryOf(quest.Id))));
    }

    /// <summary>
    /// A row with no log would be given a fresh pending history when the store next opens (H3): reopened over the same
    /// file, a cleared quest stays gone.
    /// </summary>
    [Fact]
    public async Task A_cleared_quest_stays_gone_when_the_store_opens_again()
    {
        var quest = await Closed("Gone for good");
        await Desk().ClearAsync([QuestUnit(quest.Id)], Now.AddHours(1));

        var reopened = await QuestStore.OpenAsync(_connection);

        Assert.Null(await reopened.FindAsync(quest.Id));
        Assert.Empty(await reopened.HistoryAsync(quest.Id));
        Assert.Empty(await reopened.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>
    /// A quest's work takes every closed question its sessions asked (D79), applied again to what each adds, as the driver's
    /// reading of a work does (D132 §1): the question's sessions too, and its teammate's copies, so nothing left behind names
    /// a session nothing holds.
    /// </summary>
    [Fact]
    public async Task A_quests_work_takes_each_closed_question_its_sessions_asked_with_their_sessions()
    {
        var quest = await Closed("The work");
        var asking = await Served(quest);
        var question = await Closed("Which one?", to: "Asker", publishedBy: asking.Id, from: "Homebody");
        var answering = await Served(question);
        var further = await Closed("And the other?", to: "Homebody", publishedBy: answering.Id);
        var copy = await Theirs(question);

        var plan = await Desk().PlanAsync(QuestUnit(quest.Id));

        Assert.True(plan.Clearable);
        Assert.Equal([quest.Id, question.Id, further.Id], plan.Quests);
        Assert.Equal(new[] { asking.Id, answering.Id }.Order(), plan.Sessions.Order());
        Assert.Equal([copy.Id], plan.Teammates);

        Assert.True(Assert.Single(await Desk().ClearAsync([QuestUnit(quest.Id)], Now.AddHours(1))).Cleared);
        foreach (var id in new[] { quest.Id, question.Id, further.Id }) Assert.Null(await _quests.FindAsync(id));
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
    }

    [Fact]
    public async Task A_quest_or_an_ask_this_machine_does_not_hold_is_unknown()
    {
        var quest = await Desk().PlanAsync(QuestUnit("#0123456789ab"));
        var ask = await Desk().PlanAsync(AskUnit("abcdef"));
        var failed = await Desk().PlanAsync(FailedUnit("0123456789ab"));

        Assert.Equal(HistoryRefusal.Unknown, quest.Refusal!.Refusal);
        Assert.Equal("No quest `#0123456789ab` on this machine.", quest.Refusal.Message);
        Assert.Equal(HistoryRefusal.Unknown, ask.Refusal!.Refusal);
        Assert.Equal("No ask `#abcdef` on this machine.", ask.Refusal.Message);
        Assert.Equal(HistoryRefusal.Unknown, failed.Refusal!.Refusal);
        Assert.False((await Desk().ClearAsync([QuestUnit("0123456789ab")], Now)).Single().Cleared);
    }

    /// <summary>🔴 Open or taken work is in progress, and its record is what the driver plans from (design §1.2, §4).</summary>
    [Fact]
    public async Task An_open_or_a_taken_quest_is_work_in_progress_and_stays()
    {
        var open = (await Exchange().PublishAsync("Asker", "Homebody", "Not yet", "why", Now)).Quest!;
        var taken = (await Exchange().PublishAsync("Asker", "Homebody", "Under way", "why", Now)).Quest!;
        await _quests.MoveAsync(taken.Id, QuestStatus.Taken, null, Now.AddMinutes(1));

        var stillOpen = await Desk().PlanAsync(QuestUnit(open.Id));
        var stillTaken = await Desk().PlanAsync(QuestUnit(taken.Id));
        var pressed = await Desk().ClearAsync([QuestUnit(open.Id), QuestUnit(taken.Id)], Now.AddHours(1));

        Assert.Equal((HistoryRefusal.Open, open.Id), (stillOpen.Refusal!.Refusal, stillOpen.Refusal.Quest));
        Assert.Equal($"Quest `#{open.Id}` is still open: its record is work in progress.", stillOpen.Refusal.Message);
        Assert.Equal($"Quest `#{taken.Id}` is taken: its record is work in progress.", stillTaken.Refusal!.Message);
        Assert.All(pressed, outcome => Assert.False(outcome.Cleared));
        Assert.NotNull(await _quests.FindAsync(open.Id));
        Assert.NotNull(await _quests.FindAsync(taken.Id));
    }

    /// <summary>A quest an ask asked is cleared with its ask, never alone: the ask would read as a proposal again (H6).</summary>
    [Fact]
    public async Task A_quest_an_ask_asked_is_cleared_only_with_its_ask()
    {
        var ask = (await Asking().AskAsync(new AskRequest(Workspaces.Default, "Please fix the thing") { To = "Homebody" }, Now)).Ask!;
        var quest = ask.Quests[0];
        await _quests.MoveAsync(quest, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.MoveAsync(quest, QuestStatus.Done, "Fixed.", Now.AddMinutes(2));

        var alone = await Desk().PlanAsync(QuestUnit(quest));

        Assert.Equal((HistoryRefusal.Asked, ask.Id, quest), (alone.Refusal!.Refusal, alone.Refusal.Ask, alone.Refusal.Quest));
        Assert.Equal($"Ask `#{ask.Id}` asked `#{quest}`; clear the ask, which takes every quest it became.", alone.Refusal.Message);
        Assert.True((await Desk().PlanAsync(AskUnit(ask.Id))).Clearable);
    }

    /// <summary>
    /// A session that still runs writes into its record, and a teammate's that still reads as running has not ended yet
    /// (design §1.2): either keeps the whole work.
    /// </summary>
    [Fact]
    public async Task A_running_session_here_or_a_teammates_still_reading_as_running_keeps_the_work()
    {
        var mine = await Closed("Mine still runs");
        var running = await Served(mine, SessionState.Working);
        var theirs = await Closed("Theirs still reads as running");
        var copy = await Theirs(theirs, SessionState.Working);

        var here = await Desk().PlanAsync(QuestUnit(mine.Id));
        var there = await Desk().PlanAsync(QuestUnit(theirs.Id));

        Assert.Equal((HistoryRefusal.Live, running.Id), (here.Refusal!.Refusal, here.Refusal.Session));
        Assert.Equal($"Session `{running.Id}` is still running; stop it first.", here.Refusal.Message);
        Assert.Equal((HistoryRefusal.Live, copy.Id, "b@two"), (there.Refusal!.Refusal, there.Refusal.Session, there.Refusal.Origin));
        Assert.Equal($"Session `{copy.Id}` still reads as running on `b@two`.", there.Refusal.Message);
    }

    /// <summary>
    /// 🔴 What waits on the person is never cleared under them (design §1.2): a parked session, a done held for their yes
    /// (its chain's next step is published on it, H8), and an ask they have yet to publish or close.
    /// </summary>
    [Fact]
    public async Task What_waits_on_the_person_keeps_the_work()
    {
        var parked = await Closed("A session asked the person");
        var asking = await Served(parked, SessionState.AwaitingPerson);
        var held = (await Exchange().PublishAsync("Asker", "Homebody", "Done, departing", "why", Now)).Quest!;
        await _quests.MoveAsync(held.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.MoveAsync(
            held.Id, QuestStatus.Done, "Done another way.", Now.AddMinutes(2),
            answers: [new QuestAnswer(1, null, "The words asked for the other way.", "the other way")]);
        var proposed = (await Asking().AskAsync(new AskRequest(Workspaces.Default, "Somebody should look at this"), Now)).Ask!;

        var session = (await Desk().PlanAsync(QuestUnit(parked.Id))).Refusal!;
        var accept = (await Desk().PlanAsync(QuestUnit(held.Id))).Refusal!;
        var ask = (await Desk().PlanAsync(AskUnit(proposed.Id))).Refusal!;

        Assert.Equal((HistoryRefusal.NeedsYou, asking.Id), (session.Refusal, session.Session));
        Assert.Equal($"Session `{asking.Id}` waits on you.", session.Message);
        Assert.Equal((HistoryRefusal.NeedsYou, held.Id), (accept.Refusal, accept.Quest));
        Assert.Equal($"Quest `#{held.Id}` is done and waits for you to accept it.", accept.Message);
        Assert.Equal((HistoryRefusal.NeedsYou, proposed.Id), (ask.Refusal, ask.Ask));
        Assert.Equal($"Ask `#{proposed.Id}` waits for you to publish or close it.", ask.Message);
    }

    /// <summary>
    /// A rule proposal a session of the work made waits on the person while it is unsettled (design §2.2); once settled it
    /// is history, and goes with the session it names.
    /// </summary>
    [Fact]
    public async Task A_pending_rule_proposal_keeps_the_work_and_a_settled_one_goes_with_it()
    {
        var quest = await Closed("A session proposed a rule");
        var session = await Served(quest);
        var (id, _) = _proposals.Propose(
            new RuleChange("add", "machine", null, "deny", "Bash(rm:*)", null, null), "it should not", session.Id, null, null, Now);
        var other = await Closed("Somebody else's proposal stays");
        var (untouched, _) = _proposals.Propose(
            new RuleChange("add", "machine", null, "deny", "Bash(git push:*)", null, null), "not this work's", "zz99zz99", null, null, Now);

        var pending = await Desk().PlanAsync(QuestUnit(quest.Id));

        Assert.Equal((HistoryRefusal.NeedsYou, session.Id), (pending.Refusal!.Refusal, pending.Refusal.Session));
        Assert.Equal($"A rule proposal from session `{session.Id}` waits on you.", pending.Refusal.Message);

        var path = Path.Combine(Home, RuleProposalBox.Folder, $"{id}.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"state\": \"proposed\"", "\"state\": \"applied\"", StringComparison.Ordinal));

        Assert.True(Assert.Single(await Desk().ClearAsync([QuestUnit(quest.Id)], Now.AddHours(1))).Cleared);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(Path.Combine(Home, RuleProposalBox.Folder, $"{untouched}.json")));
        Assert.NotNull(await _quests.FindAsync(other.Id));
    }

    /// <summary>
    /// 🔴 Open work naming it keeps it (design §1.2): a taken quest waiting on its answer would be stranded (H7), an open
    /// question its session published belongs to the work, and a chain's open next step builds on its last session here.
    /// </summary>
    [Fact]
    public async Task Open_work_naming_it_keeps_it()
    {
        var question = await Closed("Which one?", to: "Asker", from: "Homebody");
        var waiting =(await Exchange().PublishAsync("Asker", "Homebody", "Waits on the answer", "why", Now)).Quest!;
        await _quests.MoveAsync(waiting.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.WaitAsync(waiting.Id, question.Id, Now.AddMinutes(2));

        var asker = await Closed("The work that asked");
        var session = await Served(asker);
        var open = (await Exchange().PublishAsync(
            new QuestAsk("Homebody", "Asker", "Still being asked", "why") { PublishedBy = session.Id }, Now)).Quest!;

        var parent = (await Exchange().PublishAsync(
            new QuestAsk("Asker", "Homebody", "First step", "why") { Then = [new QuestStep("Homebody", "Then verify {parent}", "b")] },
            Now)).Quest!;
        await _quests.MoveAsync(parent.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        var step = (await _quests.MoveAsync(parent.Id, QuestStatus.Done, "Landed.", Now.AddMinutes(2))).FollowUp!;

        var answered = (await Desk().PlanAsync(QuestUnit(question.Id))).Refusal!;
        var asked = (await Desk().PlanAsync(QuestUnit(asker.Id))).Refusal!;
        var chained = (await Desk().PlanAsync(QuestUnit(parent.Id))).Refusal!;

        Assert.Equal((HistoryRefusal.Awaited, waiting.Id), (answered.Refusal, answered.Quest));
        Assert.Equal($"Quest `#{waiting.Id}` waits on its answer from `#{question.Id}`.", answered.Message);
        Assert.Equal((HistoryRefusal.Awaited, open.Id, session.Id), (asked.Refusal, asked.Quest, asked.Session));
        Assert.Equal($"Quest `#{open.Id}` was published by its session `{session.Id}` and is still open.", asked.Message);
        Assert.Equal((HistoryRefusal.Awaited, step.Id), (chained.Refusal, chained.Quest));
        Assert.Equal($"Quest `#{step.Id}`, its chain's next step, is still open and builds on its work.", chained.Message);
    }

    /// <summary>
    /// 🔴 On a wired workspace, moves the remote has not numbered are the team's copy still to come (design §1.2): clearing
    /// them first would be a done the team never hears of. A receiver that never leaves the machine has nothing to push.
    /// </summary>
    [Fact]
    public async Task Moves_a_wired_workspace_has_not_pushed_keep_the_quest()
    {
        var shared = await Closed("The team's work", to: "Federated");
        var local = await Closed("This machine's own", to: "Homebody");
        var desk = Desk(new UnreachableRemote());

        var unpushed = (await desk.PlanAsync(QuestUnit(shared.Id))).Refusal!;

        Assert.Equal((HistoryRefusal.Unpushed, Workspaces.Default), (unpushed.Refusal, unpushed.Workspace));
        Assert.Equal("Its last moves have not reached the remote for `default`; sync, then clear it.", unpushed.Message);
        var plan = await desk.PlanAsync(QuestUnit(local.Id));
        Assert.True(plan.Clearable);
        Assert.Empty(plan.Forgotten);
    }

    // ——— A closed quest's failed sessions (design §1.1, §4).

    /// <summary>
    /// A closed quest's failed sessions of this machine's go, and nothing else: the quest, its log and its other sessions
    /// stay. A teammate's failed session is listed and kept, since its record is theirs.
    /// </summary>
    [Fact]
    public async Task A_closed_quests_failed_sessions_go_and_a_teammates_is_listed_and_kept()
    {
        var quest = await Closed("Done after a few tries");
        var failedOnce = await Served(quest, SessionState.Failed);
        var failedTwice = await Served(quest, SessionState.Failed);
        var landed = await Served(quest);
        var theirs = await Theirs(quest, SessionState.Failed);

        var plan = await Desk().PlanAsync(FailedUnit(quest.Id));

        Assert.True(plan.Clearable);
        Assert.Empty(plan.Quests);
        Assert.Equal(new[] { failedOnce.Id, failedTwice.Id }.Order(), plan.Sessions.Order());
        var kept = Assert.Single(plan.Kept);
        Assert.Equal((HistoryRefusal.NotOurs, theirs.Id, "b@two"), (kept.Refusal, kept.Session, kept.Origin));
        Assert.Equal($"Session `{theirs.Id}` ran on `b@two`; its record is theirs.", kept.Message);

        var cleared = Assert.Single(await Desk().ClearAsync([FailedUnit(quest.Id)], Now.AddHours(1)));

        Assert.True(cleared.Cleared);
        Assert.Contains($"Cleared 2 failed sessions of `#{quest.Id}`", cleared.Message);
        Assert.Null(await _sessions.FindAsync(failedOnce.Id));
        Assert.Null(await _sessions.FindAsync(failedTwice.Id));
        Assert.NotNull(await _sessions.FindAsync(landed.Id));
        Assert.NotNull(await _sessions.FindAsync(theirs.Id));
        Assert.NotNull(await _quests.FindAsync(quest.Id));
        Assert.NotEmpty(await _quests.HistoryAsync(quest.Id));
    }

    /// <summary>
    /// 🔴 A session of an open or taken quest is never cleared (design §4): its strikes are counted from these records, and
    /// clearing one would lower the count under RETRY1's mark. Archive is how it leaves the list.
    /// </summary>
    [Fact]
    public async Task Failed_sessions_of_an_open_quest_are_its_strikes_and_stay()
    {
        var quest = (await Exchange().PublishAsync("Asker", "Homebody", "Still trying", "why", Now)).Quest!;
        var failed = await Served(quest, SessionState.Failed);

        var plan = await Desk().PlanAsync(FailedUnit(quest.Id));
        var pressed = Assert.Single(await Desk().ClearAsync([FailedUnit(quest.Id)], Now.AddHours(1)));

        Assert.Equal((HistoryRefusal.Open, quest.Id), (plan.Refusal!.Refusal, plan.Refusal.Quest));
        Assert.Equal(
            $"Quest `#{quest.Id}` is still open or taken, and these failed sessions count against it; archive them instead.",
            plan.Refusal.Message);
        Assert.False(pressed.Cleared);
        Assert.NotNull(await _sessions.FindAsync(failed.Id));
    }

    /// <summary>Nothing to clear is information, never a refusal (D48 §6): a closed quest with no failed session says so.</summary>
    [Fact]
    public async Task A_closed_quest_with_no_failed_session_has_nothing_to_clear()
    {
        var quest = await Closed("Clean first time");
        await Served(quest);

        var plan = await Desk().PlanAsync(FailedUnit(quest.Id));
        var pressed = Assert.Single(await Desk().ClearAsync([FailedUnit(quest.Id)], Now.AddHours(1)));

        Assert.True(plan.Clearable);
        Assert.Empty(plan.Sessions);
        Assert.Equal($"Nothing to clear: `#{quest.Id}` has no failed session of this machine's.", pressed.Message);
        Assert.Single(await _sessions.ListAsync(includeClosed: true));
    }

    // ——— An ask's work (design §1.1): the ask, its intake, every quest of its work, or not at all.

    /// <summary>
    /// An ask whose work closed goes whole: the ask, the quests it became and their sessions, its intake's record, and the
    /// files both kept on this machine. Asks never leave the machine (D65), so an ask always simply goes.
    /// </summary>
    [Fact]
    public async Task An_ask_whose_work_closed_goes_with_its_quests_its_intake_and_its_files()
    {
        var desk = Asking();
        var ask = (await desk.AskAsync(
            new AskRequest(Workspaces.Default, "Look into the slow start")
            {
                Uploads = [new QuestUpload("note.txt", Encoding.UTF8.GetBytes("words"))],
            },
            Now)).Ask!;
        var ledger = new SessionLedger(_quests, _sessions, _service, _asks);
        var intake = (await ledger.OpenIntakeAsync(ask.Id, "stub", Path.Combine(_root, "room"), Now)).Session!;
        var quest = (await desk.PublishAsync(ask.Id, "Homebody", Now.AddMinutes(1), session: intake.Id)).Quest!;
        await _sessions.SetStateAsync(intake.Id, SessionState.Completed, null, null, null, Now.AddMinutes(2));
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(3));
        var work = await Served((await _quests.FindAsync(quest.Id))!);
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, "Faster now.", Now.AddMinutes(4));
        var kept = _files.For(AskDesk.Folder);
        Assert.True(kept.Has(ask.Id, ask.Attachments[0]));

        var plan = await Desk().PlanAsync(AskUnit(ask.Id));

        Assert.True(plan.Clearable);
        Assert.Equal([ask.Id], plan.Asks);
        Assert.Equal([quest.Id], plan.Quests);
        Assert.Equal(new[] { intake.Id, work.Id }.Order(), plan.Sessions.Order());

        Assert.True(Assert.Single(await Desk().ClearAsync([AskUnit(ask.Id)], Now.AddHours(1))).Cleared);
        Assert.Null(await _asks.FindAsync(ask.Id));
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
        Assert.False(kept.Has(ask.Id, ask.Attachments[0]));
    }

    /// <summary>One quest that may not go keeps the whole ask (D95's rule, kept): nothing of it is cleared.</summary>
    [Fact]
    public async Task An_ask_with_a_quest_still_open_stays_whole()
    {
        var desk = Asking();
        var ask = (await desk.AskAsync(new AskRequest(Workspaces.Default, "Two things, please") { To = "Homebody" }, Now)).Ask!;
        var second = (await desk.PublishAsync(ask.Id, "Federated", Now.AddMinutes(1))).Quest!;
        await _quests.MoveAsync(ask.Quests[0], QuestStatus.Taken, null, Now.AddMinutes(2));
        await _quests.MoveAsync(ask.Quests[0], QuestStatus.Done, "One done.", Now.AddMinutes(3));

        var plan = await Desk().PlanAsync(AskUnit(ask.Id));
        var pressed = Assert.Single(await Desk().ClearAsync([AskUnit(ask.Id)], Now.AddHours(1)));

        Assert.Equal((HistoryRefusal.Open, second.Id), (plan.Refusal!.Refusal, plan.Refusal.Quest));
        Assert.False(pressed.Cleared);
        Assert.NotNull(await _asks.FindAsync(ask.Id));
        Assert.NotNull(await _quests.FindAsync(ask.Quests[0]));
    }

    // ——— A workspace's finished history, and the press.

    /// <summary>
    /// A workspace lists each closed quest no ask here asked and each ask whose work closed or that was closed, once: a
    /// question rides with the work that asked it, a quest with its ask, and nothing open or of another workspace is listed.
    /// </summary>
    [Fact]
    public async Task A_workspace_lists_each_closed_quest_and_finished_ask_once()
    {
        var quest = await Closed("The work");
        var asking = await Served(quest);
        var question = await Closed("Which one?", to: "Asker", publishedBy: asking.Id, from: "Homebody");
        await Exchange().PublishAsync("Asker", "Homebody", "Not yet", "why", Now);
        var ask = (await Asking().AskAsync(new AskRequest(Workspaces.Default, "Fix it") { To = "Homebody" }, Now)).Ask!;
        await _quests.MoveAsync(ask.Quests[0], QuestStatus.Taken, null, Now.AddMinutes(1));
        await _quests.MoveAsync(ask.Quests[0], QuestStatus.Declined, "Not ours.", Now.AddMinutes(2));
        var closedAsk = (await Asking().AskAsync(new AskRequest(Workspaces.Default, "Never mind this one"), Now)).Ask!;
        await Asking().CloseAsync(closedAsk.Id, "Asked by mistake.", Now.AddMinutes(1));
        var elsewhere = await _quests.PublishAsync("Asker", "Homebody", "In another circle", "why", Now, workspace: "other");
        await _quests.MoveAsync(elsewhere.Id, QuestStatus.Declined, "No.", Now.AddMinutes(1));

        var units = await Desk().PlanWorkspaceAsync(Workspaces.Default);

        Assert.Equal(
            new[] { $"ask:{ask.Id}", $"ask:{closedAsk.Id}", $"quest:{quest.Id}" }.Order(),
            units.Select(unit => $"{HistoryUnitRef.Spell(unit.Kind)}:{unit.Id}").Order());
        Assert.Equal([quest.Id, question.Id], units.Single(unit => unit.Id == quest.Id).Quests);
        Assert.All(units, unit => Assert.True(unit.Clearable));
        Assert.Single(await Desk().PlanWorkspaceAsync("other"));
        Assert.Empty(await Desk().PlanWorkspaceAsync("nobody-wired-this"));
    }

    /// <summary>
    /// 🔴 The second press clears exactly the units named and judges each again where it clears it (D88, design §5): one
    /// that changed since the list is kept with its word, and the next unit still goes.
    /// </summary>
    [Fact]
    public async Task The_press_judges_each_unit_again_and_keeps_one_that_changed_since_the_list()
    {
        var changed = await Closed("Somebody started again");
        var still = await Closed("Nothing moved");
        Assert.True((await Desk().PlanAsync(QuestUnit(changed.Id))).Clearable);
        var started = await Served(changed, SessionState.Working);

        var pressed = await Desk().ClearAsync([QuestUnit(changed.Id), QuestUnit(still.Id)], Now.AddHours(1));

        Assert.Equal([false, true], pressed.Select(outcome => outcome.Cleared));
        Assert.Equal((HistoryRefusal.Live, started.Id), (pressed[0].Unit.Refusal!.Refusal, pressed[0].Unit.Refusal!.Session));
        Assert.Equal(pressed[0].Unit.Refusal!.Message, pressed[0].Message);
        Assert.NotNull(await _quests.FindAsync(changed.Id));
        Assert.Null(await _quests.FindAsync(still.Id));
    }

    /// <summary>Each refusal's word is the one the driver reads, kebab-case as every word on the wire (design §6.3).</summary>
    [Theory]
    [InlineData(HistoryRefusal.Unknown, "unknown")]
    [InlineData(HistoryRefusal.Open, "open")]
    [InlineData(HistoryRefusal.Asked, "asked")]
    [InlineData(HistoryRefusal.Live, "live")]
    [InlineData(HistoryRefusal.NeedsYou, "needs-you")]
    [InlineData(HistoryRefusal.Awaited, "awaited")]
    [InlineData(HistoryRefusal.Unpushed, "unpushed")]
    [InlineData(HistoryRefusal.NotOurs, "not-ours")]
    public void A_refusal_is_spelled_as_its_word(HistoryRefusal refusal, string word) =>
        Assert.Equal(word, HistoryKept.Spell(refusal));

    [Theory]
    [InlineData("quest", HistoryUnitKind.Quest)]
    [InlineData("ask", HistoryUnitKind.Ask)]
    [InlineData("failed", HistoryUnitKind.Failed)]
    public void A_unit_kind_is_read_back_from_its_word(string word, HistoryUnitKind kind)
    {
        Assert.Equal(kind, HistoryUnitRef.Parse(word));
        Assert.Equal(word, HistoryUnitRef.Spell(kind));
        Assert.Null(HistoryUnitRef.Parse("workspace"));
    }
}
