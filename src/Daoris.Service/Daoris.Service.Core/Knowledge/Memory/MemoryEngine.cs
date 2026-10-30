namespace Daoris.Knowledge;

/// <summary>
/// One entry of a repository's memory as a session names it (the memory coordinator design §1): the repository by the registry's
/// name, and the entry by its engine's id, written <c>repository:entry</c>. No machine path.
/// </summary>
public sealed record MemoryReference(string Repository, string Entry)
{
    /// <summary>The reference <paramref name="text"/> writes, or null where it writes none.</summary>
    public static MemoryReference? Parse(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        var at = trimmed.IndexOf(':');
        return at > 0 && at < trimmed.Length - 1 ? new(trimmed[..at].Trim(), trimmed[(at + 1)..].Trim()) : null;
    }

    public override string ToString() => $"{Repository}:{Entry}";
}

/// <summary>An entry as its engine answers it: its text, its revision, and why it ended where it did.</summary>
public sealed record MemoryEntry(string Id, string Text, long Revision, string? Ended = null);

/// <summary>A relation from one entry to another of the same memory, and the weight its repeats gave it.</summary>
public sealed record MemoryLink(string From, string To, string Kind, double Weight);

/// <summary>An entry reached from another by a relation.</summary>
public sealed record MemoryRelated(MemoryLink Link, MemoryEntry Entry);

/// <summary>
/// Whether a recall reinforces what it returns. The coordinator opens every engine with <see cref="None"/>, so a read changes nothing
/// of the owner's (the memory coordinator design §2).
/// </summary>
public enum MemoryReinforcement
{
    None,
    OnRecall,
}

/// <summary>
/// One repository's memory engine (Lyntai's, from KNOWCENTRAL2d): the reads every memory tool makes, and the writes only the service
/// makes once a change is released.
/// </summary>
public interface IMemoryEngine
{
    /// <summary>The current entries <paramref name="query"/> matches, best first.</summary>
    Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>The entry, ended or not; null where the memory keeps none by that id.</summary>
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>The entries <paramref name="id"/> relates to.</summary>
    Task<IReadOnlyList<MemoryRelated>> ExpandAsync(string id, CancellationToken ct = default);

    /// <summary>An entry written; a same-content write answers the entry it repeats, its revision moved.</summary>
    Task<MemoryEntry> RememberAsync(string text, CancellationToken ct = default);

    /// <summary>A relation written; a repeated one answers its link, its weight added to.</summary>
    Task<MemoryLink> RelateAsync(string from, string to, string kind, CancellationToken ct = default);

    /// <summary>The entry confirmed, its revision moved; null where it is gone or ended.</summary>
    Task<MemoryEntry?> ConfirmAsync(string id, CancellationToken ct = default);

    /// <summary>The entry ended at <paramref name="revision"/>; null where it is gone, ended, or at another revision.</summary>
    Task<MemoryEntry?> EndAsync(string id, long revision, string reason, CancellationToken ct = default);
}

/// <summary>Each repository's memory, opened by its registry name.</summary>
public interface IMemoryEngines
{
    /// <summary>The memory of <paramref name="repository"/>, or null where it keeps none.</summary>
    IMemoryEngine? Open(string repository, MemoryReinforcement reinforceOn);
}
