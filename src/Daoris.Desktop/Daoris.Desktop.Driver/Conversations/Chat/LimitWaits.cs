using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The conversations that wait for an account to be ready, and the look's <c>limits</c> stage that wakes them (LIMITRESUME1c; D206
/// §3.4, §3.5): one its account's limit cut off with no other account of its agent to go on on, live or ended, and an ended one whose
/// person's words wait for its own cooling account (D137 point 7). Over the runner's context, as <see cref="LimitMove"/> is.
/// </summary>
/// <remarks>
/// <para><b>The wake is the look, and nothing beside it</b> (§3.4): no timer and no loop of its own. The stage keeps the held
/// conversations' earliest wait in memory with a stamp of what it was computed from (the cooling store, the harness settings, the
/// agents' account folders and <c>driver.json</c>), so a look with none due reads a few file times and asks nothing; a changed stamp
/// (<i>Try now</i>, a sign-in, an added account) recomputes the wait at that look.</para>
///
/// <para><b>Own first</b> (§3.5): its own account ready, the same conversation goes on, the next turn on the same process where it
/// still runs, else its agent's own resume on that account (<see cref="ChatGoOn"/>), handed the refused turns' words, the words said
/// while it waited and Daoris's word last, each once. Otherwise another account of its agent ready, it moves there handed the brief
/// (<see cref="LimitMove"/>). An ended chat whose words wait for its own account never met a limit, and is woken on that account
/// only.</para>
///
/// <para><b>Who leaves the wait</b>: one that went on or moved; one the person stopped, or told to stop waiting; one whose refusal waits
/// for them. Each is read from the record and its events, so a shell that comes up reads what the one before it saw.</para>
/// </remarks>
internal sealed class LimitWaits(ChatContext context, LimitMove move, ChatGoOn goOn)
{
    // The conversations held, by session: what the stage looks at, read whole once as the runner comes up and kept since.
    private readonly ConcurrentDictionary<string, byte> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _looking = new(1, 1);
    private volatile bool _read;
    private volatile bool _dirty = true;
    private string? _stamp;
    private DateTimeOffset? _due;

    /// <summary>How many times the stage asked the service anything: a test holds that a look with no wait come asks nothing.</summary>
    internal int Asked { get; private set; }

    /// <summary>
    /// A live conversation whose move found no other account (LIMITGOON1's <i>stays</i>, written as a wait): its turns wait, its words
    /// with them, and its conversation says when the first account is ready again. False where its turns would not wait.
    /// </summary>
    public bool Wait(string session, ChatRunner.ITurnedChat chat, string adapter)
    {
        if (!chat.Turns.WaitForLimit()) return false;
        if (context.Harnesses.CoolingsOf(adapter) is [var first, ..]) context.Note(session, LimitNotes.Waits(first, context.Harnesses.Zone));
        Add(session);
        return true;
    }

    /// <summary>A conversation the stage looks at from now on: held by a limit, or an ended one whose words wait for its own account.</summary>
    public void Add(string session)
    {
        _held[session] = 0;
        _dirty = true;
    }

    /// <summary>Whether the stage holds the conversation.</summary>
    public bool Holds(string session) => _held.ContainsKey(session);

    /// <summary>
    /// The look's <c>limits</c> stage (§3.4): each held conversation whose wait has come goes on, or moves, in the record's order. Asks
    /// nothing where no wait has come and nothing it was computed from moved. One at a time; never once the runner is closing.
    /// </summary>
    /// <returns>What became of each conversation it woke.</returns>
    public async Task<IReadOnlyList<ChatStart>> LookAsync(DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (context.Closing || !await _looking.WaitAsync(0, ct).ConfigureAwait(false)) return [];
        try
        {
            var stamp = Stamp();
            var now = context.Harnesses.Clock();
            if (_read && !_dirty && stamp == _stamp && (_due is not { } due || now < due)) return [];
            _dirty = false;
            _stamp = stamp;

            Asked++;
            var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
            var records = SessionRecords.Parse(json);
            if (!_read)
            {
                // Read whole once: the conversations a shell before this one left waiting.
                foreach (var each in records.Where(record => Candidate(record, json))) _held[each.Id] = 0;
                _read = true;
            }

            var woke = new List<ChatStart>();
            DateTimeOffset? next = null;
            foreach (var each in records.Where(record => _held.ContainsKey(record.Id)))
            {
                if (context.Closing || ct.IsCancellationRequested) break;
                var (start, waits) = await OneAsync(each.Id, json, config, onEnded, ct).ConfigureAwait(false);
                if (start is not null) woke.Add(start);
                if (waits is { } until && (next is null || until < next)) next = until;
            }

            // A held id the records no longer list leaves.
            foreach (var gone in _held.Keys.Where(id => records.All(record => !string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase))))
            {
                _held.TryRemove(gone, out _);
            }

            _due = next;
            return woke;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          or ObjectDisposedException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // The service did not answer: the next look asks again.
            _dirty = true;
            return [];
        }
        finally
        {
            _looking.Release();
        }
    }

    /// <summary>
    /// One held conversation at a look: woken, moved, left, or still waiting and until when. Null for the moment where nothing names
    /// one (every account signed out, none in the set): only a stamp change wakes it.
    /// </summary>
    private async Task<(ChatStart? Start, DateTimeOffset? Waits)> OneAsync(
        string session, string json, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (context.GoingOn.ContainsKey(session)) return (null, null);
        if (ServiceClient.ReadRecord(json, session) is not { Kind: "chat", Teammate: false, Adapter: { Length: > 0 } adapter } record
            || WordsNever.ContinuedIn(json, session) is not null)
        {
            _held.TryRemove(session, out _);
            return (null, null);
        }

        var events = context.Events.After(session, 0).Events;
        var limited = LimitHeld.Waiting(events);
        var live = context.Turned.TryGetValue(session, out var chat);
        if (live && chat!.Turns.Limited is false)
        {
            // Live and taking words: it went on, or never waited.
            _held.TryRemove(session, out _);
            return (null, null);
        }

        if (!live && (PersonStopped(record)
                      || (limited is null && !(record.WordsWaiting && Ended(record) && !GoOnMarks.Judged(record, context.Marks.Read(session))))))
        {
            _held.TryRemove(session, out _);
            return (null, null);
        }

        CoolingEntry? own;
        IReadOnlyList<CoolingEntry> cooling;
        try
        {
            own = context.Harnesses.CoolingOf(adapter, record.Profile);
            cooling = context.Harnesses.CoolingsOf(adapter);
        }
        catch (DriverException)
        {
            // An adapter no longer here: nothing of it is ready again by itself.
            _held.TryRemove(session, out _);
            return (null, null);
        }

        // 🔴 Own first (§3.5): its own account ready, the same conversation goes on, whatever else is ready.
        if (own is null)
        {
            var until = context.Harnesses.Clock();
            ChatStart start;
            if (live)
            {
                start = await WakeLiveAsync(session, adapter, chat!, limited, until, ct).ConfigureAwait(false);
            }
            else
            {
                start = await goOn.WakeAsync(session, limited is not null, until, config, onEnded, ct).ConfigureAwait(false);
            }

            _held.TryRemove(session, out _);
            // Its account refused again: a new cool-off, which the stamp hears, holds it once more.
            if (start.SessionId is null && WaitsAgain(session)) _held[session] = 0;
            return (start, null);
        }

        // An ended chat whose person's words wait for its own account never met a limit: woken on that account only (§3.5, N5).
        if (limited is null) return (null, own.Until);

        // Another account of its agent ready first: it moves there, handed the brief and both kinds of words, none twice.
        var moved = live
            ? await move.MoveAsync(session, config, onEnded, ct).ConfigureAwait(false)
            : await move.MoveEndedAsync(session, lost: null, config, onEnded, ct).ConfigureAwait(false);
        if (moved is { SessionId: { } to })
        {
            _held.TryRemove(session, out _);
            return (new ChatStart(to, moved.Message), null);
        }

        // Nothing has room: the first of its agent's accounts ready again is its wait.
        return (null, cooling.Count > 0 ? cooling[0].Until : null);
    }

    /// <summary>
    /// The wake of a live conversation on its own account (§3.5): Daoris's word kept on its record first, then every word that waited,
    /// with the refused turns' words before them and Daoris's last, one turn on the same process. Nothing is sent where it no longer waits.
    /// </summary>
    private async Task<ChatStart> WakeLiveAsync(
        string session, string adapter, ChatRunner.ITurnedChat chat, LimitHeld? held, DateTimeOffset until, CancellationToken ct)
    {
        SaidWordView? word = null;
        try
        {
            var said = await context.Service.SayAsync(
                    session, LimitResetWord.Text, ct: ct, from: DaorisWords.From, key: DaorisKeys.LimitReset(session, until), running: true)
                .ConfigureAwait(false);
            word = said is { Kept: true, Word: { } keptWord } ? keptWord with { From = DaorisWords.From } : null;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
        {
            // Kept nowhere, it still goes, in this process alone, as a word a service before the door would not keep does.
        }

        word ??= new SaidWordView(null, LimitResetWord.Text, until, [], false) { From = DaorisWords.From };
        var resent = (held?.Words ?? []).Select(e => new ChatMessage(e.Text!, ChatFiles.Kept(context.Home, session, e.Files ?? []))).ToList();
        var kept = context.Conversations.Read(session)?.Conversation ?? "";
        var woke = chat.Turns.WakeFromLimit(waited =>
        {
            var files = new Dictionary<SaidWordView, IReadOnlyList<KeptFile>>(ReferenceEqualityComparer.Instance);
            var words = new List<SaidWordView>();
            foreach (var message in waited)
            {
                var view = new SaidWordView(message.Kept ? message.Id : null, message.Text, until, [.. message.Files.Select(file => file.Name)], false);
                files[view] = message.Files;
                words.Add(view);
            }

            words.Add(word);
            var ask = new ResumeAsk(kept, words, LimitNotes.Resumed(until), files: view => files.GetValueOrDefault(view) ?? [])
            {
                Resent = resent,
            };
            return new ChatMessage(ask.Prompt, [])
            {
                GoesOn = ask,
                Attempting = () => SaidDoor.AttemptedAsync(context.Service, session, ask.Words),
                // Off its record by their ids once on the wire, the waiting words and Daoris's alike.
                Delivered = () => _ = TakenOffAsync(session, ask.Ids),
            };
        });

        if (!woke)
        {
            if (word.Id is { } id) await TakenOffAsync(session, [id]).ConfigureAwait(false);
            return new ChatStart(null, $"conversation `{session}` no longer waits for its account.");
        }

        context.Service.AccountSaid(Continuations.Reopened(session, adapter, "working", why: null, context.Events.DoorOf(session, []), kind: "chat"));
        return new ChatStart(session, $"conversation `{session}` went on in its own conversation, its account ready again.");
    }

    /// <summary>The words off a record by their ids; what the service does not take waits there, said nowhere.</summary>
    private async Task TakenOffAsync(string session, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) return;
        try
        {
            await context.Service.TakenAsync(session, ids, by: null).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
        {
            // A word left on a record that runs is taken with the next; one left on an ended record is uncertain at the next come-up.
        }
    }

    /// <summary>Whether a conversation the wake could not take on still waits for its limit: its record's events read again.</summary>
    private bool WaitsAgain(string session) =>
        LimitHeld.Waiting(context.Events.After(session, 0).Events) is not null
        && !(context.Turned.TryGetValue(session, out var chat) && !chat.Turns.Limited);

    /// <summary>A conversation a runner coming up looks at: an ended chat of this machine's held by its limit, or whose words wait.</summary>
    private bool Candidate(SessionRecord record, string json)
    {
        if (record.Kind != "chat" || record.Teammate) return false;
        if (context.Turned.TryGetValue(record.Id, out var chat)) return chat.Turns.Limited;
        if (ServiceClient.ReadRecord(json, record.Id) is not { } prior || !Ended(prior) || PersonStopped(prior)) return false;
        if (WordsNever.ContinuedIn(json, record.Id) is not null) return false;
        if (LimitHeld.Waiting(context.Events.After(record.Id, 0).Events) is not null) return true;
        return prior.WordsWaiting && !GoOnMarks.Judged(prior, context.Marks.Read(record.Id))
            && context.Harnesses.CoolingOf(prior.Adapter ?? "", prior.Profile) is not null;
    }

    private static bool Ended(PriorSession record) => record.State is "completed" or "declined" or "failed" or "stopped";

    /// <summary>The person ended it (D206 §3.7, <i>Stop</i>): the wake leaves it, and their next word goes on as any does.</summary>
    private static bool PersonStopped(PriorSession record) =>
        record.PersonStopped && record.Note?.StartsWith(ChatRunner.EndedByPersonNote, StringComparison.Ordinal) == true;

    /// <summary>
    /// What the wait was computed from, by file times alone (§3.4, Codex 5): the cooling store, the harness settings, the agents' account
    /// folders and the machine's <c>driver.json</c>. <i>Try now</i> and a sign-in end a cool-off in the store; an added account changes
    /// the settings or its agent's folder.
    /// </summary>
    private string Stamp()
    {
        var parts = new List<string>
        {
            HomeWrites.Stamp(AccountCooling.PathOf(context.Home)),
            HomeWrites.Stamp(context.Harnesses.SettingsPath),
            // The settings the roster folds the account set from (HarnessSettings.Profiles) beside them.
            HomeWrites.Stamp(DaorisHome.Entry(context.Home, HomeEntry.Harnesses)),
            HomeWrites.Stamp(DriverConfig.ResolvePath(context.Home)),
        };
        try
        {
            var agents = DaorisHome.Entry(context.Home, HomeEntry.AgentHomes);
            if (Directory.Exists(agents))
            {
                parts.AddRange(Directory.EnumerateDirectories(agents).Order(StringComparer.OrdinalIgnoreCase)
                    .Select(folder => $"{Path.GetFileName(folder)}:{Directory.GetLastWriteTimeUtc(folder).Ticks}"));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder that does not list stamps as it is: the next look reads it again.
        }

        return string.Join("|", parts);
    }
}
