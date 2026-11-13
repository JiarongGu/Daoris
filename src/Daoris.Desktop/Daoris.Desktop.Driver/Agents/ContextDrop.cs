namespace Daoris.Driver;

/// <summary>
/// Reads a compaction off a session's context counter: the harnesses report the context in use as their turns go
/// (<c>usage_update</c>), and a reading under half the one before it, from a context that held at least <see cref="Floor"/> tokens,
/// is a harness that compacted. Read only for a door whose harness names no compaction itself (<see cref="ISpawn.NamesCompaction"/>).
/// </summary>
internal sealed class ContextDrop
{
    /// <summary>The least context, in tokens, whose halving is read as a compaction rather than a small context's noise.</summary>
    internal const long Floor = 10_000;

    private long _last;

    /// <summary>Takes the next reading of the context in use; whether it fell to under half the one before.</summary>
    public bool Dropped(long used)
    {
        var before = _last;
        _last = used;
        return before >= Floor && used * 2 < before;
    }
}
