using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>The service's doors a source's reopen is told through (FABRIC-CLOSE1c8): a seam, so the look's rule is held by tests without a host.</summary>
public interface IReopenDoors
{
    /// <summary>This machine's record with this id, as a run that goes on reads it; null where none is kept here.</summary>
    Task<PriorSession?> RecordAsync(string session, CancellationToken ct);

    /// <summary>
    /// The say door (MSG1a): Daoris's words kept on a parked or ended record of this machine's, for it to go on with, marked as
    /// Daoris's under their fact's key (COWORK1b), so a say again under the same key is answered with the word kept.
    /// </summary>
    Task<SayAnswer> SayAsync(string session, string text, string key, CancellationToken ct);

    /// <summary>One quest as it stands now, for whether a word's fact still holds where it is delivered (COWORK1b); null where none.</summary>
    Task<QuestView?> QuestAsync(string quest, CancellationToken ct);

    /// <summary>
    /// A running session's inbox where this loop runs it and its door takes words (COWORK1b, SESS3), or null: a conversation, a
    /// session another loop runs, or one whose door takes none.
    /// </summary>
    DrivenInbox? InboxOf(string session);

    /// <summary>
    /// Take Daoris's words whose fact no longer holds off a record (COWORK1b), saying so in its conversation; a refusal is the next
    /// look's to try again.
    /// </summary>
    Task DropAsync(string session, IReadOnlyList<SaidWordView> words, CancellationToken ct);

    /// <summary>
    /// A running session's record as its one dispatcher keeps words on it (COWORK1c), or null where none can, and the inbox holds
    /// Daoris's words in this process alone, as before.
    /// </summary>
    ISaidDoor? SaidOf(string session) => null;
}

/// <summary>The reopen's doors through the loop's service, its sessions' inboxes and its records of what each session did.</summary>
public sealed class ServiceReopenDoors(ServiceClient service, SessionProcesses processes, SessionEvents events) : IReopenDoors
{
    public Task<PriorSession?> RecordAsync(string session, CancellationToken ct) => service.RecordAsync(session, ct);

    public Task<SayAnswer> SayAsync(string session, string text, string key, CancellationToken ct) =>
        service.SayAsync(session, text, null, ct, from: DaorisWords.From, key: key);

    public Task<QuestView?> QuestAsync(string quest, CancellationToken ct) => service.FindQuestAsync(quest, ct);

    public DrivenInbox? InboxOf(string session) => processes.InboxOf(session);

    public ISaidDoor? SaidOf(string session) => new SaidDoor(service, session);

    public async Task DropAsync(string session, IReadOnlyList<SaidWordView> words, CancellationToken ct)
    {
        var ids = words.Select(word => word.Id).OfType<string>().ToList();
        if (ids.Count == 0) return;
        var (taken, message) = await service.TakenAsync(session, ids, ct: ct).ConfigureAwait(false);
        if (!taken) throw new DriverException(message);
        events.Keep(session, new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = DaorisWords.DroppedLine, Words = ids, Code = SessionEventCodes.Dropped,
        }, say: null);
    }
}

/// <summary>
/// A source's reopen told to the session that said its <c>ready</c> (FABRIC-CLOSE1c8; the fabric events design §E5, <i>its session
/// is told at its next step</i>): once, through the say door, so the planner's go-on resumes that conversation with the words.
/// </summary>
/// <remarks>
/// <para><b>Who.</b> The session the reopen names (<see cref="QuestReopenView.Session"/>, the one that said the reopened ready), on
/// this machine alone: a teammate's (<c>origin/id</c>) is never told here, and a reopen naming none (a ready the respond door made,
/// or a host before the field) tells nobody, the run reading it as waiting for its agent. Never the quest's last record here, which
/// need not be the one that said it: where a later session ran, the planner goes on with the words in the named one all the same
/// (FABRIC-CLOSE1c9, <see cref="Snapshot.NamedByReopen"/>), unless the person stopped it, last or not (FABRIC-CLOSE1c12), when it
/// is told nothing and the look says so once, since Daoris's words would release the person's stop.</para>
///
/// <para><b>When.</b> While the quest is taken and its reopen stands; a later submission answers it, so nothing is said. A parked
/// or ended record is told at once: words on a park are its answer, on an ended record they reopen it, and the planner's go-on
/// resumes it at the next look. A running one this loop runs is told through its inbox as Daoris's urgent words (COWORK1b, the
/// message-urgency design §5): at its next step where its door takes words then, held for its turn's end where it takes them only
/// then, and judged again by the word's key where it is delivered (<see cref="DaorisWords"/>). A running one no inbox here reaches,
/// a conversation or another loop's, is told at the first look that finds it parked or ended, since the say door refuses a running
/// record. A record this machine keeps no more, or whose tree is gone, is told nothing, since its go-on would make the work
/// again: the look says why, once.</para>
///
/// <para><b>Once.</b> Every word is said as Daoris's under its fact's key (<see cref="DaorisKeys"/>), and the say door keeps one word
/// per key, answering a say again with the word kept. A mark beside the session's transcript (<see cref="ReopenMarks"/>), written
/// once the say door kept the words, or once the inbox delivered them, keyed by the quest and the reopen's machine and sequence,
/// holds it across a restart; this telling's own memory holds it where the mark could not be written, and holds a refusal other than
/// <c>running</c>, said once and not asked again. A crash between the say and the mark says the words again under the same key, so
/// the record keeps one word (the design's trap 19). Words held in an inbox and lost with the driver were never marked, and are told
/// again once the record is not running.</para>
///
/// <para><b>Dropped, never said late</b> (the design §4.3): Daoris's words waiting on a record the planner would go on with are judged
/// at each look by their keys, before the go-on that would deliver them, and those whose fact no longer holds are taken off, said in
/// the session's conversation.</para>
///
/// <para><b>The cost.</b> Nothing is read where no quest this look read is a reopened source; otherwise one read of the records per
/// reopen not yet told, unless the look's last records or <see cref="Snapshot.NamedByReopen"/> hold its session.</para>
///
/// <para><b>The step's go-on</b> (FABRIC-CLOSE1c13): a step the person went on with after its last session here had ended normally,
/// parked after its <c>ready</c> or completed (<see cref="Planner.GoneOnSince"/>), is told so in that session with Daoris's words, and
/// the start that goes on with them brings its branch up to date with the source's new submission first (<see cref="BringsToSource"/>,
/// which reads the word's mark and key, never its sentence). Once with no mark, by the fact itself; nothing more is read. Never to a
/// running session: one that runs across the go-on is carried on at its end with its branch brought up to date
/// (<see cref="Planner.ReopenedFrom"/>), which only a start can do (D188's COWORK1b note).</para>
/// </remarks>
public sealed class ReopenTelling(ReopenMarks marks)
{
    /// <summary>The reopens settled in this run, by <see cref="Key"/>: told, or said once why they are not.</summary>
    private readonly ConcurrentDictionary<string, bool> _settled = new(StringComparer.Ordinal);

    /// <summary>
    /// One look's telling over the quests it read: the lines it says, each classed as it is made (NOTIFY2h2): a reopen that reaches no
    /// record or no tree is news, since nobody is told and nobody must answer; a stop of the person's their own; a telling not made the
    /// next look's; the rest what moved.
    /// </summary>
    /// <param name="said">The look's classing; none classes and keeps nothing.</param>
    /// <exception cref="OperationCanceledException">The loop is closing.</exception>
    public async Task<IReadOnlyList<string>> LookAsync(IReopenDoors doors, Snapshot snapshot, CancellationToken ct, LookSaid? said = null)
    {
        var lines = new LookLines(said ?? LookSaid.None, "reopen");
        await DropAnsweredAsync(doors, snapshot, lines, ct).ConfigureAwait(false);
        foreach (var quest in snapshot.Quests)
        {
            if (quest is not { Status: "Taken", Reopened: { Session: { Length: > 0 } session } reopen } || session.Contains('/')) continue;
            var key = Key(quest.Id, reopen);
            if (_settled.ContainsKey(key) || marks.Told(session, quest.Id, reopen)) continue;

            try
            {
                await TellAsync(doors, snapshot, quest.Id, reopen, session, key, lines, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Retry($"reopen  quest #{quest.Id}'s reopen was not told to session {session} at this look, and the next tries again: {error.Message}");
            }
        }

        // FABRIC-CLOSE1c13 (§E5, the step rebinds once unheld, its branch brought up to date): a step the person went on with after its
        // session here had ended normally is told so in that session, which the planner then goes on with, from the look's own reads.
        foreach (var step in snapshot.Quests)
        {
            if (!snapshot.LastRun.TryGetValue(step.Id, out var last) || !Planner.GoneOnSince(step, last)) continue;
            var key = $"{step.Id}\n{last.Session}\n{step.GrewFrom!.Machine}\n{step.GrewFrom.Sequence}\ngone-on";
            if (_settled.ContainsKey(key)) continue;

            try
            {
                await TellGoneOnAsync(doors, step, last, key, lines, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Retry($"reopen  quest #{step.Id}'s go-on was not told to session {last.Session} at this look, and the next tries again: {error.Message}");
            }
        }

        return lines.Said;
    }

    /// <summary>
    /// Tell <paramref name="last"/> that the person went on with <paramref name="step"/> (FABRIC-CLOSE1c13): words on a park are its
    /// answer, on an ended record they reopen it, so the planner goes on in that same session. A tree gone is told nothing, as a
    /// reopen's is, since its go-on would make the work again; said once.
    /// </summary>
    private async Task TellGoneOnAsync(IReopenDoors doors, QuestView step, PriorSession last, string key, LookLines lines, CancellationToken ct)
    {
        if (last.Tree is not { Length: > 0 } tree || !Directory.Exists(tree))
        {
            if (Settles(key))
            {
                lines.News(
                    new LookNews(LookSaid.ReopenTreeGone, step.Id, last.Session, step.Id, last.Workspace),
                    $"reopen  quest #{step.Id} was gone on with on the new work of #{step.Parent} after session {last.Session} ended, and its tree "
                    + "is gone: it is told nothing, and no work is made again for it");
            }

            return;
        }

        var said = await doors.SayAsync(
            last.Session, GoneOnWords(step.Id, step.Parent!, step.GrewFrom!), DaorisKeys.GoneOn(step.Id, step.GrewFrom!), ct).ConfigureAwait(false);
        if (said.Kept)
        {
            _settled[key] = true;
            lines.Progress(
                $"reopen  session {last.Session} was told the person went on with quest #{step.Id} on the new work of #{step.Parent}, and goes on "
                + "with Daoris's words");
            return;
        }

        // A record that began running since it was read is told at a later look; any other refusal is said once.
        if (said.Refusal != "running" && Settles(key)) lines.Progress($"reopen  session {last.Session} was not told of quest #{step.Id}'s go-on: {said.Message}");
    }

    private async Task TellAsync(
        IReopenDoors doors, Snapshot snapshot, string quest, QuestReopenView reopen, string session, string key, LookLines lines,
        CancellationToken ct)
    {
        // The look's last records name the session where it is the quest's newest here, and the look's snapshot where a later one
        // is (FABRIC-CLOSE1c9); otherwise its record is read by its id.
        var record = snapshot.LastRun.Values.FirstOrDefault(run => string.Equals(run.Session, session, StringComparison.Ordinal))
                     ?? (snapshot.NamedByReopen.TryGetValue(quest, out var held) && string.Equals(held.Session, session, StringComparison.Ordinal)
                         ? held
                         : null)
                     ?? await doors.RecordAsync(session, ct).ConfigureAwait(false);
        var named = $"quest #{quest}'s reopened submission names session {session}";
        if (record is null)
        {
            if (Settles(key)) lines.News(new LookNews(LookSaid.ReopenUnrecorded, quest, Quest: quest), $"reopen  {named}, which this machine keeps no record of: nobody here is told");
            return;
        }

        // 🔴 COWORK1b (the design's §14 trap): a running session is told at its next step through its inbox, never left to its end.
        // One no inbox here reaches is told once a later look finds it parked or ended, since the say door refuses a running record.
        if (record.Running)
        {
            if (doors.InboxOf(session) is { } inbox) await TellRunningAsync(doors, inbox, quest, reopen, session, key, lines, ct).ConfigureAwait(false);
            return;
        }

        if (record.Tree is not { Length: > 0 } tree || !Directory.Exists(tree))
        {
            if (Settles(key))
            {
                lines.News(new LookNews(LookSaid.ReopenTreeGone, quest, session, quest, record.Workspace),
                    $"reopen  {named}, whose tree is gone: it is told nothing, and no work is made again for it");
            }

            return;
        }

        // 🔴 The person stopped it (FABRIC-CLOSE1c9, and last or not, FABRIC-CLOSE1c12): words on it would go on in it, releasing the
        // person's stop as their own words do, and these are Daoris's. So it is told nothing, and the stop is theirs to lift.
        if (record.PersonStopped)
        {
            if (Settles(key)) lines.Own($"reopen  {named}, which the person stopped: it is told nothing, and nothing goes on with it until they say so");
            return;
        }

        var said = await doors.SayAsync(session, Words(quest, reopen), DaorisKeys.Reopen(quest, reopen), ct).ConfigureAwait(false);
        if (said.Kept)
        {
            _settled[key] = true;
            marks.Mark(session, quest, reopen, DateTimeOffset.UtcNow);
            lines.Progress($"reopen  session {session} was told the person reopened its submission on quest #{quest}, and goes on with Daoris's words");
            return;
        }

        // A record that began running since it was read is told at a later look; any other refusal is said once.
        if (said.Refusal != "running" && Settles(key)) lines.Progress($"reopen  session {session} was not told of quest #{quest}'s reopen: {said.Message}");
    }

    /// <summary>
    /// Tell a running session through its inbox (COWORK1b, the message-urgency design §3.1, §5): Daoris's words at urgent, which go
    /// at its next step where its door takes words then, and are held for its turn's end where it takes them only then. Judged again
    /// where they leave a hold, by the quest as it stands then; the mark is written as they go. An inbox closing as the session
    /// winds up takes none, and a later look finds the record parked or ended. COWORK1c: kept on the running record first, under the
    /// fact's key, through the session's one dispatcher, so a restart before they go leaves them waiting there.
    /// </summary>
    private async Task TellRunningAsync(
        IReopenDoors doors, DrivenInbox inbox, string quest, QuestReopenView reopen, string session, string key, LookLines lines,
        CancellationToken ct)
    {
        var fact = DaorisKeys.Read(DaorisKeys.Reopen(quest, reopen))!;
        var word = new ChatMessage(Words(quest, reopen), [])
        {
            Level = MessageLevel.Urgent,
            Daoris = new DaorisWord(DaorisKeys.Reopen(quest, reopen))
            {
                Stands = async ct => DaorisKeys.Stands(fact, await doors.QuestAsync(quest, ct).ConfigureAwait(false)),
                Delivered = () => marks.Mark(session, quest, reopen, DateTimeOffset.UtcNow),
            },
        };
        var kept = await inbox.KeepOrHoldAsync(doors.SaidOf(session), word, ct).ConfigureAwait(false);
        if (kept.Outcome == InboxKeep.Closed) return;

        _settled[key] = true;
        var when = kept.Reach switch
        {
            DrivenReach.NextStep => "at its next step",
            DrivenReach.TurnEnd => "when its turn ends",
            _ => "as its first prompt goes",
        };
        lines.Progress($"reopen  session {session} is told the person reopened its submission on quest #{quest} {when}, as Daoris's words");
    }

    /// <summary>
    /// Daoris's words waiting on the records the planner would go on with (COWORK1b, the design §4.3), judged by their keys against the
    /// quests this look read, before the go-on that would deliver them: those whose fact no longer holds are taken off and said once.
    /// A word whose quest this look did not read, or whose key names no fact of Daoris's, waits as it is.
    /// </summary>
    private async Task DropAnsweredAsync(IReopenDoors doors, Snapshot snapshot, LookLines lines, CancellationToken ct)
    {
        var records = snapshot.LastRun.Values.Concat(snapshot.NamedByReopen.Values)
            .Where(record => !record.Teammate && record.WordsWaiting)
            .DistinctBy(record => record.Session, StringComparer.Ordinal);
        foreach (var record in records)
        {
            var answered = record.Waiting
                .Where(word => word is { Daoris: true, Id: { } id } && !_settled.ContainsKey(Dropping(record.Session, id))
                               && DaorisKeys.Read(word.Key) is { } fact
                               && snapshot.Quests.FirstOrDefault(quest => string.Equals(quest.Id, fact.Quest, StringComparison.OrdinalIgnoreCase)) is { } quest
                               && !DaorisKeys.Stands(fact, quest))
                .ToList();
            if (answered.Count == 0) continue;

            try
            {
                await doors.DropAsync(record.Session, answered, ct).ConfigureAwait(false);
                foreach (var word in answered) _settled[Dropping(record.Session, word.Id!)] = true;
                lines.Progress(
                    $"reopen  session {record.Session}'s words from Daoris were taken off its record, since what they said no longer holds: "
                    + string.Join(", ", answered.Select(word => word.Key)));
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Retry($"reopen  session {record.Session}'s words from Daoris that no longer hold were not taken off at this look, and the next tries again: {error.Message}");
            }
        }
    }

    private static string Dropping(string session, string word) => $"{session}\n{word}\ndropped";

    /// <summary>Whether a reopen not told is said now: once, and never asked again in this run.</summary>
    private bool Settles(string key) => _settled.TryAdd(key, false);

    private static string Key(string quest, QuestReopenView reopen) => $"{quest}\n{reopen.Machine}\n{reopen.Sequence}";

    /// <summary>
    /// What a step's session is told once the person went on with it on its source's new submission (FABRIC-CLOSE1c13): Daoris's own
    /// sentence, naming that submission. The start knows to bring its branch up to date from the word's mark and key, never from this
    /// sentence (<see cref="BringsToSource"/>, COWORK1b). What the start did with the branch is said after it (LANDSYNC2's place).
    /// </summary>
    public static string GoneOnWords(string quest, string source, QuestSubmissionRef grew) =>
        $"Daoris: the person reopened the work of `#{source}` that quest `#{quest}` grew from, and went on with `#{quest}` on its new "
        + $"submission `{grew.Machine}` {grew.Sequence}, so any result submitted on `#{quest}` was cleared. Bring your work up to date with "
        + $"that submission (what Daoris did with your branch follows, where it did anything), then say `ready` on `#{quest}` again.";

    /// <summary>
    /// Whether a step going on from <paramref name="record"/> is first brought up to date with its source's new submission, then the line
    /// (FABRIC-CLOSE1c5's <c>GoOnInSyncAsync</c>): the session the reopen stopped, carried on, and (FABRIC-CLOSE1c13) one that had ended
    /// normally, going on with Daoris's words of the go-on to the submission the step grows from now, while it has no result again.
    /// Every other going-on syncs with the line alone, as before.
    /// </summary>
    /// <remarks>
    /// 🔴 FABRIC-CLOSE1c17 (COWORK1b): the go-on is read from the word's mark, Daoris's under the go-on's key, never from its sentence.
    /// The person's words equal to the sentence are theirs, and a word an older host kept, with no mark, reads as the person's: either
    /// goes on with the line alone.
    /// </remarks>
    public static bool BringsToSource(QuestView step, PriorSession record) =>
        step.GrewFrom is { } grew
        && (Planner.StoppedByReopen(record)
            || (step is { Submission: null, Parent: { Length: > 0 } }
                && record.Waiting.Any(word => word.Daoris && string.Equals(word.Key, DaorisKeys.GoneOn(step.Id, grew), StringComparison.Ordinal))));

    /// <summary>What the session is told: Daoris's own sentence naming the reopened submission, then the person's words quoted.</summary>
    public static string Words(string quest, QuestReopenView reopen)
    {
        var sentence =
            $"Daoris: the person reopened submission `{reopen.Of.Machine}` {reopen.Of.Sequence} of quest `#{quest}`, which your `ready` "
            + $"made, so the quest is not ready any more. Address what they reopened it for, then say `ready` on `#{quest}` again.";
        if (reopen.Note?.Trim() is not { Length: > 0 } note) return sentence;

        var quoted = string.Join("\n", note.ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));
        return $"{sentence} Their words:\n\n{quoted}";
    }
}
