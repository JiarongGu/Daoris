using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The judgement half of the session system, in ONE place for the same reason `QuestExchange` is:
/// the driver and the HTTP host both move sessions, and two copies of "what may move where" would
/// drift into a session that is finished through one door and still active through the other.
///
/// The deeper rule under test: the ledger never writes QUEST state. The spawned session claims its
/// own quest through its own connector (D46), so driven and outside work stay indistinguishable at
/// the quest layer — the ledger only guards the records the driver acts on.
/// </summary>
public sealed class SessionLedgerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-19T10:00:00Z");

    /// <summary>The registered root — `Owner`'s main tree, and what an unstated open resolves to.</summary>
    private const string MainTree = "/trees/owner";

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        // A registry too, because a chat names its repository directly rather than inheriting it from
        // a quest (D49 §3) — and a repository nobody registered has no working tree to talk in.
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        // With a ROOT, because since D51 the lock keys on the tree a session runs in and an open that
        // names none resolves the registration's — so the fixture has to have one for that path to be
        // exercised at all.
        await service.RegisterAsync(
            new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: MainTree), Now);

        _ledger = new SessionLedger(_quests, _sessions, service);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Quest> Publish(string to = "Owner", string title = "Do the thing") =>
        _quests.PublishAsync("Asker", to, title, "Here is why.", Now);

    [Fact]
    public async Task Opening_a_session_for_an_open_quest_queues_it()
    {
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
        Assert.Equal(SessionState.Queued, outcome.Session!.State);
        Assert.Equal("Owner", outcome.Session.Repository);
        Assert.Contains(outcome.Session.Id, outcome.Message);
    }

    [Fact]
    public async Task An_unknown_quest_is_refused()
    {
        var outcome = await _ledger.OpenAsync("zzzzzz", "stub", Now);

        Assert.Equal(SessionOpenRefusal.QuestNotFound, outcome.Refusal);
        Assert.Null(outcome.Session);
    }

    /// <summary>A printed id is pasted back with its `#`, so the ledger must accept one.</summary>
    [Fact]
    public async Task A_hash_prefixed_quest_id_works()
    {
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync($"#{quest.Id}", "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    /// <summary>
    /// `Taken` is the mutex between the driver and outside work: a quest an interactive session has
    /// already claimed is not the driver's to start, and the refusal must say who has it.
    /// </summary>
    [Fact]
    public async Task A_quest_already_taken_is_refused()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.QuestNotOpen, outcome.Refusal);
        Assert.Contains("Taken", outcome.Message);
    }

    /// <summary>
    /// A resume (D79): a taken quest whose taker waited on a question may take a session again once the
    /// question is answered — and not while it is still asked, when the quest is waiting, not stuck.
    /// </summary>
    [Fact]
    public async Task A_taken_quest_that_waited_resumes_once_its_question_is_answered()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);
        var question = await Publish(to: "Asker", title: "Is the header a free string?");
        await _quests.WaitAsync(quest.Id, question.Id, Now);

        var early = await _ledger.OpenAsync(quest.Id, "stub", Now);
        Assert.Equal(SessionOpenRefusal.QuestNotOpen, early.Refusal);
        Assert.Contains($"#{question.Id}", early.Message);

        await _quests.MoveAsync(question.Id, QuestStatus.Done, "A free string.", Now.AddMinutes(5));
        var resumed = await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(6));

        Assert.Equal(SessionOpenRefusal.None, resumed.Refusal);
    }

    /// <summary>
    /// D80, found on FG5's second run: the session took its quest, worked half an hour, and the timeout
    /// killed it. The quest stayed taken with its work in the tree and nothing would ever start on it.
    /// A taken quest whose last session HERE failed is this machine's to carry on.
    /// </summary>
    [Fact]
    public async Task A_taken_quest_whose_last_session_here_failed_may_be_carried_on()
    {
        var quest = await Publish();
        var first = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _ledger.AdvanceAsync(first.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(first.Id, "working", null, null, null, Now);
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(
            first.Id, "failed", "timed out after 30 minutes and was killed.", null, null, Now.AddMinutes(30))).Refusal);

        var carried = await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(31));

        Assert.Equal(SessionOpenRefusal.None, carried.Refusal);
    }

    /// <summary>
    /// D104, found running the owner's ticket: the orphan sweep and a shutdown both ended the record
    /// `stopped`, and a take ended so sat taken with nothing to move it. A stop that says it was
    /// interrupted — not the person's — is carried on like a failure; the person's own stop never is.
    /// </summary>
    [Theory]
    [InlineData(true, SessionOpenRefusal.None)]
    [InlineData(false, SessionOpenRefusal.QuestNotOpen)]
    public async Task A_taken_quest_whose_last_session_here_was_interrupted_may_be_carried_on_and_a_persons_stop_is_not(
        bool interrupted, SessionOpenRefusal expected)
    {
        var quest = await Publish();
        var first = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _ledger.AdvanceAsync(first.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(first.Id, "working", null, null, null, Now);
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        var stopped = await _ledger.AdvanceAsync(
            first.Id, "stopped", interrupted ? "the driver was stopped while this ran." : "the person stopped it.",
            null, null, Now.AddMinutes(5), interrupted: interrupted);
        Assert.Equal(SessionAdvanceRefusal.None, stopped.Refusal);
        Assert.Equal(interrupted, stopped.Session!.Interrupted);

        var carried = await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(6));

        Assert.Equal(expected, carried.Refusal);
    }

    /// <summary>
    /// Interrupted says whose decision a STOP was, so only a stop carries it: a move anywhere else asking
    /// for it is refused, and the record does not move.
    /// </summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("completed")]
    [InlineData("stood-down")]
    public async Task Only_a_stop_may_be_interrupted(string state)
    {
        var quest = await Publish();
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _ledger.AdvanceAsync(session.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(session.Id, "working", null, null, null, Now);

        var refused = await _ledger.AdvanceAsync(session.Id, state, null, null, null, Now, interrupted: true);

        Assert.Equal(SessionAdvanceRefusal.InvalidMove, refused.Refusal);
        Assert.Contains("stopped", refused.Message);
        Assert.Equal(SessionState.Working, (await _sessions.FindAsync(session.Id))!.State);
    }

    /// <summary>
    /// TOOL4c (D125 §5.2): the driver says a failure was an account's limit, and the record keeps it. The
    /// ledger still opens the carry-on (D80); waiting for the reset is the driver's, at spawn (TOOL4d).
    /// </summary>
    [Fact]
    public async Task A_failure_may_say_it_was_an_accounts_limit_and_the_record_keeps_it()
    {
        var quest = await Publish();
        var first = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _ledger.AdvanceAsync(first.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(first.Id, "working", null, null, null, Now);
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));

        var failed = await _ledger.AdvanceAsync(
            first.Id, "failed", "the ACP agent refused the call: You've hit your weekly limit", null, null,
            Now.AddMinutes(5), limit: true);

        Assert.Equal(SessionAdvanceRefusal.None, failed.Refusal);
        Assert.True(failed.Session!.Limit);
        Assert.True((await _sessions.FindAsync(first.Id))!.Limit);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(6))).Refusal);
    }

    /// <summary>
    /// A limit says why a turn FAILED, so only a failure carries it: a move anywhere else asking for it is
    /// refused, and the record does not move.
    /// </summary>
    [Theory]
    [InlineData("stopped")]
    [InlineData("completed")]
    [InlineData("stood-down")]
    public async Task Only_a_failure_may_say_limit(string state)
    {
        var quest = await Publish();
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _ledger.AdvanceAsync(session.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(session.Id, "working", null, null, null, Now);

        var refused = await _ledger.AdvanceAsync(session.Id, state, null, null, null, Now, limit: true);

        Assert.Equal(SessionAdvanceRefusal.InvalidMove, refused.Refusal);
        Assert.Contains("failed", refused.Message);
        Assert.Equal(SessionState.Working, (await _sessions.FindAsync(session.Id))!.State);
    }

    /// <summary>
    /// 🔴 A stand-down means somebody else has the quest, so the take is not this machine's — and a
    /// taken quest nobody here ran is somebody else's too. Neither is carried on.
    /// </summary>
    [Fact]
    public async Task A_taken_quest_whose_last_session_stood_down_or_that_nobody_here_ran_is_refused()
    {
        var quest = await Publish();
        var first = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddMinutes(1));
        await _ledger.AdvanceAsync(first.Id, "stood-down", "someone else has it.", null, null, Now.AddMinutes(2));
        var untouched = await Publish(title: "Taken by a person");
        await _quests.MoveAsync(untouched.Id, QuestStatus.Taken, null, Now);

        Assert.Equal(SessionOpenRefusal.QuestNotOpen, (await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(3))).Refusal);
        Assert.Equal(SessionOpenRefusal.QuestNotOpen, (await _ledger.OpenAsync(untouched.Id, "stub", Now.AddMinutes(3))).Refusal);
    }

    /// <summary>
    /// STANDDOWN2: only a running session's own quest is marked as its take — a different quest, or a
    /// session that has already ended, changes nothing.
    /// </summary>
    [Fact]
    public async Task A_take_is_marked_only_on_a_running_sessions_own_quest()
    {
        var quest = await Publish();
        var other = await Publish(title: "Another ask");
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;

        Assert.False(await _ledger.MarkTookAsync(session.Id, other.Id));
        Assert.True(await _ledger.MarkTookAsync(session.Id, $"#{quest.Id}"));
        Assert.True((await _sessions.FindAsync(session.Id))!.Took);

        foreach (var state in new[] { "starting", "working", "completed" })
        {
            Assert.Equal(SessionAdvanceRefusal.None, (await _ledger.AdvanceAsync(session.Id, state, null, null, null, Now.AddMinutes(1))).Refusal);
        }

        Assert.False(await _ledger.MarkTookAsync(session.Id, quest.Id));
    }

    /// <summary>
    /// STANDDOWN2: a driven session that parked to ask the person is answered, its record ends with
    /// their words, and its taken quest may then be carried on — the answer is what the next session
    /// is handed. Only a parked record answers; anything else is told why not.
    /// </summary>
    [Fact]
    public async Task Answering_a_parked_session_ends_it_with_the_words_and_lets_its_quest_carry_on()
    {
        var quest = await Publish();
        var parked = (await _ledger.OpenAsync(quest.Id, "stub", Now)).Session!;
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);
        foreach (var state in new[] { "starting", "working" })
        {
            await _ledger.AdvanceAsync(parked.Id, state, null, null, null, Now);
        }

        Assert.Equal(SessionAdvanceRefusal.InvalidMove, (await _ledger.AnswerAsync(parked.Id, "merged", Now)).Refusal);
        await _ledger.AdvanceAsync(parked.Id, "awaiting-person", "needs a merge, a sign-in and a go-ahead.", null, null, Now);
        Assert.Equal(SessionOpenRefusal.QuestNotOpen, (await _ledger.OpenAsync(quest.Id, "stub", Now)).Refusal);

        var answered = await _ledger.AnswerAsync(parked.Id, "Signed in; apply to dev.", Now.AddMinutes(5));

        Assert.Equal(SessionAdvanceRefusal.None, answered.Refusal);
        var record = (await _sessions.FindAsync(parked.Id))!;
        Assert.Equal(SessionState.Completed, record.State);
        Assert.Equal("Signed in; apply to dev.", record.Answer);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(6))).Refusal);
    }

    [Fact]
    public async Task A_closed_quest_is_refused()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, "landed", Now);

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.QuestNotOpen, outcome.Refusal);
    }

    /// <summary>One session per working tree — the unit of exclusion (D51, D46 before it).</summary>
    [Fact]
    public async Task A_busy_tree_refuses_a_second_session_and_names_the_first()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        var opened = await _ledger.OpenAsync(first.Id, "stub", Now);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
        Assert.Contains(opened.Session!.Id, outcome.Message);
    }

    // ——— The tree is the unit of exclusion (D51). What was two claims welded together — "two agents
    // in one working tree corrupt it" and "a repository has one working tree" — is now one claim and
    // a fact about the registry. Nothing here creates a tree; that is SURF3.

    /// <summary>
    /// The refusal names the SESSION holding the tree, never the tree's PATH. A path is machine-local
    /// material (D47 §4), and a message is the one surface with no strip on it: it is composed here
    /// and rendered verbatim wherever it lands, including a browser over a keyed remote.
    /// </summary>
    [Fact]
    public async Task A_busy_refusal_names_the_holder_and_never_a_path()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        var opened = await _ledger.OpenAsync(first.Id, "stub", Now);

        var driven = await _ledger.OpenAsync(second.Id, "stub", Now);
        var chat = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Contains(opened.Session!.Id, driven.Message);
        Assert.DoesNotContain(MainTree, driven.Message);
        Assert.DoesNotContain(MainTree, chat.Message);
    }

    /// <summary>
    /// The record says which tree it ran in, because that is what the lock is keyed on and what a
    /// person reviewing it wants to know. Unstated resolves to the registration's root.
    /// </summary>
    [Fact]
    public async Task A_session_that_names_no_tree_runs_in_the_registered_root()
    {
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(MainTree, outcome.Session!.Tree);
    }

    [Fact]
    public async Task A_chat_that_names_no_tree_runs_in_the_registered_root_too()
    {
        var outcome = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Equal(MainTree, outcome.Session!.Tree);
    }

    /// <summary>
    /// The point of D51, testable before a tree is ever created: two sessions in ONE repository, in
    /// two trees, is not the corruption the lock exists to prevent.
    /// </summary>
    [Fact]
    public async Task Two_trees_in_one_repository_run_at_once()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        await _ledger.OpenAsync(first.Id, "stub", Now, tree: MainTree);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now, tree: "/trees/owner-session-2");

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    /// <summary>
    /// …and the half that keeps it safe: an open that STATES the root and one that leaves it unsaid
    /// are the same tree, so the second is refused. They converge because the ledger resolves both
    /// through the registry rather than comparing what it was handed.
    /// </summary>
    [Fact]
    public async Task A_stated_root_and_an_unstated_one_are_the_same_tree()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        await _ledger.OpenAsync(first.Id, "stub", Now);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now, tree: MainTree);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
    }

    /// <summary>A trailing separator is not a different tree, and a comparison that said so would
    /// hand two agents one working tree on a technicality.</summary>
    [Fact]
    public async Task A_trailing_separator_is_the_same_tree()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        await _ledger.OpenAsync(first.Id, "stub", Now, tree: MainTree);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now, tree: $"{MainTree}/");

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
    }

    /// <summary>
    /// A chat holds a tree exactly as driven work does — and a chat in a SECOND tree is the thing
    /// D51 was decided for: the person and the driver working in one repository at once.
    /// </summary>
    [Fact]
    public async Task A_chat_opens_beside_a_driven_session_when_it_has_a_tree_of_its_own()
    {
        var quest = await Publish();
        await _ledger.OpenAsync(quest.Id, "stub", Now);

        var beside = await _ledger.OpenChatAsync("Owner", "stub", Now, tree: "/trees/owner-chat");
        var sameTree = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, beside.Refusal);
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, sameTree.Refusal);
    }

    /// <summary>
    /// The conservative half: a record that never said which tree it was in holds the WHOLE
    /// repository. Unknown means "possibly yours", and the lock errs toward refusing — the cost of
    /// being wrong the other way is two agents in one tree.
    /// </summary>
    [Fact]
    public async Task A_session_with_no_tree_recorded_holds_every_tree()
    {
        // Straight to the store, because the ledger always resolves one: this is the pre-D51 record,
        // or one mirrored from a machine that knew better than to send a path.
        await _sessions.CreateAsync("abc123", "Owner", "stub", Now);
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now, tree: "/trees/somewhere-else");

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
    }

    [Fact]
    public async Task A_repository_frees_up_when_its_session_finishes()
    {
        var first = await Publish(title: "First ask");
        var second = await Publish(title: "Second ask");
        var opened = await _ledger.OpenAsync(first.Id, "stub", Now);
        await _ledger.AdvanceAsync(opened.Session!.Id, "stood-down", null, null, null, Now);

        var outcome = await _ledger.OpenAsync(second.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    /// <summary>Two repositories working at once is the point of the whole direction.</summary>
    [Fact]
    public async Task Different_repositories_run_concurrently()
    {
        var here = await Publish();
        var there = await Publish(to: "Elsewhere");

        await _ledger.OpenAsync(here.Id, "stub", Now);
        var outcome = await _ledger.OpenAsync(there.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
    }

    [Fact]
    public async Task The_ledger_never_touches_the_quest()
    {
        var quest = await Publish();

        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await _ledger.AdvanceAsync(opened.Session!.Id, "starting", null, null, null, Now);
        await _ledger.AdvanceAsync(opened.Session.Id, "failed", "exit 2", null, null, Now);

        Assert.Equal(QuestStatus.Open, (await _quests.FindAsync(quest.Id))!.Status);
    }

    [Fact]
    public async Task The_ordinary_life_advances_cleanly()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        var id = opened.Session!.Id;

        Assert.Equal(SessionState.Starting, (await Advance(id, "starting")).Session!.State);
        Assert.Equal(SessionState.Working, (await Advance(id, "working")).Session!.State);
        var done = await _ledger.AdvanceAsync(id, "completed", null, "gates: green; 2 commits", null, Now.AddHours(1));
        Assert.Equal(SessionState.Completed, done.Session!.State);
        Assert.Equal("gates: green; 2 commits", done.Session.Evidence);
    }

    /// <summary>The state names travel over HTTP in the design's spelling, not the enum's.</summary>
    [Fact]
    public async Task Kebab_case_state_names_are_understood()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "starting");
        await Advance(opened.Session.Id, "working");

        var outcome = await Advance(opened.Session.Id, "awaiting-person");

        Assert.Equal(SessionAdvanceRefusal.None, outcome.Refusal);
        Assert.Equal(SessionState.AwaitingPerson, outcome.Session!.State);
    }

    [Fact]
    public async Task An_unknown_state_is_refused_and_lists_the_real_ones()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);

        var outcome = await Advance(opened.Session!.Id, "paused");

        Assert.Equal(SessionAdvanceRefusal.UnknownState, outcome.Refusal);
        Assert.Contains("awaiting-person", outcome.Message);
    }

    /// <summary>
    /// Enum.TryParse accepts numeric strings, so a bare parse turns "99" into an undefined state that
    /// escapes the unknown-state branch. The ledger must share Session.TryParse — the strict one — or
    /// the two doors disagree about what a state name is.
    /// </summary>
    [Fact]
    public async Task A_numeric_state_is_unknown_not_undefined()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);

        var outcome = await Advance(opened.Session!.Id, "99");

        Assert.Equal(SessionAdvanceRefusal.UnknownState, outcome.Refusal);
        Assert.Contains("Unknown state '99'", outcome.Message);
    }

    [Fact]
    public async Task An_unknown_session_is_not_found()
    {
        var outcome = await Advance("zzzzzzzz", "working");

        Assert.Equal(SessionAdvanceRefusal.NotFound, outcome.Refusal);
    }

    /// <summary>A finished session is a record, and records do not move.</summary>
    [Fact]
    public async Task A_terminal_session_refuses_to_move()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "stopped");

        var outcome = await Advance(opened.Session.Id, "working");

        Assert.Equal(SessionAdvanceRefusal.Terminal, outcome.Refusal);
    }

    /// <summary>Forward only: a session that skips the states its meaning depends on is lying.</summary>
    [Fact]
    public async Task An_illegal_move_is_refused_and_names_what_is_allowed()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);

        var outcome = await Advance(opened.Session!.Id, "completed");

        Assert.Equal(SessionAdvanceRefusal.InvalidMove, outcome.Refusal);
        Assert.Contains("starting", outcome.Message);
    }

    /// <summary>Nothing advances TO queued — it is where a session begins, never where it returns.</summary>
    [Fact]
    public async Task Nothing_moves_back_to_queued()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "starting");

        var outcome = await Advance(opened.Session.Id, "queued");

        Assert.NotEqual(SessionAdvanceRefusal.None, outcome.Refusal);
    }

    /// <summary>The person clears a parked session; a resume-capable adapter may put it back to work.</summary>
    [Fact]
    public async Task A_parked_session_can_resume_or_close()
    {
        var quest = await Publish();
        var opened = await _ledger.OpenAsync(quest.Id, "stub", Now);
        await Advance(opened.Session!.Id, "starting");
        await Advance(opened.Session.Id, "working");
        await Advance(opened.Session.Id, "awaiting-person");

        var resumed = await Advance(opened.Session.Id, "working");

        Assert.Equal(SessionAdvanceRefusal.None, resumed.Refusal);
    }

    // ——— Chats (D49 §3). The same entity, entered by a person instead of planned from a quest: the
    // same record, the same observed lifecycle, and above all the same lock.

    [Fact]
    public async Task A_chat_opens_with_no_quest_at_all()
    {
        var outcome = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, outcome.Refusal);
        Assert.Equal(SessionKind.Chat, outcome.Session!.Kind);
        Assert.Null(outcome.Session.Quest);
        Assert.Equal(SessionState.Queued, outcome.Session.State);
        Assert.Equal("Owner", outcome.Session.Repository);
    }

    /// <summary>A chat runs IN a repository, so there has to be one on this machine's registry.</summary>
    [Fact]
    public async Task A_chat_in_a_repository_nobody_registered_is_refused()
    {
        var outcome = await _ledger.OpenChatAsync("Stranger", "stub", Now);

        Assert.Equal(SessionOpenRefusal.RepositoryUnknown, outcome.Refusal);
        Assert.Contains("connect", outcome.Message);
        Assert.Null(outcome.Session);
    }

    /// <summary>
    /// The lock is the working tree, not the kind of work: two agents in one tree corrupt each other's
    /// git state regardless of who is typing.
    /// </summary>
    [Fact]
    public async Task A_chat_cannot_open_where_a_driven_session_is_working()
    {
        var quest = await Publish();
        await _ledger.OpenAsync(quest.Id, "stub", Now);

        var outcome = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
        Assert.Contains("one session per working tree", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>…and the same in reverse: a conversation holds the tree exactly as driven work does.</summary>
    [Fact]
    public async Task A_driven_session_cannot_start_where_a_chat_is_open_and_the_refusal_says_it_is_a_chat()
    {
        await _ledger.OpenChatAsync("Owner", "stub", Now);
        var quest = await Publish();

        var outcome = await _ledger.OpenAsync(quest.Id, "stub", Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
        // Naming WHAT holds it is the actionable half: a person stops a chat differently from the way
        // they wait out a driven run.
        Assert.Contains("a chat", outcome.Message);
    }

    /// <summary>A chat that ended releases the tree, like any other finished session.</summary>
    [Fact]
    public async Task A_finished_chat_frees_the_repository()
    {
        var chat = await _ledger.OpenChatAsync("Owner", "stub", Now);
        await Advance(chat.Session!.Id, "starting");
        await Advance(chat.Session.Id, "working");
        await Advance(chat.Session.Id, "completed");

        var again = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Equal(SessionOpenRefusal.None, again.Refusal);
    }

    /// <summary>
    /// A chat moves through the same lifecycle — observed, not self-reported. Nothing about the state
    /// machine is chat-specific, which is what keeps one set of rules rather than two that drift.
    /// </summary>
    [Fact]
    public async Task A_chat_walks_the_same_lifecycle()
    {
        var chat = await _ledger.OpenChatAsync("Owner", "stub", Now);

        Assert.Equal(SessionAdvanceRefusal.None, (await Advance(chat.Session!.Id, "starting")).Refusal);
        Assert.Equal(SessionAdvanceRefusal.None, (await Advance(chat.Session.Id, "working")).Refusal);
        Assert.Equal(SessionAdvanceRefusal.None, (await Advance(chat.Session.Id, "stopped")).Refusal);
    }

    private Task<SessionAdvanceOutcome> Advance(string id, string state) =>
        _ledger.AdvanceAsync(id, state, null, null, null, Now);
}
