namespace Daoris.Knowledge;

/// <summary>How a presented entry stands (the memory coordinator design §6, at presentation).</summary>
public static class MemoryStanding
{
    /// <summary>Read again now and its cited records holding: stamped <c>checked at</c> its revision.</summary>
    public const string Current = "current";

    /// <summary>Ended, by an end or a replacement.</summary>
    public const string Ended = "ended";

    /// <summary>Its memory keeps it no more.</summary>
    public const string Gone = "gone";

    /// <summary>No check answered that it holds: shown as read, never stamped.</summary>
    public const string Historical = "historical";
}

/// <summary>
/// An entry as presented: its reference, the revision it was read at, how it stands now, the entry as read now where it was, and why where
/// it is not current (or what amended a current one's cited record).
/// </summary>
public sealed record MemoryShown(MemoryReference Reference, long Read, string Standing, MemoryEntry? Entry = null, string? Why = null)
{
    /// <summary>The most of an entry's text one line carries.</summary>
    public const int TextLimit = 300;

    /// <summary>The revision it was checked at; null where it was not stamped.</summary>
    public long? Checked => Standing == MemoryStanding.Current ? Entry?.Revision : null;

    /// <summary>The entry in one line of Daoris's English.</summary>
    public string Line => Standing switch
    {
        MemoryStanding.Current => $"`{Reference}`, checked at revision {Entry!.Revision}{(Why is null ? "" : $", though {Why}")}: {Text}",
        MemoryStanding.Ended => $"`{Reference}`, read at revision {Read}, has ended: {Why?.TrimEnd('.')}. It said: {Text}",
        MemoryStanding.Gone => $"`{Reference}`, read at revision {Read}, is no longer kept by its memory.",
        _ => $"`{Reference}`, as read at revision {Read} and not checked as current, because {Why}{(Entry is null ? "." : $": {Text}")}",
    };

    private string Text
    {
        get
        {
            var text = string.Join(' ', (Entry?.Text ?? "").Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return text.Length <= TextLimit ? text : text[..(TextLimit - 1)].TrimEnd() + "…";
        }
    }
}

/// <summary>
/// Memory shown as current is current (KNOWCENTRAL2f; the memory coordinator design §3, §6 at presentation): each entry a line read is
/// read again and its cited records rechecked, an entry stamped <c>checked at</c> its revision only where every check holds, and shown as
/// read where none answers. An entry that moved between the reading and the answer's delivery discards the answer, which is read again, up
/// to <see cref="Tries"/> times; what still moves is shown as read. What is presented is kept as the line's read, so a move after it is told
/// once and one before it is not told again.
/// </summary>
/// <param name="ops">The log whose journal says what moved, and where the read set is kept.</param>
/// <param name="engines">Each repository's memory; none where memory is off, and everything shows as read.</param>
/// <param name="knowledge">The index a cited record is rechecked against; none, and an entry citing a record shows as read.</param>
public sealed class MemoryPresenter(MemoryOpStore ops, IMemoryEngines? engines, KnowledgeService? knowledge = null)
{
    /// <summary>The most times an answer an entry's move discarded is read again.</summary>
    public const int Tries = 3;

    /// <summary>Run after the entries are read and before the answer is checked against the journal; a test's, to move one there.</summary>
    internal Func<Task>? BeforeDelivery { get; set; }

    /// <summary>Each read, as it stands now, in the order given; each kept under the session that read it at the revision shown.</summary>
    public async Task<IReadOnlyList<MemoryShown>> PresentAsync(IReadOnlyList<(string Session, MemoryRead Read)> reads, CancellationToken ct = default)
    {
        if (reads.Count == 0) return [];
        var repositories = reads.Select(each => each.Read.Reference.Repository).Distinct(StringComparer.Ordinal).ToList();
        for (var attempt = 1; ; attempt++)
        {
            var cursor = await ops.CursorAsync(repositories, ct).ConfigureAwait(false);
            var shown = new List<MemoryShown>(reads.Count);
            foreach (var (_, read) in reads) shown.Add(await ShowAsync(read, ct).ConfigureAwait(false));

            // Kept before the check, so a move after the check marks the read and one before it is in the journal past the cursor.
            foreach (var session in reads.Select((each, at) => (each.Session, Shown: shown[at])).GroupBy(each => each.Session, StringComparer.Ordinal))
            {
                await ops.KeepReadsAsync(
                    session.Key, [.. session.Select(each => new MemoryRead(each.Shown.Reference, each.Shown.Entry?.Revision ?? each.Shown.Read))], ct)
                    .ConfigureAwait(false);
            }

            if (BeforeDelivery is { } before) await before().ConfigureAwait(false);
            var changes = await ops.ChangesAsync(cursor, repositories, ct).ConfigureAwait(false);
            HashSet<MemoryReference> moved = changes.Revalidate ? [.. shown.Select(each => each.Reference)] : [.. changes.Changed];
            if (!shown.Any(each => moved.Contains(each.Reference))) return shown;
            if (attempt == Tries)
            {
                return [.. shown.Select(each => moved.Contains(each.Reference)
                    ? each with { Standing = MemoryStanding.Historical, Why = "it moved while Daoris checked it" }
                    : each)];
            }
        }
    }

    private async Task<MemoryShown> ShowAsync(MemoryRead read, CancellationToken ct)
    {
        var reference = read.Reference;
        if (engines?.Open(reference.Repository, MemoryReinforcement.None) is not { } engine)
        {
            return new(reference, read.Revision, MemoryStanding.Historical, Why: "its memory is not open here");
        }

        MemoryEntry? entry;
        try
        {
            entry = await engine.GetAsync(reference.Entry, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(reference, read.Revision, MemoryStanding.Historical, Why: "its memory could not be read");
        }

        if (entry is null) return new(reference, read.Revision, MemoryStanding.Gone);
        if (entry.Ended is { } ended) return new(reference, read.Revision, MemoryStanding.Ended, entry, MemoryEndToken.Strip(ended));

        string? amended = null;
        foreach (var citation in MemoryCitations.Read(await ops.CitedAsync(reference.Repository, entry.Id, ct).ConfigureAwait(false)))
        {
            var check = await RecheckAsync(citation, ct).ConfigureAwait(false);
            if (check is { Standing: CitationStanding.Holds }) continue;
            if (check is { Standing: CitationStanding.Amended })
            {
                amended ??= $"its cited record `{citation.Id}` is amended{(check.By is { } by ? $" by {by}" : "")}";
                continue;
            }

            return new(reference, read.Revision, MemoryStanding.Historical, entry, check is { Standing: CitationStanding.Failed }
                ? $"its cited record `{citation.Id}` is superseded by {check.By ?? "a later record"}"
                : $"its cited record `{citation.Id}` could not be checked{(check is null ? "" : $" ({check.Says})")}");
        }

        return new(reference, read.Revision, MemoryStanding.Current, entry, amended);
    }

    private async Task<CitationCheck?> RecheckAsync(Citation citation, CancellationToken ct)
    {
        if (knowledge is null) return null;
        try
        {
            return await knowledge.RecheckAsync(citation, ct: ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return null;
        }
    }
}
