using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The person's words to a session of this machine's, whatever its state: a running session hears them at
/// its door as before; one that parked or ended has them kept on its record by the service's say door
/// and the loop is nudged, so the driver goes on with them; and words said as a session winds up are held until its
/// record ends, then kept the same way, never refused.
/// </summary>
/// <remarks>
/// <para><b>What never goes on is said by a code</b>, judged from the record before anything is posted by the
/// driver library's one table, <see cref="WordsNever"/>, which the terminal's <c>sessions say</c> judges by too: a
/// teammate's record, an intake, a session that stood down, Ask Daoris's own conversation, a session whose quest went
/// on in a later session here, and a conversation handed over or moved, which names the one it went on in. The
/// service judges again as it keeps them, and its refusal is the answer.</para>
///
/// <para><b>Words said while a conversation is handed over or moved</b> wait here as
/// words said as a session winds up do, from the take's ask until it is told over (<see cref="OnTakeEnded"/>), never kept on
/// the record that is ending; then they go to the conversation that continues it.</para>
///
/// <para><b>Both doors.</b> The screen's box says words through <see cref="SayAsync"/>; a terminal's <c>sessions say</c>
/// reaches the shell's request watch, which hands it to <see cref="HoldAsync"/>, the same judgement at the door
/// <c>terminal</c>.</para>
///
/// <para><b>Shown at once, under the record's id.</b> The words are written to the session's conversation the moment the
/// say door keeps them, as the person's, with the reach <c>resume</c>, the id the record gave them and the door they were
/// said at: the resumed run says them again under the same id where it took them, and the machine log's
/// <c>session.reopened</c> names the door. Words held while a session winds up have no id until the record keeps
/// them, so they are shown once it does.</para>
///
/// <para><b>Held words wait in this process</b>, for the record to move through this machine's client or for a slow look,
/// whichever comes first, <b>and in a small file under the home</b> (<see cref="HeldPath"/>), written whole beside
/// and renamed each time a word is held or leaves. The page is told they are held, so a restart in that moment must not
/// lose them: the next shell's judge reads them back as it starts and tries them as before.</para>
///
/// <para><b>Against a host whose marks keep the moving open and the head nothing is held</b>:
/// a word for a record that winds up, or whose conversation move stands, is kept on its <c>said</c> at once, asked <c>ending</c>; the
/// move's open moves it and the say door refuses the old part a word after it, naming the head. The file is read once as the shell starts, its
/// words kept so, and written only for a word such a host still refused, one from before the row.</para>
/// </remarks>
public sealed class SessionWords : IDisposable
{
    private readonly DriverLoop _loop;
    private readonly WordsQueueEdits _edits;
    private readonly WordsShown _shown;
    private readonly WordsReach _reach;
    private readonly HeldWordsQueue _held;

    /// <summary>The loop's judge; it reads back the words an earlier shell held when it closed.</summary>
    public SessionWords(DriverLoop loop)
    {
        _loop = loop;
        _edits = new WordsQueueEdits(loop, Tell);
        _shown = new WordsShown(loop, Tell);
        _reach = new WordsReach(loop, TakeWaits);
        _held = new HeldWordsQueue(this, loop, _shown, _stopping.Token);
        _loop.Processes.KeptLeft += OnKeptLeft;
        _held.Restore();
    }

    /// <summary>
    /// One key per press of the person's, where the door that took the words named none:
    /// the record keeps one word per key, so a press that arrives twice keeps one.
    /// </summary>
    private static string PressKey() => $"press/{Guid.NewGuid():N}";

    /// <summary>
    /// Words kept for a driven session's run that stay on its record as its inbox closed under them: its door failed, or the
    /// shell is shutting down. Shown again under the ids they were shown by, as waiting for the session to go on (the reach
    /// <c>resume</c>), so the page never says they were lost; and the loop is nudged, so a look goes on with them.
    /// </summary>
    private void OnKeptLeft(string id, IReadOnlyList<IAgentWord> words)
    {
        foreach (var delivered in words)
        {
            var word = ChatMessage.FromAgent(delivered);
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
    /// Where words held while a session winds up are kept across a restart: each in the order said, with its session,
    /// its text, the names its files are kept under and the door it is said at, and, where it has them, its producer's key, its level
    /// and its display choice. Absent while nothing is held.
    /// </summary>
    /// <remarks>Its place is the driver library's (<see cref="HeldWordsFile"/>), whose one <c>Forget</c> takes a deleted session's words out through the queue.</remarks>
    public static string HeldPath(string home) => HeldWordsFile.PathOf(home);

    /// <summary>The longest held words wait for a move before they are tried again: a move made by another client says nothing here.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();

    /// <summary>
    /// A record moved through this machine's client (<see cref="ServiceClient.Moved"/>): words held for it are tried now, and
    /// what a word said to it now would do is told again, since a move is what changes it.
    /// </summary>
    public void OnMoved(SessionMoved moved)
    {
        _held.Wake(moved.Session);
        // A record a word is kept on for its next part has ended, so it goes on with it.
        if (moved.State is "completed" or "declined" or "failed" or "stopped") GoesOn(moved.Session, move: false);
    }

    /// <summary>
    /// A conversation's hand-over, curve move or limit move has been taken (<see cref="ChatRunner.TakeEnded"/>), its move
    /// ended in the store: words held for it during the take go on now, to the conversation that continues it, and what a word said to it
    /// now would do is told again, since it went on elsewhere. A word kept on its record meanwhile goes with the move's open, or,
    /// where the move ended before it, waits there for the record's go-on.
    /// </summary>
    public void OnTakeEnded(string session)
    {
        _held.Wake(session);
        GoesOn(session, move: true);
    }

    /// <summary>
    /// A record opened through this machine's client (<see cref="ServiceClient.Opened"/>): a driven session's quest went on in
    /// it, so its earlier sessions here take no words now (<c>superseded</c>), and theirs is told again, since
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
            catch (Exception error) when (WordsRead.Unanswered(error) || error is ObjectDisposedException)
            {
                // Nothing told is nothing claimed: the page keeps what it is told, and a word said is refused. The
                // loop closing under it disposes the client, which says nothing either.
            }
        });
    }

    /// <summary>
    /// What a word said now to a session would do, told again: the shell writes it on
    /// <c>SESSION_QUEUED</c>, so the page follows the box it offers without asking again on each move.
    /// </summary>
    public event Action<string, WordsAnswer>? Reached;

    // The sessions whose reach is being read to be told, each with whether it is asked again meanwhile: one read at a time
    // per session, so what is told last is what is read last, and a burst of moves costs one more read, not one each.
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
            catch (Exception error)
            {
                // The loop is closing, or its service went away: the next move tells it again. Any other throw, a hearer's among them,
                // is said and no more: it must not leave this session marked as being told, which no later move would clear.
                if (!WordsRead.Unanswered(error) && error is not ObjectDisposedException)
                {
                    _loop.Log?.Error("error", ("where", "a session's reach told"), ("message", error.Message));
                }
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

    /// <summary>What a word said now to a session that runs here would do, known without reading its record; null where only the record can say.</summary>
    public WordsAnswer? ReachHere(string id) => _reach.Here(id);

    /// <summary>What a word said now to this session would do (<c>SESSION_QUEUE</c>), for the page to offer the box by.</summary>
    public Task<WordsAnswer> ReachAsync(string id, CancellationToken ct) => _reach.Async(id, ct);

    /// <summary>
    /// The person's words to this session (<c>SESSION_INPUT</c>): held at a running session's door, kept on a
    /// parked or ended record for it to go on with, or held until a winding-up record ends. Null when the loop's service is
    /// not answering yet and nothing here runs the session, so nothing can keep them.
    /// </summary>
    /// <param name="door">Where they were said, <c>screen</c> or <c>terminal</c>: the machine log names it.</param>
    /// <param name="level">
    /// When the person asked the words to reach a working session: urgent by default, at its next step where
    /// its door takes words then; ordinary at its turn's end; <c>now</c>, the person's *Send now*, carried out by its door
    /// (<see cref="DrivenInbox.HoldNowAsync"/>, <see cref="ChatRunner.SayNowAsync"/>). No level stops a turn. One that parked or
    /// ended has no turn running, and goes on with the words as before.
    /// </param>
    /// <exception cref="DriverException">
    /// A session this machine runs that takes no words at all (an intake), in the driver's words; files the person
    /// attached that a conversation cannot carry; or a service whose answer could not be read.
    /// </exception>
    /// <param name="choice">
    /// The answer of a park's question the words begin with, as a press composes them: the conversation
    /// shows them under it, and the resumed run is handed them as that answer chosen. Null for every other word.
    /// </param>
    public Task<WordsAnswer?> SayAsync(
        string id, string text, IReadOnlyList<ChatUpload> files, string? preface, string door, CancellationToken ct,
        MessageLevel level = MessageLevel.Urgent, string? key = null, string? choice = null, string? question = null) =>
        SayAsync(id, text, files, preface, door, ct, level, key, choice, question, new HeadWalk(id));

    internal async Task<WordsAnswer?> SayAsync(
        string id, string text, IReadOnlyList<ChatUpload> files, string? preface, string door, CancellationToken ct,
        MessageLevel level, string? key, string? choice, string? question, HeadWalk walk)
    {
        // A conversation whose agent refused its choice at the open is answered by its replacement, and while
        // that opens a word joins the words moving to it, so it reaches the replacement once.
        if (_loop.Chat is { } replaced)
        {
            id = replaced.CurrentOf(id);
            if (replaced.Replacing(id) && replaced.Say(id, text, files, preface)) return new(true, null, null) { Running = true };
        }

        var now = level == MessageLevel.Now;
        // A driven session that hears words during its run (the native door's run): kept on its record
        // first, under the press's key, then held by its one dispatcher under the record's id.
        if (_loop.Processes.InboxOf(id) is { } inbox)
        {
            var inboxKey = key ?? PressKey();
            var forRun = await inbox
                .KeepOrHoldAsync(
                    _loop.Service is { } service ? new SaidDoor(service, id) : null,
                    new ChatMessage(text, []) { Level = level, Key = inboxKey }, ct)
                .ConfigureAwait(false);
            // The record is no longer its line's head, so its inbox held nothing: said at the head,
            // under the key this say is kept under.
            if (forRun is { Outcome: InboxKeep.Continued, Head: { } head })
            {
                return await OnAsync(head, text, files, preface, door, ct, level, inboxKey, choice, question, walk).ConfigureAwait(false);
            }

            // A retry the record answered settled is a word held or gone already: nothing is held,
            // told or kept on the ask again, and no word is shown.
            if (forRun is { Outcome: InboxKeep.Held, Settled: true }) return new(true, null, null);
            switch (forRun)
            {
                case { Outcome: InboxKeep.Held, Now: { } sentNow }:
                    return new(true, sentNow.Reaches, sentNow.Why) { Running = true, Now = sentNow.Now, Word = forRun.Said?.Word?.Id };
                case { Outcome: InboxKeep.Held }:
                    return new(true, WordsReach.Spelled(forRun.Reach), null) { Running = true, Word = forRun.Said?.Word?.Id };
            }
        }

        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        // Ask Daoris's conversation placed and still starting: its words are kept on its record at once, where its door takes them
        // from the moment it opens, and wait as its queue, opening. Nothing holds them in this process.
        WordsAnswer? startAnswer = null;
        string? startHead = null;
        var starting = false;
        using (var keeping = await _loop.HelpStarting.KeepingAsync(id, ct).ConfigureAwait(false))
        {
            if (keeping is not null)
            {
                starting = true;
                var startFiles = files.Count > 0 ? ChatFiles.Keep(_loop.Home, id, files) : [];
                var startWord = new HeldSay(text, [.. startFiles.Select(file => file.Name)], door, choice, key ?? PressKey(), level, question);
                (startAnswer, startHead) = await KeepOrHoldAsync(id, startWord, ct, running: true, moving: true).ConfigureAwait(false);
            }
        }

        if (starting)
        {
            Tell(id);
            return startHead is { } startOnward
                ? await OnAsync(startOnward, text, files, preface, door, ct, level, key, choice, question, walk).ConfigureAwait(false)
                : startAnswer;
        }

        var running = _loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase);
        var refuses = _loop.Processes.RefusesInput(id);
        if (running && refuses is null && _loop.Chat is { } chat)
        {
            // A conversation this machine runs: its turns take the words, into the running turn at its next step where
            // its agent takes them then, when the running one ends elsewhere, or now; its runner says which.
            // Kept on its record first, under the press's key, and its turns are their one dispatcher.
            var chatKey = key ?? PressKey();
            if (await chat.SayKeptAsync(id, text, files, preface, level, chatKey, ct).ConfigureAwait(false) is { } chatSaid)
            {
                // Its record is no longer its line's head, so its turns queued nothing: said at the head, under the same key.
                if (chatSaid.Head is { } head)
                {
                    return await OnAsync(head, text, files, preface, door, ct, level, chatKey, choice, question, walk).ConfigureAwait(false);
                }

                // Row p: settled, a retry of a word held or gone already: not running, so nothing is kept on the ask again.
                if (chatSaid.Settled) return new(true, null, null);
                return chatSaid.Now is { } sentNow
                    ? new(true, sentNow.Reaches, sentNow.Why) { Running = true, Now = sentNow.Now, Word = chatSaid.Word }
                    : new(true, chatSaid.Reaches, null) { Running = true, Word = chatSaid.Word };
            }
        }

        WordsRead? read;
        try
        {
            read = await WordsRead.OfAsync(_loop, id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (WordsRead.Unanswered(error))
        {
            throw new DriverException($"the service did not answer for session `{id}`, so your words were not kept: {error.Message}");
        }

        // 🔴 Running here and taking no person's line, with nothing to say it is a quest's session: refused in the driver's
        // words. An intake takes nothing; false would tell a stale page it ended when it is running.
        if (running && refuses is not null && read?.Record is not { Ask: null })
        {
            throw new DriverException(refuses);
        }

        if (read is null) return null;
        if (read.Record is not { } record) return WordsAnswer.Refused(WordsNever.NotFound);

        // A conversation being handed over or moved, its box kept until the take is done.
        // Its words are never kept on the record that is ending, which would restart it in the tree its successor holds: they wait
        // here, and go to the conversation that continues it once the take is over (PumpAsync). Judged before the nevers, since the
        // successor's record opens before the take is done.
        var holding = TakeWaits(record);
        if (!holding && (await read.JudgedAsync(_loop, ct).ConfigureAwait(false)).Never(record) is { } never) return never;

        // Kept as a conversation keeps what is attached, so the names on the record name files that exist.
        var kept = files.Count > 0 ? ChatFiles.Keep(_loop.Home, id, files) : [];
        var word = new HeldSay(text, [.. kept.Select(file => file.Name)], door, choice, key ?? PressKey(), level, question);
        // A host whose marks keep the moving open and the head keeps the word on the record the
        // move leaves, which its open moves, or refuses it naming the head; a host without them, today's hold.
        var keeps = await KeepsAsync(ct).ConfigureAwait(false);
        if (holding && !keeps) return await HoldForTakeAsync(id, word, ct).ConfigureAwait(false);

        var (answer, at) = await KeepOrHoldAsync(id, word, ct, keeps, moving: holding).ConfigureAwait(false);
        // The record is no longer its line's head: said at the head, once this record's held words let go.
        return at is { } onward ? await OnAsync(onward, text, files, preface, door, ct, level, key, choice, question, walk).ConfigureAwait(false) : answer;
    }

    /// <summary>
    /// The words said again at their line's head, as they were said first: their key,
    /// level, files and display choice, through the shell's whole judgement there, the head's inbox, its turns or its record. The answer names the
    /// head that took them. A walk that comes back to a record it asked, or passes its bound, ends refused with the driver's sentence.
    /// </summary>
    private async Task<WordsAnswer?> OnAsync(
        string head, string text, IReadOnlyList<ChatUpload> files, string? preface, string door, CancellationToken ct, MessageLevel level,
        string? key, string? choice, string? question, HeadWalk walk)
    {
        if (!walk.On(head)) return WordsAnswer.Refused(WordsNever.Continued, walk.Ended(head));
        var said = await SayAsync(head, text, files, preface, door, ct, level, key, choice, question, walk).ConfigureAwait(false);
        return said is { Sent: true, At: null } ? said with { At = head } : said;
    }

    /// <summary>
    /// Take back one of the person's words waiting on a parked or ended record, or reword it: what
    /// <c>SESSION_QUEUE_EDIT</c> falls back to once nothing here holds the word. The queue is told again whatever the service
    /// answers, so a list the page showed stale is read anew.
    /// </summary>
    public Task<QueueEdit> TakeBackAsync(string id, string word, string? text, CancellationToken ct) =>
        _edits.TakeBackAsync(id, word, text, ct);

    /// <summary>Move one of the person's words held on a part's record to the line's head.</summary>
    public Task<MoveAnswer> MoveAsync(string id, string word, string to, CancellationToken ct) =>
        _edits.MoveAsync(id, word, to, ct);

    /// <summary>A terminal's <c>sessions take-back</c>, handed to its request watch as <see cref="SessionRequestWatch.TakeBack"/>.</summary>
    public Task<WordsHeld> HoldTakeBackAsync(SessionRequest request, CancellationToken ct) =>
        _edits.HoldTakeBackAsync(request, ct);

    /// <summary>
    /// Whether a conversation's hand-over or curve move waits or is being taken: its box is kept from the ask until the
    /// take is over, however it came out. A quest's session's box is the driver's look's, which carries its words on itself.
    /// </summary>
    internal bool TakeWaits(SessionRecord record) =>
        record is { Kind: "chat", Teammate: false } && TakeStanding(record.Id);

    /// <summary>
    /// Whether a conversation move, the hand-over, the curve's or the limit move, stands for a record and has not yet put its words on the line's
    /// head: the shell hands it a reading of the move record, which this names no type of. Never true for a
    /// move held for the person or ended, whose line has gone on and whose words follow its head; false until the shell hands one.
    /// </summary>
    public Func<string, bool> TakeStanding { get; set; } = _ => false;

    /// <summary>
    /// A word said while its conversation is handed over or moved, held behind any already held for it and tried once
    /// the take is told over (<see cref="OnTakeEnded"/>), or at once where the box went while it is held.
    /// </summary>
    private async Task<WordsAnswer> HoldForTakeAsync(string id, HeldSay word, CancellationToken ct)
    {
        var waiting = _held.WaitingFor(id);
        await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var moved = _held.MovedOf(waiting);
            _held.Hold(waiting, word);
            _held.StartPump(id, waiting, moved);
        }
        finally
        {
            waiting.Turn.Release();
        }

        // Taken while held: no end will be told for it now.
        if (!TakeStanding(id)) _held.Wake(id);
        return new(true, "resume", null);
    }

    /// <summary>
    /// What became of a park once a go-ahead it asked has been answered at the go-ahead's own door with <c>goesOn</c>:
    /// the service judged there, as for the ask's page and the terminal, whether none of the session's go-aheads
    /// is open any more, and kept the park's blank answer where none is. That decision is read here off the record
    /// and never judged again: a park now answered goes on, and the loop is nudged so a look takes it up now; one still
    /// parked with no answer waits on the person.
    /// </summary>
    /// <returns>
    /// The park as <c>SESSION_INPUT</c> words a reach, and whether it still waits on the person: a go-ahead it asked still
    /// open, or, where none is, its own answer, since a changed answer wakes nothing. A record that is not parked,
    /// or never goes on, waits on nothing.
    /// </returns>
    /// <exception cref="DriverException">The loop's service is not answering yet, or did not answer for the record.</exception>
    public async Task<(WordsAnswer Park, bool Waits)> AfterGoAheadAsync(string id, CancellationToken ct)
    {
        if (id.Contains('/')) return (WordsAnswer.Refused(WordsNever.Teammate), false);

        WordsRead? read;
        try
        {
            read = await WordsRead.OfAsync(_loop, id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (WordsRead.Unanswered(error))
        {
            throw new DriverException(
                $"the service did not answer for session `{id}`, so whether it goes on is not known; its go-ahead is answered: {error.Message}");
        }

        if (read is null) throw new DriverException(NotUp);
        if (read.Record is not { } record) return (WordsAnswer.Refused(WordsNever.NotFound), false);
        if (read.Never(record) is { } never) return (never, false);
        if (record.State != "awaiting-person") return (new(false, null, null), false);
        if (!record.Answered) return (new(false, null, null), true);

        _loop.Nudge();
        return (new(true, "resume", null), false);
    }

    /// <summary>
    /// A terminal's words: the shell's half of <c>daoris session say</c>, handed to its request
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
        // The request's own key, so a request read again after a restart keeps one word.
        var said = await SayAsync(
                request.Session, text, Kept(request.Session, request.Files), preface: null, RequestDoor.Terminal, ct, request.Asked,
                SessionRequests.WordKey(request))
            .ConfigureAwait(false);
        if (said is null) return WordsHeld.Failed(NotUp);
        if (said.Running) KeepOnAsk(said.At ?? request.Session, text);
        // A *Send now* is answered whole: what the door did, and why where it did not; any other say in the usual shape.
        // The head that took them, where the record named went on in it, which the verb names.
        if (said.Sent && request.Asked == MessageLevel.Now)
        {
            return new WordsHeld(true, said.Reaches, said.Why) { Word = said.Word, Now = said.Now, Head = said.At };
        }

        if (said.Sent) return new WordsHeld(true, said.Reaches, null) { Word = said.Word, Head = said.At };
        return said.Why is { } why ? WordsHeld.Refused(why, said.Message) : WordsHeld.Failed(said.Message ?? NotUp);
    }

    /// <summary>
    /// A terminal's files, by the names the verb kept them under for the session: read back from where they lie, so
    /// the box's own path keeps them, which finds each already there since a file is kept by its content.
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
    /// What the person said to a running session, kept on the ask its work is for, once the session took
    /// it. Both doors keep them, by the driver library's one rule, <see cref="WordsOnAsk.Keep"/>, which the headless
    /// host's loop keeps them by too: never awaited, so nothing the service answers changes what the person is told.
    /// </summary>
    public void KeepOnAsk(string session, string text) => _ = WordsOnAsk.Keep(_loop.Service, session, text);

    /// <summary>
    /// What a door is told while the loop's service is not answering and nothing here runs the session: nothing could keep
    /// the words yet, which is a moment the person can wait out.
    /// </summary>
    internal const string NotUp = "the driver is still coming up — its service is not answering yet. A moment.";

    /// <summary>
    /// The words to the say door, behind any already held for this record: kept, shown and the loop nudged; held while the
    /// record still runs; or refused by the service's word. Where the record is no longer its line's head, nothing is
    /// kept, held or shown here, and the head is answered instead, for the caller to say them there once this record's turn let go.
    /// </summary>
    /// <param name="keeps">
    /// The host's marks keep the moving open and the head: the say asks <c>ending</c>, so a record that winds up or moves keeps the
    /// word for its next part; one refused <c>running</c> all the same, by a host before the row, is held as before.
    /// </param>
    /// <param name="moving">A conversation move stands for the record: the word kept is the move's, which shows it at its destination, never here.</param>
    private async Task<(WordsAnswer? Answer, string? Head)> KeepOrHoldAsync(
        string id, HeldSay word, CancellationToken ct, bool keeps = false, bool moving = false, bool running = false)
    {
        var service = _loop.Service ?? throw new DriverException(NotUp);
        var waiting = _held.WaitingFor(id);
        await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Behind the words already held, so the record keeps them in the order said.
            if (waiting.Words.Count > 0)
            {
                _held.Hold(waiting, word);
                return (new(true, "resume", null), null);
            }

            // Taken before the say, so a move while it is on its way is not missed.
            var moved = _held.MovedOf(waiting);
            SayAnswer said;
            try
            {
                said = await service.SayAsync(
                    id, word.Text, word.Files, ct, key: word.Key, level: running ? MessageLevels.Spelled(word.Level) : null, running: running,
                    ending: keeps, question: word.Question).ConfigureAwait(false);
            }
            catch (Exception error) when (WordsRead.Unanswered(error))
            {
                throw new DriverException($"the service did not answer for session `{id}`, so your words were not kept: {error.Message}");
            }

            // The record is no longer its line's head, which the answer names.
            if (said.Head is { } head) return (null, head);

            if (said.Settled is not null)
            {
                // Row p: the record answered the retry settled, its word left a record of the line: nothing to show and no word.
                return (new(true, "resume", null), null);
            }

            if (said.Kept)
            {
                Kept(id, said, word, keeps, moving);
                return (new(true, "resume", null) { Word = said.Word?.Id }, null);
            }

            if (said.Refusal == "running")
            {
                // It winds up, or runs where this machine's routes do not reach: held for its record to end.
                _held.Hold(waiting, word);
                _held.StartPump(id, waiting, moved);
                if (moving && !TakeStanding(id)) _ = Task.Run(() => _held.Wake(id));
                return (new(true, "resume", null), null);
            }

            return said.Refusal is { } refusal
                ? (WordsAnswer.Refused(WordsNever.Code(refusal), said.Message), null)
                : throw new DriverException(said.Message);
        }
        finally
        {
            waiting.Turn.Release();
        }
    }

    /// <summary>Whether the loop's host keeps words for a record that winds up or moves; false with no service yet.</summary>
    internal async Task<bool> KeepsAsync(CancellationToken ct) =>
        _loop.Service is { } service && await service.Opens.KeepsAsync(ct).ConfigureAwait(false);

    // The records this shell kept a word on for their next part, which the record's end hands to the go-on, and those it kept one
    // on while their move stood, which the move's end does.
    private readonly HashSet<string> _forNext = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _forMove = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A word the record kept: shown under its id and the loop nudged, as before; but a word kept for a moving record is the move's, which its open
    /// carries and its delivery shows at its destination, so nothing is shown here. A word kept for a record's next part is noted, so
    /// the record's end, or its move's, hands it to the go-on (<see cref="OnMoved"/>, <see cref="OnTakeEnded"/>).
    /// </summary>
    internal void Kept(string id, SayAnswer said, HeldSay word, bool keeps, bool moving)
    {
        if (keeps)
        {
            lock (_gate) (moving ? _forMove : _forNext).Add(id);
        }

        if (!moving) _shown.Kept(id, said, word);
    }

    /// <summary>
    /// A record this shell kept a word on for its next part ended, or its move is told over: an ended conversation goes on with what
    /// waits on it, as a word kept on it then would have; one the move's open left, or that went on elsewhere, its go-on refuses as it stands.
    /// </summary>
    private void GoesOn(string id, bool move)
    {
        lock (_gate)
        {
            if (!(move ? _forMove : _forNext).Remove(id)) return;
        }

        _loop.Chat?.AnswerGoesOn(id);
    }

    /// <summary>Stop trying held words: the loop is closing. The source is cancelled, never disposed, so a pump still starting reads it.</summary>
    public void Dispose()
    {
        _loop.Processes.KeptLeft -= OnKeptLeft;
        _stopping.Cancel();
        _held.Dispose();
    }
}
