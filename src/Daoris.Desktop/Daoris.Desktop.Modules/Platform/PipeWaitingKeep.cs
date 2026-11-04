namespace Daoris.Desktop;

/// <summary>
/// The waiting presses' records (CONFIRMWAIT1a-ii, §7.1), which the pipe's parts reach (<see cref="InstancePipe"/>): each kept by its
/// confirmation's id, rewritten as it is shown and as it ends, under a lock of its own, never the pipe's
/// (<see cref="PipeState.Gate"/>), since it writes a file. Neither lock is taken inside the other.
/// </summary>
/// <remarks>
/// It owns the records' folder, each kept record, each ending's answer as its record was rewritten with it (CONFIRMWAIT1 §15.2 item 8,
/// for its line), and each Confirm whose record says <c>begun</c> until its run begins (DRAINHOLD1: it runs whatever begins after, and a
/// close leaves it under way rather than keeping it as a press that waits).
/// </remarks>
internal sealed class PipeWaitingKeep(PipeWaiting records)
{
    private readonly Dictionary<string, PipeWaitingRecord> _kept = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _endings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _begun = new(StringComparer.Ordinal);
    private readonly Lock _recording = new();

    /// <summary>The records' files as the last instance left them, read once by the restore.</summary>
    public IReadOnlyList<PipeWaitingFile> Read() => records.Read();

    /// <summary>A record's file removed by its confirmation's id.</summary>
    public void Remove(string confirmation) => records.Remove(confirmation);

    /// <summary>A record brought back by the restore, kept as it was read.</summary>
    public void Hold(PipeWaitingRecord record)
    {
        lock (_recording)
        {
            _kept[record.Confirmation] = record;
        }
    }

    /// <summary>A new record written and kept: false, and nothing kept, where it could not be written.</summary>
    public bool Keep(PipeWaitingRecord record)
    {
        lock (_recording)
        {
            if (!records.Write(record)) return false;
            _kept[record.Confirmation] = record;
            return true;
        }
    }

    /// <summary>A press's card shown for the first time (§7.1): its record keeps when.</summary>
    public void Shown(PipeConfirmation card)
    {
        lock (_recording)
        {
            if (!_kept.TryGetValue(card.Id, out var record)) return;
            _kept[card.Id] = record = record with { ShownAt = card.ShownAt };
            records.Write(record);
        }
    }

    /// <summary>
    /// A press's record rewritten as it ends (§7.1's order): <c>begun</c>, or <c>ended</c> with its answer. False where it could not be
    /// written (§15.2 item 8), its record left as it was; true where the press keeps none.
    /// </summary>
    public bool Rewrite(string confirmation, string state, string? answer = null)
    {
        lock (_recording)
        {
            if (!_kept.TryGetValue(confirmation, out var record)) return true;
            var rewritten = record with { State = state, Answer = answer };
            if (!records.Write(rewritten)) return false;
            _kept[confirmation] = rewritten;
            return true;
        }
    }

    /// <summary>A Confirm marked <c>begun</c> before its record says so, or the mark taken back where the record could not be written.</summary>
    public void MarkBegun(string confirmation, bool begun)
    {
        lock (_recording)
        {
            if (begun) _begun.Add(confirmation);
            else _begun.Remove(confirmation);
        }
    }

    /// <summary>Whether a press's record says <c>begun</c> and its run has not begun yet (DRAINHOLD1).</summary>
    public bool Begun(string confirmation)
    {
        lock (_recording) return _begun.Contains(confirmation);
    }

    /// <summary>The answer a press's record was rewritten with as its ending was taken, kept for its line.</summary>
    public void Ended(string confirmation, string text)
    {
        lock (_recording) _endings[confirmation] = text;
    }

    /// <summary>The answer kept for a press's line, taken once; null where its ending wrote none (a Confirm that met the close).</summary>
    public string? TakeEnding(string confirmation)
    {
        string? text;
        lock (_recording) _endings.Remove(confirmation, out text);
        return text;
    }

    /// <summary>A press's record removed once its ending is journaled, or its run begun; one that cannot be is left, and the next start removes it.</summary>
    public void Forget(string confirmation)
    {
        lock (_recording)
        {
            _kept.Remove(confirmation);
            records.Remove(confirmation);
            _begun.Remove(confirmation);
        }
    }

    /// <summary>Whether a waiting press's record says it carries a key.</summary>
    public bool Keyed(string confirmation)
    {
        lock (_recording)
        {
            return _kept.TryGetValue(confirmation, out var record) && record.Keyed;
        }
    }
}
