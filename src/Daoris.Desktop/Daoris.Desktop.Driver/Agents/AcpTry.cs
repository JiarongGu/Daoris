namespace Daoris.Driver;

/// <summary>
/// One try of a transient episode (<see cref="AcpEpisodes"/>): one prompt of the person's held words, verbatim and in the order said,
/// then Daoris's go-on, kept through the run's dispatcher before the episode's first try and carried as a plain block on every later
/// one, neither kept nor shown again. It passes the run's door as every prompt does, and reaches the session through
/// <see cref="ITurnPromptWiring"/>.
/// </summary>
internal sealed class AcpTry(ITurnPromptWiring wiring, AcpPrompting prompting)
{
    /// <summary>The try written and answered: the stop reason of its answer.</summary>
    public async Task<string> TryAsync(TransientEpisode episode, CancellationToken ct)
    {
        var retry = wiring.Retry!;
        var held = wiring.EpisodeWords?.TakeHeld() ?? [];
        // The held words go through the run's door from here, so a run that ends before the try is written hands them back.
        var passage = wiring.Open.Enter(held);
        var text = TransientRetry.GoOnText(episode.Words);
        IAgentWord? goOn = null;
        if (episode.Tries == 1 && retry.KeepGoOn is { } keep)
        {
            try
            {
                goOn = await keep(episode, text, ct).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException cancelled || cancelled.CancellationToken != ct)
            {
                // A keep that failed, or cancelled on its own, costs the go-on its record, never the try.
                ct.ThrowIfCancellationRequested();
                wiring.Say($"[the go-on could not be kept: {error.Message}]");
            }
        }

        if (wiring.SessionId is null) throw new DriverException("this ACP session is not open — nothing can be prompted on it.");

        // 🔴 A kept word goes to the wire only once its record acknowledged its attempt: the dispatcher's mark fails open, and a word
        // written unmarked whose take-off then failed would read as never sent at a restart. One of the person's that the record would not
        // mark is not written: it waits on its record for the next run, which sends it once. The go-on unmarked is settled without the
        // wire, its kept copy off the record, and goes as the try's plain block. Each mark is waited for only until the try is cut; the
        // held words then go back with the run's, through the door.
        List<IAgentWord> going = [];
        foreach (var word in held)
        {
            if (await MarkedAsync(word).WaitAsync(ct).ConfigureAwait(false)) going.Add(word);
            else wiring.Say($"[the record did not mark `{word.Id}` attempted, so no try sends it: it waits on its record for the next run]");
        }

        if (goOn is not null && !await MarkedAsync(goOn).WaitAsync(ct).ConfigureAwait(false))
        {
            goOn.Went?.Invoke();
            goOn = null;
        }

        List<IAgentWord> words = [.. going, .. goOn is null ? [] : new[] { goOn }];
        List<string> texts = [.. going.Select(word => word.Prompt), text];
        var prompt = AcpPrompting.Blocks(texts, [.. going.SelectMany(word => word.Files)]);

        var open = new OpenPrompt(words.FirstOrDefault(), [.. words.Skip(1)]);
        // A word the record would not mark waits on its record: it is not handed back with the rest.
        wiring.Open.Going(passage, going);

        var wrote = words.Select(each => prompting.Wrote(null, each)).OfType<Action>().ToList();
        try
        {
            // A try the cut reached before its write is never written, and goes back with the run's words; the cut alone cancels nothing
            // on the wire.
            var result = await wiring.RequestAsync(
                    "session/prompt", new { sessionId = wiring.SessionId, prompt }, ct,
                    prompting.OnWire(open, () =>
                    {
                        // On the wire: a word said from now goes as it did, and the go-on leaves the record by its id. Where the try fails,
                        // the episode holds them again before it settles.
                        wiring.EpisodeWords?.Waited();
                        wiring.OnPrompted?.Invoke();
                        foreach (var each in wrote) each();
                        if (goOn?.Id is { } id) episode.GoOnId = id;
                    }),
                    answered: frame => prompting.Answered(open, frame), admit: () => wiring.Open.Admit(passage, open, () => !ct.IsCancellationRequested))
                .ConfigureAwait(false);
            return result.TryGetProperty("stopReason", out var reason) ? reason.GetString() ?? "unknown" : "unknown";
        }
        catch (AcpTurnedAway) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (OperationCanceledException)
        {
            if (wiring.Stopped) await wiring.CancelTurnAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// A try's word marked attempted on its record before the wire: a kept word's mark as its record answered it, and every other word,
    /// kept nowhere, marked as any is, after the attempts asked before it.
    /// </summary>
    private async Task<bool> MarkedAsync(IAgentWord word)
    {
        return await prompting.Attempted(word).ConfigureAwait(false);
    }
}
