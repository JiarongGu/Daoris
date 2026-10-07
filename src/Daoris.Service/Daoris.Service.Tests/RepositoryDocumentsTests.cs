using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The service reads the records a repository declares (DOC5; D122,
/// `docs/2026-10-01-development-documents-design.md` §2.7 and §5): the declared decisions, fixes and
/// archive before the scanner's own candidates, and the router as a document.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *the development documents*): the CLI's
/// <c>checkDocuments</c> (<c>src/Daoris.Cli/src/documents.ts</c>) reads the same field, and the two share
/// no code. <see cref="What_a_declaration_is_read_as"/> holds the CLI's <c>documents-manifest.test.ts</c>
/// rows, its reading table and then its refusals, in their order, before this side's own edges.</para>
///
/// <para>Where the answers differ, they differ because one side gates and the other indexes, and the row
/// says so: the CLI refuses the whole manifest for one role it cannot honour, so <c>check</c> fails and
/// <c>sync</c> refuses; the service reads that role as undeclared, reads the roles beside it, and never
/// fails the corpus for one repository's file.</para>
/// </remarks>
public sealed class RepositoryDocumentsTests : IDisposable
{
    // One folder above the repository, so a path that climbs out of it lands somewhere this test owns.
    private readonly string _parent = Path.Combine(
        Path.GetTempPath(), "daoris-documents-" + Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_parent, "repo");

    public void Dispose()
    {
        if (Directory.Exists(_parent)) Directory.Delete(_parent, recursive: true);
    }

    private void Write(string relative, string content, string? under = null)
    {
        var file = Path.Combine(under ?? Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private void Declare(string documents) =>
        Write("daoris.json", $$"""{"source":"s","packs":[],"documents":{{documents}}}""");

    private IReadOnlyList<KnowledgeEntry> Scan() => new RepositoryScanner().Scan(Root);

    private static List<string> Of(IEnumerable<KnowledgeEntry> entries, EntryKind kind) =>
        entries.Where(e => e.Kind == kind).Select(e => $"{e.Title} @ {e.RelativePath}").ToList();

    // ── the declared logs come before the candidates ───────────────────────────────────────────────

    /// <summary>
    /// The row's proof: a decisions log at a path no candidate names is found. Before DOC5 the scanner
    /// knew only its candidate names, and a log anywhere else was invisible to every search.
    /// </summary>
    [Fact]
    public void A_declared_decisions_log_no_candidate_names_is_found_once()
    {
        Declare("""{"decisions":"records/choices.md"}""");
        Write("records/choices.md", "# Choices\n\n## C1 — one\n\nBecause.\n\n## C2 — two\n\nAnd because.\n");

        var entries = Scan();

        Assert.Equal(["C1 — one @ records/choices.md", "C2 — two @ records/choices.md"], Of(entries, EntryKind.Decision));
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct().Count());
        Assert.All(entries, e => Assert.Equal(Provenance.Local, e.Provenance));
    }

    /// <summary>Found once, never twice, when a candidate names the declared path too.</summary>
    [Theory]
    [InlineData("docs/DECISIONS.md")]
    [InlineData("./docs\\DECISIONS.md")]
    public void A_declared_log_a_candidate_also_names_is_found_once(string declared)
    {
        Declare($$"""{"decisions":{{System.Text.Json.JsonSerializer.Serialize(declared)}}}""");
        Write("docs/DECISIONS.md", "## D1 — one\n\nA.\n\n## D2 — two\n\nB.\n");

        Assert.Equal(["D1 — one @ docs/DECISIONS.md", "D2 — two @ docs/DECISIONS.md"], Of(Scan(), EntryKind.Decision));
    }

    /// <summary>
    /// Before its candidates: the candidates are alternative names for the one log, and the declaration
    /// says which is it. A file at a candidate name beside a declared log is not read as a second log.
    /// </summary>
    [Fact]
    public void The_declared_log_comes_before_a_candidate()
    {
        Declare("""{"decisions":"records/choices.md"}""");
        Write("records/choices.md", "## C1 — declared\n\nOurs.\n");
        Write("docs/DECISIONS.md", "## D1 — a candidate\n\nAn older file.\n");

        Assert.Equal(["C1 — declared @ records/choices.md"], Of(Scan(), EntryKind.Decision));
    }

    [Theory]
    [InlineData("fixes", EntryKind.Fix)]
    [InlineData("archive", EntryKind.TaskOutcome)]
    public void The_declared_fixes_and_archive_are_read_as_their_logs(string role, EntryKind kind)
    {
        Declare($$"""{"{{role}}":"records/{{role}}.md"}""");
        Write($"records/{role}.md", "## 2026-10-01 — one\n\nA.\n\n## 2026-10-02 — two\n\nB.\n");

        Assert.Equal(
            [$"2026-10-01 — one @ records/{role}.md", $"2026-10-02 — two @ records/{role}.md"],
            Of(Scan(), kind));
    }

    /// <summary>
    /// A declared path the disk does not hold falls through to the candidates: `check` fails on it, and
    /// the index is never left with less than it had before the repository declared.
    /// </summary>
    [Fact]
    public void A_declared_log_that_is_not_there_falls_through_to_the_candidates()
    {
        Declare("""{"decisions":"records/missing.md"}""");
        Write("docs/DECISIONS.md", "## D1 — a candidate\n\nRead as before.\n");

        Assert.Equal(["D1 — a candidate @ docs/DECISIONS.md"], Of(Scan(), EntryKind.Decision));
    }

    /// <summary>
    /// A declared path may be a folder (DOC3): a folder of decision records is the decisions role (§2.1),
    /// one record per file, below it too, each read whole since its headings are the record's own parts,
    /// and titled by its first (DOC8c).
    /// </summary>
    [Fact]
    public void A_declared_folder_is_one_record_per_file()
    {
        Declare("""{"decisions":"docs/adr"}""");
        Write("docs/adr/0001-one.md", "# One\n\n## Context\n\nWhy.\n\n## Decision\n\nWhat.\n");
        Write("docs/adr/done/0002-two.md", "# Two\n\nDone.\n");
        Write("docs/adr/notes.txt", "Not a record.\n");

        var decisions = Scan().Where(e => e.Kind == EntryKind.Decision).ToList();

        Assert.Equal(
            ["One @ docs/adr/0001-one.md", "Two @ docs/adr/done/0002-two.md"],
            Of(decisions, EntryKind.Decision));
        Assert.Contains("## Decision", decisions[0].Body);
        Assert.All(decisions, d => Assert.Null(d.Anchor));
    }

    /// <summary>
    /// One file is one place in the index. Declared for two roles, it would be read twice under the same
    /// ids, and the store's primary key fails the whole refresh on the second (REV3).
    /// </summary>
    [Fact]
    public void A_file_declared_for_two_roles_is_read_once()
    {
        Declare("""{"decisions":"records/log.md","archive":"records/log.md"}""");
        Write("records/log.md", "## One\n\nA.\n\n## Two\n\nB.\n");

        var entries = Scan();

        Assert.Equal(["One @ records/log.md", "Two @ records/log.md"], Of(entries, EntryKind.Decision));
        Assert.Empty(Of(entries, EntryKind.TaskOutcome));
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct().Count());
    }

    // ── the router ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The router is read whole at a task's start, and its rows are about each other: one file, one
    /// entry, never split at its headings. On demand, so knowledge; the repository's own, so local.
    /// </summary>
    [Fact]
    public void The_router_is_indexed_as_a_document()
    {
        Declare("""{"router":"docs/README.md"}""");
        Write("docs/README.md", "# The documents\n\n## Contracts\n\n| Document | Kind |\n|---|---|\n\n## Records\n\nThe log.\n");

        var router = Assert.Single(Scan());

        Assert.Equal(EntryKind.Knowledge, router.Kind);
        Assert.Equal(Provenance.Local, router.Provenance);
        Assert.Equal("README", router.Title);
        Assert.Equal("docs/README.md", router.RelativePath);
        Assert.Null(router.Anchor);
        Assert.Contains("## Contracts", router.Body);
        Assert.Contains("## Records", router.Body);
    }

    /// <summary>
    /// Declared, never found: the router has no candidates, since a `docs/README.md` in a repository that
    /// declared nothing may be a site's front page as easily as a router of its documents.
    /// </summary>
    [Fact]
    public void An_undeclared_router_is_not_guessed()
    {
        Write("docs/README.md", "# The documents\n");

        Assert.Empty(Scan());
    }

    /// <summary>A router the knowledge tier already read (a repository with no lock reads both roots) is one entry.</summary>
    [Fact]
    public void A_router_a_tier_already_read_is_one_entry()
    {
        Declare("""{"router":".agents/knowledge/index.md"}""");
        Write(".agents/knowledge/index.md", "# The documents\n");

        var only = Assert.Single(Scan());

        Assert.Equal(".agents/knowledge/index.md", only.RelativePath);
    }

    // ── a declaration the CLI refuses ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The row's third proof. A declaration the CLI refuses (exit 2, so `check` fails and `sync` refuses)
    /// is read as undeclared, and the scanner reads its candidates exactly as it did before DOC5. It never
    /// throws, never reads the refused path, and never guesses which of two declarations was meant.
    /// </summary>
    [Theory]
    [InlineData("a list", """{"source":"s","packs":[],"documents":["records/choices.md"]}""")]
    [InlineData("a path that climbs out", """{"source":"s","packs":[],"documents":{"decisions":"../outside/DECISIONS.md"}}""")]
    [InlineData("an absolute path", """{"source":"s","packs":[],"documents":{"decisions":"/outside/DECISIONS.md"}}""")]
    [InlineData("a ceiling the CLI refuses", """{"source":"s","packs":[],"documents":{"decisions":{"path":"records/choices.md","words":0}}}""")]
    [InlineData("a field nobody reads", """{"source":"s","packs":[],"documents":{"decisions":{"path":"records/choices.md","file":"x"}}}""")]
    [InlineData("a role declared twice", """{"source":"s","packs":[],"documents":{"decisions":"records/choices.md","decisions":"records/other.md"}}""")]
    [InlineData("documents declared twice", """{"source":"s","documents":{"decisions":"records/choices.md"},"packs":[],"documents":{"decisions":"records/other.md"}}""")]
    [InlineData("a manifest that is not JSON", """{"source":"s","documents":{"decisions":""")]
    public void A_declaration_the_CLI_refuses_leaves_the_candidates_read_as_before(string name, string manifest)
    {
        Assert.NotEmpty(name);
        Write("daoris.json", manifest);
        Write("docs/DECISIONS.md", "## D1 — the candidate\n\nRead as before.\n");
        Write("records/choices.md", "## C1 — refused\n\nNever read as a decision.\n");
        Write("records/other.md", "## C2 — refused\n\nNor this.\n");
        Write("DECISIONS.md", "## X1 — outside\n\nAnother folder's.\n", under: Path.Combine(_parent, "outside"));

        Assert.Equal(["D1 — the candidate @ docs/DECISIONS.md"], Of(Scan(), EntryKind.Decision));
    }

    /// <summary>A declared log that is a link is some other folder's log, and the candidates are not read in its place.</summary>
    [Fact]
    public void A_declared_log_or_router_that_is_a_link_is_never_followed()
    {
        var elsewhere = Path.Combine(_parent, "elsewhere");
        Write("choices.md", "## X1 — somebody else's\n\nNot ours.\n", under: elsewhere);
        Write("README.md", "# Somebody else's router\n", under: elsewhere);
        Declare("""{"decisions":"records/choices.md","router":"docs/README.md","fixes":"records/fixes"}""");
        Write("records/fixes/one.md", "# One\n\nOurs.\n");
        Write("docs/DECISIONS.md", "## D1 — a candidate\n\nNot read in the link's place.\n");

        try
        {
            File.CreateSymbolicLink(Path.Combine(Root, "records", "choices.md"), Path.Combine(elsewhere, "choices.md"));
            File.CreateSymbolicLink(Path.Combine(Root, "docs", "README.md"), Path.Combine(elsewhere, "README.md"));
            File.CreateSymbolicLink(Path.Combine(Root, "records", "fixes", "two.md"), Path.Combine(elsewhere, "choices.md"));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            return; // this machine makes no links (xunit v2 has no dynamic skip); the refusal rows hold the reading
        }

        var entries = Scan();

        Assert.Empty(Of(entries, EntryKind.Decision));
        Assert.Empty(Of(entries, EntryKind.Knowledge));
        Assert.Equal(["One @ records/fixes/one.md"], Of(entries, EntryKind.Fix));
    }

    // ── the twin table ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What a declaration is read as: the CLI's <c>documents-manifest.test.ts</c>, its
    /// <c>MANIFEST_ROWS</c> and then its <c>REFUSED_ROWS</c>, row for row and in order, then this side's own
    /// edges. A reading is <c>role path words</c> per declared role in the roles' order, <c>-</c> for none,
    /// joined by <c>|</c>; empty is nothing declared. A row the CLI refuses reads here as that role
    /// undeclared, and every other role as declared.
    /// </summary>
    /// <param name="documents">The <c>documents</c> field's JSON; null for none. Ignored when <paramref name="raw"/> is set.</param>
    /// <param name="raw">A whole manifest, for the rows only the text can show.</param>
    /// <param name="agents">Whether the manifest names the agents layout, as the CLI's mirror row does.</param>
    [Theory]
    // MANIFEST_ROWS: the CLI's reading, and this side's, are one.
    [InlineData("absent is none", null, null, false, "")]
    [InlineData("null is none", "null", null, false, "")]
    [InlineData("empty is none", "{}", null, false, "")]
    [InlineData(
        "a string is a path with no ceiling; an object a path and a ceiling; brief and room a ceiling alone",
        """{"backlog":{"path":"TASKS.md","words":6600},"room":{"words":600},"decisions":"docs/DECISIONS.md","brief":{"words":1300},"gates":"./daoris.gates.json","archive":"docs\\archive\\"}""",
        null, false,
        "brief - 1300|room - 600|decisions docs/DECISIONS.md -|backlog TASKS.md 6600|archive docs/archive -|gates daoris.gates.json -")]
    [InlineData("an object with a path and no ceiling", """{"router":{"path":"docs/README.md"}}""", null, false, "router docs/README.md -")]
    [InlineData(
        "the index of where things are, a path, after the router",
        """{"decisions":"docs/decisions","index":"docs/index/README.md","router":"docs/README.md"}""",
        null, false,
        "router docs/README.md -|index docs/index/README.md -|decisions docs/decisions -")]
    // REFUSED_ROWS: the CLI refuses the manifest; this side reads the role as undeclared.
    [InlineData("a list", """["docs/DECISIONS.md"]""", null, false, "")]
    [InlineData("an unknown role, naming the known ones", """{"roadmap":"ROADMAP.md"}""", null, false, "")]
    [InlineData("a ceiling alone on the index, which takes a path", """{"index":{"words":100}}""", null, false, "")]
    [InlineData("a tier the index already lists", """{"knowledge":"docs/notes"}""", null, false, "")]
    [InlineData("a skill, the same", """{"skill":"x"}""", null, false, "")]
    [InlineData("a path on the brief", """{"brief":"BRIEF.md"}""", null, false, "")]
    [InlineData("a path on the room", """{"room":{"path":"src","words":600}}""", null, false, "")]
    [InlineData("a number for a path", """{"decisions":7}""", null, false, "")]
    [InlineData("an object with no path", """{"decisions":{"words":10}}""", null, false, "")]
    [InlineData("a field nobody reads", """{"backlog":{"path":"TASKS.md","word":10}}""", null, false, "")]
    [InlineData("a ceiling that is not a whole number above zero", """{"backlog":{"path":"TASKS.md","words":0}}""", null, false, "")]
    [InlineData("a ceiling as text", """{"brief":{"words":"1500"}}""", null, false, "")]
    [InlineData("a path that climbs out", """{"decisions":"../elsewhere/DECISIONS.md"}""", null, false, "")]
    [InlineData("an absolute path", """{"decisions":"/srv/DECISIONS.md"}""", null, false, "")]
    [InlineData("a drive path", """{"decisions":"C:/notes/DECISIONS.md"}""", null, false, "")]
    [InlineData("the repository's root", """{"decisions":"./"}""", null, false, "")]
    [InlineData("inside the doctrine target", """{"glossary":".claude/knowledge/glossary.md"}""", null, false, "")]
    [InlineData("inside the mirror root", """{"fixes":".claude/skills/fixes.md"}""", null, true, "")]
    // The CLI refuses the whole manifest; this side cannot know which decisions was meant, and reads the rest.
    [InlineData(
        "a role declared twice, which JSON would keep the last of silently", null,
        """{ "source": "s", "packs": [], "documents": { "decisions": "a.md", "backlog": "b.md", "decisions": "c.md" } }""",
        false, "backlog b.md -")]
    [InlineData(
        "documents declared twice", null,
        """{ "source": "s", "documents": { "decisions": "a.md" }, "packs": [], "documents": { "backlog": "b.md" } }""",
        false, "")]
    // This side's own edges.
    [InlineData("a refused role beside a good one: the good one is read", """{"decisions":"../x.md","fixes":"docs/FIX-LOG.md"}""", null, false, "fixes docs/FIX-LOG.md -")]
    [InlineData("a refused index beside a good router: the router is read", """{"index":{"words":100},"router":"docs/README.md"}""", null, false, "router docs/README.md -")]
    [InlineData("a ceiling the CLI reads as a whole number, in another spelling", """{"backlog":{"path":"TASKS.md","words":1e3},"router":{"path":"docs/README.md","words":2500.0}}""", null, false, "router docs/README.md 2500|backlog TASKS.md 1000")]
    [InlineData("a null for a role", """{"decisions":null}""", null, false, "")]
    [InlineData("a null path on the room is still a path named", """{"room":{"path":null}}""", null, false, "")]
    // The CLI keeps a path's spelling and checks the spelling; this side reads the file it names, and never
    // the root or the target that a `..` inside reaches.
    [InlineData("a path with a .. inside is read where it lands", """{"decisions":"docs/../DECISIONS.md"}""", null, false, "decisions DECISIONS.md -")]
    [InlineData("a .. inside that lands on the root", """{"decisions":"docs/.."}""", null, false, "")]
    [InlineData("a .. inside that lands in the target", """{"glossary":"x/../.claude/knowledge/g.md"}""", null, false, "")]
    [InlineData("spelled inside the target, whatever a .. makes of it", """{"glossary":".claude/../docs/g.md"}""", null, false, "")]
    [InlineData("a manifest that is not JSON", null, """{"documents":""", false, "")]
    public void What_a_declaration_is_read_as(string name, string? documents, string? raw, bool agents, string reading)
    {
        Assert.NotEmpty(name);
        var layout = agents ? ",\"harness\":\"agents\",\"target\":\".agents\"" : "";
        Write("daoris.json", raw ?? (documents is null
            ? $$"""{"source":"s","packs":[]{{layout}}}"""
            : $$"""{"source":"s","packs":[]{{layout}},"documents":{{documents}}}"""));

        var read = RepositoryLayout.Of(Root, DaorisLock.Read(Root)).Documents;

        Assert.Equal(reading, string.Join("|", read.Select(d => $"{d.Role} {d.Path ?? "-"} {d.Words?.ToString() ?? "-"}")));
    }
}
