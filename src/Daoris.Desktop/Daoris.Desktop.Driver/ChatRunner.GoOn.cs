using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// An ended chat goes on (MSG1c, D137 §2.2, §4.2): the person's words, kept on its record while it had ended (MSG1a) and
/// shown in its conversation by whichever door they were said at (MSG1d), are taken up here at once. Its record takes the
/// ledger's one move out of an ended state, and its own harness conversation is resumed by the id kept for it, with every
/// word waiting its first turn; or, where it cannot go on, the words stay waiting, marked, and its conversation says why.
/// </summary>
/// <remarks>
/// <para><b>One record per harness conversation; Daoris resumes only by the id it kept</b> (D137 §8): the protocol door's
/// <c>session/resume</c> or <c>session/load</c>, the native door's <c>--resume &lt;id&gt;</c>. Never <c>--continue</c> nor
/// a name, and never a new conversation under the same record.</para>
///
/// <para><b>Judged as a driven record is</b> (<see cref="Continuations.Judge"/>): what never goes on, no words, the adapter
/// it ran on, the account, its tree, a kept id and a door that resumes. A chat holds no quest, so nothing carries its words
/// on by itself: where it cannot go on, *Start a conversation with these words* is the person's press (MSG1f).</para>
///
/// <para><b>What holds it rather than refusing it</b> leaves the words waiting and unmarked, said in its conversation: an
/// account the selection will not start, a replay holding its repository's trees, the ledger refusing the move (another
/// session holds its tree, D51). The next word said to it tries again.</para>
/// </remarks>
public sealed partial class ChatRunner
{
    // The harness's own conversation per session (ANSWER1a), a chat's now too, beside the transcripts under the same home.
    private readonly HarnessConversations _conversations;

    // The words a chat could not go on with (MSG1b's marks), so a reader knows they were tried.
    private readonly GoOnMarks _marks;

    // Each chat going on, until its run is watched or it is said why not: one at a time, and what closing waits on.
    private readonly ConcurrentDictionary<string, Task> _goingOn = new(StringComparer.OrdinalIgnoreCase);

    // Closing: no chat goes on from here.
    private volatile bool _closing;

    /// <summary>The endings a chat goes on from: every one but a stand-down, which never goes on (D137 §2.2).</summary>
    private static readonly HashSet<string> Ended = new(StringComparer.OrdinalIgnoreCase) { "completed", "declined", "failed", "stopped" };

    /// <summary>
    /// An ended chat going on (MSG1c): its record as it ended, what its resumed run carries and learns (<see cref="ResumeAsk"/>),
    /// and whether its words went, which on the native door needs its line on stdin and its harness naming the conversation.
    /// </summary>
    private sealed class GoingOn(PriorSession record, ResumeAsk ask)
    {
        private readonly object _gate = new();
        private bool _sent;
        private bool _taken;

        public PriorSession Record => record;

        public ResumeAsk Ask => ask;

        /// <summary>The words are on the wire. True once, where they went now: at once, or where the harness already named it.</summary>
        /// <param name="named">Whether they went only once the harness has named its conversation too (the native door).</param>
        public bool Sent(bool named)
        {
            lock (_gate)
            {
                _sent = true;
                return Take(named);
            }
        }

        /// <summary>The native door's harness named its conversation. True once, where the words went now.</summary>
        public bool Named()
        {
            lock (_gate)
            {
                ask.Named = true;
                return Take(named: true);
            }
        }

        private bool Take(bool named)
        {
            if (_taken || !_sent || (named && !ask.Named)) return false;
            _taken = true;
            ask.Prompted = true;
            return true;
        }
    }

    /// <summary>
    /// A record event (MSG1d's shown words): the person's words with the reach <c>resume</c>, said to a session nothing here
    /// runs, are taken up at once where it is an ended chat of this machine's. A driven record is the planner's (MSG1b), and
    /// its record says so: reading it here costs one look and starts nothing.
    /// </summary>
    private void Heard(string session, SessionEvent e)
    {
        if (_closing || e.Kind != SessionEventKind.User || e.Reaches != "resume") return;
        if (_talking.ContainsKey(session) || _turned.ContainsKey(session) || _goingOn.ContainsKey(session)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await GoOnAsync(session).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Said in its conversation wherever it could be; a word said later tries again.
            }
        });
    }

    /// <summary>
    /// Go on with an ended chat the person wrote to (MSG1c, D137 §2.2): its record reopened, its own conversation resumed by
    /// the id kept for it with every word waiting its first turn, or why not said in its conversation. Nothing for a record
    /// that is not an ended chat of this machine's with words waiting.
    /// </summary>
    /// <param name="config">The machine's choices; read from the home's <c>driver.json</c> where none is handed.</param>
    /// <param name="onEnded">Told when the conversation it goes on in ends, or when its agent would not resume it.</param>
    /// <returns>The session where it went on; else null and why.</returns>
    public async Task<ChatStart> GoOnAsync(
        string sessionId, DriverConfig? config = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (_closing) return new(null, "the application is closing, so no conversation goes on now.");
        if (_talking.ContainsKey(sessionId)) return new(null, $"conversation `{sessionId}` runs here: its words reach it at its door.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_goingOn.TryAdd(sessionId, going.Task)) return new(null, $"conversation `{sessionId}` is already going on.");
        try
        {
            DriverConfig choices;
            try
            {
                choices = config ?? MachineConfig();
            }
            catch (Exception unreadable) when (unreadable is DriverException or System.Text.Json.JsonException or IOException
                                                   or UnauthorizedAccessException)
            {
                // The words wait, said: the next word tries again, and a torn driver.json is the person's to mend.
                return Held(sessionId, $"this machine's driver.json could not be read: {unreadable.Message}");
            }

            return await GoingOnAsync(sessionId, choices, onEnded, ct).ConfigureAwait(false);
        }
        finally
        {
            _goingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// The machine's choices for a chat taken up by itself (MSG1c): the file every door reads where its override names this
    /// home's, as the shell's own config path is; this home's <c>driver.json</c> otherwise.
    /// </summary>
    private DriverConfig MachineConfig()
    {
        var chosen = Environment.GetEnvironmentVariable(DriverConfig.PathVariable);
        var path = chosen is { Length: > 0 } && SamePath(DriverConfig.HomeOf(chosen), _home) ? chosen : Path.Combine(_home, "driver.json");
        return DriverConfig.Load(path);
    }

    private async Task<ChatStart> GoingOnAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        // Only an ended chat of this machine's with words waiting: a driven record goes on through the planner (MSG1b), a live
        // one hears words at its door, and Ask Daoris's conversation opens anew (D137 §2.2), so no word is kept for it.
        var record = await _service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        if (record is not { Kind: "chat", WordsWaiting: true } || record.Teammate
            || string.Equals(record.Repository, HelpRoom.Repository, StringComparison.OrdinalIgnoreCase)
            || !(Ended.Contains(record.State) || string.Equals(record.State, "stood-down", StringComparison.OrdinalIgnoreCase)))
        {
            return new(null, $"session `{sessionId}` is no ended conversation of this machine's with words waiting.");
        }

        // The person wrote to it, so it stays in view whatever comes of it (D137 §2.3), and an earlier mark of words it could
        // not take is past: these are taken up now.
        new SessionArchive(_home).Unarchive([sessionId]);
        _marks.Clear(sessionId);

        // Its conversation is the adapter's that opened it, so it goes on on that one or not at all.
        var ranOn = record.Adapter ?? config.Adapter;
        ISessionAdapter? adapter = null;
        try
        {
            adapter = _harnesses.Adapters.Resolve(ranOn);
        }
        catch (DriverException)
        {
            // An adapter no longer here (a plugin's, removed): its conversation cannot be resumed.
        }

        // What can be known before anything is asked of the toolchain, on the account it ran on: the nevers, no words, its
        // tree, a kept id and a door that resumes. Where the selection lands on another account is judged after it.
        var kept = _conversations.Read(sessionId);
        var why = adapter is null
            ? ContinueWhy.Of(ContinueWhy.Refused)
            : Continuations.Judge(record, adapter.Name, adapter.Resumes, record.Profile, kept);
        if (why is not null || adapter is null || kept is null) return CannotGoOn(record, why ?? ContinueWhy.Of(ContinueWhy.Unkept), ranOn);

        var registry = await _service.RegistryAsync(ct).ConfigureAwait(false);
        var known = registry
            .FirstOrDefault(r => string.Equals(r.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        if (known is null || string.IsNullOrWhiteSpace(known.Root))
        {
            return Held(sessionId, $"`{record.Repository}` has no checkout on this machine any more");
        }

        // The account it ran on, asked for by name, as a chat's picker names one (D137 §2.2): its conversation lives in that
        // account's home. One the selection will not start holds the words; one it lands elsewhere cannot go on.
        var selection = await _harnesses
            .SelectAsync(adapter.Name, config, known.Workspace, record.Profile, StartKind.Conversation, ct)
            .ConfigureAwait(false);
        if (!selection.Allowed) return Held(sessionId, selection.Refusal!);
        if (Continuations.Judge(record, adapter.Name, adapter.Resumes, selection.Profile, kept) is { } account)
        {
            return CannotGoOn(record, account, adapter.Name);
        }

        // A tree of its own is taken again: its repository's trees are held until the ledger holds this one once more (LEFT2),
        // so bringing the repository up to date does not rebase it under the conversation.
        var own = !SamePath(record.Tree!, known.Root!);
        using var starting = own ? TreeLock.TryStarting(_home, known.Workspace, known.Repository) : null;
        if (own && starting is null) return Held(sessionId, TreeLock.Replaying(known.Repository));

        // 🔴 The ledger's one move out of an ended state, with the person's words waiting (MSG1a, D137 §2.3), before anything
        // is spawned: it holds the tree again, and refuses where another session holds it (D51).
        try
        {
            await _service.AdvanceAsync(sessionId, "working", note: Continuations.GoingOn, ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return Held(sessionId, refused.Message);
        }

        starting?.Dispose();

        // Every word waiting once it moved: none can be kept on a record that works (the say door answers `running`), so the
        // list read now is whole, a word said while it was judged included.
        var now = await ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = now is { Waiting.Count: > 0 } ? now.Waiting : record.Waiting;
        var ask = new ResumeAsk(
            kept.Conversation, words,
            Continuations.Opening(adapter.Name, selection.Version, record.HarnessVersion, answer: false));

        return await RunAsync(
            sessionId, $"conversation `{sessionId}` goes on with your words in its own conversation.", adapter, selection, config,
            RepositoryPlace(config, registry, known, record.Repository!, record.Tree!), onEnded, ct,
            new GoingOn(record, ask)).ConfigureAwait(false);
    }

    /// <summary>The record read again, or null where the service did not answer: the first read's words then stand.</summary>
    private async Task<PriorSession?> ReadAgainAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            return await _service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>
    /// The words went to the conversation (MSG1c): off its record by their ids (MSG1a's taken door), so the service keeps
    /// them on the ask as <c>reopened</c>, and the machine log's <c>session.reopened</c> says it resumed. A refusal costs a
    /// line in its conversation, never the conversation.
    /// </summary>
    private async Task WentOnAsync(string sessionId, GoingOn goOn, string adapter)
    {
        var ids = goOn.Ask.Ids;
        if (ids.Count > 0)
        {
            string? failed;
            try
            {
                var (taken, message) = await _service.TakenAsync(sessionId, ids, by: null).ConfigureAwait(false);
                failed = taken ? null : message;
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException)
            {
                failed = error.Message;
            }

            if (failed is not null) Note(sessionId, $"— your words could not be taken off its record: {failed}");
        }

        _service.AccountSaid(Continuations.Reopened(
            sessionId, adapter, goOn.Record.State, why: null, _events.DoorOf(sessionId, ids), kind: "chat"));
    }

    /// <summary>
    /// The agent would not resume the conversation (MSG1c): nothing went, so the record goes back to how it ended, its flags
    /// kept and its note saying why, and its words stay waiting, marked, as where it could not go on.
    /// </summary>
    private async Task WentBackAsync(
        string sessionId, GoingOn goOn, ContinueReason why, string adapter, Func<string, string, Task>? onEnded)
    {
        var record = goOn.Record;
        try
        {
            await _service.AdvanceAsync(
                    sessionId, record.State, note: Continuations.CannotNote(record, why), interrupted: record.Interrupted,
                    limit: record.Limit)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort, as every conclusion here is: the host may be gone on the same shutdown.
        }

        CannotGoOn(record, why, adapter);
        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, record.State).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }

    /// <summary>
    /// Where a chat's words cannot go on in it (MSG1c, D137 §2.2): nothing carries a chat's words on by itself, so they stay
    /// waiting as said and are marked by their ids; its conversation says why with their ids and the reason's code, which the
    /// page words with its one press (MSG1f); and the machine log says it did not resume.
    /// </summary>
    private ChatStart CannotGoOn(PriorSession record, ContinueReason why, string adapter)
    {
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _marks.Mark(record.Session, words, why, DateTimeOffset.UtcNow);
        var note = Continuations.Cannot(words, why);
        _output?.Append(record.Session, note.Text!);
        _events.Keep(record.Session, note, say: null);
        _service.AccountSaid(Continuations.Reopened(
            record.Session, adapter, record.State, why, _events.DoorOf(record.Session, words), kind: "chat"));
        return new(null, $"it cannot go on in this conversation, because {why.Sentence}.");
    }

    /// <summary>
    /// What holds a chat's words rather than refusing them (MSG1c): said in its conversation, the words left waiting and
    /// unmarked, so the next word said to it tries again.
    /// </summary>
    private ChatStart Held(string sessionId, string why)
    {
        Note(sessionId, $"— it does not go on yet: {why.TrimEnd('.', ' ')}. Your words wait on it.");
        return new(null, why);
    }

    /// <summary>A driver's line in a conversation: its console and its record.</summary>
    private void Note(string sessionId, string text)
    {
        _output?.Append(sessionId, text);
        _events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = text }, say: null);
    }

    /// <summary>Whether two paths name one folder, whatever their separators or a trailing one.</summary>
    private static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
