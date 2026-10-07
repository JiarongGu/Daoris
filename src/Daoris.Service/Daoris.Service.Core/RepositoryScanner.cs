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
/// A repository may still say where its records are, in its manifest's <c>documents</c> (DOC5; D122
/// §2.7, <see cref="RepositoryDocuments"/>). A declaration adds a path and is never required: the
/// declared decisions, fixes or archive is the first candidate for its log, a file or a folder of
/// records, each titled by its first heading (DOC8c), a decision's dated notes each an entry of its own
/// (ORIENT1g, <see cref="DecisionNotes"/>), and the declared router is read as a document. A
/// declaration the CLI refuses is read as none, so the candidates are read as before it was written. One
/// file is one place in the index.
///
/// Where the documents live is the layout's (D117 §5.5, LAYOUT4): the lock's root before the
/// manifest's, both roots for a repository with no lock, the lock's mirrors skipped so each skill is
/// found once, and each declared room's <c>AGENTS.md</c> read as the repository's own knowledge
/// (<see cref="RepositoryLayout"/>). A link, or a link held as text, is skipped and never followed
/// (<see cref="RepositoryLinks"/>): what it points at is not this repository's document.
///
/// Until a repository adopts, its root <c>README.md</c> is read too, split at its headings as a log is,
/// as local knowledge labelled by its path (WSSETUP8; D124 §5). It assigns no role. Once a lock exists
/// the repository has declared its documents, and the README is its front page again.
///
/// A deployment may name two more folders (ORIENT1c), read in every repository it registers and in none
/// by default: a folder of documents, each markdown file under it split at its headings with its opening
/// titled by its first heading; and a generated index, each table row and list item of a markdown file
/// under it one entry (<see cref="IndexRows"/>). Both are read after the router and the logs, so a file a
/// role read is not read again, and the index is never read as documents.
///
/// The declared index of where things are (ORIENT2e; D151 §6) is read after the deployment's index and before
/// its documents: every markdown file in the folder its README is in, split at its headings, each table row an
/// entry of its own naming its line (<see cref="IndexSections"/>, ORIENT2h), as entries of
/// <see cref="EntryKind.Index"/>. A folder the deployment named as its index is read a row at a time with its
/// cells labelled, as it chose, and never at its headings beside.
///
/// Every entry keeps the lines of its file its body is (<see cref="KnowledgeEntry.Lines"/>, ORIENT2e), counted
/// from the file's first line whatever the read trimmed above it, except a deployment's index row, which is its
/// cells labelled by their columns and no line as written.
/// </remarks>
/// <param name="documents">The documents folder, repository-relative with forward slashes; null reads none.</param>
/// <param name="index">The generated index's folder, the same; null reads none.</param>
public sealed class RepositoryScanner(string? documents = null, string? index = null)
{
    /// <summary>A room's instructions, the file every agent reads in that folder (D117 §2.2).</summary>
    private const string RoomInstructions = "AGENTS.md";

    /// <summary>An unadopted repository's front page, read at its root in any case (WSSETUP8; D124 §5).</summary>
    private const string ReadmeFile = "README.md";

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
        // 🔴 One file is one place in the index: read twice, under two kinds or two readers, its entries
        // share ids, and the store's primary key fails the whole refresh on the second (REV3).
        var indexed = entries.Select(entry => entry.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        void Add(IEnumerable<KnowledgeEntry> read)
        {
            foreach (var entry in read)
            {
                entries.Add(entry);
                indexed.Add(entry.RelativePath);
            }
        }

        Add(ScanRooms(repositoryRoot, name, layout, indexed));

        // The router, declared and never guessed (DOC5): a `docs/README.md` in a repository that
        // declared nothing may be a site's front page as easily as a router of its documents. A
        // document, not a log: it is read whole at a task's start, and its rows are about each other.
        if (layout.PathOf("router") is { } router && !indexed.Contains(router))
        {
            if (Directory.Exists(Absolute(repositoryRoot, router)))
            {
                Add(ScanFolder(repositoryRoot, name, router, EntryKind.Knowledge, indexed, byHeading: false));
            }
            else if (ReadDocumentAt(repositoryRoot, router) is { } read)
            {
                Add([new KnowledgeEntry(
                    name, EntryKind.Knowledge, Provenance.Local, Path.GetFileNameWithoutExtension(router), read.Text, router,
                    Lines: Whole(read))]);
            }
        }

        foreach (var (role, candidates, kind) in Logs)
        {
            // FIRST match wins. The candidates are alternative NAMES for one log, not several logs —
            // and scanning them all double-counts on a case-insensitive filesystem, where
            // `docs/DECISIONS.md` and `docs/decisions.md` are the same file. Found immediately, on
            // Windows; on Linux it would have waited until someone happened to have both.
            //
            // The declared path is the first candidate (DOC5), so a log at a name no candidate knows is
            // found, and one a candidate also names is found once. Declared, it may be a folder of
            // records; absent from the disk, the candidates are read as before it was declared.
            var declared = layout.PathOf(role) is { } path && Exists(repositoryRoot, path, folder: true) ? path : null;
            var found = declared ?? candidates.FirstOrDefault(candidate => Exists(repositoryRoot, candidate, folder: false));
            // A log reached through a link is some other folder's log (LAYOUT4): skipped, never followed,
            // and never replaced by a candidate, which would be a guess at which log is the repository's.
            if (found is null || indexed.Contains(found)) continue;

            Add(Directory.Exists(Absolute(repositoryRoot, found))
                ? ScanFolder(repositoryRoot, name, found, kind, indexed, byHeading: true)
                : ScanLog(repositoryRoot, name, found, kind));
        }

        // What the deployment named (ORIENT1c), after the roles that are files of their own: its index a row
        // at a time, as it chose for that folder, before the declared index, so a deployment that names the
        // declared index's own folder reads it as it always has (one file, one place).
        if (index is not null && Directory.Exists(Absolute(repositoryRoot, index)))
        {
            Add(ScanIndex(repositoryRoot, name, index, indexed));
        }

        // The index of where things are, declared and never guessed (ORIENT2e; D151 §6), as the router is: a
        // folder named like one may be anything. Every file of it the deployment did not read, at its headings.
        if (layout.PathOf("index") is { } declaredIndex)
        {
            Add(ScanDeclaredIndex(repositoryRoot, name, declaredIndex, indexed));
        }

        // Then the deployment's documents a section at a time, each file read once and the index never as a
        // document.
        if (documents is not null && Directory.Exists(Absolute(repositoryRoot, documents)))
        {
            Add(ScanDocumentFolder(repositoryRoot, name, documents, indexed));
        }

        // The README, as the repository's own word until it adopts (WSSETUP8; D124 §5): what its authors
        // wrote for a newcomer, and a session searching the index is one. Last, so a declaration that
        // names the file reads it by its role, and this never reads it a second time.
        if (!layout.Locked && Readme(repositoryRoot) is { } readme && !indexed.Contains(readme))
        {
            Add(ScanReadme(repositoryRoot, name, readme));
        }

        return entries;
    }

    /// <summary>
    /// The root's <c>README.md</c> in any case, spelled as the disk spells it so a session can open it;
    /// null when there is none. Only that file: not <c>docs/README.md</c>, which may be a site's front page
    /// (DOC5), and no other format, since only markdown has headings the splitter reads.
    /// </summary>
    /// <remarks>Two spellings side by side, which only a case-sensitive disk holds, read as the first in ordinal order.</remarks>
    private static string? Readme(string root) =>
        Directory.EnumerateFiles(root)
            .Select(Path.GetFileName)
            .Where(file => string.Equals(file, ReadmeFile, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// A README split at its headings, as a log is: the part before the first heading titled
    /// <c>README</c>, each section after by its heading. Knowledge, the repository's own, labelled by its
    /// path and claiming no role: one hit among others, where a router guessed would be followed as a map
    /// (D124 §5, on DOC5's objection).
    /// </summary>
    private static IEnumerable<KnowledgeEntry> ScanReadme(string root, string repository, string relative)
    {
        // Through the one boundary: a README that is a link, or a link held as text, is some other
        // folder's word (D117 §5.5).
        if (ReadDocumentAt(root, relative) is not { } read) yield break;

        // The part a log drops is the part a newcomer reads first. Unanchored, so a reader following it
        // lands at the top of the file, and its id is never a section's.
        var preamble = MarkdownSections.PreambleAt(read.Text);
        if (preamble.Body.Length > 0)
        {
            yield return new KnowledgeEntry(
                repository, EntryKind.Knowledge, Provenance.Local, Path.GetFileNameWithoutExtension(ReadmeFile), preamble.Body, relative,
                Lines: Span(preamble, read.First));
        }
        foreach (var entry in Sections(repository, EntryKind.Knowledge, relative, read)) yield return entry;
    }

    /// <summary>
    /// The logs, each with the role a repository declares it under (D122 §2.7) and the names the scanner
    /// tries when it declares none.
    /// </summary>
    private static readonly (string Role, string[] Candidates, EntryKind Kind)[] Logs =
    [
        ("decisions", DecisionFiles, EntryKind.Decision),
        ("fixes", FixFiles, EntryKind.Fix),
        ("archive", TaskOutcomeFiles, EntryKind.TaskOutcome),
    ];

    private static string Absolute(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Whether a log is there: a file, or a folder of records where one is declared.</summary>
    private static bool Exists(string root, string relative, bool folder) =>
        File.Exists(Absolute(root, relative)) || (folder && Directory.Exists(Absolute(root, relative)));

    /// <summary>
    /// A document's text, or null when it is not one to index: absent, reached through a link, or a
    /// link held as text (D117 §5.5). Read once, through the same boundary every document goes through.
    /// </summary>
    private static string? ReadDocument(string root, string relative) => ReadDocumentAt(root, relative)?.Text;

    /// <summary>
    /// <see cref="ReadDocument"/>, and the line of the file its text starts on (ORIENT2e), so every entry read
    /// from it names the file's own lines.
    /// </summary>
    private static (string Text, int First)? ReadDocumentAt(string root, string relative)
    {
        if (RepositoryLinks.Crosses(root, relative)) return null;
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute)) return null;
        var read = Text.ReadDocumentAt(absolute);
        return RepositoryLinks.HeldAsText(root, relative, read.Text) ? null : read;
    }

    /// <summary>The lines a document read whole is; none for an empty one, which is no line's text.</summary>
    private static LineSpan? Whole((string Text, int First) read) =>
        read.Text.Length == 0 ? null : new LineSpan(read.First, read.First + LineSpan.LineCount(read.Text) - 1);

    /// <summary>The lines of a file a section of its text is, the text starting on <paramref name="first"/>; none for an empty one.</summary>
    private static LineSpan? Span(MarkdownSection section, int first) =>
        section.First > 0 ? new LineSpan(first - 1 + section.First, first - 1 + section.Last) : null;

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
            if (ReadDocumentAt(root, relative) is not { } read) continue;

            yield return new KnowledgeEntry(
                repository, EntryKind.Knowledge, Provenance.Local, room, read.Text, relative, Lines: Whole(read));
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

            var rules = DoctrineRegion.ReadAt(Path.Combine(root, span.Key));
            foreach (var entry in span.Value)
            {
                if (!rules.TryGetValue(entry.Source, out var rule)) continue;

                yield return new KnowledgeEntry(
                    repository,
                    EntryKind.Rule,
                    Provenance.Canonical,
                    Path.GetFileNameWithoutExtension(entry.Target),
                    rule.Body,
                    // The file it actually lives in, with the rule as the anchor: a reader following
                    // this goes to the region and finds the rule, which is where it is.
                    span.Key,
                    Path.GetFileNameWithoutExtension(entry.Target),
                    Lines: rule.Lines);
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
            if (ReadDocumentAt(root, relative) is not { } read) continue;

            yield return new KnowledgeEntry(
                repository,
                kind,
                daorisLock.ProvenanceOf(relative),
                Path.GetFileNameWithoutExtension(fileName),
                read.Text,
                relative,
                Lines: Whole(read));
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
            if (ReadDocumentAt(root, relative) is not { } read) continue;

            yield return new KnowledgeEntry(
                repository,
                EntryKind.Skill,
                daorisLock.ProvenanceOf(relative),
                skillName,
                read.Text,
                relative,
                Lines: Whole(read));
        }
    }

    /// <summary>
    /// A declared folder (DOC5; D122 §2.1): one entry per markdown file in it or below, each read whole,
    /// since a record's headings are its own parts (an ADR's context and consequences) and not records of
    /// their own.
    /// </summary>
    /// <param name="indexed">What is already read: a file another reader took is not read again.</param>
    /// <param name="byHeading">
    /// Whether each file is titled by its first heading, and by its name where it has none (DOC8c; D134 §5).
    /// A log's folder is: a search for a decision showed <c>D130</c> where its heading says what was decided,
    /// and an ADR's file name is a number and a slug. A router's folder is not: it holds documents, named by
    /// their files as the knowledge tier's are (D122). The id is the path either way, so a title is never a key.
    /// </param>
    private static IEnumerable<KnowledgeEntry> ScanFolder(
        string root, string repository, string folder, EntryKind kind, ISet<string> indexed, bool byHeading)
    {
        foreach (var relative in Records(root, folder).Order(StringComparer.Ordinal))
        {
            if (indexed.Contains(relative)) continue;
            if (ReadDocumentAt(root, relative) is not { } read) continue;

            var title = (byHeading ? MarkdownSections.FirstHeading(read.Text) : null) ?? Path.GetFileNameWithoutExtension(relative);
            if (byHeading && kind == EntryKind.Decision)
            {
                foreach (var entry in Decision(repository, relative, title, read)) yield return entry;
                continue;
            }
            yield return new KnowledgeEntry(repository, kind, Provenance.Local, title, read.Text, relative, Lines: Whole(read));
        }
    }

    /// <summary>
    /// A decision in a folder of records (ORIENT1g; D134 §5 as amended): its text before its first dated note,
    /// at the id it always had, then each note an entry of its own, titled by the decision's file and the
    /// note's label and anchored by that label, as the decisions digest gives them (<see cref="DecisionNotes"/>).
    /// </summary>
    /// <remarks>
    /// A question about one note landed on the whole decision, or past it, while every other note's words
    /// diluted the one that answered (D125's twenty-two). A label twice in one file is told apart by its count,
    /// as a heading twice is (REV3), so one file never yields one id twice.
    /// </remarks>
    private static IEnumerable<KnowledgeEntry> Decision(string repository, string relative, string title, (string Text, int First) read)
    {
        // The entry is the text's lines from its first, and each note its own lines, as the digest counts them.
        var (entry, notes) = DecisionNotes.Split(read.Text);
        yield return new KnowledgeEntry(
            repository, EntryKind.Decision, Provenance.Local, title, entry, relative, Lines: Whole((entry, read.First)));

        var name = Path.GetFileNameWithoutExtension(relative);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var note in notes)
        {
            var seen = used.GetValueOrDefault(note.Label) + 1;
            used[note.Label] = seen;
            yield return new KnowledgeEntry(
                repository, EntryKind.Decision, Provenance.Local, $"{name} › {note.Label}", note.Body, relative,
                seen == 1 ? note.Label : $"{note.Label} ({seen})",
                Lines: new LineSpan(read.First - 1 + note.First, read.First - 1 + note.Last));
        }
    }

    /// <summary>
    /// The declared index of where things are (ORIENT2e; D151 §6, the orientation design §3.1): every markdown
    /// file in the folder its README is in, and below, split at its headings and each table row an entry of its
    /// own (<see cref="IndexSections"/>, ORIENT2h), as entries of the index's own kind, the repository's own, each
    /// keeping its lines.
    /// </summary>
    /// <remarks>
    /// The folder the README is in, since the index is the folder and the README only names its files; a declared
    /// folder is that folder. A README at the repository's root is read alone: its folder is the whole repository,
    /// and the index is the small, reviewed statement of where things are, never every file beside it. A section
    /// or a row is anchored by its title, a title twice in one file told apart by its count (REV3); text before any
    /// heading is the file's own, unanchored.
    /// </remarks>
    private static IEnumerable<KnowledgeEntry> ScanDeclaredIndex(
        string root, string repository, string declared, ISet<string> indexed)
    {
        IEnumerable<string> files;
        if (Directory.Exists(Absolute(root, declared)))
        {
            files = Records(root, declared);
        }
        else if (File.Exists(Absolute(root, declared)))
        {
            var slash = declared.LastIndexOf('/');
            files = slash < 0 ? [declared] : Records(root, declared[..slash]);
        }
        else
        {
            yield break;
        }

        foreach (var relative in files.Order(StringComparer.Ordinal))
        {
            if (indexed.Contains(relative)) continue;
            if (ReadDocumentAt(root, relative) is not { } read) continue;

            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var section in IndexSections.Read(read.Text, relative, read.First))
            {
                // The file's opening is the file, unanchored; prose before any heading after a table is a second run
                // of it, and anchored as a repeat, so no two entries share an id (REV3). A row is counted with the
                // sections, since a row's title can be a subsection's.
                var seen = used.GetValueOrDefault(section.Title) + 1;
                used[section.Title] = seen;
                var anchor = section.Opening && seen == 1 ? null
                    : seen == 1 ? section.Title
                    : $"{section.Title} ({seen})";
                yield return new KnowledgeEntry(
                    repository, EntryKind.Index, Provenance.Local, section.Title, section.Body, relative, anchor,
                    Lines: section.Lines);
            }
        }
    }

    /// <summary>
    /// A generated index (ORIENT1c): each table row and list item of each markdown file one entry, and the
    /// file's prose one more, so a search for where something is lands on the row that says so.
    /// </summary>
    /// <remarks>
    /// Entries of the index's kind (ORIENT2e), as the declared index's sections are, so a search by kind finds
    /// them and a search naming none holds them to two. A row is its cells labelled by their columns, not its
    /// file's line as written, so it names no lines; its cells name the places themselves.
    /// </remarks>
    private static IEnumerable<KnowledgeEntry> ScanIndex(
        string root, string repository, string folder, ISet<string> indexed)
    {
        foreach (var relative in Records(root, folder).Order(StringComparer.Ordinal))
        {
            if (indexed.Contains(relative)) continue;
            if (ReadDocument(root, relative) is not { } text) continue;

            foreach (var row in IndexRows.Read(text, Path.GetFileNameWithoutExtension(relative)))
            {
                yield return new KnowledgeEntry(
                    repository, EntryKind.Index, Provenance.Local, row.Title, row.Body, relative, row.Anchor);
            }
        }
    }

    /// <summary>
    /// A folder of documents (ORIENT1c): each markdown file under it split at its level-two headings, as a log
    /// is, with the part before the first one titled by the document's first heading. A design's sections are
    /// what a question about it is answered by; the whole file buries the answer as a whole log would.
    /// </summary>
    private IEnumerable<KnowledgeEntry> ScanDocumentFolder(
        string root, string repository, string folder, ISet<string> indexed)
    {
        foreach (var relative in Records(root, folder).Order(StringComparer.Ordinal))
        {
            if (indexed.Contains(relative)) continue;
            if (index is not null && RepositoryLayout.Within(relative.ToLowerInvariant(), index.ToLowerInvariant())) continue;
            if (ReadDocumentAt(root, relative) is not { } read) continue;

            // An opening that is only the title says nothing a section does not.
            var opening = MarkdownSections.PreambleAt(read.Text);
            if (opening.Body.Split('\n').Any(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#')))
            {
                yield return new KnowledgeEntry(
                    repository, EntryKind.Knowledge, Provenance.Local,
                    MarkdownSections.FirstHeading(read.Text) ?? Path.GetFileNameWithoutExtension(relative), opening.Body, relative,
                    Lines: Span(opening, read.First));
            }
            foreach (var entry in Sections(repository, EntryKind.Knowledge, relative, read)) yield return entry;
        }
    }

    /// <summary>
    /// The markdown files in a folder and below, repository-relative. A folder reached through a link is
    /// some other folder (LAYOUT4) and is never entered, so a link that loops back is never walked round.
    /// </summary>
    private static IEnumerable<string> Records(string root, string folder)
    {
        if (RepositoryLinks.Crosses(root, folder)) yield break;

        var absolute = Absolute(root, folder);
        foreach (var file in Directory.EnumerateFiles(absolute, "*.md"))
        {
            yield return $"{folder}/{Path.GetFileName(file)}";
        }
        foreach (var below in Directory.EnumerateDirectories(absolute))
        {
            foreach (var file in Records(root, $"{folder}/{Path.GetFileName(below)}")) yield return file;
        }
    }

    private static IEnumerable<KnowledgeEntry> ScanLog(
        string root, string repository, string relativePath, EntryKind kind)
    {
        // Through the one boundary every document goes through: a log reached through a link, or a link
        // held as text, is some other folder's (LAYOUT4).
        if (ReadDocumentAt(root, relativePath) is not { } read) yield break;

        // A log is always the repository's own: canonical files are rules, knowledge and skills.
        foreach (var entry in Sections(repository, kind, relativePath, read)) yield return entry;
    }

    /// <summary>
    /// One local entry per section of a file split at its headings, each anchored by its heading and keeping the
    /// lines of the file its body is (ORIENT2e).
    /// </summary>
    private static IEnumerable<KnowledgeEntry> Sections(
        string repository, EntryKind kind, string relativePath, (string Text, int First) read)
    {
        // 🔴 An anchor is unique within its file (REV3). Two sections under one heading — date-only fix
        // headings do it — shared an id, and the store's primary key threw on the second, failing the
        // whole refresh. The first keeps the id it always had; each repeat is told apart by its count.
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var section in MarkdownSections.Split(read.Text))
        {
            if (section.Body.Length == 0) continue;
            var seen = used.GetValueOrDefault(section.Heading) + 1;
            used[section.Heading] = seen;
            yield return new KnowledgeEntry(
                repository,
                kind,
                Provenance.Local,
                section.Heading,
                section.Body,
                relativePath,
                seen == 1 ? section.Heading : $"{section.Heading} ({seen})",
                Lines: Span(section, read.First));
        }
    }
}
