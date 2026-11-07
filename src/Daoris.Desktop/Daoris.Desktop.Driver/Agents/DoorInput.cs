namespace Daoris.Driver;

/// <summary>One word the run's input carries (D243 point 1): its ids on the record, its line as the process reads it, what its record opens with.</summary>
/// <param name="Ids">The ids the record gave the words this line carries; empty for a line that carries none (a driver's own prompt).</param>
/// <param name="Line">The text written to the process, as one write.</param>
/// <param name="Opening">What the record says first when the line goes with a process that resumes a conversation; null for none.</param>
/// <param name="Written">Told once, as the write returns, the first time the line is written (<c>sent</c>), a replay included.</param>
internal sealed record DoorWord(IReadOnlyList<string> Ids, string Line, IReadOnlyList<SessionEvent>? Opening = null, Action? Written = null)
{
    public DoorWord(string? id, string line, Action? written = null)
        : this(id is null ? [] : [id], line, null, written)
    {
    }
}

/// <summary>What became of a word handed to the run's input.</summary>
internal enum Delivery
{
    /// <summary>The write returned.</summary>
    Written,

    /// <summary>The process was going and the run will start it once more: the word waits in the queue and is written to that process.</summary>
    Held,

    /// <summary>Nothing is listening and nothing will: the word was not written and is not kept.</summary>
    Refused,
}

/// <summary>
/// The one queue of a run's words (the agent-provider design §2.5, D243): every word a process is handed goes through it, nothing above
/// the run writes to a process's stdin. A word is attempted on its record before it is handed here, is written as the write returns, and is
/// acknowledged once the harness names the conversation it went to; a process that ended before naming one never opened it, so the run's
/// one retry hands the next process every word written to that one, in order, and then those said during the restart, and a word
/// acknowledged is never written again.
/// </summary>
/// <remarks>
/// It is the native door's port (D246). A protocol door's words are requests its session answers, each acknowledged by its own answer and
/// retried on the same session, so they are <see cref="ProtocolWords"/>' and its input refuses every line, naming <see cref="Refuses"/>.
/// </remarks>
/// <param name="refuses">The ability a line is refused as, on a door whose words are not lines; null for a native door.</param>
internal sealed class DoorInput(string? refuses = null)
{
    private readonly object _gate = new();
    private readonly List<DoorWord> _written = [];
    private readonly List<DoorWord> _waiting = [];
    private readonly HashSet<DoorWord> _told = [];
    private Func<string, CancellationToken, Task<bool>>? _write;
    private bool _closed;

    /// <summary>The ability every line is refused as on a door whose words are requests rather than lines; null where lines are written.</summary>
    public string? Refuses => refuses;

    /// <summary>Whether the run will start the same resume once more, so a word its process cannot take waits rather than is refused.</summary>
    public bool MayRetry { get; set; }

    /// <summary>The ids of the words written and not yet acknowledged, in the order written.</summary>
    public IReadOnlyList<string> Unacknowledged
    {
        get
        {
            lock (_gate) return [.. _written.SelectMany(word => word.Ids)];
        }
    }

    /// <summary>
    /// The lines the next process is handed, in order, nothing written yet: each written to the one before and never acknowledged, then each
    /// waiting. A run whose door takes its prompt whole at the start composes it from these, and attaches once the door wrote it.
    /// </summary>
    public IReadOnlyList<string> Lines()
    {
        lock (_gate) return [.. _written.Concat(_waiting).Select(word => word.Line)];
    }

    /// <summary>What the record opens a process with that carries the waiting words: their openings, in order.</summary>
    public IReadOnlyList<SessionEvent> Openings()
    {
        lock (_gate) return [.. _waiting.SelectMany(word => word.Opening ?? [])];
    }

    /// <summary>
    /// A process is up and reads <paramref name="write"/>: it is handed, in order, each word written to the one before that was never
    /// acknowledged, then each that waited. Answers whether every write returned.
    /// </summary>
    public async Task<bool> AttachAsync(Func<string, CancellationToken, Task<bool>> write, CancellationToken ct)
    {
        Admit();
        List<DoorWord> replay;
        lock (_gate)
        {
            _write = write;
            _closed = false;
            replay = [.. _written, .. _waiting];
            _waiting.Clear();
            _written.Clear();
        }

        var all = true;
        foreach (var word in replay) all &= await WriteAsync(word, ct, retried: true).ConfigureAwait(false) != Delivery.Refused;
        return all;
    }

    /// <summary>The run's last process is gone and no retry comes: what is said now is refused. Before any process is attached a word waits for it.</summary>
    public void Detach()
    {
        lock (_gate)
        {
            _write = null;
            _closed = true;
        }
    }

    /// <summary>
    /// Keep a word for the next process the run starts without writing it to the one that ended: a go-on's held words, written as that
    /// process's one prompt.
    /// </summary>
    public void Hold(DoorWord word)
    {
        Admit();
        lock (_gate) _waiting.Add(word);
    }

    /// <summary>Hand a word to the process.</summary>
    public Task<Delivery> SayAsync(DoorWord word, CancellationToken ct) =>
        refuses is null ? WriteAsync(word, ct, retried: false) : Task.FromException<Delivery>(AgentRun.Unsupported(refuses));

    /// <summary>
    /// A control line that is no word (an interrupt): written to the process now, never kept, never replayed. False where nothing listens.
    /// </summary>
    public async Task<bool> ControlAsync(string line, CancellationToken ct)
    {
        Admit();
        Func<string, CancellationToken, Task<bool>>? write;
        lock (_gate) write = _write;
        return write is not null && await write(line, ct).ConfigureAwait(false);
    }

    /// <summary>The harness named its conversation: it opened the one the words went to, so none is written again (D243 point 2).</summary>
    public IReadOnlyList<DoorWord> Acknowledge()
    {
        lock (_gate)
        {
            var taken = _written.ToList();
            _written.Clear();
            MayRetry = false;
            return taken;
        }
    }

    private void Admit()
    {
        if (refuses is not null) throw AgentRun.Unsupported(refuses);
    }

    private async Task<Delivery> WriteAsync(DoorWord word, CancellationToken ct, bool retried)
    {
        Func<string, CancellationToken, Task<bool>>? write;
        lock (_gate) write = _write;
        if (write is not null && await write(word.Line, ct).ConfigureAwait(false))
        {
            bool first;
            lock (_gate)
            {
                _written.Add(word);
                first = _told.Add(word);
            }

            if (first) word.Written?.Invoke();
            return Delivery.Written;
        }

        // Nothing took it. Before the run's first process is attached the word waits for it; a process that took no write, or none left,
        // keeps it only for the retry that comes, and any other answers it is lost.
        lock (_gate)
        {
            var starting = write is null && !_closed;
            if (!starting && !MayRetry && !retried) return Delivery.Refused;
            _waiting.Add(word);
            return Delivery.Held;
        }
    }
}
