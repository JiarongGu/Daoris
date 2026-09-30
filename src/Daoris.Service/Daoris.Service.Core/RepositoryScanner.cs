namespace Daoris.Knowledge;

/// <summary>
/// Reads one repository's knowledge into entries.
/// </summary>
/// <remarks>
/// Two shapes of source, and they are read differently on purpose:
///
/// <list type="bullet">
///   <item><b>Documents</b> — a rule, a knowledge file, a skill. One file, one entry.</item>
///   <item><b>Logs</b> — decisions, fixes, completed tasks. One file, many entries, split at their
///   headings. Returning the whole decisions log for a query about one decision buries the answer in
///   every other decision ever made.</item>
/// </list>
///
/// Where a log lives differs per repository, so the paths below are candidates rather than a
/// contract: each is read if present and ignored if not. That is deliberately not configuration —
/// a scanner that needs setting up before it can read anything gets set up for one repository and
/// then never for the rest.
///
/// Where the documents live is the layout's (D117 §5.5, LAYOUT4): the lock's root before the
/// manifest's, both roots for a repository with no lock, the lock's mirrors skipped so each skill is
/// found once, and each declared room's <c>AGENTS.md</c> read as the repository's own knowledge
/// (<see cref="RepositoryLayout"/>). A link, or a link held as text, is skipped and never followed
/// (<see cref="RepositoryLinks"/>): what it points at is not this repository's document.
/// </remarks>
public sealed class RepositoryScanner
{
    /// <summary>A room's instructions, the file every agent reads in that folder (D117 §2.2).</summary>
    private const string RoomInstructions = "AGENTS.md";

    private static readonly string[] DecisionFiles =
    [
        "docs/DECISIONS.md",
        "DECISIONS.md",
        "docs/decisions.md",
    ];

    private static readonly string[] FixFiles =
    [
        "docs/FIX-LOG.md",
        "docs/fix-log.md",
        "docs/archive/fix-log.md",
        "FIX-LOG.md",
    ];

    private static readonly string[] TaskOutcomeFiles =
    [
        "docs/task-archive.md",
        "docs/TASK-ARCHIVE.md",
        "docs/archive/tasks.md",
    ];

    /// <summary>
    /// The backlog, by the family's convention — where inbound requests land.
    /// </summary>
    /// <summary>Read every entry from a repository. A missing directory or file is simply absent.</summary>
    public IReadOnlyList<KnowledgeEntry> Scan(string repositoryRoot)
    {
        if (!Directory.Exists(repositoryRoot)) return [];

        var name = new DirectoryInfo(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, '/')).Name;
        var daorisLock = DaorisLock.Read(repositoryRoot);
        var layout = RepositoryLayout.Of(repositoryRoot, daorisLock);
        var entries = new List<KnowledgeEntry>();

        // The always-loaded tier, wherever this repository keeps it. A repository on the old layout
        // has files under `rules/`; one that has synced since D59 has a SPAN in the file every
        // harness reads, and the lock is what says which — so both are read and neither is guessed at.
        foreach (var root in layout.Roots)
        {
            entries.AddRange(ScanDocuments(repositoryRoot, name, daorisLock, RepositoryLayout.Under(root, "rules"), EntryKind.Rule));
        }
        entries.AddRange(ScanRegion(repositoryRoot, name, daorisLock));
        foreach (var root in layout.Roots)
        {
            entries.AddRange(ScanDocuments(repositoryRoot, name, daorisLock, RepositoryLayout.Under(root, "knowledge"), EntryKind.Knowledge));
        }
        foreach (var root in layout.Roots)
        {
            entries.AddRange(ScanSkills(repositoryRoot, name, daorisLock, RepositoryLayout.Under(root, "skills")));
        }
        entries.AddRange(ScanRooms(
            repositoryRoot, name, layout,
            entries.Select(entry => entry.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase)));

        foreach (var (candidates, kind) in new[]
                 {
                     (DecisionFiles, EntryKind.Decision),
                     (FixFiles, EntryKind.Fix),
                     (TaskOutcomeFiles, EntryKind.TaskOutcome),
                 })
        {
            // FIRST match wins. The candidates are alternative NAMES for one log, not several logs —
            // and scanning them all double-counts on a case-insensitive filesystem, where
            // `docs/DECISIONS.md` and `docs/decisions.md` are the same file. Found immediately, on
            // Windows; on Linux it would have waited until someone happened to have both.
            var found = candidates.FirstOrDefault(candidate =>
                File.Exists(Path.Combine(repositoryRoot, candidate.Replace('/', Path.DirectorySeparatorChar))));
            // A log reached through a link is some other folder's log (LAYOUT4): skipped, never followed.
            if (found is not null && !RepositoryLinks.Crosses(repositoryRoot, found))
            {
                entries.AddRange(ScanLog(repositoryRoot, name, found, kind));
            }
        }

        return entries;
    }

    /// <summary>
    /// A document's text, or null when it is not one to index: absent, reached through a link, or a
    /// link held as text (D117 §5.5). Read once, through the same boundary every document goes through.
    /// </summary>
    private static string? ReadDocument(string root, string relative)
    {
        if (RepositoryLinks.Crosses(root, relative)) return null;
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute)) return null;
        var text = Text.ReadDocument(absolute);
        return RepositoryLinks.HeldAsText(root, relative, text) ? null : text;
    }

    /// <summary>
    /// Each declared room's instructions, as the repository's own knowledge (D117 §5.5): a folder's
    /// reference, which is what the search exists to find. Always local, since Daoris never writes a
    /// room's text, and named by its folder, since every room's file has the same name.
    /// </summary>
    /// <param name="indexed">
    /// What the tiers already yielded: a room the CLI accepts may still sit in a folder read as a tier
    /// (a knowledge folder on the root a layout moved from), and one file is one entry.
    /// </param>
    private static IEnumerable<KnowledgeEntry> ScanRooms(
        string root, string repository, RepositoryLayout layout, ISet<string> indexed)
    {
        foreach (var room in layout.Rooms)
        {
            var relative = $"{room}/{RoomInstructions}";
            if (indexed.Contains(relative)) continue;
            if (ReadDocument(root, relative) is not { } body) continue;

            yield return new KnowledgeEntry(repository, EntryKind.Knowledge, Provenance.Local, room, body, relative);
        }
    }

    /// <summary>
    /// The always-loaded rules, out of the region the lock points at (CANON8e / D59).
    /// </summary>
    /// <remarks>
    /// <para>Each rule is its OWN entry, split by its provenance line — the same reason a decisions
    /// log is split at its headings. Returning the whole tier for a question about one rule buries
    /// the answer in every other rule.</para>
    ///
    /// <para>Provenance is <b>canonical by construction</b> here: the lock is what named this span,
    /// and a span exists because daoris wrote it. The adopter's own text around the region is theirs
    /// and is never swept in — the markers are the boundary, and nothing outside them is read.</para>
    /// </remarks>
    private static IEnumerable<KnowledgeEntry> ScanRegion(
        string root, string repository, DaorisLock daorisLock)
    {
        foreach (var span in daorisLock.Spans)
        {
            // The file is named by the lock, which nobody reads closely (D18): never read outside the
            // repository, and never through a link.
            var host = RepositoryLayout.Declared(span.Key);
            if (RepositoryLayout.Escapes(host) || RepositoryLinks.Crosses(root, host)) continue;

            var rules = DoctrineRegion.Read(Path.Combine(root, span.Key));
            foreach (var entry in span.Value)
            {
                if (!rules.TryGetValue(entry.Source, out var body)) continue;

                yield return new KnowledgeEntry(
                    repository,
                    EntryKind.Rule,
                    Provenance.Canonical,
                    Path.GetFileNameWithoutExtension(entry.Target),
                    body,
                    // The file it actually lives in, with the rule as the anchor: a reader following
                    // this goes to the region and finds the rule, which is where it is.
                    span.Key,
                    Path.GetFileNameWithoutExtension(entry.Target));
            }
        }
    }

    private static IEnumerable<KnowledgeEntry> ScanDocuments(
        string root, string repository, DaorisLock daorisLock, string directory, EntryKind kind)
    {
        var absolute = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
        // A file where the folder must go is a folder link held as text, and a folder reached through a
        // link is some other folder: neither is enumerated (D117 §5.5).
        if (!Directory.Exists(absolute) || RepositoryLinks.Crosses(root, directory)) yield break;

        foreach (var file in Directory.EnumerateFiles(absolute, "*.md").Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(file);
            // The index is generated from the others; indexing it would return a table of contents
            // as though it were content.
            if (fileName.Equals("RULES_INDEX.md", StringComparison.OrdinalIgnoreCase)) continue;

            var relative = $"{directory}/{fileName}";
            if (daorisLock.IsMirror(relative)) continue;
            if (ReadDocument(root, relative) is not { } body) continue;

            yield return new KnowledgeEntry(
                repository,
                kind,
                daorisLock.ProvenanceOf(relative),
                Path.GetFileNameWithoutExtension(fileName),
                body,
                relative);
        }
    }

    private static IEnumerable<KnowledgeEntry> ScanSkills(
        string root, string repository, DaorisLock daorisLock, string directory)
    {
        var absolute = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absolute) || RepositoryLinks.Crosses(root, directory)) yield break;

        // A skill is a directory whose entry point is SKILL.md; its supporting files are read only
        // when the skill runs, so they are not separately addressable knowledge.
        foreach (var skillDirectory in Directory.EnumerateDirectories(absolute).Order(StringComparer.Ordinal))
        {
            var skillName = new DirectoryInfo(skillDirectory).Name;
            var relative = $"{directory}/{skillName}/SKILL.md";
            // 🔴 A mirror is a copy of its source for the one agent that reads only here (D117 §3.2):
            // indexed, it would put every skill in a search twice per repository.
            if (daorisLock.IsMirror(relative)) continue;
            if (ReadDocument(root, relative) is not { } body) continue;

            yield return new KnowledgeEntry(
                repository,
                EntryKind.Skill,
                daorisLock.ProvenanceOf(relative),
                skillName,
                body,
                relative);
        }
    }

    private static IEnumerable<KnowledgeEntry> ScanLog(
        string root, string repository, string relativePath, EntryKind kind)
    {
        var absolute = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute)) yield break;

        // 🔴 An anchor is unique within its file (REV3). Two sections under one heading — date-only fix
        // headings do it — shared an id, and the store's primary key threw on the second, failing the
        // whole refresh. The first keeps the id it always had; each repeat is told apart by its count.
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var section in MarkdownSections.Split(Text.ReadDocument(absolute)))
        {
            if (section.Body.Length == 0) continue;
            var seen = used.GetValueOrDefault(section.Heading) + 1;
            used[section.Heading] = seen;
            // A log is always the repository's own: canonical files are rules, knowledge and skills.
            yield return new KnowledgeEntry(
                repository,
                kind,
                Provenance.Local,
                section.Heading,
                section.Body,
                relativePath,
                seen == 1 ? section.Heading : $"{section.Heading} ({seen})");
        }
    }
}
