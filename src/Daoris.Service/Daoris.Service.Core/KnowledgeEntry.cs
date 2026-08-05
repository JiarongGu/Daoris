namespace Daoris.Knowledge;

/// <summary>
/// What kind of thing a knowledge entry is. The kind decides how it is read, not where it lives:
/// a decision record is a decision record whatever the repository calls the file.
/// </summary>
public enum EntryKind
{
    /// <summary>An always-loaded rule.</summary>
    Rule,

    /// <summary>An on-demand knowledge document.</summary>
    Knowledge,

    /// <summary>An invocable skill.</summary>
    Skill,

    /// <summary>One numbered decision and its reasoning.</summary>
    Decision,

    /// <summary>One recorded fix: symptom, root cause, verification.</summary>
    Fix,


    /// <summary>One completed task and its outcome.</summary>
    TaskOutcome,
}

/// <summary>
/// Where an entry came from, which is the question the lock already answers.
/// </summary>
/// <remarks>
/// This distinction is the reason the index is worth building at all. Canonical content is
/// <em>identical in every adopting repository by construction</em> — indexing it once per repository
/// would produce a dozen copies of the same rule and call that a corpus. What differs between
/// repositories, and therefore what is worth searching across them, is the local material: the
/// decisions, the fixes, the outcomes that only that repository knows.
/// </remarks>
public enum Provenance
{
    /// <summary>Materialized by daoris and recorded in the lock. The same everywhere it is installed.</summary>
    Canonical,

    /// <summary>The repository's own. Invisible to the tool, and the only thing that varies across repositories.</summary>
    Local,
}

/// <summary>
/// One addressable piece of knowledge: a rule, a skill, a decision, a fix, a task outcome.
/// </summary>
/// <remarks>
/// Entries are <em>sections</em> rather than files wherever a file holds many of them. A decisions
/// log is one file and twenty decisions; returning the file for a query about one of them buries the
/// answer in nineteen others.
/// </remarks>
/// <param name="Repository">The repository this came from, by its directory name.</param>
/// <param name="Kind">What sort of entry it is.</param>
/// <param name="Provenance">Canonical or the repository's own.</param>
/// <param name="Title">The heading, or the document name where the whole file is one entry.</param>
/// <param name="Body">The entry's text, without its heading.</param>
/// <param name="RelativePath">Path within the repository, always '/'-separated.</param>
/// <param name="Anchor">The heading this section was split at, when it was split from a larger file.</param>
public sealed record KnowledgeEntry(
    string Repository,
    EntryKind Kind,
    Provenance Provenance,
    string Title,
    string Body,
    string RelativePath,
    string? Anchor = null)
{
    /// <summary>
    /// A stable identity for the entry, so re-ingesting the same repository updates rather than
    /// duplicates. Deliberately derived from location rather than content: an entry whose text was
    /// edited is the same entry, and treating it as a new one is how an index accumulates ghosts.
    /// </summary>
    public string Id => Anchor is null
        ? $"{Repository}:{RelativePath}"
        : $"{Repository}:{RelativePath}#{Anchor}";
}
