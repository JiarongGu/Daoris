using System.Text.Json;
using static Daoris.Driver.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a person's words to a session do, and what a word said now would do (MSG1d, D137 §2, §5.3): the one answer
/// <c>SESSION_INPUT</c> and <c>SESSION_QUEUE</c> give, for every state.
/// </summary>
/// <param name="Sent">The words are held or taken: they reach the session, now or when it can.</param>
/// <param name="Reaches">
/// When they reach it: <c>next-step</c>, <c>turn-end</c> or <c>resume</c>. Null where they are taken at once (a chat between
/// turns), where the door is not known yet (a driven session still opening), and where nothing takes them.
/// </param>
/// <param name="Why">
/// Where nothing takes them, why, by a code the page words itself: <c>teammate</c>, <c>intake</c>, <c>stood-down</c>,
/// <c>help</c>, <c>superseded</c>, <c>continued</c>, <c>not-found</c> or <c>no-words</c>. Null where they are taken or held,
/// except where the person's *Send now* was not carried out (STEER2a): then its <see cref="SendNowWhy"/> code, beside
/// <c>sent: true</c>.
/// </param>
public sealed record WordsAnswer(bool Sent, string? Reaches, string? Why)
{
    /// <summary>
    /// The person's *Send now* was carried out (STEER2a, D136's 2026-10-16 note): steered into the running turn (reach
    /// <c>now</c>), held with the turn stopped so they go next (<c>turn-end</c>), or taken at once because no turn ran. False
    /// where it was not asked, and where the door could do neither, with <see cref="Why"/> saying why.
    /// </summary>
    public bool Now { get; init; }

    /// <summary>
    /// Where <see cref="Why"/> is <c>continued</c> (HANDWORDS1), the conversation it went on in, the latest that continues it: where
    /// the words go instead. Null otherwise.
    /// </summary>
    public string? To { get; init; }

    /// <summary>Whether the words reached a running session's own door, which keeps them on its ask only as it takes them.</summary>
    public bool Running { get; init; }

    /// <summary>
    /// The id the record gave words it kept, which its events say again where the session took them: what a terminal's say
    /// follows to tell whether the same session took them (MSG1e2). Null where nothing kept them yet.
    /// </summary>
    public string? Word { get; init; }

    /// <summary>The service's sentence beside its refusal, which a terminal prints for a code it does not word itself.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// What a running session's door can do with a word while a turn runs (COWORK1a): whether its agent steers, and whether it has
    /// the keeping stop. Null where nothing here runs it, its door is not known yet, or it carries only text.
    /// </summary>
    public TurnDoor? Door { get; init; }

    /// <summary>
    /// The person's words waiting on its record (SAIDBACK1b, D137 §2.4), where the record was read: what the queue lists when
    /// nothing here holds the session, so the composer's *Edit* and *Remove* reach them. Null where the record was not read (a
    /// session running here, or nothing known); empty on a record that runs again, whose run is taking them.
    /// </summary>
    public IReadOnlyList<WaitingWord>? Waiting { get; init; }

    /// <summary>Nothing takes them, for this reason.</summary>
    public static WordsAnswer Refused(string why, string? message = null) => new(false, null, why) { Message = message };
}

/// <summary>
/// One of the person's words waiting on a record (SAIDBACK1b, D137 §2.4), as the queue lists it: the id the record gave it, which
/// a take-back names it by, its words and its files' names.
/// </summary>
public sealed record WaitingWord(string Id, string Text, IReadOnlyList<string> Files);

/// <summary>
/// The person's words to a session of this machine's, whatever its state (MSG1d, D137 §2): a running session hears them at
/// its door as before (D90, D136); one that parked or ended has them kept on its record by the service's say door (MSG1a)
/// and the loop is nudged, so the driver goes on with them (MSG1b); and words said as a session winds up are held until its
/// record ends, then kept the same way, never refused.
/// </summary>
/// <remarks>
/// <para><b>What never goes on is said by a code</b> (D137 §2.2), judged from the record before anything is posted by the
/// driver library's one table, <see cref="WordsNever"/>, which the terminal's <c>sessions say</c> judges by too: a
/// teammate's record, an intake, a session that stood down, Ask Daoris's own conversation, a session whose quest went
/// on in a later session here, and a conversation handed over or moved, which names the one it went on in (HANDWORDS1). The
/// service judges again as it keeps them, and its refusal is the answer.</para>
///
/// <para><b>Words said while a conversation is handed over or moved</b> (HANDWORDS1, D165's SWITCH1c-ii note) wait here as
/// words said as a session winds up do, from the take's ask until it is told over (<see cref="OnTakeEnded"/>), never kept on
/// the record that is ending; then they go to the conversation that continues it.</para>
///
/// <para><b>Both doors.</b> The screen's box says words through <see cref="SayAsync"/>; a terminal's <c>sessions say</c>
/// reaches the shell's request watch, which hands it to <see cref="HoldAsync"/>, the same judgement at the door
/// <c>terminal</c> (MSG1e2).</para>
///
/// <para><b>Shown at once, under the record's id.</b> The words are written to the session's conversation the moment the
/// say door keeps them, as the person's, with the reach <c>resume</c>, the id the record gave them and the door they were
/// said at: the resumed run says them again under the same id where it took them (D137 §3.1), and the machine log's
/// <c>session.reopened</c> names the door (§3.3). Words held while a session winds up have no id until the record keeps
/// them, so they are shown once it does.</para>
///
/// <para><b>Held words wait in this process</b>, for the record to move through this machine's client or for a slow look,
/// whichever comes first, <b>and in a small file under the home</b> (<see cref="HeldPath"/>, MSG1d4), written whole beside
/// and renamed each time a word is held or leaves. The page was told they are held, so a restart in that moment must not
/// lose them: the next shell's judge reads them back as it starts and tries them as before.</para>
/// </remarks>
public sealed class SessionWords : IDisposable
{
    private readonly DriverLoop _loop;

    /// <summary>The loop's judge; it reads back the words an earlier shell held and was closed with (MSG1d4).</summary>
    public SessionWords(DriverLoop loop)
    {
        _loop = loop;
        _loop.Processes.KeptLeft += OnKeptLeft;
        Restore();
    }

    /// <summary>
    /// One key per press of the person's (COWORK1c, the message-urgency design §6), where the door that took the words named none:
    /// the record keeps one word per key, so a press that arrives twice keeps one.
    /// </summary>
    private static string PressKey() => $"press/{Guid.NewGuid():N}";

    /// <summary>
    /// Words kept for a driven session's run that stay on its record as its inbox closed under them (COWORK1c): its door failed, or the
    /// shell is shutting down. Shown again under the ids they were shown by, as waiting for the session to go on (the reach
    /// <c>resume</c>), so the page never says they were lost; and the loop is nudged, so a look goes on with them.
    /// </summary>
    private void OnKeptLeft(string id, IReadOnlyList<ChatMessage> words)
    {
        foreach (var word in words)
        {
            var daoris = word.Daoris is not null;
            _loop.Output.Append(id, $"— the session ended before it took {(daoris ? "Daoris's words" : "what the person added")}, which wait for it to go on: {word.Text}");
            _loop.Events.Keep(id, new SessionEvent
            {
                Kind = SessionEventKind.User,
                Origin = daoris ? "target" : "person",
                From = daoris ? DaorisWords.From : null,
                Id = word.Id,
                Text = word.Text,
                Reaches = "resume",
                Level = daoris ? null : MessageLevels.Spelled(word.Level),
            }, null);
        }

        Tell(id);
        _loop.Said(id);
    }

    /// <summary>
    /// Where words held while a session winds up are kept across a restart (MSG1d4): each in the order said, with its session,
    /// its text, the names its files are kept under and the door it was said at. Absent while nothing is held.
    /// </summary>
    /// <remarks>Its place is the driver library's (<see cref="HeldWordsFile"/>), which takes a cleared or deleted session's words out of it (HIST1c).</remarks>
    public static string HeldPath(string home) => HeldWordsFile.PathOf(home);

    /// <summary>The longest held words wait for a move before they are tried again: a move made by another client says nothing here.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, Waiting> _waiting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The words held for one session's record to end, in the order said, and what tells them it moved.</summary>
    private sealed class Waiting
    {
        public SemaphoreSlim Turn { get; } = new(1, 1);

        public Queue<Word> Words { get; } = new();

        public TaskCompletionSource Moved { get; set; } = Signal();

        public bool Pumping { get; set; }
    }

    /// <summary>
    /// One word as it goes to the say door: its text, the names of its files, the door it was said at, and the answer of its park's
    /// question it was pressed as (PARKVIEW1), which the conversation shows it under; null for every other word.
    /// </summary>
    private sealed record Word(string Text, IReadOnlyList<string> Files, string Door, string? Choice = null);

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// A record moved through this machine's client (<see cref="ServiceClient.Moved"/>): words held for it are tried now, and
    /// what a word said to it now would do is told again (MSG1f2), since a move is what changes it.
    /// </summary>
    public void OnMoved(SessionMoved moved) => Wake(moved.Session);

    /// <summary>
    /// A conversation's hand-over or curve move was taken (<see cref="ChatRunner.TakeEnded"/>, HANDWORDS1), its box gone: words held
    /// for it while it was taken go on now, to the conversation that continues it, and what a word said to it now would do is told
    /// again, since it went on elsewhere.
    /// </summary>
    public void OnTakeEnded(string session) => Wake(session);

    /// <summary>The words held for a record are tried now, and what a word said to it now would do is told again (MSG1f2).</summary>
    private void Wake(string session)
    {
        TaskCompletionSource? told = null;
        lock (_gate)
        {
            if (_waiting.TryGetValue(session, out var waiting))
            {
                told = waiting.Moved;
                waiting.Moved = Signal();
            }
        }

        told?.TrySetResult();
        Tell(session);
    }

    /// <summary>
    /// A record opened through this machine's client (<see cref="ServiceClient.Opened"/>): a driven session's quest went on in
    /// it, so its earlier sessions here take no words now (<c>superseded</c>), and theirs is told again (MSG1f2), since
    /// nothing moved their records.
    /// </summary>
    public void OnOpened(SessionOpened opened)
    {
        if (opened.Kind != SessionOpened.Driven || _loop.Service is not { } service) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var records = SessionRecords.Parse(await service.SessionRecordsJsonAsync(_stopping.Token).ConfigureAwait(false));
                var quest = records.FirstOrDefault(each => string.Equals(each.Id, opened.Session, StringComparison.Ordinal))?.Quest;
                if (quest is null) return;
                foreach (var earlier in records.Where(each =>
                             !each.Teammate && !string.Equals(each.Id, opened.Session, StringComparison.Ordinal)
                             && string.Equals(each.Quest, quest, StringComparison.OrdinalIgnoreCase)))
                {
                    Tell(earlier.Id);
                }
            }
            catch (Exception error) when (Unanswered(error) || error is ObjectDisposedException)
            {
                // Nothing told is nothing claimed: the page keeps what it was told, and a word said refuses as before. The
                // loop closing under it disposes the client, which says nothing either.
            }
        });
    }

    /// <summary>
    /// What a word said now to a session would do, told again (MSG1f2, D137 §5.3): the shell writes it on
    /// <c>SESSION_QUEUED</c>, so the page follows the box it offers without asking again on each move.
    /// </summary>
    public event Action<string, WordsAnswer>? Reached;

    // The sessions whose reach is being read to be told, each with whether it was asked again meanwhile: one read at a time
    // per session, so what is told last is what was read last, and a burst of moves costs one more read, not one each.
    private readonly Dictionary<string, bool> _telling = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Read this session's reach and tell it (<see cref="Reached"/>), in the background. Asked again while a read is on its
    /// way, it reads once more after; a service that does not answer tells nothing, since nothing known is no claim.
    /// </summary>
    public void Tell(string id)
    {
        lock (_gate)
        {
            if (_telling.ContainsKey(id))
            {
                _telling[id] = true;
                return;
            }

            _telling[id] = false;
        }

        _ = Task.Run(() => TellingAsync(id));
    }

    private async Task TellingAsync(string id)
    {
        while (true)
        {
            try
            {
                var reach = await ReachAsync(id, _stopping.Token).ConfigureAwait(false);
                if (reach is not { Sent: false, Why: null }) Reached?.Invoke(id, reach);
            }
            catch (Exception error) when (Unanswered(error) || error is ObjectDisposedException)
            {
                // The loop is closing, or its service went away: the next move tells it again.
            }

            lock (_gate)
            {
                if (_stopping.IsCancellationRequested || !_telling[id])
                {
                    _telling.Remove(id);
                    return;
                }

                _telling[id] = false;
            }
        }
    }

    /// <summary>
    /// What a word said now to a session that runs here would do, known without reading its record (MSG1f2): an open door's
    /// reach (D136), or a conversation's turns (MSG1c). Null where only the record can say: nothing here runs it, or its door
    /// has closed and it winds up.
    /// </summary>
    public WordsAnswer? ReachHere(string id)
    {
        // MODELROLE1d2: the replacement a refused start moved to answers for it; moving there, a word joins its words.
        if (_loop.Chat is { } replaced)
        {
            id = replaced.CurrentOf(id);
            if (replaced.Replacing(id)) return new(true, null, null);
        }

        // What the door can do while a turn runs rides it (COWORK1a): whether its agent steers, and whether it can stop a turn
        // keeping the session, which the page offers the levels and the keeping stop by.
        if (_loop.Processes.InboxOf(id) is { State.Taking: true } inbox)
        {
            return new(true, Spelled(inbox.Reach), null) { Door = inbox.Reach is null ? null : new TurnDoor(inbox.Steers, inbox.KeepingStop) };
        }

        // Ask Daoris's conversation still starting (HELPSTART1): a word waits for its door, which opens with it as its first turn.
        if (_loop.HelpStarting.Queue(id) is not null) return new(true, null, null);

        if (_loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) && _loop.Processes.RefusesInput(id) is null)
        {
            // A conversation this machine runs (D49 §3): a word goes into the running turn where its agent takes words at its
            // next step (MSG1c), waits for the turn to end elsewhere, or starts one now (CONV4a).
            return new(true, _loop.Chat?.Reach(id), null) { Door = _loop.Chat?.Door(id) };
        }

        return null;
    }

    /// <summary>
    /// What a word said now to this session would do (<c>SESSION_QUEUE</c>, D137 §5.3), for the page to offer the box by, or
    /// the line saying why nothing takes words. Never a refusal: a page asks of every conversation it shows, and a service
    /// that does not answer is nothing known, never a claim either way.
    /// </summary>
    public async Task<WordsAnswer> ReachAsync(string id, CancellationToken ct)
    {
        if (ReachHere(id) is { } here) return here;
        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        try
        {
            if (await ReadAsync(id, ct).ConfigureAwait(false) is not { } read) return new(false, null, null);
            if (read.Record is not { } record) return WordsAnswer.Refused(WordsNever.NotFound);
            // The person's words waiting on it, which the queue lists where nothing here holds it (SAIDBACK1b).
            var waiting = WaitingOn(read.Json, id);
            // Being handed over or moved (HANDWORDS1): a word said now is held for the take, as SayAsync holds it.
            if (TakeWaits(record)) return new(true, "resume", null) { Waiting = waiting };
            return (Never(record, read) ?? new(true, "resume", null)) with { Waiting = waiting };
        }
        catch (Exception error) when (Unanswered(error))
        {
            return new(false, null, null);
        }
    }

    /// <summary>
    /// The person's words to this session (<c>SESSION_INPUT</c>, D137 §5.3): held at a running session's door, kept on a
    /// parked or ended record for it to go on with, or held until a winding-up record ends. Null when the loop's service is
    /// not answering yet and nothing here runs the session, so nothing can keep them.
    /// </summary>
    /// <param name="door">Where they were said, <c>screen</c> or <c>terminal</c>: the machine log names it (D137 §3.3).</param>
    /// <param name="level">
    /// When the person asked the words to reach a working session (COWORK1a, D188 point 1): urgent by default, at its next step where
    /// its door takes words then; ordinary at its turn's end; <c>now</c>, the person's *Send now* (STEER2a), carried out by its door
    /// (<see cref="DrivenInbox.HoldNowAsync"/>, <see cref="ChatRunner.SayNowAsync"/>). No level stops a turn. One that parked or
    /// ended has no turn running, and goes on with the words as before.
    /// </param>
    /// <exception cref="DriverException">
    /// A session this machine runs that takes no words at all (an intake, INT4h), in the driver's words; files the person
    /// attached that a conversation cannot carry; or a service whose answer could not be read.
    /// </exception>
    /// <param name="choice">
    /// The answer of a park's question the words begin with, as a press composes them (PARKVIEW1; Fable's read, Q4): the conversation
    /// shows them under it, and the resumed run is handed them as that answer chosen. Null for every other word.
    /// </param>
    public async Task<WordsAnswer?> SayAsync(
        string id, string text, IReadOnlyList<ChatUpload> files, string? preface, string door, CancellationToken ct,
        MessageLevel level = MessageLevel.Urgent, string? key = null, string? choice = null)
    {
        // MODELROLE1d2 (D205 §3.5(b)): a conversation whose agent refused its choice at the open is answered by its replacement, and while
        // that opens a word joins the words moving to it, so it reaches the replacement once.
        if (_loop.Chat is { } replaced)
        {
            id = replaced.CurrentOf(id);
            if (replaced.Replacing(id) && replaced.Say(id, text, files, preface)) return new(true, null, null) { Running = true };
        }

        var now = level == MessageLevel.Now;
        // A driven session that hears words during its run (SESS3, D136; the native door's run, MSG1b). COWORK1c: kept on its record
        // first, under the press's key, then held by its one dispatcher under the record's id.
        if (_loop.Processes.InboxOf(id) is { } inbox)
        {
            var forRun = await inbox
                .KeepOrHoldAsync(
                    _loop.Service is { } service ? new SaidDoor(service, id) : null,
                    new ChatMessage(text, []) { Level = level, Key = key ?? PressKey() }, ct)
                .ConfigureAwait(false);
            switch (forRun)
            {
                case { Outcome: InboxKeep.Held, Now: { } sentNow }:
                    return new(true, sentNow.Reaches, sentNow.Why) { Running = true, Now = sentNow.Now, Word = forRun.Said?.Word?.Id };
                case { Outcome: InboxKeep.Held }:
                    return new(true, Spelled(forRun.Reach), null) { Running = true, Word = forRun.Said?.Word?.Id };
            }
        }

        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        // Ask Daoris's conversation placed and still starting (HELPSTART1): its words wait for its door, as its queue, opening.
        if (_loop.HelpStarting.Hold(id, text, files, preface))
        {
            Tell(id);
            return new(true, null, null);
        }

        var running = _loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase);
        var refuses = _loop.Processes.RefusesInput(id);
        if (running && refuses is null && _loop.Chat is { } chat)
        {
            // A conversation this machine runs (D49 §3): its turns take the words, into the running turn at its next step where
            // its agent takes them then (MSG1c), when the running one ends elsewhere (CONV4a), or now; its runner says which.
            // COWORK1c: kept on its record first, under the press's key, and its turns are their one dispatcher.
            if (await chat.SayKeptAsync(id, text, files, preface, level, key ?? PressKey(), ct).ConfigureAwait(false) is { } chatSaid)
            {
                return chatSaid.Now is { } sentNow
                    ? new(true, sentNow.Reaches, sentNow.Why) { Running = true, Now = sentNow.Now, Word = chatSaid.Word }
                    : new(true, chatSaid.Reaches, null) { Running = true, Word = chatSaid.Word };
            }
        }

        Read? read;
        try
        {
            read = await ReadAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            throw new DriverException($"the service did not answer for session `{id}`, so your words were not kept: {error.Message}");
        }

        // 🔴 Running here and taking no person's line, with nothing to say it is a quest's session: refused in the driver's
        // words, as INT4h has it. An intake takes nothing; false would tell a stale page it ended when it is running.
        if (running && refuses is not null && read?.Record is not { Ask: null })
        {
            throw new DriverException(refuses);
        }

        if (read is null) return null;
        if (read.Record is not { } record) return WordsAnswer.Refused(WordsNever.NotFound);

        // 🔴 HANDWORDS1 (D163 point 1, D165 point 3): a conversation being handed over or moved, its box kept until the take is done.
        // Its words are never kept on the record that is ending, which would restart it in the tree its successor holds: they wait
        // here, and go to the conversation that continues it once the take is over (PumpAsync). Judged before the nevers, since the
        // successor's record opens before the take is done.
        var holding = TakeWaits(record);
        if (!holding && Never(record, read) is { } never) return never;

        // Kept as a conversation keeps what is attached (CONV4c), so the names on the record name files that exist.
        var kept = files.Count > 0 ? ChatFiles.Keep(_loop.Home, id, files) : [];
        var word = new Word(text, [.. kept.Select(file => file.Name)], door, choice);
        return holding
            ? await HoldForTakeAsync(id, word, ct).ConfigureAwait(false)
            : await KeepOrHoldAsync(id, word, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Take back one of the person's words waiting on a parked or ended record, or reword it (SAIDBACK1b, D76), through the
    /// service's take-back door (SAIDBACK1a): what <c>SESSION_QUEUE_EDIT</c> falls back to once nothing here holds the word.
    /// Done, the conversation settles the old word with the withdrawn line (QUEUEEDIT1a) and shows a reword's new words as the
    /// person's under their new id, as <see cref="Show"/> shows words kept. Whatever the service answers, the queue is told again
    /// (<see cref="Tell"/>), so a list the page showed stale, a word a terminal took back meanwhile, is read anew.
    /// </summary>
    /// <returns>
    /// <see cref="QueueEdit.Done"/>; or <see cref="QueueEdit.Taken"/> where nothing holds the word for this machine: no service
    /// yet, a word no longer on the record, a record that runs again or is gone, or a host without the door, as before it.
    /// </returns>
    /// <exception cref="DriverException">The service's refusal, in its sentence (what never goes on); or a service that did not answer.</exception>
    public async Task<QueueEdit> TakeBackAsync(string id, string word, string? text, CancellationToken ct)
    {
        if (await TakeBackToServiceAsync(id, word, text, ct).ConfigureAwait(false) is not { } answer) return QueueEdit.Taken;
        if (!answer.Done)
        {
            return answer.Why is null or "taken" or WordsNever.NotFound ? QueueEdit.Taken : throw new DriverException(answer.Message);
        }

        return QueueEdit.Done;
    }

    /// <summary>
    /// A terminal's <c>sessions take-back</c> (SAIDBACK1c, D137 §2.4): the shell's half, handed to its request watch as
    /// <see cref="SessionRequestWatch.TakeBack"/>, so the withdrawn note is written in the record of the conversation this shell
    /// holds, which the page may have open. Answered in the say's shape: sent is done, a refusal's code is <c>why</c>.
    /// </summary>
    public async Task<WordsHeld> HoldTakeBackAsync(SessionRequest request, CancellationToken ct)
    {
        if (request.Word is not { Length: > 0 } word) return WordsHeld.Failed("a take-back names the word to take back.");
        var back = await TakeBackToServiceAsync(request.Session, word, request.Text, ct).ConfigureAwait(false);
        if (back is null) return WordsHeld.Failed(NotUp);
        if (back.Done) return new WordsHeld(true, null, null) { Word = back.Word?.Id, Message = back.Message };
        return back.Why is { } why ? WordsHeld.Refused(why, back.Message) : WordsHeld.Failed(back.Message);
    }

    /// <summary>
    /// The service's take-back door asked, the queue told again whatever it answers, and, where it is done, the conversation
    /// settled and a reword's new words shown. Null where there is no service yet.
    /// </summary>
    private async Task<TakeBackAnswer?> TakeBackToServiceAsync(string id, string word, string? text, CancellationToken ct)
    {
        if (_loop.Service is not { } service) return null;

        TakeBackAnswer answer;
        try
        {
            answer = await service.TakeBackAsync(id, word, text, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            throw new DriverException($"the service did not answer for session `{id}`, so word `{word}` was not taken back: {error.Message}");
        }

        Tell(id);
        if (!answer.Done) return answer;

        // QUEUEEDIT1a's line, by its code, which the page words; the one note shape every door keeps (WITHDRAWNLINE1).
        _loop.Output.Append(id, $"— the person took back word {word} before the session took it.");
        _loop.Events.Keep(id, SessionNotes.Withdrawn(word), null);
        if (answer.Word is { } kept)
        {
            _loop.Output.Append(id, $"— the person said, which the same session goes on with: {kept.Text}");
            _loop.Events.Keep(id, LoopWords.Shown(kept, "screen"), line => _loop.Output.Append(id, line));
        }

        return answer;
    }

    /// <summary>
    /// Whether a conversation's hand-over or curve move waits or is being taken (HANDWORDS1): its box is kept from the ask until the
    /// take is over, however it came out. A quest's session's box is the driver's look's, which carries its words on itself.
    /// </summary>
    private bool TakeWaits(SessionRecord record) =>
        record is { Kind: "chat", Teammate: false } && new HandOverBox(_loop.Home).Read(record.Id) is not null;

    /// <summary>
    /// What never goes on, as the answer says it (D137 §2.2): a conversation that went on in another names that one (HANDWORDS1),
    /// with the terminal's sentence beside the code. Null where the record can go on with words.
    /// </summary>
    private static WordsAnswer? Never(SessionRecord record, Read read) =>
        WordsNever.Judge(record, read.Last, read.Next) switch
        {
            null => null,
            WordsNever.Continued => WordsAnswer.Refused(WordsNever.Continued, WordsNever.WentOn(record.Id, read.Next!)) with { To = read.Next },
            var never => WordsAnswer.Refused(never),
        };

    /// <summary>
    /// A word said while its conversation is handed over or moved (HANDWORDS1), held behind any already held for it and tried once
    /// the take is told over (<see cref="OnTakeEnded"/>), or at once where the box went while it was held.
    /// </summary>
    private async Task<WordsAnswer> HoldForTakeAsync(string id, Word word, CancellationToken ct)
    {
        var waiting = WaitingFor(id);
        await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            TaskCompletionSource moved;
            lock (_gate) moved = waiting.Moved;
            Hold(waiting, word);
            StartPump(id, waiting, moved);
        }
        finally
        {
            waiting.Turn.Release();
        }

        // Taken while it was held: no end will be told for it now.
        if (new HandOverBox(_loop.Home).Read(id) is null) Wake(id);
        return new(true, "resume", null);
    }

    /// <summary>
    /// What became of a park once a go-ahead it asked was answered at the go-ahead's own door with <c>goesOn</c> (GOAHEAD2b,
    /// D135 §2): the service judged there, as for the ask's page and the terminal, whether none of the session's go-aheads
    /// is open any more, and kept the park's blank answer where none is (GOAHEAD2). That decision is read here off the record
    /// and never judged again: a park now answered goes on, and the loop is nudged so a look takes it up now; one still
    /// parked with no answer waits on the person.
    /// </summary>
    /// <returns>
    /// The park as <c>SESSION_INPUT</c> words a reach, and whether it still waits on the person: a go-ahead it asked still
    /// open, or, where none is, its own answer, since a changed answer wakes nothing (GOAHEAD2). A record that is not parked,
    /// or never goes on, waits on nothing.
    /// </returns>
    /// <exception cref="DriverException">The loop's service is not answering yet, or did not answer for the record.</exception>
    public async Task<(WordsAnswer Park, bool Waits)> AfterGoAheadAsync(string id, CancellationToken ct)
    {
        if (id.Contains('/')) return (WordsAnswer.Refused(WordsNever.Teammate), false);

        Read? read;
        try
        {
            read = await ReadAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            throw new DriverException(
                $"the service did not answer for session `{id}`, so whether it goes on is not known; its go-ahead is answered: {error.Message}");
        }

        if (read is null) throw new DriverException(NotUp);
        if (read.Record is not { } record) return (WordsAnswer.Refused(WordsNever.NotFound), false);
        if (Never(record, read) is { } never) return (never, false);
        if (record.State != "awaiting-person") return (new(false, null, null), false);
        if (!record.Answered) return (new(false, null, null), true);

        _loop.Nudge();
        return (new(true, "resume", null), false);
    }

    /// <summary>
    /// A terminal's words (MSG1e2, D137 §5.2): the shell's half of <c>daoris-driver sessions say</c>, handed to its request
    /// watch as <see cref="SessionRequestWatch.Say"/>. Judged as the box's words are (<see cref="SayAsync"/>), at the door
    /// <c>terminal</c>, and answered in the request's shape: a conversation the window runs hears them, words a running
    /// session took are kept on its ask, kept words are shown at once and the loop nudged, and words said as a session winds
    /// up wait here for its record to end rather than being asked again.
    /// </summary>
    /// <exception cref="DriverException">
    /// A file the request names that is not kept for the session, or what <see cref="SayAsync"/> refuses in the driver's
    /// words; the watch answers it as the sentence it is.
    /// </exception>
    public async Task<WordsHeld> HoldAsync(SessionRequest request, CancellationToken ct)
    {
        var text = request.Text ?? "";
        // COWORK1c: the request's own key, so a request read again after a restart keeps one word.
        var said = await SayAsync(
                request.Session, text, Kept(request.Session, request.Files), preface: null, RequestDoor.Terminal, ct, request.Asked,
                request.Key is { Length: > 0 } key ? $"request/{key}" : null)
            .ConfigureAwait(false);
        if (said is null) return WordsHeld.Failed(NotUp);
        if (said.Running) KeepOnAsk(request.Session, text);
        // A *Send now* is answered whole (STEER2c): what the door did, and why where it did not; any other say as it always was.
        if (said.Sent && request.Asked == MessageLevel.Now) return new WordsHeld(true, said.Reaches, said.Why) { Word = said.Word, Now = said.Now };
        if (said.Sent) return new WordsHeld(true, said.Reaches, null) { Word = said.Word };
        return said.Why is { } why ? WordsHeld.Refused(why, said.Message) : WordsHeld.Failed(said.Message ?? NotUp);
    }

    /// <summary>
    /// A terminal's files, by the names the verb kept them under for the session (MSG1e): read back from where they lie, so
    /// the box's own path keeps them, which finds each already there since a file is kept by its content (CONV4c).
    /// </summary>
    private IReadOnlyList<ChatUpload> Kept(string id, IReadOnlyList<string> names) =>
    [
        .. names.Select(name =>
        {
            // Of two files given one name, the newer is the one just said (ChatFiles.Find, which a resumed run reads by too).
            var kept = ChatFiles.Find(_loop.Home, id, name)
                ?? throw new DriverException($"`{name}` is not kept for session `{id}`, so your words were not sent; say them again with it.");
            return new ChatUpload(name, File.ReadAllBytes(kept.Path));
        }),
    ];

    /// <summary>
    /// What the person said to a running session, kept on the ask its work is for (DRIFT1a2, D133 §1), once the session took
    /// it. Both doors keep them (MSG1e2), by the driver library's one rule, <see cref="WordsOnAsk.Keep"/>, which the headless
    /// host's loop keeps them by too (MSG1e5): never awaited, so nothing the service answers changes what the person is told.
    /// </summary>
    public void KeepOnAsk(string session, string text) => _ = WordsOnAsk.Keep(_loop.Service, session, text);

    /// <summary>
    /// What a door is told while the loop's service is not answering and nothing here runs the session: nothing could keep
    /// the words yet, which is a moment the person can wait out.
    /// </summary>
    private const string NotUp = "the driver is still coming up — its service is not answering yet. A moment.";

    /// <summary>
    /// The words to the say door, behind any already held for this record: kept, shown and the loop nudged; held while the
    /// record still runs; or refused by the service's word.
    /// </summary>
    private async Task<WordsAnswer> KeepOrHoldAsync(string id, Word word, CancellationToken ct)
    {
        var service = _loop.Service ?? throw new DriverException(NotUp);
        var waiting = WaitingFor(id);
        await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Behind the words already held, so the record keeps them in the order said.
            if (waiting.Words.Count > 0)
            {
                Hold(waiting, word);
                return new(true, "resume", null);
            }

            // Taken before the say, so a move while it is on its way is not missed.
            TaskCompletionSource moved;
            lock (_gate) moved = waiting.Moved;
            SayAnswer said;
            try
            {
                said = await service.SayAsync(id, word.Text, word.Files, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (Unanswered(error))
            {
                throw new DriverException($"the service did not answer for session `{id}`, so your words were not kept: {error.Message}");
            }

            if (said.Kept)
            {
                Show(id, said, word);
                return new(true, "resume", null) { Word = said.Word?.Id };
            }

            if (said.Refusal == "running")
            {
                // It winds up, or runs where this machine's routes do not reach (D137 §2.1): held for its record to end.
                Hold(waiting, word);
                StartPump(id, waiting, moved);
                return new(true, "resume", null);
            }

            return said.Refusal is { } refusal
                ? WordsAnswer.Refused(WordsNever.Code(refusal), said.Message)
                : throw new DriverException(said.Message);
        }
        finally
        {
            waiting.Turn.Release();
        }
    }

    private Waiting WaitingFor(string id)
    {
        lock (_gate)
        {
            if (!_waiting.TryGetValue(id, out var waiting)) _waiting[id] = waiting = new Waiting();
            return waiting;
        }
    }

    private void StartPump(string id, Waiting waiting, TaskCompletionSource moved)
    {
        if (waiting.Pumping) return;
        waiting.Pumping = true;
        _ = Task.Run(() => PumpAsync(id, waiting, moved));
    }

    /// <summary>
    /// Held words, tried each time the record moves here or a slow look passes, until the record keeps them all. A word the
    /// service refuses for good is said in the conversation; one it could not answer for is tried again.
    /// </summary>
    private async Task PumpAsync(string id, Waiting waiting, TaskCompletionSource moved)
    {
        var ct = _stopping.Token;
        try
        {
            while (true)
            {
                await Task.WhenAny(moved.Task, Task.Delay(Poll, ct)).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    lock (_gate) moved = waiting.Moved;
                    // HANDWORDS1: while its conversation is being handed over or moved the words wait; once it went on in another,
                    // they go there, never onto the record that ended.
                    var (waits, next) = await WhereAsync(id, ct).ConfigureAwait(false);
                    while (!waits && waiting.Words.TryPeek(out var word))
                    {
                        if (next is not null)
                        {
                            if (!await PassAsync(id, next, word, ct).ConfigureAwait(false)) break;
                            Taken(waiting);
                            continue;
                        }

                        if (_loop.Service is not { } service) break;
                        SayAnswer said;
                        try
                        {
                            said = await service.SayAsync(id, word.Text, word.Files, ct).ConfigureAwait(false);
                        }
                        catch (Exception error) when (Unanswered(error) && !ct.IsCancellationRequested)
                        {
                            break;
                        }

                        if (said.Refusal == "running") break;
                        // Off the file before it is shown: the record has it now, and a restart must not say it twice.
                        Taken(waiting);
                        if (said.Kept) Show(id, said, word);
                        else Lost(id, said);
                    }

                    if (waiting.Words.Count == 0)
                    {
                        waiting.Pumping = false;
                        return;
                    }
                }
                finally
                {
                    waiting.Turn.Release();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The loop is closing: what is held stays in its file for the next shell to try (MSG1d4).
        }
    }

    /// <summary>
    /// Where held words go now (HANDWORDS1): nowhere yet while the record's conversation is handed over or moved (its box stands), or
    /// where the service did not answer; to the conversation it went on in where one did; else to the record itself, as before.
    /// </summary>
    private async Task<(bool Waits, string? Next)> WhereAsync(string id, CancellationToken ct)
    {
        try
        {
            if (await ReadAsync(id, ct).ConfigureAwait(false) is not { Record: { } record } read) return (false, null);
            return TakeWaits(record) ? (true, null) : (false, read.Next);
        }
        catch (Exception error) when (Unanswered(error) && !ct.IsCancellationRequested)
        {
            // Nothing known is nothing claimed: the words are tried again at the record's next move, or the next look.
            return (true, null);
        }
    }

    /// <summary>
    /// A held word said to the conversation its record went on in (HANDWORDS1), at the door it was first said at, with its files read
    /// back from where they were kept: that conversation's turns take it where it runs here, else its record keeps it and it is shown
    /// there, as any word said to it would be. True where it is off this record's queue: taken, or refused and said so here. False
    /// where nothing could answer now, so it is tried again.
    /// </summary>
    private async Task<bool> PassAsync(string id, string next, Word word, CancellationToken ct)
    {
        WordsAnswer? said;
        try
        {
            said = await SayAsync(next, word.Text, Uploads(id, word.Files), preface: null, word.Door, ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            // The service did not answer for the conversation it went on in (its files were taken once already): tried again.
            return false;
        }

        if (said is null) return false;
        if (said.Running) KeepOnAsk(next, word.Text);
        if (!said.Sent) Lost(id, said.Why, said.Message);
        return true;
    }

    /// <summary>A held word's files, read back from where its record kept them; one gone since is left out, as a resumed run's is (MSG1d3).</summary>
    private IReadOnlyList<ChatUpload> Uploads(string id, IReadOnlyList<string> names)
    {
        var uploads = new List<ChatUpload>();
        foreach (var name in names)
        {
            try
            {
                if (ChatFiles.Find(_loop.Home, id, name) is { } kept) uploads.Add(new ChatUpload(name, File.ReadAllBytes(kept.Path)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held open or gone since it was kept: the words still go.
            }
        }

        return uploads;
    }

    /// <summary>A word held behind the others for this record, and the file written with it (MSG1d4).</summary>
    private void Hold(Waiting waiting, Word word)
    {
        lock (_gate)
        {
            waiting.Words.Enqueue(word);
            Save();
        }
    }

    /// <summary>The first word held for this record, kept or refused for good, off the queue and the file (MSG1d4).</summary>
    private void Taken(Waiting waiting)
    {
        lock (_gate)
        {
            waiting.Words.Dequeue();
            Save();
        }
    }

    /// <summary>
    /// Every word held, written whole to <see cref="HeldPath"/> beside and renamed, LF, as every file under the home is; the
    /// file removed once nothing is held (MSG1d4). Called under the gate, where every queue is changed, so the file is never
    /// written from a queue mid-change. A write that fails costs only the words' surviving a restart: they are still held here.
    /// </summary>
    private void Save()
    {
        var path = HeldPath(_loop.Home);
        try
        {
            var held = _waiting
                .SelectMany(pair => pair.Value.Words.Select(word => (Session: pair.Key, Word: word)))
                .ToList();
            if (held.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                HomeWrites.Mine(path);
                return;
            }

            JsonFile.Write(path, writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("held");
                foreach (var (session, word) in held)
                {
                    writer.WriteStartObject();
                    writer.WriteString("session", session);
                    writer.WriteString("text", word.Text);
                    writer.WriteStartArray("files");
                    foreach (var name in word.Files) writer.WriteStringValue(name);
                    writer.WriteEndArray();
                    writer.WriteString("door", word.Door);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            // The loop's own write: its watch of the file hears it and wakes no look (FABRIC-EVENT4).
            HomeWrites.Mine(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Still held in this process; only a restart before the record keeps them would lose them, as before MSG1d4.
        }
    }

    /// <summary>
    /// The words an earlier shell held and was closed with (MSG1d4), read back as this one starts: each queued for its record
    /// in the order said, and tried at once, then as any held words are, since the record may well have ended while no shell
    /// ran. A file that does not read holds nothing, and the next word held replaces it.
    /// </summary>
    private void Restore()
    {
        List<(string Session, Word Word)> held;
        try
        {
            held = ReadHeld(HeldPath(_loop.Home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return;
        }

        foreach (var words in held.GroupBy(each => each.Session, StringComparer.OrdinalIgnoreCase))
        {
            var waiting = WaitingFor(words.Key);
            lock (_gate)
            {
                foreach (var (_, word) in words) waiting.Words.Enqueue(word);
            }

            var now = Signal();
            now.SetResult();
            StartPump(words.Key, waiting, now);
        }
    }

    /// <summary>The held words in a file <see cref="Save"/> wrote, in order; an entry missing its session, text or door is skipped.</summary>
    private static List<(string Session, Word Word)> ReadHeld(string path)
    {
        if (!File.Exists(path)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("held", out var held) || held.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var words = new List<(string Session, Word Word)>();
        foreach (var entry in held.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (Text(entry, "session") is not { Length: > 0 } session || Text(entry, "text") is not { } text
                || Text(entry, "door") is not { Length: > 0 } door)
            {
                continue;
            }

            IReadOnlyList<string> files = entry.TryGetProperty("files", out var named) && named.ValueKind == JsonValueKind.Array
                ? [.. named.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
                : [];
            words.Add((session, new Word(text, files, door)));
        }

        return words;
    }

    /// <summary>
    /// The kept words in the session's conversation, the moment the record keeps them (D137 §3.1): the person's, under the
    /// id the record gave them, with the reach <c>resume</c> and the door; then the loop is nudged so a look takes them up
    /// now rather than at its next interval.
    /// </summary>
    private void Show(string id, SayAnswer said, Word word)
    {
        if (said.Word is { } kept)
        {
            _loop.Output.Append(id, $"— the person said, which the same session goes on with: {kept.Text}");
            _loop.Events.Keep(id, new SessionEvent
            {
                Kind = SessionEventKind.User,
                // SUPERVISOR1a-i (the supervisor design §6.1): shown as whose the record kept it, never the person's by default.
                Origin = kept.Persons ? "person" : "target",
                From = kept.Daoris ? DaorisWords.From : kept.Supervisors ? SupervisorHanded.From : kept.From,
                Id = kept.Id,
                Text = kept.Text,
                Files = kept.Files.Count > 0 ? kept.Files : null,
                Reaches = "resume",
                Door = word.Door,
                // PARKVIEW1 (Fable's read, Q4): pressed as an answer of its park's question, shown as that answer chosen.
                Choice = word.Choice is { } choice && kept.Text.StartsWith(choice, StringComparison.Ordinal) ? choice : null,
            }, line => _loop.Output.Append(id, line));
            // The record's waiting words are the queue's now (SAIDBACK1b): told again, so the box's list offers this one too.
            Tell(id);
        }

        _loop.Said(id);
    }

    /// <summary>Held words the record would not keep once it ended, said where the person wrote them, never dropped silently.</summary>
    private void Lost(string id, SayAnswer said) =>
        Lost(id, said.Refusal is { } refusal ? WordsNever.Code(refusal) : null, said.Message);

    /// <summary>Held words nothing would take, by the refusal's code where there is one, said where the person wrote them.</summary>
    private void Lost(string id, string? why, string? message)
    {
        // Both ways: the `Why` makes the page say it cannot go on here (it wins where it reads), and the parts word the line
        // by code where there is none or the page does not know it (CONVNOTE2d2).
        var line = WordsNotes.NotKept(message ?? $"nothing takes them ({why ?? "refused"}).");
        _loop.Output.Append(id, line.Note);
        _loop.Events.Keep(id, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts, Why = why }, null);
    }

    /// <summary>
    /// The records, and this one among them with its quest's last session here and the conversation it went on in; null with no
    /// service yet.
    /// </summary>
    private async Task<Read?> ReadAsync(string id, CancellationToken ct)
    {
        if (_loop.Service is not { } service) return null;
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var records = SessionRecords.Parse(json);
        var record = records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        return new Read(record, WordsNever.LastHere(json, record?.Quest), WordsNever.ContinuedIn(records, id), json);
    }

    /// <summary>
    /// A record as read, the session its quest last ran in here (D79's reading), and the conversation it went on in (HANDWORDS1),
    /// each null for none; and the records' answer it was read from, whose words waiting only the queue reads (SAIDBACK1b).
    /// </summary>
    private sealed record Read(SessionRecord? Record, string? Last, string? Next, string Json);

    /// <summary>
    /// The person's words waiting on this record (SAIDBACK1b, D137 §2.4), from the records' answer, in the order said. None on a
    /// record that runs again (queued, starting, working), whose run is already taking them; and never another agent's words
    /// waiting there, a second opinion's findings (<c>by</c>) or a helper's answer (<c>helper</c>), which are not the person's to
    /// take back or reword. A word without its id or its words is skipped, as the driver's client skips it.
    /// </summary>
    internal static IReadOnlyList<WaitingWord> WaitingOn(string json, string id)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        foreach (var record in document.RootElement.EnumerateArray())
        {
            if (!string.Equals(Text(record, "id"), id, StringComparison.Ordinal)) continue;
            if (Text(record, "state") is "queued" or "starting" or "working") return [];
            return
            [
                // The person's alone: never another agent's (by, helper), nor Daoris's own words (COWORK1b's from).
                .. Items(record, "said")
                    .Where(word => NonEmpty(word, "by") is null && NonEmpty(word, "helper") is null && NonEmpty(word, "from") is null)
                    .Select(word => NonEmpty(word, "id") is { } said && Text(word, "text") is { } text
                        ? new WaitingWord(said, text, Strings(word, "files"))
                        : null)
                    .OfType<WaitingWord>(),
            ];
        }

        return [];
    }

    /// <summary>A driven inbox's reach in the wire's spelling, as the record's events spell it (D136).</summary>
    private static string? Spelled(DrivenReach? reach) => reach switch
    {
        DrivenReach.NextStep => "next-step",
        DrivenReach.TurnEnd => "turn-end",
        _ => null,
    };

    private static bool Unanswered(Exception error) =>
        error is HttpRequestException or DriverException or JsonException or OperationCanceledException or TaskCanceledException;

    /// <summary>Stop trying held words: the loop is closing. The source is cancelled, never disposed, so a pump still starting reads it.</summary>
    public void Dispose()
    {
        _loop.Processes.KeptLeft -= OnKeptLeft;
        _stopping.Cancel();
    }
}
