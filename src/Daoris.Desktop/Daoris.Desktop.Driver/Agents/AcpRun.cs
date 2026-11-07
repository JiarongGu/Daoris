namespace Daoris.Driver;

/// <summary>
/// A driven session's whole life on the protocol door: the open or the resume, the target as the first prompt, then the person's words
/// one prompt each, in the order said, so the session keeps its context, until nothing is held and no word is on its way. The run's
/// door shuts at its end before its words are counted, so a word is the session's, handed back, or said unreached, never lost.
/// It reaches the session through <see cref="ITurnRunWiring"/>, and judges a turn's end through <see cref="AcpWaits"/>.
/// </summary>
internal sealed class AcpRun(ITurnRunWiring wiring, AcpPrompting prompting, AcpSteering steering, AcpWaits ends)
{
    /// <summary>The run, end to end; its parameters are <see cref="AcpSession.RunAsync"/>'s.</summary>
    public async Task<AcpOutcome> RunAsync(
        string cwd, string prompt, CancellationToken ct, ConnectorPlan? plan,
        IAgentInbox? inbox, Action<IAgentWord>? asked, string? resume,
        IReadOnlyList<string>? blocks, Action? prompted, IReadOnlyList<KeptFile>? files,
        Func<CancellationToken, Task<bool>>? waitsOnBackground,
        Func<Task<(IReadOnlyList<string> Blocks, IReadOnlyList<KeptFile>? Files)?>>? attempting, TransientRetry? retry)
    {
        wiring.Begin(ct, asked, inbox, retry, prompted);
        try
        {
            try
            {
                await ProtocolOpen.BeginAsync(
                    resume, () => wiring.OpenAsync(cwd, ct, plan), named => wiring.ResumeAsync(cwd, named, ct, plan)).ConfigureAwait(false);
            }
            catch (AcpChoiceNotOffered refused)
            {
                // The choice refused at the open, no prompt sent, so nothing of the person's is taken; the session closes here and its
                // record ends as the start's re-ask says.
                wiring.Say($"[{refused.Message}]");
                await wiring.CloseAsync(ct).ConfigureAwait(false);
                return new AcpOutcome(AcpOutcome.ChoiceNotOffered, wiring.SessionId ?? "", wiring.Updates, wiring.Usage) { NotOffered = refused.Refusal };
            }

            // Where the agent says it takes a prompt during its turn, each word goes the moment it is said and reaches it at its next step;
            // anywhere else it waits for the turn's end. Words flow only once the target is on the wire, so none overtakes the prompt it
            // follows. The person's *Send now* steers into the running turn where the agent offers that too.
            inbox?.Attach(
                wiring.CancelTurnAsync,
                wiring.NextStep ? words => prompting.MidTurnAsync(words, ct) : null,
                wiring.Steers ? words => steering.SteeredAsync(words, ct, handBack: inbox is null ? null : inbox.HandBack) : null);
            Action? sent = inbox is null && prompted is null
                ? null
                : () =>
                {
                    inbox?.Flow();
                    prompted?.Invoke();
                };
            // The words a resumed run carries are marked attempted on its record once the agent took the conversation back and before the
            // prompt carrying them is written: acknowledged, and the blocks and files read again from the words it names, so an answer
            // naming fewer than asked sends only those; none named, nothing is prompted and the words wait on their record.
            if (attempting is not null)
            {
                if (await attempting().ConfigureAwait(false) is not { } named) throw new AcpWordsUnmarked(AcpWordsUnmarked.Sentence);
                blocks = named.Blocks;
                files = named.Files;
            }

            // Each prompt site of a run handed a retry reads its failure, and a transient one opens the episode here.
            var stopReason = await wiring.PromptingAsync(
                    () => blocks is { Count: > 0 }
                        ? prompting.SendPromptAsync(blocks, files, ct, sent, words: null)
                        : prompting.SendPromptAsync(prompt, files: null, ct, sent, words: null),
                    ct)
                .ConfigureAwait(false);

            // What the loop asks of the quest is its own wait on background work, or a carry-on owed for a done quest's tree.
            var turnWaits = ends.Of(waitsOnBackground);

            // The person's words, one prompt each, in the order said. A word sent during the turn is waited for until it is answered; one
            // held is prompted now, and a turn stopped to send it ends `cancelled` before it goes.
            while (true)
            {
                if (wiring.Stages.Of<AcpLimitTurn>().Ended(wiring.LimitSaid) is { } limited) throw limited;

                // A turn that ended while the session's own background work runs, where its quest would park it on the person, waits for
                // that work and goes on with a turn that says it ended. Words held for the turn's end go first; words that go at once
                // still go while it waits.
                if (!Holds(inbox) && await ends.AfterBackgroundAsync(turnWaits, ct).ConfigureAwait(false) is { } wentOn)
                {
                    stopReason = wentOn;
                    continue;
                }

                // A silent turn of a closed quest is left and the run's door shut: every word still held goes back unprompted.
                if (wiring.DoorShut)
                {
                    var waiting = inbox?.Close() ?? [];
                    wiring.Open.HandBack(waiting);
                    break;
                }

                // The turn ended with its quest done and its tree uncommitted, and nothing of its own runs: not its end.
                if (!Holds(inbox) && await ends.CarryOnAsync(ct).ConfigureAwait(false) is { } carriedOn)
                {
                    stopReason = carriedOn;
                    continue;
                }

                if (inbox?.NextOrClose() is not { } next) break;

                // A steer, or a stop still on its way, is no turn of its own: the run waits for it, then asks again.
                if (next.Waits is { } waits)
                {
                    await waits.ConfigureAwait(false);
                    continue;
                }

                if (next.Held is { } held)
                {
                    // The setup changed while the turn ran, so the run ends at this turn's end and every word held goes back unprompted,
                    // to go on in the same conversation under the new setup. Daoris's words go back unjudged: the look judges them on the
                    // record before its go-on.
                    if (wiring.SetupChanged?.Invoke() == true)
                    {
                        var rest = inbox!.Close();
                        wiring.Open.HandBack([held, .. rest]);
                        break;
                    }

                    // Daoris's word held for the turn's end is judged again by its key; one whose fact a later submission answered is
                    // dropped, said so, and never prompted late.
                    if (!await held.GoesAsync(ct).ConfigureAwait(false))
                    {
                        wiring.Emit(held.Dropped());
                        // Settled without the wire, so off its record too.
                        held.Went?.Invoke();
                        continue;
                    }

                    try
                    {
                        stopReason = await wiring.PromptingAsync(() => prompting.SendPromptAsync(held.Prompt, held.Files, ct, sent: null, held), ct)
                            .ConfigureAwait(false);
                    }
                    catch (AcpWordsUnmarked)
                    {
                        // The record does not name it marked, so it is not written: it waits on its record, said, never the session's work.
                        wiring.Note(WordsNotes.Unreached(1, "were not sent", UnreachedWhy.Unmarked));
                    }

                    continue;
                }

                try
                {
                    stopReason = await next.Answer!.ConfigureAwait(false);
                }
                catch (AcpWordTurnFailed failed)
                {
                    // 🔴 The word is taken, so the turn that failed is the one it opened, and its refusal is that turn's prompt failing:
                    // read as any prompt site's, a transient one opening the episode.
                    stopReason = await wiring.PromptingAsync(() => Task.FromException<string>(failed.Refusal), ct).ConfigureAwait(false);
                }
                catch (AcpRefusal refused)
                {
                    // A word the agent would not take costs that word, said in its own words, never the session's work.
                    wiring.Note(WordsNotes.Refused(refused.Words));
                }
                catch (AcpWordsUnmarked)
                {
                    // A word the record does not name marked is not written: it waits on its record, said, never the session's work.
                    wiring.Note(WordsNotes.Unreached(1, "were not sent", UnreachedWhy.Unmarked));
                }
            }

            await wiring.CloseAsync(ct).ConfigureAwait(false);
            return new AcpOutcome(stopReason, wiring.SessionId!, wiring.Updates, wiring.Usage) { Cut = prompting.TurnCut };
        }
        finally
        {
            // 🔴 The run's door shuts before its words are counted. A word on its way to the wire, its mark still awaited, is in no list
            // the end reads, and would be written to the closed session once the mark landed; it goes back with the rest
            // (HandedBack), and nothing is written after.
            await ShutAsync().ConfigureAwait(false);

            // Words sent while it worked that it never took: said, never shown for ever as on their way.
            if (Untaken() is > 0 and var untaken)
            {
                wiring.Note(WordsNotes.Unreached(untaken, "while it worked never reached it", UnreachedWhy.EndedFirst));
            }

            // A driven session's end is its last turn's: what it left running ends with it, and is said while the caller's transcript is
            // still open, before the reader is let go. Named for the driver, which ends the agent's tree where it does not exit with that
            // work still running.
            wiring.Left(wiring.Background.Running);
            await wiring.EndStreamsAsync().ConfigureAwait(false);
            wiring.Release();
        }
    }

    /// <summary>Whether the person's words are held for the turn's end: those go before anything else.</summary>
    private static bool Holds(IAgentInbox? inbox) => inbox?.Holds == true;

    /// <summary>
    /// The run's door shut at its end, between two writes, never inside one: from here no word of the run's reaches the wire. The inbox
    /// holds every word said from now, so none is dispatched to the session, and each goes back with the inbox's close; a word dispatched
    /// and not yet written is turned away at its write and goes back to the driver; one already written is the session's, answered or
    /// said unreached (<see cref="Untaken"/>).
    /// </summary>
    private async Task ShutAsync()
    {
        await wiring.WriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            wiring.Shut();
        }
        finally
        {
            wiring.WriteLock.Release();
        }
    }

    /// <summary>How many of the person's words are on the wire and not yet taken, forgotten as they are counted and kept as unreached.</summary>
    /// <remarks>
    /// 🔴 <b>After an episode opened, a written prompt with no answer is uncertain, never unreached</b>: a transport lost behind a written
    /// word faults its request with no answer to read its disposition from, and kept as unreached the driver would keep it to go again
    /// (<see cref="WordsLeft"/>). It is said by the <c>lost</c> note and never sent again. A run with no episode keeps it unreached.
    /// </remarks>
    private int Untaken()
    {
        var uncertain = wiring.Open.Untaken(wiring.Retry);
        if (uncertain.Count > 0)
        {
            wiring.Retry!.Unacknowledged(uncertain);
            List<string> ids = [.. uncertain.Select(word => word.Id).OfType<string>()];
            if (ids.Count > 0) wiring.Emit(new SessionEvent { Kind = SessionEventKind.Note, Text = SessionNotes.LostLine, Words = ids, Code = SessionEventCodes.Lost });
        }

        return wiring.Open.Unreached.Count;
    }
}
