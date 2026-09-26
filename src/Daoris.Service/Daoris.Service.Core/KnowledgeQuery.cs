namespace Daoris.Knowledge;

/// <summary>
/// What to look for, and what to look in.
/// </summary>
/// <remarks>
/// The filters are all optional and all narrowing. Absent means "no restriction" rather than
/// "exclude everything", because a query object whose defaults return nothing is a trap every caller
/// falls into once.
/// </remarks>
/// <param name="Text">The terms to match. Empty matches everything, which is how a caller browses.</param>
public sealed record KnowledgeQuery(string Text = "")
{
    /// <summary>Restrict to these kinds. Null means every kind.</summary>
    public IReadOnlySet<EntryKind>? Kinds { get; init; }

    /// <summary>Restrict to these repositories, by directory name. Null means every repository.</summary>
    public IReadOnlySet<string>? Repositories { get; init; }

    /// <summary>
    /// The circle to answer from (D48). Null spans every workspace the store holds — which a caller
    /// must then SAY, rather than present as one family's answer (design §4, the D24 shape: report the
    /// scope that ran). Every door resolves an ambient default before it gets here.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// Restrict to canonical or local. Null means both — but <see cref="Provenance.Local"/> is the
    /// interesting one across repositories, since canonical content is identical wherever it is
    /// installed.
    /// </summary>
    public Provenance? Provenance { get; init; }

    /// <summary>How many hits to return.</summary>
    public int Limit { get; init; } = 20;

    /// <summary>Whether this entry passes the query's filters, ignoring its text.</summary>
    public bool Admits(KnowledgeEntry entry) =>
        (Kinds is null || Kinds.Contains(entry.Kind))
        && (Repositories is null || Repositories.Contains(entry.Repository))
        && (Provenance is null || Provenance == entry.Provenance)
        && (Workspace is null || Workspaces.Same(Workspace, entry.Workspace));

    /// <summary>
    /// A caller's comma-separated kind filter, as every door parses it — HERE, in Core, because "what
    /// may a caller ask for" is query judgement (D36): a kind alias landing in one host and not the
    /// other would answer the same question differently through two doors.
    /// </summary>
    public static IReadOnlySet<EntryKind>? ParseKinds(string? value)
    {
        var names = ParseSet(value);
        if (names is null) return null;

        var kinds = new HashSet<EntryKind>();
        foreach (var name in names)
        {
            // "task" is friendlier than "taskoutcome", and a model will reach for the short form.
            var normalized = name.Equals("task", StringComparison.OrdinalIgnoreCase) ? "TaskOutcome" : name;
            // 🔴 A name that is no kind is refused, never skipped (REV3): skipping left no kinds, and no
            // kinds means every kind — so a typo widened the search it was meant to narrow.
            if (!Enum.TryParse<EntryKind>(normalized, ignoreCase: true, out var kind) || int.TryParse(normalized, out _))
            {
                throw new ArgumentException(
                    $"'{name}' is not a kind of knowledge — one of: rule, knowledge, skill, decision, fix, task.");
            }

            kinds.Add(kind);
        }

        return kinds;
    }

    /// <summary>A caller's comma-separated list, case-insensitive; null when it names nothing.</summary>
    public static IReadOnlySet<string>? ParseSet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length > 0 ? new HashSet<string>(items, StringComparer.OrdinalIgnoreCase) : null;
    }
}

/// <summary>One result: the entry, how well it matched, and where it matched.</summary>
/// <param name="Entry">The matched entry.</param>
/// <param name="Score">Relative score within one result set. Not comparable across searches.</param>
/// <param name="Excerpt">
/// The passage that matched, so a caller can show why without loading the whole entry. A result list
/// that cannot show its reasoning gets treated as an oracle, which is exactly what it is not.
/// </param>
public sealed record KnowledgeHit(KnowledgeEntry Entry, double Score, string? Excerpt = null);

/// <summary>
/// A search's hits, and which halves ANSWERED it (TIER1, D24) — never which were configured. A half
/// that threw did not answer, and nothing answering is not nothing matching.
/// </summary>
/// <param name="Failure">Why a half did not answer, in its own words; null when every half did.</param>
public sealed record SearchAnswer(
    IReadOnlyList<KnowledgeHit> Hits, bool Lexical, bool Semantic, string? Failure = null)
{
    /// <summary>The tier as a token for a wire: `lexical+semantic`, `lexical`, `semantic`, or `none`.</summary>
    public string Tier => (Lexical, Semantic) switch
    {
        (true, true) => "lexical+semantic",
        (true, false) => "lexical",
        (false, true) => "semantic",
        _ => "none",
    };
}
