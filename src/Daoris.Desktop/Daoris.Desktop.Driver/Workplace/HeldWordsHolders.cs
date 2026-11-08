using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Whoever keeps the held words in memory beside their file, which it then owns: it drops a session's words from both together,
/// so the file it writes next from memory never brings one back.
/// </summary>
public interface IHeldWordsHolder
{
    /// <summary>Drop every word held for one of <paramref name="sessions"/>; the sessions that had any come back.</summary>
    IReadOnlyCollection<string> Forget(IReadOnlyCollection<string> sessions);
}

/// <summary>
/// The holder of each home's held-words file in this process: the shell's queue, while it lives. A process with none (a terminal's
/// own delete with no Daoris running) edits the file in place, which nothing else is then writing.
/// </summary>
public static class HeldWordsHolders
{
    private static readonly ConcurrentDictionary<string, IHeldWordsHolder> Holders = new(StringComparer.OrdinalIgnoreCase);

    private static string KeyOf(string home) => Path.GetFullPath(HeldWordsFile.PathOf(home));

    /// <summary>
    /// <paramref name="holder"/> keeps this home's held words in this process, until the answer is disposed. A second holder of the
    /// same home replaces the first, and the first's disposal then leaves the second.
    /// </summary>
    public static IDisposable Hold(string home, IHeldWordsHolder holder)
    {
        var key = KeyOf(home);
        Holders[key] = holder;
        return new Release(key, holder);
    }

    /// <summary>The holder of this home's held words in this process, or null where none is.</summary>
    public static IHeldWordsHolder? Of(string home) => Holders.GetValueOrDefault(KeyOf(home));

    private sealed class Release(string key, IHeldWordsHolder holder) : IDisposable
    {
        public void Dispose() => Holders.TryRemove(new KeyValuePair<string, IHeldWordsHolder>(key, holder));
    }
}
