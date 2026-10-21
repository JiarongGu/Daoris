using System.Text.Json.Nodes;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1c8 (the fabric events design §E5, <i>its session is told at its next step</i>; D173's note): the person's reopen of a
/// source's submission is told to the session that said its <c>ready</c>, on this machine, once, with Daoris's sentence and the
/// person's words quoted, through the say door; the planner's go-on resumes it. Doors standing in for the service, and the real
/// driver over the stand-in ledger for the go-on.
/// </summary>
public sealed class ReopenTellingTests : IDisposable
{
    private const string Note = "The bridge is wrong.\nUse the v3 one.";

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-reopen-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public ReopenTellingTests() => Directory.CreateDirectory(Tree);

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Tree => DaorisHome.Entry(_home, HomeEntry.Trees, "s-1");

    /// <summary>
    /// The service's doors standing in: this machine's records by id, every read and every word said with its key, the words a
    /// record keeps, one per key as the say door keeps them (COWORK1b), the running sessions' inboxes, the quests as they stand
    /// now, and the words taken off a record whose fact no longer holds.
    /// </summary>
    private sealed class Doors : IReopenDoors
    {
        public Dictionary<string, PriorSession> Records { get; } = new(StringComparer.Ordinal);

        public List<string> Reads { get; } = [];

        public List<(string Session, string Text)> Said { get; } = [];

        public List<string> Keys { get; } = [];

        /// <summary>What each record keeps, by session: one word per key, as the say door answers a retry with the word kept.</summary>
        public Dictionary<string, List<string>> Kept { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, DrivenInbox> Inboxes { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, QuestView?> Quests { get; } = new(StringComparer.Ordinal);

        public List<(string Session, IReadOnlyList<string?> Words)> Dropped { get; } = [];

        public string? Refuse { get; set; }

        public Task<PriorSession?> RecordAsync(string session, CancellationToken ct)
        {
            Reads.Add(session);
            return Task.FromResult(Records.GetValueOrDefault(session));
        }

        public Task<SayAnswer> SayAsync(string session, string text, string key, CancellationToken ct)
        {
            Said.Add((session, text));
            Keys.Add(key);
            if (Refuse is not null) return Task.FromResult(new SayAnswer(false, $"Refused: {Refuse}.") { Refusal = Refuse });

            var kept = Kept.TryGetValue(session, out var words) ? words : Kept[session] = [];
            if (!kept.Contains(key)) kept.Add(key);
            return Task.FromResult(new SayAnswer(true, "Kept."));
        }

        public Task<QuestView?> QuestAsync(string quest, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(quest));

        public DrivenInbox? InboxOf(string session) => Inboxes.GetValueOrDefault(session);

        public Task DropAsync(string session, IReadOnlyList<SaidWordView> words, CancellationToken ct)
        {
            Dropped.Add((session, [.. words.Select(word => word.Id)]));
            return Task.CompletedTask;
        }

        /// <summary>A running record's <c>said</c> (COWORK1c), where a test gives one; none holds Daoris's words in the inbox alone.</summary>
        public StandInSaid? Running { get; set; }

        public ISaidDoor? SaidOf(string session) => Running;
    }

    /// <summary>A source as the service lists it, read through the driver's own client: reopened unless <paramref name="sequence"/> is none.</summary>
    private static QuestView Source(long? sequence = 44, string? note = Note, string? session = "s1", string status = "Taken", string id = "q1")
    {
        var quest = new JsonObject
        {
            ["id"] = id, ["from"] = "game", ["to"] = "engine", ["title"] = "Ship it", ["body"] = "", ["status"] = status,
            ["closure"] = "gph-0123",
        };
        if (sequence is { } named)
        {
            var reopen = new JsonObject
            {
                ["machine"] = "desk", ["sequence"] = named, ["of"] = new JsonObject { ["machine"] = "desk", ["sequence"] = 41 },
                ["at"] = "2026-10-18T09:00:00+00:00",
            };
            if (note is not null) reopen["note"] = note;
            if (session is not null) reopen["session"] = session;
            quest["reopened"] = reopen;
        }
        else
        {
            quest["submission"] = new JsonObject { ["machine"] = "desk", ["sequence"] = 45, ["session"] = session };
        }

        return ServiceClient.ReadQuests(new JsonArray(quest).ToJsonString())[0];
    }

    private static Snapshot Look(params QuestView[] quests) => new(quests, [], []);

    private PriorSession Record(string state, string session = "s1", string? tree = null) => new(session, tree ?? Tree, state, Repository: "engine");

    private Task<IReadOnlyList<string>> LookAsync(ReopenTelling telling, Doors doors, Snapshot snapshot) =>
        telling.LookAsync(doors, snapshot, _closing.Token);

    private ReopenTelling Telling() => new(new ReopenMarks(_home));

    // ——— Who, when and once.

    [Theory]
    [InlineData("awaiting-person")]
    [InlineData("completed")]
    [InlineData("failed")]
    public async Task A_parked_or_ended_session_that_said_the_reopened_ready_is_told_once_and_not_again_after_a_restart(string state)
    {
        var doors = new Doors();
        doors.Records["s1"] = Record(state);
        var source = Source();

        var told = await LookAsync(Telling(), doors, Look(source));

        var said = Assert.Single(doors.Said);
        Assert.Equal(("s1", ReopenTelling.Words("q1", source.Reopened!)), said);
        // COWORK1b: said as Daoris's under its fact's key, the quest and the reopen's machine and sequence.
        Assert.Equal("reopen/q1/desk/44", Assert.Single(doors.Keys));
        Assert.Contains(told, line => line.StartsWith("reopen  ", StringComparison.Ordinal) && line.Contains("session s1", StringComparison.Ordinal));
        Assert.True(File.Exists(new ReopenMarks(_home).PathOf("s1")));

        var telling = Telling();
        await LookAsync(telling, doors, Look(source));
        await LookAsync(telling, doors, Look(source));
        Assert.Single(doors.Said);
    }

    [Fact]
    public async Task The_same_telling_says_it_once_though_the_mark_is_not_written()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("completed");
        // A home whose sessions folder is a file: no mark can be written under it.
        var blocked = Path.Combine(_home, "blocked");
        Directory.CreateDirectory(blocked);
        File.WriteAllText(DaorisHome.Entry(blocked, HomeEntry.Sessions), "not a folder");
        var telling = new ReopenTelling(new ReopenMarks(blocked));

        await LookAsync(telling, doors, Look(Source()));
        await LookAsync(telling, doors, Look(Source()));

        Assert.Single(doors.Said);
        Assert.False(new ReopenMarks(blocked).Told("s1", "q1", Source().Reopened!));
    }

    /// <summary>
    /// 🔴 COWORK1b (the message-urgency design §5's Daoris row, trap 17): a running session this loop runs is told at its next step,
    /// as Daoris's urgent words through its inbox, never the say door, which refuses a running record. Once: the mark is written as
    /// the words go, and the look tells it nothing more.
    /// </summary>
    [Fact]
    public async Task A_running_session_whose_door_takes_words_at_its_next_step_is_told_there_once_as_daoris_words()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("working");
        var sent = new List<ChatMessage>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message =>
        {
            sent.Add(message);
            return Task.FromResult("end_turn");
        });
        inbox.Flow();
        doors.Inboxes["s1"] = inbox;
        var telling = Telling();

        var told = await LookAsync(telling, doors, Look(Source()));
        Assert.Empty(await LookAsync(telling, doors, Look(Source())));

        var word = Assert.Single(sent);
        Assert.Equal(ReopenTelling.Words("q1", Source().Reopened!), word.Text);
        Assert.Equal((MessageLevel.Urgent, "reopen/q1/desk/44"), (word.Level, word.Daoris?.Key));
        Assert.Contains("at its next step, as Daoris's words", Assert.Single(told), StringComparison.Ordinal);
        Assert.Empty(doors.Said);
        Assert.True(new ReopenMarks(_home).Told("s1", "q1", Source().Reopened!));
        Assert.Empty(await LookAsync(Telling(), doors, Look(Source())));
    }

    /// <summary>
    /// COWORK1b (the design §3.1): on a door that takes words only at its turn's end, Daoris's words are held for it, marked urgent,
    /// and judged again where they are delivered: while the reopen stands they go, and the mark is written then; once a later
    /// submission answered it they are dropped, never said late, and nothing is marked.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_running_session_whose_door_takes_words_at_its_turns_end_holds_them_and_judges_them_again_there(bool stands)
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("working");
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        doors.Inboxes["s1"] = inbox;
        var telling = Telling();

        var told = await LookAsync(telling, doors, Look(Source()));
        Assert.Contains("when its turn ends, as Daoris's words", Assert.Single(told), StringComparison.Ordinal);
        Assert.False(new ReopenMarks(_home).Told("s1", "q1", Source().Reopened!));

        // Between the look and the turn's end the session said `ready` again, or did not.
        doors.Quests["q1"] = stands ? Source() : Source(sequence: null);
        var held = inbox.NextOrClose()!.Held!;
        Assert.Equal(stands, await DaorisWords.GoesAsync(held, _closing.Token));

        Assert.Equal(stands, new ReopenMarks(_home).Told("s1", "q1", Source().Reopened!));
        Assert.Empty(await LookAsync(telling, doors, Look(Source())));
        Assert.Empty(doors.Said);
    }

    /// <summary>
    /// COWORK1c (the message-urgency design §6): Daoris's words to a running session are kept on its record first, as Daoris's, at
    /// urgent and under the fact's key, then held by its inbox under the record's id: a shell that restarts before the turn's end finds
    /// them waiting there for the session's go-on. Once the run takes them they leave the record by that id, and the mark is written.
    /// </summary>
    [Fact]
    public async Task A_running_sessions_reopen_is_kept_on_its_record_first_and_leaves_it_once_taken()
    {
        var doors = new Doors { Running = new StandInSaid() };
        doors.Records["s1"] = Record("working");
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        doors.Inboxes["s1"] = inbox;
        doors.Quests["q1"] = Source();
        var telling = Telling();

        var told = await LookAsync(telling, doors, Look(Source()));

        Assert.Contains("when its turn ends, as Daoris's words", Assert.Single(told), StringComparison.Ordinal);
        var kept = Assert.Single(doors.Running.Said);
        Assert.Equal(
            ("w1", ReopenTelling.Words("q1", Source().Reopened!), DaorisWords.From, "reopen/q1/desk/44", MessageLevel.Urgent),
            (kept.Id, kept.Text, kept.From, kept.Key, kept.Level));
        Assert.Empty(doors.Said);
        Assert.Empty(await LookAsync(telling, doors, Look(Source())));

        var held = inbox.NextOrClose()!.Held!;
        Assert.Equal("w1", held.Id);
        Assert.True(await DaorisWords.GoesAsync(held, _closing.Token));
        // Its door writes it, as the protocol door does: attempted first, off the record once written.
        await held.Attempting!();
        held.Went!();
        await inbox.TakingOff;

        Assert.Equal(["w1"], doors.Running.TakenOff);
        Assert.Empty(doors.Running.Said);
        Assert.True(new ReopenMarks(_home).Told("s1", "q1", Source().Reopened!));
    }

    /// <summary>
    /// A running session no inbox here reaches, a conversation or one another loop runs, is told at the first look that finds it
    /// parked or ended, through the say door, as before (D188's COWORK1b note).
    /// </summary>
    [Fact]
    public async Task A_running_session_no_inbox_here_reaches_is_told_once_a_look_finds_it_parked_or_ended()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("working");
        var telling = Telling();

        Assert.Empty(await LookAsync(telling, doors, Look(Source())));
        Assert.Empty(doors.Said);

        doors.Records["s1"] = Record("completed");
        await LookAsync(telling, doors, Look(Source()));
        Assert.Equal("s1", Assert.Single(doors.Said).Session);
    }

    /// <summary>
    /// 🔴 COWORK1b (the design §6, trap 19): a failure between the say and the mark tells one word. The driver started again says the
    /// same words under the same key, and the say door answers it with the word kept.
    /// </summary>
    [Fact]
    public async Task A_failure_between_the_say_and_the_mark_tells_one_word()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("completed");
        // A home whose sessions folder is a file: the mark after the say is never written.
        var blocked = Path.Combine(_home, "blocked");
        Directory.CreateDirectory(blocked);
        File.WriteAllText(DaorisHome.Entry(blocked, HomeEntry.Sessions), "not a folder");

        await LookAsync(new ReopenTelling(new ReopenMarks(blocked)), doors, Look(Source()));
        await LookAsync(new ReopenTelling(new ReopenMarks(blocked)), doors, Look(Source()));

        Assert.Equal(2, doors.Said.Count);
        Assert.Equal(["reopen/q1/desk/44", "reopen/q1/desk/44"], doors.Keys);
        Assert.Equal(["reopen/q1/desk/44"], doors.Kept["s1"]);
    }

    /// <summary>
    /// COWORK1b (the design §4.3): Daoris's words waiting on a record the planner would go on with are judged at each look by the key
    /// each carries, before the go-on that would deliver them: one whose reopen a later submission answered, or whose go-on another
    /// moved past, is taken off and said once; one whose fact holds, and the person's words, wait as they are.
    /// </summary>
    [Fact]
    public async Task Daoris_words_waiting_on_a_record_whose_fact_no_longer_holds_are_taken_off_and_said_once()
    {
        var doors = new Doors();
        var stale = new SaidWordView("w1", "Daoris: reopened.", Moved, [], Reopens: true) { From = DaorisWords.From, Key = "reopen/q1/desk/44" };
        var standing = new SaidWordView("w2", "Daoris: reopened.", Moved, [], Reopens: true) { From = DaorisWords.From, Key = "reopen/q3/desk/50" };
        var persons = new SaidWordView("w3", "Also the changelog.", Moved, [], Reopens: true);
        var last = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase)
        {
            ["q1"] = Record("completed") with { Said = [stale, persons] },
            ["q3"] = Record("completed", "s3") with { Said = [standing] },
        };
        var look = Look(Source(sequence: null), Source(session: "s3", id: "q3", sequence: 50)) with { LastRun = last };
        var telling = Telling();

        var lines = await LookAsync(telling, doors, look);
        await LookAsync(telling, doors, look);

        var dropped = Assert.Single(doors.Dropped);
        Assert.Equal("s1", dropped.Session);
        Assert.Equal(["w1"], dropped.Words);
        Assert.Contains(lines, line => line.StartsWith("reopen  ", StringComparison.Ordinal) && line.Contains("no longer holds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_running_refusal_is_tried_again_and_any_other_refusal_is_said_once_and_never_tried_again()
    {
        var doors = new Doors { Refuse = "running" };
        doors.Records["s1"] = Record("completed");
        var telling = Telling();

        await LookAsync(telling, doors, Look(Source()));
        doors.Refuse = "stood-down";
        var refused = await LookAsync(telling, doors, Look(Source()));
        var again = await LookAsync(telling, doors, Look(Source()));

        Assert.Equal(2, doors.Said.Count);
        Assert.Contains(refused, line => line.Contains("Refused: stood-down.", StringComparison.Ordinal));
        Assert.Empty(again);
        Assert.False(File.Exists(new ReopenMarks(_home).PathOf("s1")));
    }

    [Fact]
    public async Task A_record_gone_or_a_tree_tidied_is_told_nothing_and_says_why_once()
    {
        var doors = new Doors();
        var telling = Telling();

        var gone = await LookAsync(telling, doors, Look(Source()));
        Assert.Contains("keeps no record", Assert.Single(gone), StringComparison.Ordinal);
        Assert.Empty(await LookAsync(telling, doors, Look(Source())));

        doors.Records["s2"] = Record("completed", "s2", Path.Combine(_home, "tidied"));
        var tidied = await LookAsync(telling, doors, Look(Source(session: "s2", id: "q2")));
        Assert.Contains("tree is gone", Assert.Single(tidied), StringComparison.Ordinal);
        Assert.Empty(await LookAsync(telling, doors, Look(Source(session: "s2", id: "q2"))));

        Assert.Empty(doors.Said);
    }

    /// <summary>
    /// NOTIFY2h2 (D196's NOTIFY2h note): a reopen that reaches no record or no tree tells nobody and asks nobody, so each is news, one
    /// coded notice in <i>Done and failed</i> however many looks meet it; a session the person stopped is their own, and makes none.
    /// </summary>
    [Fact]
    public async Task A_reopen_that_reaches_no_record_or_no_tree_is_news_and_the_persons_stop_is_not()
    {
        var doors = new Doors();
        var telling = Telling();
        var said = new LookSaid(_home, () => null);
        doors.Records["s2"] = Record("completed", "s2", Path.Combine(_home, "tidied"));
        doors.Records["s3"] = Record("stopped", "s3");

        foreach (var _ in new[] { 1, 2 })
        {
            await telling.LookAsync(doors, Look(Source()), _closing.Token, said);
            await telling.LookAsync(doors, Look(Source(session: "s2", id: "q2")), _closing.Token, said);
            await telling.LookAsync(doors, Look(Source(session: "s3", id: "q3")), _closing.Token, said);
        }

        var notices = Notices.Read(_home, DateTimeOffset.UtcNow)!;
        Assert.Equal(
            [("look-reopen-tree-gone-q2", LookSaid.ReopenTreeGone, "s2"), ("look-reopen-unrecorded-q1", LookSaid.ReopenUnrecorded, null)],
            notices.Select(notice => (notice.Id, notice.Code, notice.Session)).Order());
    }

    [Fact]
    public async Task The_session_the_reopen_names_is_told_and_never_the_quests_last_record_here()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("completed");
        doors.Records["s2"] = Record("completed", "s2");
        var last = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = doors.Records["s2"] };

        await LookAsync(Telling(), doors, Look(Source()) with { LastRun = last });
        await LookAsync(Telling(), doors, Look(Source(session: null, id: "q2")) with
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q2"] = doors.Records["s2"] },
        });

        Assert.Equal("s1", Assert.Single(doors.Said).Session);
    }

    [Fact]
    public async Task Another_machines_session_and_a_reopen_naming_none_are_told_nothing_and_read_nothing()
    {
        var doors = new Doors();
        doors.Records["laptop/s1"] = Record("completed", "laptop/s1");

        var lines = await LookAsync(Telling(), doors, Look(Source(session: "laptop/s1"), Source(session: null, id: "q2")));

        Assert.Empty(lines);
        Assert.Empty(doors.Reads);
        Assert.Empty(doors.Said);
    }

    [Fact]
    public async Task An_answered_reopen_and_an_unbound_look_read_nothing_and_write_nothing()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("completed");

        // Answered by a later ready, a quest done, and a quest never reopened: no reopen to tell.
        var lines = await LookAsync(Telling(), doors, Look(Source(sequence: null), Source(status: "Done", id: "q2"), Source(sequence: null, session: null, id: "q3")));

        Assert.Empty(lines);
        Assert.Empty(doors.Reads);
        Assert.Empty(doors.Said);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Sessions)));
    }

    [Fact]
    public async Task A_later_reopen_of_the_same_quest_is_told_again_and_the_mark_before_is_harmless()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("completed");

        await LookAsync(Telling(), doors, Look(Source()));
        await LookAsync(Telling(), doors, Look(Source(sequence: 47, note: "Not yet.")));
        await LookAsync(Telling(), doors, Look(Source(sequence: 47, note: "Not yet.")));

        Assert.Equal(2, doors.Said.Count);
        Assert.Contains("> Not yet.", doors.Said[1].Text, StringComparison.Ordinal);
    }

    // ——— FABRIC-CLOSE1c9: the named session when a later one is the quest's last here.

    /// <summary>
    /// A session the person stopped after its ready, when a later session ran: Daoris's words would release the person's stop, so it
    /// is told nothing, and the look says so once. Where it is the quest's last record it is told nothing too (FABRIC-CLOSE1c12), and
    /// a stop the driver's shutdown made is no person's.
    /// </summary>
    [Fact]
    public async Task A_session_the_person_stopped_that_is_not_the_quests_last_is_told_nothing_and_says_so_once()
    {
        var doors = new Doors();
        doors.Records["s1"] = Record("stopped");
        var later = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Record("completed", "s2") };
        var telling = Telling();

        var lines = await LookAsync(telling, doors, Look(Source()) with { LastRun = later });
        Assert.Empty(await LookAsync(telling, doors, Look(Source()) with { LastRun = later }));

        var line = Assert.Single(lines);
        Assert.StartsWith("reopen  ", line, StringComparison.Ordinal);
        Assert.Contains("session s1, which the person stopped", line, StringComparison.Ordinal);
        Assert.Contains("nothing goes on with it until they say so", line, StringComparison.Ordinal);
        Assert.Empty(doors.Said);
        Assert.False(File.Exists(new ReopenMarks(_home).PathOf("s1")));

        // FABRIC-CLOSE1c12: the same where it is the quest's last record, since any words release the person's stop.
        var last = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = doors.Records["s1"] };
        var alone = await LookAsync(Telling(), doors, Look(Source()) with { LastRun = last });
        var aloneLine = Assert.Single(alone);
        Assert.StartsWith("reopen  ", aloneLine, StringComparison.Ordinal);
        Assert.Contains("session s1, which the person stopped", aloneLine, StringComparison.Ordinal);
        Assert.DoesNotContain("a later session ran", aloneLine, StringComparison.Ordinal);
        doors.Records["s3"] = Record("stopped", "s3") with { Interrupted = true };
        await LookAsync(Telling(), doors, Look(Source(session: "s3", id: "q3")) with { LastRun = later });
        Assert.Equal(["s3"], doors.Said.Select(said => said.Session));
    }

    /// <summary>
    /// The look's snapshot names the record each taken quest's reopen names, from the records it already read: this machine's, and
    /// not the quest's last here. A teammate's, the last, a record not kept and a quest not taken name none.
    /// </summary>
    [Fact]
    public void The_snapshot_names_the_reopens_session_where_it_is_this_machines_and_not_the_quests_last()
    {
        const string records = """
            [{ "id": "s1", "quest": "q1", "state": "completed", "tree": "t1", "created": "2026-10-18T08:00:00Z", "said": [] },
             { "id": "s2", "quest": "q1", "state": "completed", "tree": "t2", "created": "2026-10-18T08:30:00Z", "said": [] },
             { "id": "s4", "quest": "q4", "state": "completed", "tree": "t4", "created": "2026-10-18T08:00:00Z", "said": [] },
             { "id": "laptop/s5", "quest": "q5", "state": "completed", "created": "2026-10-18T08:00:00Z" }]
            """;
        var quests = new[]
        {
            Source(), Source(session: "s4", id: "q4"), Source(session: "laptop/s5", id: "q5"), Source(session: "s9", id: "q9"),
            Source(session: "s1", status: "Open", id: "q1b"), Source(sequence: null, id: "q1c"),
        };

        var named = ServiceClient.ReadNamedByReopen(records, quests, ServiceClient.ReadLastRun(records));

        Assert.Equal(["q1"], named.Keys);
        Assert.Equal(("s1", "t1"), (named["q1"].Session, named["q1"].Tree));
    }

    // ——— The words.

    [Fact]
    public void The_words_name_the_reopened_submission_ask_for_ready_again_and_quote_the_persons_note()
    {
        var words = ReopenTelling.Words("q1", Source().Reopened!);

        Assert.StartsWith("Daoris: the person reopened submission `desk` 41 of quest `#q1`", words, StringComparison.Ordinal);
        Assert.Contains("say `ready` on `#q1` again", words, StringComparison.Ordinal);
        Assert.EndsWith("\n\n> The bridge is wrong.\n> Use the v3 one.", words, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_note_the_words_are_the_sentence_alone()
    {
        var words = ReopenTelling.Words("q1", Source(note: null).Reopened!);

        Assert.Equal(
            "Daoris: the person reopened submission `desk` 41 of quest `#q1`, which your `ready` made, so the quest is not ready "
            + "any more. Address what they reopened it for, then say `ready` on `#q1` again.",
            words);
    }

    // ——— FABRIC-CLOSE1c13: a step the person went on with after its session had ended normally is told so in that session, once.

    private static readonly QuestSubmissionRef Grew = new("desk", 43);

    private static readonly DateTimeOffset Moved = new(2026, 10, 18, 9, 10, 0, TimeSpan.Zero);

    /// <summary>q2, which grew from q1, after the person's go-on: rebound to <see cref="Grew"/>, its own result cleared, moved then.</summary>
    private static QuestView GoneOnStep() =>
        new("q2", "game", "engine", "Next", "", "Taken") { Parent = "q1", Closure = "gph-0123", GrewFrom = Grew, Updated = Moved };

    /// <summary>q2's last record here, as the look read it: ended normally ten minutes before the go-on, unless said otherwise.</summary>
    private Snapshot StepLook(PriorSession last) =>
        Look(GoneOnStep()) with { LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q2"] = last } };

    private PriorSession EndedBefore(string state, string? tree = null) =>
        Record(state, "s2", tree) with { Updated = Moved.AddMinutes(-10), Said = [] };

    [Theory]
    [InlineData("awaiting-person")]
    [InlineData("completed")]
    public async Task A_steps_session_that_ended_normally_is_told_of_the_go_on_once(string state)
    {
        var doors = new Doors();
        var telling = Telling();

        var told = await LookAsync(telling, doors, StepLook(EndedBefore(state)));
        Assert.Empty(await LookAsync(telling, doors, StepLook(EndedBefore(state))));

        Assert.Equal(("s2", ReopenTelling.GoneOnWords("q2", "q1", Grew)), Assert.Single(doors.Said));
        // COWORK1b: under its fact's key, the step and the submission it grew from.
        Assert.Equal("gone-on/q2/desk/43", Assert.Single(doors.Keys));
        Assert.Contains(told, line => line.StartsWith("reopen  session s2 ", StringComparison.Ordinal) && line.Contains("#q2", StringComparison.Ordinal));
        Assert.Empty(doors.Reads);
    }

    /// <summary>A session that ended after the go-on, one running, and one with words waiting are told nothing: each reads as before.</summary>
    [Fact]
    public async Task A_session_ended_after_the_go_on_running_or_with_words_waiting_is_told_nothing()
    {
        var doors = new Doors();
        var said = new SaidWordView("w1", "Go on.", Moved, [], Reopens: true);

        foreach (var last in new[] { EndedBefore("completed") with { Updated = Moved.AddMinutes(5) }, EndedBefore("working"), EndedBefore("completed") with { Said = [said] } })
        {
            Assert.Empty(await LookAsync(Telling(), doors, StepLook(last)));
        }

        Assert.Empty(doors.Said);
    }

    [Fact]
    public async Task A_steps_tree_gone_is_told_nothing_and_says_why_once()
    {
        var doors = new Doors();
        var telling = Telling();
        var tidied = StepLook(EndedBefore("completed", Path.Combine(_home, "tidied")));

        Assert.Contains("tree is gone", Assert.Single(await LookAsync(telling, doors, tidied)), StringComparison.Ordinal);
        Assert.Empty(await LookAsync(telling, doors, tidied));
        Assert.Empty(doors.Said);
    }

    [Fact]
    public void The_go_on_words_name_the_source_and_its_new_submission_and_ask_for_ready_again()
    {
        Assert.Equal(
            "Daoris: the person reopened the work of `#q1` that quest `#q2` grew from, and went on with `#q2` on its new submission `desk` "
            + "43, so any result submitted on `#q2` was cleared. Bring your work up to date with that submission (what Daoris did with "
            + "your branch follows, where it did anything), then say `ready` on `#q2` again.",
            ReopenTelling.GoneOnWords("q2", "q1", Grew));
    }

    /// <summary>
    /// Over the real driver: q2 submitted, its session parked after its ready or completed, then q1 reopened and the person went on with
    /// q2. The first look tells that session, the next goes on with the words in it, and a driver started again tells it nothing more.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_step_gone_on_with_after_its_session_ended_goes_on_in_that_session_once(bool parked)
    {
        using var ledger = new StandInLedger();
        ledger.Register("engine", Path.Combine(_home, "engine"));
        ledger.Publish("q1", "engine");
        ledger.Move("q1", "Taken");
        ledger.Publish("q2", "engine", parent: "q1");
        if (parked) ledger.Park("s2", "q2", Tree);
        else ledger.Driven("s2", "q2", Tree, state: "completed");
        ledger.Submitted("q2", "desk", 42, "s2");
        ledger.Reopened("q2", "desk", 41);
        ledger.Rebound("q2", "desk", 43, moved: true);
        var (driver, began) = Driver(ledger);

        var told = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var said = Assert.Single(ledger.Session("s2")["said"]!.AsArray());
        Assert.Equal(ReopenTelling.GoneOnWords("q2", "q1", Grew), said!["text"]!.GetValue<string>());
        // COWORK1b: kept as Daoris's, under the go-on's key, which is what the start reads to bring the branch up to date.
        Assert.Equal(("daoris", "gone-on/q2/desk/43"), (said["from"]?.GetValue<string>(), said["key"]?.GetValue<string>()));
        Assert.Contains(told.Events, line => line.StartsWith("reopen  session s2 ", StringComparison.Ordinal));

        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var goes = Assert.Single(next.Considerations);
        Assert.Equal((StartVerdict.Start, true, "s2"), (goes.Verdict, goes.GoesOn, goes.Resumes?.Session));
        await Poll.Until(() => began.Count == 1, within: Bound);
        Assert.Equal(ReopenTelling.GoneOnWords("q2", "q1", Grew), Assert.Single(began[0]));

        var (again, _) = Driver(ledger);
        var restarted = await again.TickAsync(_closing.Token).WaitAsync(Bound);
        Assert.DoesNotContain(restarted.Events, line => line.StartsWith("reopen  ", StringComparison.Ordinal));
        Assert.Empty(ledger.Session("s2")["said"]!.AsArray());
    }

    // ——— Over the real driver: told at one look, gone on with at the next, and not told again by a driver started again.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_told_session_goes_on_with_the_words_at_the_next_look_and_a_driver_started_again_says_nothing(bool parked)
    {
        using var ledger = new StandInLedger();
        ledger.Register("engine", Path.Combine(_home, "engine"));
        ledger.Publish("q1", "engine");
        if (parked) ledger.Park("s1", "q1", Tree);
        else ledger.Driven("s1", "q1", Tree, state: "completed");
        ledger.SourceReopened("q1", 44, 41, Note, "s1");
        var (driver, began) = Driver(ledger);

        var told = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var said = Assert.Single(ledger.Session("s1")["said"]!.AsArray());
        Assert.StartsWith("Daoris: the person reopened submission `desk` 41 of quest `#q1`", said!["text"]!.GetValue<string>());
        Assert.Equal(("daoris", "reopen/q1/desk/44"), (said["from"]?.GetValue<string>(), said["key"]?.GetValue<string>()));
        Assert.Contains(told.Events, line => line.StartsWith("reopen  ", StringComparison.Ordinal));

        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var goes = Assert.Single(next.Considerations);
        Assert.Equal((StartVerdict.Start, true, "s1"), (goes.Verdict, goes.GoesOn, goes.Resumes?.Session));
        await Poll.Until(() => began.Count == 1, within: Bound);
        Assert.StartsWith("Daoris: the person reopened", Assert.Single(began[0]), StringComparison.Ordinal);
        Assert.DoesNotContain(next.Events, line => line.StartsWith("reopen  ", StringComparison.Ordinal));

        var (again, _) = Driver(ledger);
        var restarted = await again.TickAsync(_closing.Token).WaitAsync(Bound);
        Assert.DoesNotContain(restarted.Events, line => line.StartsWith("reopen  ", StringComparison.Ordinal));
        Assert.Empty(ledger.Session("s1")["said"]!.AsArray());
    }

    /// <summary>
    /// FABRIC-CLOSE1c9 over the real driver: the session that said the reopened ready is not the quest's last record here, since a
    /// later one ran. It is told, and the next look goes on with the words in it; one the person stopped is told nothing, the look
    /// says so once, and nothing goes on.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_named_session_that_is_not_the_last_goes_on_with_the_words_unless_the_person_stopped_it(bool stopped)
    {
        using var ledger = new StandInLedger();
        ledger.Register("engine", Path.Combine(_home, "engine"));
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", Tree, state: stopped ? "stopped" : "completed");
        ledger.SourceReopened("q1", 44, 41, Note, "s1");
        ledger.Driven("s2", "q1", DaorisHome.Entry(_home, HomeEntry.Trees, "s-2"), state: "completed");
        var (driver, began) = Driver(ledger);

        var told = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var line = Assert.Single(told.Events, line => line.StartsWith("reopen  ", StringComparison.Ordinal));
        Assert.Equal(stopped ? 0 : 1, ledger.Session("s1")["said"]!.AsArray().Count);
        Assert.Equal(stopped, line.Contains("which the person stopped", StringComparison.Ordinal));

        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        Assert.DoesNotContain(next.Events, each => each.StartsWith("reopen  ", StringComparison.Ordinal));
        if (stopped)
        {
            Assert.DoesNotContain(next.Considerations, each => each.Resumes is not null || each.Verdict == StartVerdict.Start);
            Assert.Empty(began);
            return;
        }

        var goes = Assert.Single(next.Considerations);
        Assert.Equal((StartVerdict.Start, true, "s1"), (goes.Verdict, goes.GoesOn, goes.Resumes?.Session));
        await Poll.Until(() => began.Count == 1, within: Bound);
        Assert.StartsWith("Daoris: the person reopened", Assert.Single(began[0]), StringComparison.Ordinal);
        Assert.Empty(ledger.Session("s1")["said"]!.AsArray());
    }

    /// <summary>
    /// The look reads the marks of the named session as it reads the last one's (MSG1f2): words it was found unable to go on with
    /// wait there, and are not tried again at each look.
    /// </summary>
    [Fact]
    public async Task Words_the_named_session_could_not_go_on_with_wait_over_the_real_driver()
    {
        using var ledger = new StandInLedger();
        ledger.Register("engine", Path.Combine(_home, "engine"));
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", Tree, state: "completed");
        ledger.SourceReopened("q1", 44, 41, Note, "s1");
        ledger.Driven("s2", "q1", DaorisHome.Entry(_home, HomeEntry.Trees, "s-2"), state: "completed");
        var (driver, began) = Driver(ledger);

        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var word = Assert.Single(ledger.Session("s1")["said"]!.AsArray())!["id"]!.GetValue<string>();
        new GoOnMarks(_home).Mark("s1", [word], ContinueWhy.Tree, DateTimeOffset.UtcNow);

        var next = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        Assert.DoesNotContain(next.Considerations, each => each.Resumes is not null);
        Assert.Empty(began);
        Assert.Single(ledger.Session("s1")["said"]!.AsArray());
    }

    /// <summary>
    /// The real driver over the ledger, its runs standing in for <c>Driver.ContinueAsync</c>: each one goes on with the words
    /// waiting on its record, takes them off it, and ends it completed, the texts it went on with kept in order.
    /// </summary>
    private (Daoris.Driver.Driver Driver, List<IReadOnlyList<string>> Began) Driver(StandInLedger ledger)
    {
        var service = ledger.Client();
        var began = new List<IReadOnlyList<string>>();
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] };
        var driver = new Daoris.Driver.Driver(
            service, config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = async (start, opened, ct) =>
            {
                var record = start.Resumes ?? throw new InvalidOperationException($"the stand-in only goes on; #{start.Quest.Id} was planned as a start");
                opened();
                await service.AdvanceAsync(record.Session, "working", Continuations.WorkingNoted, ct: ct);
                await service.TakenAsync(record.Session, [.. record.Waiting.Select(word => word.Id).OfType<string>()], ct: CancellationToken.None);
                await service.AdvanceAsync(record.Session, "completed", note: "Went on.", ct: CancellationToken.None);
                lock (began) began.Add([.. record.Waiting.Select(word => word.Text ?? "")]);
                return new StartRun(
                    $"completed  session {record.Session} (#{start.Quest.Id} → {start.Quest.To})", true,
                    new SessionEnded(record.Session, start.Quest.To, "completed", ByPerson: false, Quest: start.Quest.Id));
            },
        };
        return (driver, began);
    }
}
