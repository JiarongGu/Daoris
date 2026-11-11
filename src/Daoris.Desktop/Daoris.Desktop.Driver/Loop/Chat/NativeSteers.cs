namespace Daoris.Driver;

/// <summary>
/// The person's *Send now* on the native wire (<see cref="NativeChat"/>): the words written at once as one line with no priority, under a
/// uuid the harness echoes where it takes them. Echoed before the running turn's result, they joined that turn at its next step; echoed
/// after it, that turn ended first and they are the next one, whose own result ends the turn they were sent into. The person is told which.
/// </summary>
/// <remarks>
/// Measured on Claude Code 2.1.322 (<c>docs/2026-10-03-steer-evidence.md</c> §8): a line written during a tool is folded into the turn
/// after the tool's result, one <c>result</c> for the turn; one written while the last text streams is the next turn, a second
/// <c>result</c>. Nothing is stopped: the harness itself makes an untaken word its next turn, so a stop would only cut the work.
/// </remarks>
/// <param name="frame">The dialect's line for the words under a uuid, or null where the harness echoes nothing.</param>
/// <param name="write">
/// Writes the line for the words: true once written, false where nothing listens, null where their record holds them, which says why.
/// </param>
/// <param name="record">The conversation's record.</param>
internal sealed class NativeSteers(
    Func<string, string, string?> frame, Func<ChatMessage, string, Task<bool?>> write, Action<SessionEvent> record)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Sent> _sent = new(StringComparer.Ordinal);

    private sealed class Sent(ChatMessage words)
    {
        public ChatMessage Words { get; } = words;

        public TaskCompletionSource Landed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // The turn they were written into ended before their echo: they are the next turn.
        public bool Ended;
        public bool Echoed;
    }

    /// <summary>Whether the harness echoes what it takes, so words sent now are written into its turn.</summary>
    public bool Steers { get; } = frame("", Guid.Empty.ToString()) is not null;

    /// <summary>
    /// The words sent now: shown with the reach <c>now</c> under their id, then written. What completes once the turn they landed in has
    /// ended, or at once where they never went; null where the harness echoes nothing.
    /// </summary>
    /// <remarks>Called under the turns' lock: nothing here calls back into them.</remarks>
    public Task? Now(ChatMessage message)
    {
        var uuid = Guid.NewGuid().ToString();
        if (frame(message.PromptWithFiles, uuid) is not { } line) return null;
        var words = ProtocolChat.Identified(message);
        if (ChatRecorded.Told(words) is { } told) record(told);
        record(ChatMessage.Words(words) with { Reaches = SendNowWhy.Spelled(TurnReach.Steered), Level = MessageLevels.Now });
        var sent = new Sent(words);
        lock (_gate) _sent[uuid] = sent;
        _ = WriteAsync(uuid, sent, line);
        return sent.Landed.Task;
    }

    /// <summary>
    /// A line the harness echoed where it took it: words sent now are taken there, as the ask of the turn that goes on or of the next,
    /// and the person told which. An echo of anything else is nothing here.
    /// </summary>
    public void Echoed(string uuid)
    {
        Sent? sent;
        bool next;
        lock (_gate)
        {
            if (!_sent.TryGetValue(uuid, out sent) || sent.Echoed) return;
            sent.Echoed = true;
            next = sent.Ended;
            if (!next) _sent.Remove(uuid);
        }

        record(ChatMessage.Words(sent.Words));
        var landed = next
            ? Noted.Of(ChatNoteCodes.WordsSentNowNextTurn, "Sent now: its turn ended before it could take this, so this began the next.")
            : Noted.Of(ChatNoteCodes.WordsSentNowInTurn, "Sent now: it went into the turn it was on.");
        record(new SessionEvent { Kind = SessionEventKind.Note, Text = landed.Note, Parts = landed.Parts });
        if (!next) sent.Landed.TrySetResult();
    }

    /// <summary>
    /// A turn's result: words that began the turn it ends are done; words not yet taken were not taken by it, and are the next turn.
    /// </summary>
    public void TurnEnded()
    {
        List<Sent> done = [];
        lock (_gate)
        {
            foreach (var (uuid, sent) in _sent.ToList())
            {
                if (sent.Echoed)
                {
                    _sent.Remove(uuid);
                    done.Add(sent);
                }
                else
                {
                    sent.Ended = true;
                }
            }
        }

        foreach (var sent in done) sent.Landed.TrySetResult();
    }

    /// <summary>The harness has gone: no echo will come, and no turn waits on one.</summary>
    public void Gone()
    {
        List<Sent> all;
        lock (_gate)
        {
            all = [.. _sent.Values];
            _sent.Clear();
        }

        foreach (var sent in all) sent.Landed.TrySetResult();
    }

    private async Task WriteAsync(string uuid, Sent sent, string line)
    {
        bool? written;
        try
        {
            written = await write(sent.Words, line).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException or DriverException)
        {
            written = false;
        }

        if (written == true) return;
        lock (_gate)
        {
            if (!_sent.Remove(uuid)) return;
        }

        if (written == false)
        {
            var gone = WordsNotes.NotSent();
            record(new SessionEvent
            {
                Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts, Words = sent.Words.Id is { } id ? [id] : null,
            });
        }

        sent.Landed.TrySetResult();
    }
}
