using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The records an entry cites, as the re-check text Lyntai keeps on it and runs nothing on (the memory coordinator design §3, §5): the
/// citations as JSON, at most Lyntai's 2,000 characters.
/// </summary>
public static class MemoryCitations
{
    /// <summary>The most characters Lyntai keeps as an entry's re-check text.</summary>
    public const int MaxChars = 2000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The re-check text of <paramref name="citations"/>; null for none.</summary>
    /// <exception cref="ArgumentException">More than the text can hold.</exception>
    public static string? Of(IReadOnlyList<Citation> citations)
    {
        if (citations.Count == 0) return null;
        var text = JsonSerializer.Serialize(citations, Json);
        return text.Length <= MaxChars
            ? text
            : throw new ArgumentException($"These citations take {text.Length} characters, and an entry keeps {MaxChars}: cite fewer records.", nameof(citations));
    }

    /// <summary>The citations a re-check text names; none where it names none or does not read.</summary>
    public static IReadOnlyList<Citation> Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Citation>>(text, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// The service's sweep: each registered repository's entries Lyntai answers as due a
/// recheck, their citations rechecked through <see cref="KnowledgeService.RecheckAsync"/>. Every citation holding is written as a
/// <c>ready</c> confirmation at the revision read, one failing as an <c>awaiting</c> end for the person; an amended or inconclusive one
/// writes nothing and is read again at the next sweep. An entry whose row from an earlier sweep has not settled is not written again. A
/// citation of another repository's entry is rechecked through that memory's view, within the citing repository's workspace.
/// </summary>
public sealed class MemorySweep(MemoryOpStore ops, IMemoryEngines engines, KnowledgeService knowledge)
{
    /// <summary>How long a confirmation stands before its entry is due again.</summary>
    public TimeSpan RecheckAfter { get; init; } = TimeSpan.FromDays(1);

    /// <summary>The most rechecks one repository's sweep writes.</summary>
    public int Limit { get; init; } = 50;

    /// <summary>The most due entries one repository's sweep asks its engine for.</summary>
    public int MaxReads { get; init; } = 1000;

    /// <summary>One sweep at <paramref name="now"/>.</summary>
    /// <returns>How many rechecks it wrote.</returns>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var pending = await ops.PendingAsync(ct).ConfigureAwait(false);
        var rechecks = new List<MemoryRecheck>();
        var failures = new List<Exception>();
        foreach (var row in (await knowledge.RegistryAsync(ct: ct).ConfigureAwait(false)).DistinctBy(row => row.Repository, StringComparer.Ordinal))
        {
            var repository = row.Repository;
            try
            {
                if (engines.Open(repository, MemoryReinforcement.None) is not { } engine) continue;
                var scope = new MemoryWorkspaceCheck(knowledge, row.InWorkspace);
                await SweepAsync(repository, engine, scope, now, pending, rechecks, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // One repository's unreadable memory loses no other's rechecks; the pass still ends failed, told by its coordinator.
                failures.Add(error);
            }
        }

        if (rechecks.Count > 0) await ops.SweepAsync(rechecks, now, ct).ConfigureAwait(false);
        if (failures.Count > 0) throw new AggregateException("A repository's memory could not be swept.", failures);
        return rechecks.Count;
    }

    // The engine pages nothing and keeps an entry that writes nothing at its front, so the ask doubles instead.
    private async Task SweepAsync(
        string repository, IMemoryEngine engine, IMemoryViewCheck scope, DateTimeOffset now, IReadOnlySet<(string Repository, string Target, string Kind)> pending,
        List<MemoryRecheck> rechecks, CancellationToken ct)
    {
        var read = new HashSet<string>(StringComparer.Ordinal);
        var written = 0;
        for (var asked = Math.Min(Limit, MaxReads); asked > 0; asked = Math.Min(asked * 2, MaxReads))
        {
            var dues = await engine.DueAsync(now - RecheckAfter, asked, ct).ConfigureAwait(false);
            foreach (var due in dues.Where(due => read.Add(due.Id)))
            {
                if (await RecheckAsync(repository, due, scope, ct).ConfigureAwait(false) is not { } recheck) continue;
                var kind = recheck.Holds ? MemoryOpKind.Confirm : MemoryOpKind.End;
                if (pending.Contains((repository, due.Id, kind))) continue;
                rechecks.Add(recheck);
                if (++written >= Limit) return;
            }

            if (dues.Count < asked || asked >= MaxReads) return;
        }
    }

    private async Task<MemoryRecheck?> RecheckAsync(string repository, MemoryDue due, IMemoryViewCheck scope, CancellationToken ct)
    {
        var citations = MemoryCitations.Read(due.Recheck);
        if (citations.Count == 0) return null;
        var holds = true;
        foreach (var citation in citations)
        {
            var check = citation.Revision is null
                ? await knowledge.RecheckAsync(citation, ct: ct).ConfigureAwait(false)
                : await MemoryViews.RecheckAsync(engines, citation, scope, ct).ConfigureAwait(false);
            if (check.Standing == CitationStanding.Failed)
            {
                return new MemoryRecheck(
                    repository, due.Id, due.Revision, Holds: false, citation.Revision is null
                        ? $"Its cited record `{citation.Id}` is superseded by {check.By ?? "a later record"}: {check.Says}"
                        : $"Its cited entry `{citation.Id}` of another repository's memory {check.Says}.");
            }

            holds &= check.Standing == CitationStanding.Holds;
        }

        return holds ? new MemoryRecheck(repository, due.Id, due.Revision, Holds: true) : null;
    }
}
