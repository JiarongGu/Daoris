using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The prompts a session writes and the answers it reads to them. Each prompt passes the run's door (<see cref="OpenPrompts"/>), its
/// words marked on their record before the wire and in the order asked, and is written under the write lock; its answer is read on the
/// reader's thread before the next frame, where the turn is recorded and handed off to the words behind it, so the record reads in the
/// order the wire said it. The session hands it the wire and its state through <see cref="ITurnPromptWiring"/>.
/// </summary>
internal sealed class AcpPrompting(ITurnPromptWiring wiring)
{
    /// <summary>
    /// The status the record closes a call with that a steer cut and that never heard again: written when the turn's prompt is answered,
    /// so the page shows it cut, never still running.
    /// </summary>
    public const string CutStatus = "cut";

    /// <summary>The attempts marked so far, in the order the words were asked to go, and the lock that guards the chain.</summary>
    private Task _attempts = Task.CompletedTask;
    private readonly object _attemptsGate = new();

    // Whether this client asked the turn to stop, and how the last turn was cut.
    private volatile bool _cancelAsked;
    private volatile TurnCut? _turnCut;

    /// <summary>Told each of the person's words as the session takes them: a run's listener, or a conversation's.</summary>
    public Action<IAgentWord>? OnAsked { get; set; }

    /// <summary>How the last turn was cut by the agent, where it was; null otherwise.</summary>
    public TurnCut? TurnCut => _turnCut;

    /// <summary>This client asked the turn in flight to stop: a turn that ends cancelled now is its own, never the agent's cut.</summary>
    public void StopAsked() => _cancelAsked = true;

    /// <summary>
    /// One prompt on the open session, answered by the agent's stop reason: the driver's own, or the person's words
    /// (<paramref name="words"/>), which the session takes as they go when no prompt is open, and when the one before them is answered
    /// otherwise, the moment the agent hands the running turn off to them.
    /// </summary>
    public Task<string> SendPromptAsync(
        string text, IReadOnlyList<KeptFile>? files, CancellationToken ct, Action? sent, IAgentWord? words, Func<bool>? when = null) =>
        SendPromptAsync([text], files, ct, sent, words, when);

    /// <summary>One prompt of one or more text blocks, each its own, then the files as links.</summary>
    /// <param name="when">
    /// Read under the write lock as the prompt is written: false where what the prompt was decided on no longer holds, and it is not
    /// written at all (<see cref="AcpTurnedAway"/>). Null always holds.
    /// </param>
    public async Task<string> SendPromptAsync(
        IReadOnlyList<string> texts, IReadOnlyList<KeptFile>? files, CancellationToken ct, Action? sent, IAgentWord? words,
        Func<bool>? when = null)
    {
        if (wiring.SessionId is null) throw new DriverException("this ACP session is not open — nothing can be prompted on it.");

        var prompt = Blocks(texts, files);
        // Through the run's door from before the mark to the write, so a run that ends between the two hands the words back and writes
        // nothing after.
        var passage = wiring.Open.Enter([words]);
        // A kept word is marked attempted on its record before the wire, in the order the prompts were asked for, so a shell that comes up
        // again never sends it twice; and written only once the record named it marked, else it waits on its record and is said.
        var marked = await Attempted(words).ConfigureAwait(false);

        // Open from its admission, under the write lock: the prompts open are those on the wire, in the order written.
        var open = wiring.Open.Opened(passage, marked, words);

        JsonElement result;
        try
        {
            result = await wiring.RequestAsync(
                "session/prompt",
                new { sessionId = wiring.SessionId, prompt },
                ct, OnWire(open, Wrote(sent, words)), answered: frame => Answered(open, frame), admit: () => wiring.Open.Admit(passage, open, when))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The person's stop is a verb on this wire: cancel the turn in flight rather than kill the process, so the agent winds up its
            // own work. Sent here rather than from a cancellation callback, which fires on whatever thread cancelled, in the worst case one
            // already inside the write lock.
            // 🔴 Only the person's stop, or the driver's shutdown, puts a cancel on the wire. The session's own cut, as it leaves a closed
            // quest's silent turn, stops its wait for the answer and nothing more: a cancel sent then lands on whatever turn runs, a turn
            // that spoke since the quiet was read among them.
            if (wiring.Stopped) await wiring.CancelTurnAsync().ConfigureAwait(false);
            throw;
        }

        return result.TryGetProperty("stopReason", out var reason)
            ? reason.GetString() ?? "unknown"
            : "unknown";
    }

    /// <summary>
    /// A word sent during a running turn, as <see cref="SendPromptAsync(string, IReadOnlyList{KeptFile}?, CancellationToken, Action?, IAgentWord?, Func{bool}?)"/>
    /// sends it. 🔴 Where the run was handed a retry and the word's prompt was refused after the word was taken (the turn before it
    /// answered, handing the turn to it), the refusal is thrown as <see cref="AcpWordTurnFailed"/>: the turn that failed is the one the
    /// word opened, never that word's own refusal. Refused before it was taken, it is the word's.
    /// </summary>
    public async Task<string> MidTurnAsync(IAgentWord words, CancellationToken ct)
    {
        try
        {
            return await SendPromptAsync(words.Prompt, words.Files, ct, sent: null, words).ConfigureAwait(false);
        }
        catch (AcpRefusal refused) when (wiring.Open.RefusedTaken(words))
        {
            throw new AcpWordTurnFailed(refused);
        }
    }

    /// <summary>
    /// A kept word's attempt, marked on its record before the wire, after every attempt asked for before it, so two words said at once
    /// are written in the order their dispatcher handed them. Completed at once for words kept nowhere, and for the driver's own prompts.
    /// True where the word may be written: nothing marks it, or the record named it marked. Never faults.
    /// </summary>
    public Task<bool> Attempted(IAgentWord? words)
    {
        if (words?.Attempting is not { } attempting) return Task.FromResult(true);
        lock (_attemptsGate)
        {
            var acknowledged = AfterAsync(_attempts, words, attempting);
            _attempts = acknowledged;
            return acknowledged;
        }

        static async Task<bool> AfterAsync(Task before, IAgentWord words, Func<Task<IReadOnlyList<string>>> attempting)
        {
            await before.ConfigureAwait(false);
            var marked = await attempting().ConfigureAwait(false);
            return words.Id is not { } id || marked.Contains(id, StringComparer.Ordinal);
        }
    }

    /// <summary>A prompt's write returned: marked on the wire, then what the write's return tells.</summary>
    public Action OnWire(OpenPrompt open, Action? wrote) => () =>
    {
        wiring.Open.Written(open);
        wrote?.Invoke();
    };

    /// <summary>
    /// What the write's return tells: the caller's own, then, for words under an id, their receipt <c>sent</c>, and their dispatcher,
    /// which takes a kept word off its record. <i>Sent</i> is the write's return, never the prompt's answer, which comes with the turn's end.
    /// </summary>
    public Action? Wrote(Action? sent, IAgentWord? words)
    {
        if (words?.Id is not { } id) return sent;
        return () =>
        {
            sent?.Invoke();
            wiring.Emit(new SessionEvent { Kind = SessionEventKind.Receipt, Id = id, Receipt = Receipts.Sent });
            try
            {
                words.Went?.Invoke();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The word went: what keeps the record is its dispatcher's, which says nothing more here.
            }
        };
    }

    /// <summary>A prompt's content: each text its own block, then each file a <c>resource_link</c> to where it is kept.</summary>
    public static object[] Blocks(IReadOnlyList<string> texts, IReadOnlyList<KeptFile>? files) =>
    [
        .. texts.Select(text => (object)new { type = "text", text }),
        .. (files ?? []).Select(file => (object)new { type = "resource_link", uri = new Uri(file.Path).AbsoluteUri, name = file.Name }),
    ];

    /// <summary>
    /// A prompt answered, on the reader's thread before the next frame is read: its turn event, as the wire's word and nothing more, and
    /// the person's words behind it taken, so the record reads in the order the wire said it and what the agent does next is the words'
    /// turn.
    /// </summary>
    public void Answered(OpenPrompt open, JsonElement frame)
    {
        var answered = frame.TryGetProperty("result", out var result);
        if (answered)
        {
            // A word an error handed nothing to, answered before the agent did anything else for it: it was taken after all.
            if (open.Words is not null && wiring.Open.TakeLate(open) is { } own) AskedAll(own);

            // A call a steer cut that never heard again ends with the turn it was cut in, as cut.
            CloseCut();
            // The turn's own counts, where the agent reported them: the response's `usage`.
            var tokens = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("usage", out var usage)
                ? TurnTokens.Read(usage, "inputTokens", "outputTokens", "cachedReadTokens", "cachedWriteTokens")
                : null;
            var stopReason = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("stopReason", out var reason)
                ? reason.GetString() ?? "unknown"
                : "unknown";
            wiring.Emit(new SessionEvent
            {
                Kind = SessionEventKind.Turn,
                StopReason = stopReason,
                Tokens = tokens,
                Models = wiring.Measure.ModelsOf(result, tokens),
            });

            // A turn that ends cancelled when this client never asked it to stop was cut by the agent, after a refusal if one came.
            _turnCut = stopReason == "cancelled" && !_cancelAsked ? new TurnCut(wiring.Refused.Last) : null;
            wiring.Refused.Last = null;
            _cancelAsked = false;

            // The turn that took the words ended, their receipt `ended`: what Daoris saw, never an answer; and only where the words were
            // taken here.
            if (wiring.Open.Took(open) && stopReason == "end_turn" && open.Words?.Id is { } word)
            {
                wiring.Emit(new SessionEvent { Kind = SessionEventKind.Receipt, Id = word, Receipt = Receipts.Ended });
            }
        }
        else
        {
            // An error answered the prompt, and the turn its updates named ends without a response: its names are not the next's.
            wiring.Measure.Forget();
        }

        // A word's disposition is read before its prompt leaves, from its turn; the words behind it are taken where it was answered.
        var taken = wiring.Open.Leaves(open, answered, wiring.Retry, out var last);
        if (taken is not null) AskedAll(taken);

        // 🔴 The last open prompt is answered, so the turn is over; a claim written from here on would start a turn of its own, so the
        // inbox refuses claims before the run judges its next step.
        if (last) wiring.Inbox?.LastAnswered();

        // A prompt fewer is open: the wait for a woken turn reads again whether its quiet may run, and a closed quest's quiet counts from
        // this answer.
        wiring.Wake.Answered();
    }

    /// <summary>
    /// An update of the oldest open prompt's turn shows its words taken, whatever answers it; after an error, the next word is taken by
    /// this first thing the agent does for it, told before the update is kept, so the answer never sits above the question.
    /// </summary>
    public void Heard()
    {
        wiring.Open.Heard();
        if (wiring.Open.TakeLate() is { } late) AskedAll(late);
    }

    /// <summary>Every word a prompt carries told taken, in the order said: a try carries the held words in one prompt.</summary>
    public void AskedAll(OpenPrompt open)
    {
        foreach (var words in open.All) Asked(words);
    }

    /// <summary>Tell the listener the session took the person's words; its failure costs a line, never the turn.</summary>
    public void Asked(IAgentWord words)
    {
        try
        {
            OnAsked?.Invoke(words);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            wiring.Say($"[the person's words could not be recorded: {error.Message}]");
        }
    }

    /// <summary>The calls a steer cut that never heard again, closed in the record as cut when the turn's prompt is answered.</summary>
    public void CloseCut()
    {
        foreach (var id in wiring.Calls.TakeCut()) wiring.Emit(new SessionEvent { Kind = SessionEventKind.Tool, Id = id, Status = CutStatus });
    }

    /// <summary>The turn its harness woke ended, by the adapter's word: closed as an answered turn is, with no counts, since no answer carried any.</summary>
    public void EndWokenTurn()
    {
        CloseCut();
        wiring.Emit(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = AcpWaits.WokenStop, Models = wiring.Measure.ModelsOf(default, null) });
    }
}
