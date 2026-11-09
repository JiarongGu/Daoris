using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The conversations that wait for an account to be ready, and the look's <c>limits</c> stage that wakes them: one its account's limit cut off with no other account of its agent to go on on, live or ended, and an ended one whose
/// person's words wait for its own cooling account. Over the runner's context, as <see cref="LimitMove"/> is.
/// </summary>
/// <remarks>
/// <para><b>The wake is the look, and nothing beside it</b>: no timer and no loop of its own. The stage keeps the held
/// conversations' earliest wait in memory with a stamp of what it is computed from (the cooling store, the harness settings, the
/// agents' account folders and <c>driver.json</c>), so a look with none due reads a few file times and asks nothing; a changed stamp
/// (<i>Try now</i>, a sign-in, an added account) recomputes the wait at that look.</para>
///
/// <para><b>Own first</b>: its own account ready, the same conversation goes on, the next turn on the same process where it
/// still runs, else its agent's own resume on that account (<see cref="ChatGoOn"/>), handed the refused turns' words, the words said
/// while it waited and Daoris's word last, each once. Otherwise another account of its agent ready, it moves there handed the brief
/// (<see cref="LimitMove"/>). An ended chat whose words wait for its own account never met a limit: another account of its agent ready, it
/// goes on there now, and otherwise it is woken at the earliest reset of any account of its agent; Ask
/// Daoris's room alone keeps its own account's.</para>
///
/// <para><b>Who leaves the wait</b>: one that went on or moved; one the person stopped, or told to stop waiting; one whose refusal waits
/// for them. Each is read from the record and its events, so a shell that comes up reads what the one before it saw.</para>
/// </remarks>
[Executor("limit-wait", ExecutorPhase.Move)]
internal sealed class LimitWaits(IExecutorWiring wiring) : IExecutor
{
    private ChatContext context => wiring.Context;

    private LimitMove move => wiring.Of<LimitMove>();

    private ChatGoOn goOn => wiring.Of<ChatGoOn>();

    // The conversations held, by session: what the stage looks at, read whole once as the runner comes up and kept since.
    private readonly ConcurrentDictionary<string, byte> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _looking = new(1, 1);
    private volatile bool _read;
    private volatile bool _dirty = true;
    private LimitStamp? _stamp;
    private DateTimeOffset? _due;

    /// <summary>What the course answered each wake, by session: a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>How many times the stage asked the service anything: a test holds that a look with no wait come asks nothing.</summary>
    internal int Asked { get; private set; }

    /// <summary>
    /// A live conversation whose move found no other account (its <i>stays</i>, written as a wait): its turns wait, its words
    /// with them, and its conversation says when the first account is ready again. False where its turns would not wait.
    /// </summary>
    /// <param name="picked">The account the person picked for it: its own reset is what it waits for, not the agent's first.</param>
    /// <param name="model">The model its line asks: the accounts it waits for are those a cool-off of that model holds.</param>
    public bool Wait(string session, ITurnedChat chat, string adapter, string? picked = null, string? model = null)
    {
        if (!chat.Turns.WaitForLimit()) return false;
        var first = picked is null
            ? LineAccount.Earliest(ResetSet.Wake, coolings: context.Harnesses.CoolingsOf(adapter, model))
            : context.Harnesses.CoolingOf(adapter, picked, model);
        if (first is not null)
        {
            context.Note(session, LimitNotes.Waits(first, context.Harnesses.Zone));
        }
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
    /// The look's <c>limits</c> stage: each held conversation whose wait has come goes on, or moves, in the record's order. Asks
    /// nothing where no wait has come and nothing it is computed from moved. One at a time; never once the runner is closing.
    /// </summary>
    /// <returns>What became of each conversation it woke.</returns>
    public async Task<IReadOnlyList<ChatStart>> LookAsync(DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (context.Closing || !await _looking.WaitAsync(0, ct).ConfigureAwait(false)) return [];
        try
        {
            var staff = Staff();
            var stamp = LimitStamp.Of(context.Home, config, staff).Beside(Accounts());
            var now = context.Harnesses.Clock();
            if (_read && !_dirty && LimitStamp.Same(stamp, _stamp) && (_due is not { } due || now < due)) return [];
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

            // A held id the records do not list any more leaves.
            foreach (var gone in _held.Keys.Where(id => records.All(record => !string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase))))
            {
                _held.TryRemove(gone, out _);
            }

            // A fact the clock alone ends (a mark, a reservation, a counted move, the day) moves no stamp, so the look is due by it.
            if (!_held.IsEmpty && staff?.NextChange(now) is { } change && (next is null || change < next)) next = change;
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

        // An older build's box for the record's ended limit move still in place is its `Never`: the wake
        // neither goes on in its own conversation nor moves it, and the record stays held, asked again at the next look, which imports the box
        // once the start's confirmation is settled or the standing move behind which it waited has ended.
        if (new LimitBoxes(context.Home).Read(session) is not null)
        {
            _dirty = true;
            return (null, null);
        }

        if (ServiceClient.ReadRecord(json, session) is not { Kind: "chat", Teammate: false, Adapter: { Length: > 0 } adapter } record
            || WordsNever.ContinuedIn(json, session) is not null)
        {
            _held.TryRemove(session, out _);
            return (null, null);
        }

        var events = context.Events.After(session, 0).Events;
        // An ended record's flag decides; a live one, and one an older build ended, by its events.
        var limited = LimitHeld.Waiting(events, record.Limit);
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
            var model = record.ModelOn(adapter);
            own = context.Harnesses.CoolingOf(adapter, record.Profile, model);
            cooling = context.Harnesses.CoolingsOf(adapter, model);
        }
        catch (DriverException)
        {
            // An adapter not here: nothing of it is ready again by itself.
            _held.TryRemove(session, out _);
            return (null, null);
        }

        // 🔴 Own first: its own account ready, the same conversation goes on, whatever else is ready. The rule reads it with no walk: the
        // wake's move and its ended move walk for themselves, and the one walk this stage judges is the go-on's.
        // An account the person picked is the rule's constraint: it waits for that account alone.
        var picked = context.Conversations.PickedOn(session, record.Profile);
        var answer = LineAccount.Decide(
            LineOwn.Of(own), new LineTrigger(Limited: limited is not null), picked ? LineConstraint.Pick(record.Profile!) : LineConstraint.None,
            LineKind.Conversation(carries: false),
            ask => throw new InvalidOperationException($"the wake asks no {ask.Place} walk of the account rule"));

        // The wake is the course's answer for CourseTrigger.LimitWake, read over the rule's
        // answer as above. Its own account ready, it resumes; a picked account cooling, it waits for that account alone. Where another account
        // might free first, the course reads no walk, so the executor goes on to the move, which walks for itself (a reading, not a change).
        var until = context.Harnesses.Clock();
        var course = CourseDecision.Decide(
            new CourseFacts(
                new CourseHead(
                    record.State, Limited: limited is not null || live, Role: record.Actor ?? "chat", Choice: record.Choice, Adapter: adapter,
                    Profile: record.Profile, Conversation: true, AskDaoris: string.Equals(record.Repository, KnowledgeRoomIdentity.Repository, StringComparison.OrdinalIgnoreCase),
                    Waiting: [.. record.Waiting.Select(word => new CourseWord(word.Id ?? "", BriefedMove.AuthorOf(word), word.Key))], Session: session,
                    Tree: record.Tree),
                live ? CourseBoundary.BetweenTurns : CourseBoundary.ProcessExited,
                Account: new CourseAccount(Answer: answer, Picked: picked), Limit: new CourseLimit(Woke: until)),
            CourseTrigger.LimitWake);
        Decided?.Invoke(session, course);
        if (course is LineCourse.Resume)
        {
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
            if (start.SessionId is null && await WaitsAgainAsync(session, ct).ConfigureAwait(false))
            {
                // What held the wake off (a hand-over's box, a config) moves no stamp, so the next look asks again, as for an older build's box.
                _held[session] = 0;
                _dirty = true;
            }

            return (start, null);
        }

        // The account it is picked on cools: its wait is that account's reset, never another account's, and it moves nowhere.
        if (picked) return (null, own!.Until);

        // An ended chat whose person's words wait for its own account never met a limit: it
        // goes on now in a new part where another account of its agent is ready, and otherwise waits for the earliest reset of any of them,
        // never its own account's alone.
        if (limited is null)
        {
            ResumeChoice? judged;
            try
            {
                judged = await goOn.JudgeWaitAsync(record, config, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
            {
                judged = null;
            }

            if (judged?.Answer is not LineAnswer.Elsewhere { Switched: true }) return (null, judged?.Selection.Cooling?.Until ?? own!.Until);

            var went = await goOn.WakeAsync(session, limited: false, context.Harnesses.Clock(), config, onEnded, ct).ConfigureAwait(false);
            if (went.SessionId is null) return (null, null);
            _held.TryRemove(session, out _);
            return (went, null);
        }

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
        return (null, LineAccount.Earliest(ResetSet.Wake, coolings: cooling)?.Until);
    }

    /// <summary>
    /// The wake of a live conversation on its own account: Daoris's word kept on its record first, then every word that waited,
    /// with the refused turns' words before them and Daoris's last, one turn on the same process. Nothing is sent where it waits no more.
    /// </summary>
    private async Task<ChatStart> WakeLiveAsync(
        string session, string adapter, ITurnedChat chat, LimitHeld? held, DateTimeOffset until, CancellationToken ct)
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

    /// <summary>Whether a conversation the wake could not take on still waits for its limit: its record and its events read again.</summary>
    private async Task<bool> WaitsAgainAsync(string session, CancellationToken ct)
    {
        if (context.Turned.TryGetValue(session, out var chat) && !chat.Turns.Limited) return false;
        // A record the service did not answer for is read by its events alone, as one from before the flag.
        var record = await context.ReadAgainAsync(session, ct).ConfigureAwait(false);
        return LimitHeld.Waiting(context.Events.After(session, 0).Events, record?.Limit == true) is not null;
    }

    /// <summary>A conversation a runner coming up looks at: an ended chat of this machine's held by its limit, or whose words wait.</summary>
    private bool Candidate(SessionRecord record, string json)
    {
        if (record.Kind != "chat" || record.Teammate) return false;
        if (context.Turned.TryGetValue(record.Id, out var chat)) return chat.Turns.Limited;
        if (ServiceClient.ReadRecord(json, record.Id) is not { } prior || !Ended(prior) || PersonStopped(prior)) return false;
        if (WordsNever.ContinuedIn(json, record.Id) is not null) return false;
        if (LimitHeld.Waiting(context.Events.After(record.Id, 0).Events, prior.Limit) is not null) return true;
        return prior.WordsWaiting && !GoOnMarks.Judged(prior, context.Marks.Read(record.Id))
            && LineAccount.OwnHold(context.Harnesses.CoolingOf(prior.Adapter ?? "", prior.Profile, prior.ModelOn(prior.Adapter ?? ""))) == NextHold.Cooling;
    }

    private static bool Ended(PriorSession record) => record.State is "completed" or "declined" or "failed" or "stopped";

    /// <summary>The person ended it (<i>Stop</i>): the wake leaves it, and their next word goes on as any does.</summary>
    private static bool PersonStopped(PriorSession record) =>
        record.PersonStopped && record.Note?.StartsWith(ChatEnded.EndedByPersonNote, StringComparison.Ordinal) == true;

    /// <summary>
    /// The accounts' parts of the stamp, by file times alone: the cooling store, the harness settings and the agents' account folders.
    /// <i>Try now</i> and a sign-in end a cool-off in the store; an added account changes the settings or its agent's folder.
    /// </summary>
    private string Accounts()
    {
        var parts = new List<string>
        {
            HomeWrites.Stamp(AccountCooling.PathOf(context.Home)),
            HomeWrites.Stamp(context.Harnesses.SettingsPath),
            // The settings the roster folds the account set from (HarnessSettings.Profiles) beside them.
            HomeWrites.Stamp(DaorisHome.Entry(context.Home, HomeEntry.Harnesses)),
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

    private StaffLedger? Staff()
    {
        try
        {
            return context.Harnesses.StaffFor(context.Home);
        }
        catch (Exception error) when (error is DesktopStoreException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
