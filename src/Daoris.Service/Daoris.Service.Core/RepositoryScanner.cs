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
/// </remarks>
public sealed class RepositoryScanner
{
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

    /// <summary>Read every entry from a repository. A missing directory or file is simply absent.</summary>
    public IReadOnlyList<KnowledgeEntry> Scan(string repositoryRoot)
    {
        if (!Directory.Exists(repositoryRoot)) return [];

        var name = new DirectoryInfo(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, '/')).Name;
        var daorisLock = DaorisLock.Read(repositoryRoot);
        var target = ".claude";
        var entries = new List<KnowledgeEntry>();

        entries.AddRange(ScanDocuments(repositoryRoot, name, daorisLock, $"{target}/rules", EntryKind.Rule));
        entries.AddRange(ScanDocuments(repositoryRoot, name, daorisLock, $"{target}/knowledge", EntryKind.Knowledge));
        entries.AddRange(ScanSkills(repositoryRoot, name, daorisLock, $"{target}/skills"));

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
            if (found is not null) entries.AddRange(ScanLog(repositoryRoot, name, found, kind));
        }

        return entries;
    }

    private static IEnumerable<KnowledgeEntry> ScanDocuments(
        string root, string repository, DaorisLock daorisLock, string directory, EntryKind kind)
    {
        var absolute = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absolute)) yield break;

        foreach (var file in Directory.EnumerateFiles(absolute, "*.md").Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(file);
            // The index is generated from the others; indexing it would return a table of contents
            // as though it were content.
            if (fileName.Equals("RULES_INDEX.md", StringComparison.OrdinalIgnoreCase)) continue;

            var relative = $"{directory}/{fileName}";
            yield return new KnowledgeEntry(
                repository,
                kind,
                daorisLock.ProvenanceOf(relative),
                Path.GetFileNameWithoutExtension(fileName),
                Text.ReadDocument(file),
                relative);
        }
    }

    private static IEnumerable<KnowledgeEntry> ScanSkills(
        string root, string repository, DaorisLock daorisLock, string directory)
    {
        var absolute = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absolute)) yield break;

        // A skill is a directory whose entry point is SKILL.md; its supporting files are read only
        // when the skill runs, so they are not separately addressable knowledge.
        foreach (var skillDirectory in Directory.EnumerateDirectories(absolute).Order(StringComparer.Ordinal))
        {
            var file = Path.Combine(skillDirectory, "SKILL.md");
            if (!File.Exists(file)) continue;

            var skillName = new DirectoryInfo(skillDirectory).Name;
            var relative = $"{directory}/{skillName}/SKILL.md";
            yield return new KnowledgeEntry(
                repository,
                EntryKind.Skill,
                daorisLock.ProvenanceOf(relative),
                skillName,
                Text.ReadDocument(file),
                relative);
        }
    }

    private static IEnumerable<KnowledgeEntry> ScanLog(
        string root, string repository, string relativePath, EntryKind kind)
    {
        var absolute = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute)) yield break;

        foreach (var section in MarkdownSections.Split(Text.ReadDocument(absolute)))
        {
            if (section.Body.Length == 0) continue;
            // A log is always the repository's own: canonical files are rules, knowledge and skills.
            yield return new KnowledgeEntry(
                repository,
                kind,
                Provenance.Local,
                section.Heading,
                section.Body,
                relativePath,
                section.Heading);
        }
    }
}
