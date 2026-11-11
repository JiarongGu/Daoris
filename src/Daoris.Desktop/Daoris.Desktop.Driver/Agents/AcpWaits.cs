namespace Daoris.Driver;

/// <summary>
/// What a driven run does where a turn ends: waits for the session's own background work and for the turn its harness wakes for it
/// carries a done quest's uncommitted tree on, and ends a session whose quest no longer waits.
/// It reads the run's clock, bounds and door through <see cref="ITurnEndWiring"/> and writes only the driver's own prompts.
/// </summary>
internal sealed class AcpWaits(ITurnEndWiring wiring)
{
    /// <summary>The stop reason of a turn its harness woke, which ends as a turn the agent finished does.</summary>
    internal const string WokenStop = "end_turn";

    private bool _waitsOnOwnWork;
    private int _carried;
    private bool _boundSaid;

    /// <summary>
    /// Asked at a turn's end: what the quest and its tree say, so a turn that ended with the quest done and
    /// the tree uncommitted is carried on rather than the session's end. Dirty, with carry-ons left, the run also waits for the session's
    /// own background work as a park would have it wait, never cutting that wait on the closed quest. Null never carries on.
    /// </summary>
    public Func<CancellationToken, Task<TurnCarry>>? CarriesOn { get; set; }

    /// <summary>
    /// Whether no turn runs and the session waits on its own background work (the watcher's state C): its quiet is
    /// judged by whether that work moves, on longer bounds.
    /// </summary>
    public bool WaitsOnOwnWork => Volatile.Read(ref _waitsOnOwnWork);

    /// <summary>
    /// What the run asks of its quest where a turn ends: yes where the quest would park, or
    /// where it stands closed with its tree uncommitted and carry-ons left, so the run waits for the session's own work first and then
    /// carries it on, never cutting the wait on the quest's close. Where no carry-on is owed the answer is whether the quest would park.
    /// </summary>
    public Func<CancellationToken, Task<bool>>? Of(Func<CancellationToken, Task<bool>>? waitsOnBackground)
    {
        if (CarriesOn is not { } carries) return waitsOnBackground;
        return async token =>
        {
            if (waitsOnBackground is not null && await waitsOnBackground(token).ConfigureAwait(false)) return true;
            // Unread is not a yes: a look that cannot be taken owes nothing, and the session ends.
            return await LookAsync(carries, token).ConfigureAwait(false) is { Dirty: true } look && Volatile.Read(ref _carried) < look.Limit;
        };
    }

    /// <summary>The look at the quest and its tree, or null where it could not be taken (never a failure of the run).</summary>
    private static async Task<TurnCarry?> LookAsync(Func<CancellationToken, Task<TurnCarry>> carries, CancellationToken ct)
    {
        try
        {
            return await carries(ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// A turn that ended with its quest done, or submitted, and its tree holding uncommitted work is not the session's end:
    /// the session is carried on in Daoris's own words to commit by its repository's conventions or restore what it does not
    /// want, and the tree is read again at that turn's end. The words are a prompt of the run's, never a card, a say or a Confirm, and
    /// Daoris commits nothing. Null where nothing is owed: the quest is not closed, the session is stopped, the tree is clean or
    /// unread, or the look's <see cref="TurnCarry.Limit"/> of carry-ons are written and the bound is said once, so the run ends.
    /// </summary>
    /// <remarks>
    /// Decided where it is written, as the go-on is (<see cref="WokenOrGoOnAsync"/>): under the write lock, the person's stop is read again
    /// and no turn of the harness's runs. A turn that took the moment is waited for, never written into (its answer would never come), and
    /// the tree is read again after it. Reached only once the session's own work has ended and the turn woken for it, which
    /// <see cref="AfterBackgroundAsync"/> waits for first.
    /// </remarks>
    /// <exception cref="DriverException">The agent's stream ends while the session waits to carry on.</exception>
    public async Task<string?> CarryOnAsync(CancellationToken ct)
    {
        if (CarriesOn is not { } carries) return null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (await LookAsync(carries, ct).ConfigureAwait(false) is not { Dirty: true } look) return null;
            if (_carried >= look.Limit)
            {
                if (!_boundSaid)
                {
                    _boundSaid = true;
                    Note(BackgroundNotes.CarryBound());
                }

                return null;
            }

            try
            {
                return await wiring.PromptingAsync(
                        () => wiring.SendOwnAsync(
                            look.Words, ct,
                            sent: () =>
                            {
                                Interlocked.Increment(ref _carried);
                                Note(BackgroundNotes.CarriedOn(look.Paths));
                            },
                            when: () => look.Holds?.Invoke() != false && wiring.Wake.NoTurnRuns(wiring.Prompted)),
                        ct)
                    .ConfigureAwait(false);
            }
            catch (AcpTurnedAway)
            {
                ct.ThrowIfCancellationRequested();
                // The person stopped it, or the door shut: nothing more is owed. Otherwise a turn of the harness's began, and it is waited for.
                if (wiring.DoorShut || look.Holds?.Invoke() == false) return null;
                if (!await wiring.Wake.IdleOrQuietAsync(wiring.Prompted, wiring.Clock.GetUtcNow(), wiring.QuestDoneQuiet, wiring.Ended, ct).ConfigureAwait(false))
                {
                    throw wiring.StreamEnded(
                        "the ACP agent's stream ended while the session waited to carry its done quest's work on — the agent exited, or it was ended.");
                }
            }
        }
    }

    private void Note(Noted noted) => wiring.Note(noted);

    /// <summary>
    /// A turn that ended while the session's own background work runs: not a question. Where the driver says its
    /// quest would otherwise park it on the person, the session stays open until the harness says that work ended, then until
    /// the turn its harness woke for it ends, whose stop reason this answers. So too a turn that ended after work of its
    /// own ended unseen, after its agent's last step: its harness wakes it with that ending once the turn is over, and the
    /// session waits for that turn. Null where nothing runs and no ending is owed a wake, or nothing waits.
    /// </summary>
    /// <remarks>
    /// <para><b>The woken turn's end is the turn's end.</b> The harness wakes its agent itself when the work ends, with no prompt
    /// open, and the adapter answers no prompt for that turn: its end is the autonomous result's
    /// <c>usage_update</c> (<see cref="AcpWake"/>). It ends here as an answered turn does, a turn in the record, and the run's
    /// loop reads again what runs and what its quest says, so a real question still parks and work it started is waited for.</para>
    ///
    /// <para><b>Daoris's own turn only where none woke.</b> 🔴 A go-on sent at once assumes its answer will come:
    /// a prompt sent into the woken turn is taken by it and never answered, and the session would wait out the
    /// timeout. It goes only where nothing is said for <see cref="AcpWake.Quiet"/> with no turn running and no
    /// prompt open, as a fresh turn the adapter answers. Nothing is cancelled: a cancel is the session's, never one turn's, and
    /// would land on the person's words as readily as on Daoris's.</para>
    ///
    /// <para><b>Bounded by the session's own bounds</b>: the person's stop ends the process, the reader with it, and the wait with
    /// that; the driver's shutdown cancels it. No time limit ends it: work that never ends (a dev server), and a
    /// woken turn whose end is never said, are waited for until the person acts, as a park would hold its tree for them;
    /// the watcher looks in on such a session once it shows no progress.</para>
    ///
    /// <para><b>And by its quest</b>: while the work runs the quest is read again every <see cref="ITurnEndWiring.BackgroundLook"/>,
    /// and once it no longer waits on that work, with no turn running, the session ends, saying so, and the work ends with it. A turn
    /// that did nothing for <see cref="ITurnEndWiring.QuestDoneQuiet"/> is left, judged at the moment the session ends (<see cref="EndAsync"/>).</para>
    /// </remarks>
    /// <exception cref="DriverException">The agent's stream ended first: a failure of the door, never a clean turn's end.</exception>
    public async Task<string?> AfterBackgroundAsync(Func<CancellationToken, Task<bool>>? waits, CancellationToken ct)
    {
        if (waits is null) return null;
        var background = wiring.Background;
        var wake = wiring.Wake;
        var work = background.RunningWork;
        // 🔴 Work that ended after the agent's last step is owed the wake its harness gives it once the turn is
        // over, as work still running is. Read only as nothing runs: a turn that ends on running work waits for it, and its wake.
        IReadOnlyList<(string Name, string State)> unseen = work.Count > 0 ? [] : background.Ended(wake.Unseen);
        if (work.Count == 0 && unseen.Count == 0) return null;
        var running = work.Select(each => each.Name).ToList();

        bool waiting;
        try
        {
            waiting = await waits(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Unread is not a yes: the work ends with the session, and the reason is said.
            Note(BackgroundNotes.Unread(error.Message));
            return null;
        }

        if (!waiting) return null;

        const string waited =
            "the ACP agent's stream ended while the session waited for its own background work — the agent exited, or it was "
            + "ended.";

        // 🔴 A command can end shortly before its turn does, after its agent's last step, while
        // the agent writes that it still runs; nothing runs at the turn's end, so the session would close and park on the person,
        // moments before its harness would wake it with the ending. It waits for that turn, or tells the agent itself.
        if (work.Count == 0)
        {
            Note(BackgroundNotes.Ended(unseen));
            return await AfterEndedAsync(BackgroundNotes.EndedSaid(unseen), waited, waits, ct).ConfigureAwait(false);
        }

        Note(BackgroundNotes.Waits(running));
        // No turn runs while it waits on its own work, which the watcher reads as state C.
        Volatile.Write(ref _waitsOnOwnWork, true);
        // A wait the quest's end cut leaves the flag set, so the watcher sends no quiet word into the session as it ends.
        var windingDown = false;
        using var waitingOn = new Disposer(() => { if (!windingDown) Volatile.Write(ref _waitsOnOwnWork, false); });

        // 🔴 The quest is read again while the work runs, never only at the turn's end. Read once, a quest that closed
        // done during the wait (its agent closing it in a turn its harness woke for one piece of work, while a long test run went
        // on) would hold its session working until the timeout killed it, a slot of the cap held all the while. Once it no
        // longer waits, and no turn runs, the session ends here and the work still running ends with it.
        using var looking = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var cut = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var closed = QuestClosedAsync(waits, looking.Token);
        var ending = background.EndedAsync(wiring.Ended, cut.Token, work.Select(each => each.Id));
        DateTimeOffset? closedAt = null;
        try
        {
            if (await Task.WhenAny(ending, closed).ConfigureAwait(false) == closed && closed.IsCompletedSuccessfully)
            {
                // A turn that speaks is never cut. Work that all ends meanwhile goes on to the woken turn's wait, the quest already read.
                closedAt = closed.Result;
                // A stream that ends meanwhile ends the work's wait too, which says so below.
                var could = wake.IdleOrQuietAsync(wiring.Prompted, closed.Result, wiring.QuestDoneQuiet, wiring.Ended, looking.Token);
                if (await Task.WhenAny(ending, could).ConfigureAwait(false) == could && could.IsCompletedSuccessfully && could.Result
                    && background.Running.Count > 0)
                {
                    await cut.CancelAsync().ConfigureAwait(false);
                    try
                    {
                        await ending.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException cancelled) when (cancelled.CancellationToken == cut.Token && !ct.IsCancellationRequested)
                    {
                        // The wait this ended: nothing more is read of the work, which ends with the session.
                    }

                    ct.ThrowIfCancellationRequested();
                    windingDown = true;
                    if (await EndAsync(closed.Result, waited, ct).ConfigureAwait(false)) Note(BackgroundNotes.TurnLeft());
                    if (background.Running is { Count: > 0 } left) Note(BackgroundNotes.WaitOver(left));
                    return null;
                }
            }
        }
        finally
        {
            await looking.CancelAsync().ConfigureAwait(false);
        }

        var ended = await ending.ConfigureAwait(false) ?? throw wiring.StreamEnded(waited);

        Note(BackgroundNotes.Ended(ended));
        return await AfterEndedAsync(BackgroundNotes.EndedSaid(ended), waited, waits, ct, closedAt).ConfigureAwait(false);
    }

    /// <summary>
    /// Once the session's background work has ended: the end of the turn its harness wakes for it, or, where none wakes
    /// within <see cref="AcpWake.Quiet"/>, the answer to Daoris's go-on, which says <paramref name="said"/>. Null where the quest no
    /// longer waits: it is read again while the session waits, as it is while work runs, from
    /// <paramref name="closedAt"/> where a look already read it closed.
    /// </summary>
    /// <remarks>
    /// <para><b>A closed quest gets no go-on</b>: the go-on is decided on a look taken at its own moment, raced against the running look,
    /// and is written only where, under the write lock, what it is decided on still holds (<see cref="WokenOrGoOnAsync"/>).</para>
    ///
    /// <para><b>A turn that speaks is never cut.</b> Once the quest reads closed, the turn the session waited for (its harness's woken
    /// turn, or the go-on's) may still end as the agent's. Otherwise the session's own waits end once no turn runs, or a turn is left
    /// silent for <see cref="ITurnEndWiring.QuestDoneQuiet"/> (<see cref="AcpWake.Judge"/>): the wake's, a go-on not yet written, a transient try's.
    /// Each observes that cut and none puts anything on the wire as it stops; then <see cref="EndAsync"/> judges
    /// at the moment it ends, so a turn that acted since is waited for again.</para>
    ///
    /// <para><b>Only the cut's own cancellation is swallowed</b>: any other ending of the wait it cut, a failure or a cancellation on
    /// another token, is the run's.</para>
    /// </remarks>
    /// <exception cref="DriverException">The agent's stream ended first, said as <paramref name="waited"/>.</exception>
    private async Task<string?> AfterEndedAsync(
        string said, string waited, Func<CancellationToken, Task<bool>> waits, CancellationToken ct, DateTimeOffset? closedAt = null)
    {
        using var looking = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var cut = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var closed = closedAt is { } at ? Task.FromResult(at) : QuestClosedAsync(waits, looking.Token);
        var wentOn = false;
        var after = WokenOrGoOnAsync(said, waited, waits, closed, () => wentOn = true, cut.Token);
        try
        {
            DateTimeOffset since;
            if (await Task.WhenAny(after, closed).ConfigureAwait(false) == after || !closed.IsCompletedSuccessfully)
            {
                // The turn it waits for ends; or the go-on's own look reads the quest closed, so nothing is sent and nothing runs.
                if (await after.ConfigureAwait(false) is { } stop) return stop;
                since = wiring.Clock.GetUtcNow();
            }
            else
            {
                since = closed.Result;
                var could = wiring.Wake.IdleOrQuietAsync(wiring.Prompted, since, wiring.QuestDoneQuiet, wiring.Ended, looking.Token);
                if (await Task.WhenAny(after, could).ConfigureAwait(false) == after)
                {
                    if (await after.ConfigureAwait(false) is { } stop) return stop;
                }
                else
                {
                    ct.ThrowIfCancellationRequested();
                    if (await CutAsync(after, cut, ct).ConfigureAwait(false) is { } stop) return stop;
                    if (!could.Result) throw wiring.StreamEnded(waited);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (await EndAsync(since, waited, ct).ConfigureAwait(false)) Note(BackgroundNotes.TurnLeft());
            else if (!wentOn) Note(BackgroundNotes.QuestClosed());
            return null;
        }
        finally
        {
            await looking.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The session's own wait cut, and awaited to its end, which the cut bounds since every wait in it observes the cut: only the cut's own cancellation is swallowed; any other end of it, a failure or a cancellation on another
    /// token, is the run's, thrown as it comes.
    /// </summary>
    internal static async Task<string?> CutAsync(Task<string?> after, CancellationTokenSource cut, CancellationToken ct)
    {
        await cut.CancelAsync().ConfigureAwait(false);
        try
        {
            return await after.ConfigureAwait(false);
        }
        catch (OperationCanceledException cancelled) when (cancelled.CancellationToken == cut.Token && !ct.IsCancellationRequested)
        {
            // The wait this cut, or the silent go-on's own wait for its answer: the quest closed, and what the agent would have said is not owed.
            return null;
        }
    }

    /// <summary>
    /// <see cref="AfterEndedAsync"/>'s wait, bounded only by the session's own bounds: the turn its harness woke, or
    /// Daoris's go-on. Null where the quest reads closed and nothing is sent; <paramref name="wentOn"/> is told once the go-on is written.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The go-on is decided where it is written</b>. It needs the quest still waiting, read at its
    /// own moment, since the look and the wake's quiet both run a minute and the running look alone let the quiet run out between a close
    /// and the look that would read it. That read takes a service's time: the harness may start a turn meanwhile, which takes a prompt and
    /// may never answer it, or the running look may read the close first. So the read is raced against the running look, and the go-on is
    /// written only where, under the write lock, nothing is read of the agent since the quiet, no turn its harness started runs, no
    /// prompt is open and no close is read (<see cref="AcpWake.StillQuiet"/>). Where any of that moved, nothing is written, and the wait
    /// begins again from the wake.
    /// </remarks>
    private async Task<string?> WokenOrGoOnAsync(
        string said, string waited, Func<CancellationToken, Task<bool>> waits, Task closed, Action wentOn, CancellationToken ct)
    {
        var wake = wiring.Wake;
        while (true)
        {
            switch (await wake.AfterAsync(wiring.Prompted, wiring.WakeQuiet, wiring.Ended, ct).ConfigureAwait(false))
            {
                case true:
                    // The turn its harness woke has ended, by the adapter's word: a turn's end as an answer's is, with no counts,
                    // since no answer carried any.
                    wiring.EndWokenTurn();
                    return WokenStop;
                case null:
                    throw wiring.StreamEnded(waited);
            }

            var told = wake.Told;
            var reading = StillWaitsAsync(waits, ct);
            if (await Task.WhenAny(reading, closed).ConfigureAwait(false) == closed || !await reading.ConfigureAwait(false)) return null;

            try
            {
                return await wiring.PromptingAsync(
                        () => wiring.SendOwnAsync(
                            $"Your background work has ended: {said}. Go on with your quest from where you left off.",
                            ct, sent: wentOn, when: () => !closed.IsCompleted && wake.StillQuiet(told, wiring.Prompted)),
                        ct)
                    .ConfigureAwait(false);
            }
            catch (AcpTurnedAway)
            {
                // What the go-on is decided on moved during its read: the wait goes on from the wake, or ends with the cut.
                ct.ThrowIfCancellationRequested();
            }
        }
    }

    /// <summary>
    /// Whether the quest still waits, read now. A look that cannot be read says nothing, so the go-on goes:
    /// an unread look is never the quest's close.
    /// </summary>
    private static async Task<bool> StillWaitsAsync(Func<CancellationToken, Task<bool>> waits, CancellationToken ct)
    {
        try
        {
            return await waits(ct).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return true;
        }
    }

    /// <summary>
    /// Where a session whose quest no longer waits ends, judged at the moment it ends, after every wait
    /// of its own is over: once <see cref="AcpWake.Judge"/> reads it idle, false, and the run's own end follows; once it
    /// reads a silent turn, true, the turn left and the run's door shut in the same step. A turn that began, spoke,
    /// opened a call or asked the person since the session last looked is waited for again, never ended on what is read before.
    /// </summary>
    /// <remarks>
    /// The judgement is taken under the write lock: no prompt is half written as it is read, and none is admitted after the door shuts.
    /// The wait for it ends with the agent's stream too: a call left open, or a request, can never close
    /// once the stream is over.
    /// </remarks>
    /// <exception cref="DriverException">The agent's stream ended first, said as <paramref name="waited"/>.</exception>
    private async Task<bool> EndAsync(DateTimeOffset since, string waited, CancellationToken ct)
    {
        while (true)
        {
            if (!await wiring.Wake.IdleOrQuietAsync(wiring.Prompted, since, wiring.QuestDoneQuiet, wiring.Ended, ct).ConfigureAwait(false))
            {
                throw wiring.StreamEnded(waited);
            }

            await wiring.WriteLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                switch (wiring.Wake.Judge(wiring.Prompted, since, wiring.QuestDoneQuiet).Now)
                {
                    case QuestDone.Idle:
                        return false;
                    case QuestDone.Quiet:
                        wiring.Shut();
                        return true;
                }
            }
            finally
            {
                wiring.WriteLock.Release();
            }
        }
    }

    /// <summary>
    /// Completes, with when it read so, once the quest, read again every <see cref="ITurnEndWiring.BackgroundLook"/> while the session waits on its own
    /// background work or on the turn its harness wakes for it, no longer waits on it.
    /// </summary>
    /// <remarks>
    /// A look that cannot be read says nothing, so the session goes on waiting and looks again: silence is never read as the quest's
    /// close, and a wait cut on a service's hiccup would end a session whose quest still waits, which then parks on the person.
    /// </remarks>
    private async Task<DateTimeOffset> QuestClosedAsync(Func<CancellationToken, Task<bool>> waits, CancellationToken ct)
    {
        while (true)
        {
            await Task.Delay(wiring.BackgroundLook, wiring.Clock, ct).ConfigureAwait(false);
            try
            {
                if (!await waits(ct).WaitAsync(ct).ConfigureAwait(false)) return wiring.Clock.GetUtcNow();
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Unread: the next look reads it again.
            }
        }
    }
}
