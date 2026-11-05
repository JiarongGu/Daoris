using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A run's transient failures (AGENTERRORS1a, D210, the agent-errors design §5): a prompt or an open the agent refused with a failure its
/// reader reads transient opens an episode, which waits on the retry's clock and tries again in the same conversation. The session
/// hands it the retry, the words an episode holds and the try itself through <see cref="ITurnRetryWiring"/>.
/// </summary>
internal sealed class AcpEpisodes(ITurnRetryWiring wiring)
{
    private bool _waitsOnTransient;

    /// <summary>
    /// Whether the run waits between a transient episode's tries (AGENTERRORS1a, the design §5.1): on Daoris's own timer, never on its
    /// agent, so the session watch reads it as a wait that is not the agent's, opening no episode of its own and ending nothing.
    /// </summary>
    public bool WaitsOnTransient => Volatile.Read(ref _waitsOnTransient);

    /// <summary>
    /// One of the run's prompts (AGENTERRORS1a, the design §5.1): its stop reason, or, where the run was handed a retry and the agent
    /// answered it with a failure the reader reads transient, the stop reason of the try that answered. Any other failure is thrown as it
    /// came, and so is one in a run handed no retry: today's end.
    /// </summary>
    public async Task<string> PromptingAsync(Func<Task<string>> prompt, CancellationToken ct)
    {
        try
        {
            return await prompt().ConfigureAwait(false);
        }
        catch (AcpRefusal refused) when (wiring.Retry is not null)
        {
            var read = ReadFailure(refused);
            if (read.Kind != AgentErrorKind.Transient)
            {
                // No episode opened: today's judgement of the words, TURNFAIL1's included (§5.1).
                wiring.ForgetUnacknowledged();
                throw;
            }

            return await EpisodeAsync(read, ct).ConfigureAwait(false);
        }
    }

    /// <summary>A prompt's failure read by the handed reader; one the reader could not read is fatal, as today (§3.2).</summary>
    private AgentError ReadFailure(AcpRefusal refused, string where = DoorWhere.Prompt)
    {
        var retry = wiring.Retry!;
        AgentError read;
        try
        {
            read = retry.Read(refused);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            wiring.Say($"[the agent's failure could not be read: {error.Message}]");
            read = new AgentError(AgentErrorKind.Fatal, DoorFailures.OfPrompt(refused), AgentErrors.Unread);
        }

        if (read.Failure.Where != where) read = read with { Failure = read.Failure with { Where = where } };
        retry.Kept(read);
        return read;
    }

    /// <summary>
    /// The open's own phase (AGENTERRORS1a2, D210, the agent-errors design §5.6): the raw opening operation (<c>session/new</c>,
    /// <c>session/resume</c> or <c>session/load</c>), and, where the run was handed a retry and the agent refused it with a failure read
    /// transient, the SAME operation again on the episode's schedule. Never the handshake again: it runs once per session. Read after
    /// D205's choice refusal, which comes later in the open, and the <c>Gone</c> and <c>Elsewhere</c> codes, which pass through as
    /// they came; anything else not transient is thrown as today.
    /// </summary>
    /// <remarks>
    /// Nothing was asked of the agent yet, so a try sends no go-on: once the operation answers, the run's own first prompt goes once, as
    /// it would have. Words said meanwhile wait in the inbox, which is not attached until the open is done.
    /// </remarks>
    /// <exception cref="AcpTransientExhausted">Every try failed: the run ends past its bound with nothing prompted.</exception>
    public async Task<JsonElement> OpeningAsync(Func<Task<JsonElement>> open, CancellationToken ct)
    {
        TransientEpisode? episode = null;
        while (true)
        {
            try
            {
                return await open().ConfigureAwait(false);
            }
            catch (AcpRefusal refused) when (wiring.Retry is not null && refused.Code != AcpProtocol.ResourceNotFound
                                             && !string.Equals(refused.Reason, AcpProtocol.ActiveWriter, StringComparison.Ordinal))
            {
                var read = ReadFailure(refused, DoorWhere.Open);
                if (read.Kind != AgentErrorKind.Transient) throw;
                var retry = wiring.Retry!;
                if (episode is null)
                {
                    episode = retry.Open(read);
                    episode.AtOpen = true;
                    var first = episode;
                    Said(() => retry.Began?.Invoke(first, retry.Clock.GetUtcNow() + TransientRetry.Delays[0]));
                }

                if (retry.PastBound(episode))
                {
                    episode.Exhausted = true;
                    throw new AcpTransientExhausted(episode);
                }

                await WaitAsync(episode, ct, opening: true).ConfigureAwait(false);
                // The owner's check (§5.5): no try where no more turn is wanted; nothing was prompted, so the open fails as today.
                if (!await retry.WantedAsync(ct).ConfigureAwait(false)) throw;
                episode.Tries++;
                var next = episode.Tries < retry.Limit ? retry.Clock.GetUtcNow() + TransientRetry.Delays[episode.Tries] : (DateTimeOffset?)null;
                var trying = episode;
                Said(() => retry.Tried?.Invoke(trying, trying.Tries, next));
            }
        }
    }

    /// <summary>
    /// The person's words a failed prompt left unacknowledged (§5.1, round 3's read, R3-O2): D188's COWORK1c uncertain words, kept with
    /// the episode and said by the <c>lost</c> note under their ids. Never sent again.
    /// </summary>
    private void SayUncertain()
    {
        var words = wiring.TakeUnacknowledged();
        if (words.Count == 0) return;
        wiring.Retry!.Unacknowledged(words);
        List<string> ids = [.. words.Select(word => word.Id).OfType<string>()];
        if (ids.Count == 0) return;
        wiring.Emit(new SessionEvent { Kind = SessionEventKind.Note, Text = SessionNotes.LostLine, Words = ids, Code = SessionEventCodes.Lost });
    }

    /// <summary>
    /// The person's words a limit-refused try carried (AGENTERRORS1b2): taken off the unacknowledged and handed to the retry's owner, which
    /// holds them for the limit's wake or move. A listener's failure costs a line, never the episode.
    /// </summary>
    private void HandRefused(Action<IReadOnlyList<IAgentWord>> hand)
    {
        var words = wiring.TakeUnacknowledged();
        if (words.Count > 0) Said(() => hand(words));
    }

    /// <summary>
    /// A transient episode (AGENTERRORS1a, D210, the design §5.1): the run stays running on the same session, account and choice, and
    /// tries again in the same conversation after 1, 2, 4, 8 and 16 minutes on the handed clock, each try one prompt of the person's
    /// held words and then Daoris's go-on. Answers the stop reason of the try that answered, or of the turn's end where the owner's
    /// check says no more turn is wanted.
    /// </summary>
    /// <remarks>
    /// <para><b>The wire settles first.</b> A word already sent behind the failed turn runs its own turn (<c>promptQueueing</c>): one
    /// that answers ends the wait, never the episode (round 3's read, R3-O1), since it shows only that the agent answered that word, so
    /// the go-on follows at once and counts as a try; one that fails transient is that failure and counts as one.</para>
    ///
    /// <para><b>Through the wake gate</b> (BGWAIT1): a turn its harness wakes during the wait ends the wait, never the episode; nothing is
    /// prompted into it, and the go-on goes once it has ended.</para>
    ///
    /// <para><b>Only held words are ever sent by a try.</b> A word on the wire is never sent again, on a try or after the episode.</para>
    ///
    /// <para>🔴 <b>Settling is bounded</b> (Codex's owed read of D210, finding 2): dispatch is held from the episode's first failure and
    /// from each try's (<see cref="DrivenInbox.Settling"/>), so the turns it settles are only those already written. Each that fails
    /// transient takes one of the tries left; once they are spent the rest still settle, since they are written, and count none
    /// (<see cref="TransientEpisode.SettledPast"/>), and the run ends past its bound with no try after them.</para>
    ///
    /// <para>🔴 <b>A hand-over is no recovery</b> (finding 1): a try the adapter answered at a queued word's echo handed its turn to that
    /// word. The episode and its count are kept through every hand-over, and the try counts as answered only once the turns handed over
    /// have settled with no transient failure; one that fails is the try's failure.</para>
    /// </remarks>
    /// <exception cref="AcpRefusal">A try failed with another kind: the episode is left for that kind's policy, once.</exception>
    /// <exception cref="AcpTransientExhausted">Every try failed: the run ends past its bound.</exception>
    private async Task<string> EpisodeAsync(AgentError first, CancellationToken ct)
    {
        var retry = wiring.Retry!;
        var clock = retry.Clock;
        var episode = retry.Open(first);
        wiring.EpisodeWords?.Settling();
        SayUncertain();
        Said(() => retry.Began?.Invoke(episode, clock.GetUtcNow() + TransientRetry.Delays[0]));
        var atOnce = false;

        while (true)
        {
            // The wire settles first: each word sent behind the failed turn runs its own turn (§5.1's settling turns), and no word joins it.
            wiring.EpisodeWords?.Settling();
            if (await SettleAsync(Spent, ct).ConfigureAwait(false) is { } settled) atOnce = settled;

            if (retry.PastBound(episode))
            {
                episode.Exhausted = true;
                throw new AcpTransientExhausted(episode);
            }

            if (!atOnce) await WaitAsync(episode, ct).ConfigureAwait(false);
            atOnce = false;

            // The owner's check (§5.5) and the wake gate at the try's admission: no try where no more turn is wanted, and none into a turn
            // its harness woke; the run closes as at a turn's end.
            if (!await AdmitAsync(retry, ct).ConfigureAwait(false))
            {
                wiring.EpisodeWords?.Waited();
                return AcpWaits.WokenStop;
            }

            episode.Tries++;
            var next = episode.Tries < retry.Limit ? clock.GetUtcNow() + TransientRetry.Delays[episode.Tries] : (DateTimeOffset?)null;
            Said(() => retry.Tried?.Invoke(episode, episode.Tries, next));
            string answered;
            try
            {
                answered = await wiring.TryAsync(episode, ct).ConfigureAwait(false);
            }
            catch (AcpRefusal refused)
            {
                var read = ReadFailure(refused);
                // A limit refused the try before the agent took its words (AGENTERRORS1b2): handed to whoever answers the limit, never lost.
                if (read.Kind == AgentErrorKind.Limit && retry.LimitRefused is { } refusedWords) HandRefused(refusedWords);
                else SayUncertain();
                if (read.Kind != AgentErrorKind.Transient) throw;
                continue;
            }

            // 🔴 Finding 1: the turns the try handed over settle inside the episode, the try already counted; one failing transient is
            // the try's failure, and the go-on follows at once where the last of them answered.
            var failed = false;
            if (await SettleAsync(() => failed = true, ct, stop => answered = stop).ConfigureAwait(false) is { } handed && failed)
            {
                atOnce = handed;
                continue;
            }

            return answered;
        }

        // A settling turn's failure takes one of the tries left, and once they are spent counts none (finding 2).
        void Spent()
        {
            if (episode.Tries < retry.Limit)
            {
                episode.Tries++;
                return;
            }

            episode.SettledPast++;
            wiring.Say("[a turn already on the wire failed after every try was spent: it counts none, and no try follows it]");
        }
    }

    /// <summary>
    /// The turns already on the wire, each awaited in the order sent (§5.1's settling turns): <paramref name="failed"/> told for each that
    /// fails transient, <paramref name="answered"/> for each that answers. Answers whether the last of them answered, or null where none
    /// was on the wire.
    /// </summary>
    /// <remarks>
    /// 🔴 Bounded by <paramref name="ct"/> (RUNENDS1 after review, finding 1): a word on the wire whose turn never answers was waited on for
    /// ever, and the cut of a closed quest's silent turn could not end that wait. A word cut short here stays on the wire, open, and is
    /// said at the run's end as any such word is.
    /// </remarks>
    /// <exception cref="AcpRefusal">One failed with another kind: the episode is left for that kind's policy, once.</exception>
    private async Task<bool?> SettleAsync(Action failed, CancellationToken ct, Action<string>? answered = null)
    {
        bool? last = null;
        while (wiring.EpisodeWords?.TakeSent() is { } settling)
        {
            try
            {
                var stop = await settling.WaitAsync(ct).ConfigureAwait(false);
                answered?.Invoke(stop);
                last = true;
            }
            catch (Exception thrown) when (thrown is AcpRefusal or AcpWordTurnFailed)
            {
                var refused = thrown as AcpRefusal ?? ((AcpWordTurnFailed)thrown).Refusal;
                var read = ReadFailure(refused);
                SayUncertain();
                if (read.Kind != AgentErrorKind.Transient)
                {
                    if (thrown is AcpRefusal) throw;
                    throw refused;
                }

                failed();
                last = false;
            }
        }

        return last;
    }

    /// <summary>
    /// A try's admission (§5.1, §5.5): a turn its harness woke ends the wait, never the episode, and nothing goes into it; where work's end
    /// is owed a wake, the quiet the go-on waits for; then the owner's check. Answers false where no more turn is wanted.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Read again after the check</b> (Codex's owed read of D210, finding 7): the check is awaited, and a wake that began meanwhile
    /// was never read before the prompt went into it, BGWAIT1's unanswered prompt. The wake is read again once the check answers, and
    /// the check asked again after any woken turn has ended.
    /// </remarks>
    /// <exception cref="DriverException">The agent's stream ended while the gate waited.</exception>
    private async Task<bool> AdmitAsync(TransientRetry retry, CancellationToken ct)
    {
        var wake = wiring.Wake;
        var quieted = false;
        while (true)
        {
            if (wake.Running)
            {
                // RUNENDS1, second read (finding 4): the stream's end ends this wait too, as every wait of the session's.
                if (!await wake.IdleAsync(wiring.Prompted, wiring.Ended, ct, wiring.WakeQuiet).ConfigureAwait(false))
                {
                    throw wiring.StreamEnded("the ACP agent's stream ended while the run waited to try again after its provider failed.");
                }

                wiring.EndWokenTurn();
            }
            else if (!quieted && wake.Unseen.Count > 0)
            {
                quieted = true;
                switch (await wake.AfterAsync(wiring.Prompted, wiring.WakeQuiet, wiring.Ended, ct).ConfigureAwait(false))
                {
                    case true:
                        wiring.EndWokenTurn();
                        break;
                    case null:
                        throw wiring.StreamEnded("the ACP agent's stream ended while the run waited to try again after its provider failed.");
                }
            }

            if (!await retry.WantedAsync(ct).ConfigureAwait(false)) return false;
            if (!wake.Running) return true;
        }
    }

    /// <summary>A listener's failure costs a line, never the episode.</summary>
    private void Said(Action tell)
    {
        try
        {
            tell();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            wiring.Say($"[the retry could not be told: {error.Message}]");
        }
    }

    /// <summary>
    /// The wait before a try (§5.1), read off the handed clock: the schedule's next delay, ended at once by a word of the person's that
    /// makes a turn, or by a turn its harness woke. Every word is held while it runs. Its end stamps the session heard, through the retry.
    /// </summary>
    /// <exception cref="DriverException">The agent's stream ended while it waited.</exception>
    private async Task WaitAsync(TransientEpisode episode, CancellationToken ct, bool opening = false)
    {
        var retry = wiring.Retry!;
        var started = retry.Clock.GetUtcNow();
        using var cut = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // At the open (§5.6) no session is there to wake and no word flows yet: only the schedule ends the wait.
        var word = opening ? Task.Delay(Timeout.Infinite, cut.Token) : wiring.EpisodeWords?.Waiting() ?? Task.Delay(Timeout.Infinite, cut.Token);
        var delay = TransientRetry.Delays[Math.Min(episode.Tries, TransientRetry.Bound - 1)];
        Volatile.Write(ref _waitsOnTransient, true);
        Said(() => retry.Waits?.Invoke(true));
        Said(() => retry.WaitsUntil?.Invoke(episode, started + delay));
        try
        {
            var timer = Task.Delay(delay, retry.Clock, cut.Token);
            var woke = opening ? Task.Delay(Timeout.Infinite, cut.Token) : wiring.Wake.WokeAsync(cut.Token);
            await Task.WhenAny(timer, word, woke, wiring.Ended).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (wiring.Ended.IsCompleted && !timer.IsCompleted && !word.IsCompleted && !woke.IsCompleted)
            {
                throw wiring.StreamEnded("the ACP agent's stream ended while the run waited to try again after its provider failed.");
            }
        }
        finally
        {
            await cut.CancelAsync().ConfigureAwait(false);
            episode.Waited += retry.Clock.GetUtcNow() - started;
            Volatile.Write(ref _waitsOnTransient, false);
            Said(() => retry.Waits?.Invoke(false));
        }
    }
}
