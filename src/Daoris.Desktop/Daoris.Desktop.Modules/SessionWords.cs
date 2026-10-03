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

    /// <summary>Nothing takes them, for this reason.</summary>
    public static WordsAnswer Refused(string why) => new(false, null, why);
}

/// <summary>
/// The person's words to a session of this machine's, whatever its state (MSG1d, D137 §2): a running session hears them at
/// its door as before (D90, D136); one that parked or ended has them kept on its record by the service's say door (MSG1a)
/// and the loop is nudged, so the driver goes on with them (MSG1b); and words said as a session winds up are held until its
/// record ends, then kept the same way, never refused.
/// </summary>
/// <remarks>
/// <para><b>What never goes on is said by a code</b> (D137 §2.2), judged from the record before anything is posted: a
/// teammate's record, an intake, a session that stood down, Ask Daoris's own conversation (its panel opens a new one, and
/// the design leaves it out), and a session whose quest went on in a later session here, whose words no look would take
/// up (MSG1b plans a quest's last session only). The service judges again as it keeps them, and its refusal is the answer.</para>
///
/// <para><b>Shown at once, under the record's id.</b> The words are written to the session's conversation the moment the
/// say door keeps them, as the person's, with the reach <c>resume</c>, the id the record gave them and the door they were
/// said at: the resumed run says them again under the same id where it took them (D137 §3.1), and the machine log's
/// <c>session.reopened</c> names the door (§3.3). Words held while a session winds up have no id until the record keeps
/// them, so they are shown once it does.</para>
///
/// <para><b>Held words wait in this process</b>, for the record to move through this machine's client or for a slow look,
/// whichever comes first. A restart before the record ends loses them: the page was told they are held, and nothing else
/// holds them yet (MSG1d's note says so).</para>
/// </remarks>
public sealed class SessionWords(DriverLoop loop) : IDisposable
{
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
    /// A record moved through this machine's client (<see cref="ServiceClient.Moved"/>): words held for it are tried now.
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
    }

    /// <summary>
    /// What a word said now to this session would do (<c>SESSION_QUEUE</c>, D137 §5.3), for the page to offer the box by, or
    /// the line saying why nothing takes words. Never a refusal: a page asks of every conversation it shows, and a service
    /// that does not answer is nothing known, never a claim either way.
    /// </summary>
    public async Task<WordsAnswer> ReachAsync(string id, CancellationToken ct)
    {
        if (loop.Processes.InboxOf(id) is { } inbox) return new(true, Spelled(inbox.Reach), null);
        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        if (loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) && loop.Processes.RefusesInput(id) is null)
        {
            // A conversation this machine runs (D49 §3): a word goes into the running turn where its agent takes words at its
            // next step (MSG1c), waits for the turn to end elsewhere, or starts one now (CONV4a).
            return new(true, loop.Chat?.Reach(id), null);
        }

        try
        {
            if (await ReadAsync(id, ct).ConfigureAwait(false) is not { } read) return new(false, null, null);
            if (read.Record is not { } record) return WordsAnswer.Refused("not-found");
            return Never(record, read.Last) is { } never ? WordsAnswer.Refused(never) : new(true, "resume", null);
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
        if (loop.Processes.InboxOf(id) is { } inbox && inbox.Hold(new ChatMessage(text, [])))
        {
            return new(true, Spelled(inbox.Reach), null) { Running = true };
        }

        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        var running = loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase);
        var refuses = loop.Processes.RefusesInput(id);
        if (running && refuses is null && loop.Chat is { } chat)
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
        if (read.Record is not { } record) return WordsAnswer.Refused("not-found");
        if (Never(record, read.Last) is { } never) return WordsAnswer.Refused(never);

        // Kept as a conversation keeps what is attached (CONV4c), so the names on the record name files that exist.
        var kept = files.Count > 0 ? ChatFiles.Keep(loop.Home, id, files) : [];
        return await KeepOrHoldAsync(id, new Word(text, [.. kept.Select(file => file.Name)], door), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The words to the say door, behind any already held for this record: kept, shown and the loop nudged; held while the
    /// record still runs; or refused by the service's word.
    /// </summary>
    private async Task<WordsAnswer> KeepOrHoldAsync(string id, Word word, CancellationToken ct)
    {
        var service = loop.Service ?? throw new DriverException("the driver is still coming up — its service is not answering yet. A moment.");
        var waiting = WaitingFor(id);
        await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Behind the words already held, so the record keeps them in the order said.
            if (waiting.Words.Count > 0)
            {
                waiting.Words.Enqueue(word);
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
                return new(true, "resume", null);
            }

            if (said.Refusal == "running")
            {
                // It winds up, or runs where this machine's routes do not reach (D137 §2.1): held for its record to end.
                waiting.Words.Enqueue(word);
                StartPump(id, waiting, moved);
                return new(true, "resume", null);
            }

            return said.Refusal is { } refusal
                ? WordsAnswer.Refused(Code(refusal))
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
                        if (loop.Service is not { } service) break;
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
                        waiting.Words.Dequeue();
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
            // The loop is closing: what is held goes with this process, as the remarks say.
        }
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
            loop.Output.Append(id, $"— the person said, which the same session goes on with: {kept.Text}");
            loop.Events.Keep(id, new SessionEvent
            {
                Kind = SessionEventKind.User,
                Origin = "person",
                Id = kept.Id,
                Text = kept.Text,
                Files = kept.Files.Count > 0 ? kept.Files : null,
                Reaches = "resume",
                Door = word.Door,
            }, line => loop.Output.Append(id, line));
        }

        loop.Nudge();
    }

    /// <summary>Held words the record would not keep once it ended, said where the person wrote them, never dropped silently.</summary>
    private void Lost(string id, SayAnswer said)
    {
        var line = $"— what you said as it wound up was not kept for it to go on with: {said.Message}";
        loop.Output.Append(id, line);
        loop.Events.Keep(id, new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = line, Why = said.Refusal is { } refusal ? Code(refusal) : null,
        }, null);
    }

    /// <summary>The records, and this one among them with its quest's last session here; null with no service yet.</summary>
    private async Task<Read?> ReadAsync(string id, CancellationToken ct)
    {
        if (loop.Service is not { } service) return null;
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var records = SessionRecords.Parse(json);
        var record = records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        var last = record?.Quest is { } quest
                   && SessionLook.From(json, [], [], _ => 0).LastRun.TryGetValue(quest, out var run)
            ? run.Session
            : null;
        return new Read(record, last);
    }

    /// <summary>A record as read, and the session its quest last ran in here (D79's reading), or null for none.</summary>
    private sealed record Read(SessionRecord? Record, string? Last);

    /// <summary>The code of what never goes on (D137 §2.2), or null where the record can go on with words.</summary>
    private static string? Never(SessionRecord record, string? last) =>
        record.Teammate ? "teammate"
        : record.Repository == HelpRoom.Repository ? "help"
        : record.Ask is not null ? "intake"
        : record.State == "stood-down" ? "stood-down"
        // MSG1b goes on with a quest's last session here only, so words to an earlier one would wait for nothing.
        : record.Quest is not null && last is not null && !string.Equals(last, record.Id, StringComparison.Ordinal) ? "superseded"
        : null;

    /// <summary>The say door's word as the page's code: its word for a teammate's record is <c>teammate</c>.</summary>
    private static string Code(string refusal) => refusal == "not-ours" ? "teammate" : refusal;

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
