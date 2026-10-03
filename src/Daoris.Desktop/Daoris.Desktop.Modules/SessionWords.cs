using System.Text.Json;
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
/// <c>help</c>, <c>superseded</c>, <c>not-found</c> or <c>no-words</c>. Null where they are taken or held.
/// </param>
public sealed record WordsAnswer(bool Sent, string? Reaches, string? Why)
{
    /// <summary>Whether the words reached a running session's own door, which keeps them on its ask only as it takes them.</summary>
    public bool Running { get; init; }

    /// <summary>
    /// The id the record gave words it kept, which its events say again where the session took them: what a terminal's say
    /// follows to tell whether the same session took them (MSG1e2). Null where nothing kept them yet.
    /// </summary>
    public string? Word { get; init; }

    /// <summary>The service's sentence beside its refusal, which a terminal prints for a code it does not word itself.</summary>
    public string? Message { get; init; }

    /// <summary>Nothing takes them, for this reason.</summary>
    public static WordsAnswer Refused(string why, string? message = null) => new(false, null, why) { Message = message };
}

/// <summary>
/// The person's words to a session of this machine's, whatever its state (MSG1d, D137 §2): a running session hears them at
/// its door as before (D90, D136); one that parked or ended has them kept on its record by the service's say door (MSG1a)
/// and the loop is nudged, so the driver goes on with them (MSG1b); and words said as a session winds up are held until its
/// record ends, then kept the same way, never refused.
/// </summary>
/// <remarks>
/// <para><b>What never goes on is said by a code</b> (D137 §2.2), judged from the record before anything is posted by the
/// driver library's one table, <see cref="WordsNever"/>, which the terminal's <c>sessions say</c> judges by too: a
/// teammate's record, an intake, a session that stood down, Ask Daoris's own conversation, and a session whose quest went
/// on in a later session here. The service judges again as it keeps them, and its refusal is the answer.</para>
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
        Restore();
    }

    /// <summary>
    /// Where words held while a session winds up are kept across a restart (MSG1d4): each in the order said, with its session,
    /// its text, the names its files are kept under and the door it was said at. Absent while nothing is held.
    /// </summary>
    public static string HeldPath(string home) => Path.Combine(home, "sessions", "held-words.json");

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

    /// <summary>One word as it goes to the say door: its text, the names of its files, and the door it was said at.</summary>
    private sealed record Word(string Text, IReadOnlyList<string> Files, string Door);

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// A record moved through this machine's client (<see cref="ServiceClient.Moved"/>): words held for it are tried now, and
    /// what a word said to it now would do is told again (MSG1f2), since a move is what changes it.
    /// </summary>
    public void OnMoved(SessionMoved moved)
    {
        TaskCompletionSource? told = null;
        lock (_gate)
        {
            if (_waiting.TryGetValue(moved.Session, out var waiting))
            {
                told = waiting.Moved;
                waiting.Moved = Signal();
            }
        }

        told?.TrySetResult();
        Tell(moved.Session);
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
            catch (Exception error) when (Unanswered(error))
            {
                // Nothing told is nothing claimed: the page keeps what it was told, and a word said refuses as before.
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
        if (_loop.Processes.InboxOf(id) is { State.Taking: true } inbox) return new(true, Spelled(inbox.Reach), null);

        if (_loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) && _loop.Processes.RefusesInput(id) is null)
        {
            // A conversation this machine runs (D49 §3): a word goes into the running turn where its agent takes words at its
            // next step (MSG1c), waits for the turn to end elsewhere, or starts one now (CONV4a).
            return new(true, _loop.Chat?.Reach(id), null);
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
            return WordsNever.Judge(record, read.Last) is { } never ? WordsAnswer.Refused(never) : new(true, "resume", null);
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
    /// <exception cref="DriverException">
    /// A session this machine runs that takes no words at all (an intake, INT4h), in the driver's words; files the person
    /// attached that a conversation cannot carry; or a service whose answer could not be read.
    /// </exception>
    public async Task<WordsAnswer?> SayAsync(
        string id, string text, IReadOnlyList<ChatUpload> files, string? preface, string door, CancellationToken ct)
    {
        // A driven session that hears words during its run (SESS3, D136; the native door's run, MSG1b).
        if (_loop.Processes.InboxOf(id) is { } inbox && inbox.Hold(new ChatMessage(text, [])))
        {
            return new(true, Spelled(inbox.Reach), null) { Running = true };
        }

        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        var running = _loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase);
        var refuses = _loop.Processes.RefusesInput(id);
        if (running && refuses is null && _loop.Chat is { } chat)
        {
            // A conversation this machine runs (D49 §3): its turns take the words, into the running turn at its next step where
            // its agent takes them then (MSG1c), when the running one ends elsewhere (CONV4a), or now; its runner says which.
            if (chat.Say(id, text, out var reaches, files, preface)) return new(true, reaches, null) { Running = true };
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
        if (WordsNever.Judge(record, read.Last) is { } never) return WordsAnswer.Refused(never);

        // Kept as a conversation keeps what is attached (CONV4c), so the names on the record name files that exist.
        var kept = files.Count > 0 ? ChatFiles.Keep(_loop.Home, id, files) : [];
        return await KeepOrHoldAsync(id, new Word(text, [.. kept.Select(file => file.Name)], door), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A park answered with no words of the person's (KNOWUSE1a2, D135 §2): the go-ahead they answered on its page is what
    /// they said, so nothing is written as their words here. The park takes the blank answer the service has always kept for
    /// one (ANSWER1b's <i>carry on.</i>) through its answer door, and the loop is nudged. A park already answered goes on with
    /// what it holds, and the resumed session reads every go-ahead it asked as it goes on, so nothing is kept again. A record
    /// that is not parked is left as it is: nothing there waits on the person's answer.
    /// </summary>
    /// <exception cref="DriverException">The loop's service is not answering yet, or did not answer for the record.</exception>
    public async Task<WordsAnswer> AnswerParkAsync(string id, CancellationToken ct)
    {
        if (id.Contains('/')) return WordsAnswer.Refused(WordsNever.Teammate);

        Read? read;
        try
        {
            read = await ReadAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            throw new DriverException($"the service did not answer for session `{id}`, so it was not answered: {error.Message}");
        }

        if (read is null || _loop.Service is not { } service) throw new DriverException(NotUp);
        if (read.Record is not { } record) return WordsAnswer.Refused(WordsNever.NotFound);
        if (WordsNever.Judge(record, read.Last) is { } never) return WordsAnswer.Refused(never);
        if (record.State != "awaiting-person") return new(false, null, null);

        if (!record.Answered)
        {
            var (answered, message) = await service.AnswerSessionAsync(id, answer: null, ct).ConfigureAwait(false);
            if (!answered) return new(false, null, null) { Message = message };
        }

        _loop.Nudge();
        return new(true, "resume", null);
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
        var said = await SayAsync(request.Session, text, Kept(request.Session, request.Files), preface: null, RequestDoor.Terminal, ct)
            .ConfigureAwait(false);
        if (said is null) return WordsHeld.Failed(NotUp);
        if (said.Running) KeepOnAsk(request.Session, text);
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
                    while (waiting.Words.TryPeek(out var word))
                    {
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
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
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
            }

            AtomicFile.WriteText(path, System.Text.Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
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

        static string? Text(JsonElement entry, string name) =>
            entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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
                Origin = "person",
                Id = kept.Id,
                Text = kept.Text,
                Files = kept.Files.Count > 0 ? kept.Files : null,
                Reaches = "resume",
                Door = word.Door,
            }, line => _loop.Output.Append(id, line));
        }

        _loop.Nudge();
    }

    /// <summary>Held words the record would not keep once it ended, said where the person wrote them, never dropped silently.</summary>
    private void Lost(string id, SayAnswer said)
    {
        var line = $"— what you said as it wound up was not kept for it to go on with: {said.Message}";
        _loop.Output.Append(id, line);
        _loop.Events.Keep(id, new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = line, Why = said.Refusal is { } refusal ? WordsNever.Code(refusal) : null,
        }, null);
    }

    /// <summary>The records, and this one among them with its quest's last session here; null with no service yet.</summary>
    private async Task<Read?> ReadAsync(string id, CancellationToken ct)
    {
        if (_loop.Service is not { } service) return null;
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        return new Read(record, WordsNever.LastHere(json, record?.Quest));
    }

    /// <summary>A record as read, and the session its quest last ran in here (D79's reading), or null for none.</summary>
    private sealed record Read(SessionRecord? Record, string? Last);

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
    public void Dispose() => _stopping.Cancel();
}
