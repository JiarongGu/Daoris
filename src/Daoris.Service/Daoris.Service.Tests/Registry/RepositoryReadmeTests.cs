using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Until a repository adopts, the service indexes its README as the repository's own word (WSSETUP8;
/// D124, `docs/2026-10-01-workspace-setup-design.md` §5): the root's <c>README.md</c>, in any case, split
/// at its headings as a log is, each section a local knowledge entry labelled by its path, titled
/// <c>README</c> for the part before the first heading and by its heading after. Never through a link or a
/// link held as text, never <c>docs/README.md</c>, no other format, and not once a lock exists.
/// </summary>
/// <remarks>
/// Not a twin of the driver's <c>SelfDescription</c>, which reads the same file for the intake: a title
/// and a paragraph there, every section for search here (design §12). Neither is held to the other.
/// </remarks>
public sealed class RepositoryReadmeTests : IDisposable
{
    // One folder above the repository, so a link can point somewhere this test owns.
    private readonly string _parent = Path.Combine(
        Path.GetTempPath(), "daoris-readme-" + Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_parent, "repo");

    private const string Readme = """
        # Report engine

        Computes the monthly figures every other repository reads.

        ## Figures

        Revenue is booked on the invoice date, never the payment date.

        ## Running it

        One command, from the root.
        """;

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

    /// <summary>What `sync` leaves behind: a manifest and a lock claiming one canonical document.</summary>
    private void Adopt()
    {
        Write("daoris.json", """{"source":"s","packs":[],"target":".claude"}""");
        var entries = new[] { new { pack = "core", source = "core/knowledge/storage.md", target = "knowledge/storage.md", sha256 = "x" } };
        Write("daoris.lock", JsonSerializer.Serialize(new { version = 1, entries }));
        Write(".claude/knowledge/storage.md", "# Storage\n\nCanonical.\n");
    }

    private IReadOnlyList<KnowledgeEntry> Scan() => new RepositoryScanner().Scan(Root);

    private static List<string> Read(IEnumerable<KnowledgeEntry> entries) =>
        entries.Select(e => $"{e.Title} @ {e.RelativePath}{(e.Anchor is null ? "" : "#" + e.Anchor)}").ToList();

    // ── read, split, labelled ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The row's first proof. A repository with nothing else to index is found by its README: each
    /// section one entry, so a search for how a figure is computed returns that section and not the
    /// whole front page.
    /// </summary>
    [Fact]
    public void An_unadopted_repository_README_is_indexed_one_entry_per_section()
    {
        Write("README.md", Readme);

        var entries = Scan();

        Assert.Equal(
            ["README @ README.md", "Figures @ README.md#Figures", "Running it @ README.md#Running it"],
            Read(entries));
        Assert.Contains("Computes the monthly figures", entries[0].Body);
        Assert.Contains("invoice date", entries[1].Body);
        Assert.DoesNotContain("invoice date", entries[0].Body);
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct().Count());
    }

    /// <summary>
    /// It claims no role (§5, *why DOC5's objection does not reach it*): not a router, not a log, whatever
    /// its headings look like. Knowledge, the repository's own, carrying its path, one hit among others.
    /// </summary>
    [Fact]
    public void A_README_entry_is_local_knowledge_and_claims_no_role()
    {
        Write("README.md", "# Ledger\n\nThe books.\n\n## D1 — looks like a decision\n\nIt is a section.\n\n## 2026-10-01\n\nSo is this.\n");

        var entries = Scan();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(EntryKind.Knowledge, e.Kind));
        Assert.All(entries, e => Assert.Equal(Provenance.Local, e.Provenance));
        Assert.All(entries, e => Assert.Equal("README.md", e.RelativePath));
    }

    /// <summary>
    /// The part before the first heading is `README`; with none, there is no such entry, and a README with
    /// no heading the splitter reads is one entry, as a log's preamble would be were it kept.
    /// </summary>
    [Theory]
    [InlineData("no part before the first heading", "## Install\n\nRun it.\n", "Install @ README.md#Install")]
    [InlineData("no heading the splitter reads", "# Tool\n\nWhat it is.\n\n# Usage\n\nHow.\n", "README @ README.md")]
    [InlineData("a heading inside a fence is an example, not a section", "# Tool\n\n```\n## Not a section\n```\n", "README @ README.md")]
    [InlineData("a section with nothing under it is not an entry", "Intro.\n\n## Empty\n\n## Full\n\nText.\n", "README @ README.md|Full @ README.md#Full")]
    [InlineData("a heading twice is two entries with two ids", "## Notes\n\nOne.\n\n## Notes\n\nTwo.\n", "Notes @ README.md#Notes|Notes @ README.md#Notes (2)")]
    public void Each_section_is_titled_by_its_heading_and_the_part_before_the_first_as_README(
        string name, string readme, string expected)
    {
        Assert.NotEmpty(name);
        Write("README.md", readme);

        var entries = Scan();

        Assert.Equal(expected, string.Join('|', Read(entries)));
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct().Count());
    }

    /// <summary>`README.md` at the root in any case, labelled by the path the disk spells, so a session can open it.</summary>
    [Theory]
    [InlineData("README.md")]
    [InlineData("readme.md")]
    [InlineData("Readme.md")]
    [InlineData("ReadMe.MD")]
    public void The_README_is_read_in_any_case(string file)
    {
        Write(file, Readme);

        var entries = Scan();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(file, e.RelativePath));
        Assert.Equal("README", entries[0].Title);
    }

    // ── what is never read ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Not `docs/README.md` (DOC5: it may be a site's front page, and a router is declared, never guessed),
    /// and no other format, since only markdown has headings the splitter reads.
    /// </summary>
    [Theory]
    [InlineData("docs/README.md")]
    [InlineData("README.rst")]
    [InlineData("README.txt")]
    [InlineData("README")]
    [InlineData("README.markdown")]
    [InlineData("src/README.md")]
    public void Only_the_root_README_md_is_read(string file)
    {
        Write(file, Readme);

        Assert.Empty(Scan());
    }

    /// <summary>A README beside a `docs/README.md` is read, and the one under `docs/` is still not.</summary>
    [Fact]
    public void The_root_README_is_read_and_docs_README_beside_it_is_not()
    {
        Write("README.md", Readme);
        Write("docs/README.md", "# The documents\n\n## Contracts\n\nA table.\n");

        var entries = Scan();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal("README.md", e.RelativePath));
    }

    /// <summary>
    /// A link held as text (D117 §5.5): on a checkout without links a README that was one is a small file
    /// holding its target. Read, it would put a path in a search where the repository's word should be.
    /// </summary>
    [Theory]
    [InlineData("docs/README.md")]
    [InlineData("../elsewhere/README.md")]
    [InlineData("./docs/README.md")]
    public void A_README_that_is_a_link_held_as_text_is_not_read(string held)
    {
        Write("docs/README.md", Readme);
        Write("README.md", held);

        Assert.Empty(Scan());
    }

    /// <summary>A README that is a real link is some other folder's word, and is never followed.</summary>
    [Fact]
    public void A_README_that_is_a_link_is_never_followed()
    {
        var elsewhere = Path.Combine(_parent, "elsewhere");
        Write("README.md", Readme, under: elsewhere);
        Directory.CreateDirectory(Root);

        try
        {
            File.CreateSymbolicLink(Path.Combine(Root, "README.md"), Path.Combine(elsewhere, "README.md"));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            return; // this machine makes no links (xunit v2 has no dynamic skip); the held-as-text rows hold the rule
        }

        Assert.Empty(Scan());
    }

    // ── until it adopts ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The row's last proof. Once a lock exists the repository has declared its documents (DOC3), and its
    /// README is its front page again: the next scan, and so the next refresh, no longer reads it.
    /// </summary>
    [Fact]
    public void A_repository_that_adopts_stops_having_its_README_read()
    {
        Write("README.md", Readme);
        Assert.Equal(3, Scan().Count(e => e.RelativePath == "README.md"));

        Adopt();
        var entries = Scan();

        Assert.DoesNotContain(entries, e => e.RelativePath == "README.md");
        Assert.Equal(["storage @ .claude/knowledge/storage.md"], Read(entries));
    }

    /// <summary>
    /// The scanner's own test for reading both roots (§5): no lock, or one it reads as none. A manifest
    /// alone is a repository mid-adoption, which has declared nothing yet; a lock the CLI would refuse is
    /// read as none everywhere else in the scanner, and so here.
    /// </summary>
    [Theory]
    [InlineData("a manifest and no lock", """{"source":"s","packs":[]}""", null)]
    [InlineData("a lock that is not JSON", null, """{"version":1,"entries":[""")]
    [InlineData("a lock with no entries", null, """{"version":1}""")]
    [InlineData("a lock whose target leaves the repository", null, """{"version":1,"harness":"agents","target":"../outside","entries":[]}""")]
    public void A_repository_with_no_lock_the_scanner_reads_has_its_README_read(string name, string? manifest, string? lockText)
    {
        Assert.NotEmpty(name);
        if (manifest is not null) Write("daoris.json", manifest);
        if (lockText is not null) Write("daoris.lock", lockText);
        Write("README.md", Readme);

        Assert.Equal(3, Scan().Count(e => e.RelativePath == "README.md"));
    }

    // ── one file, one place ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One file is one place in the index (DOC5, REV3). A README a declaration already names is read by
    /// that declaration's reader, whole as a router or split as a log, and never again here: read twice, the
    /// router's entry and the part before the first heading share an id, and the store's primary key fails
    /// the whole refresh on the second. Declared wins over the baseline.
    /// </summary>
    [Theory]
    [InlineData("router", "README @ README.md", EntryKind.Knowledge)]
    [InlineData("decisions", "Figures @ README.md#Figures|Running it @ README.md#Running it", EntryKind.Decision)]
    public void A_README_a_declaration_already_reads_is_read_once(string role, string expected, EntryKind kind)
    {
        Write("daoris.json", $$$"""{"source":"s","packs":[],"documents":{"{{{role}}}":"README.md"}}""");
        Write("README.md", Readme);

        var entries = Scan();

        Assert.Equal(expected, string.Join('|', Read(entries)));
        Assert.All(entries, e => Assert.Equal(kind, e.Kind));
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct().Count());
    }

    /// <summary>The README comes after what the repository keeps itself: its tiers and logs are read as before.</summary>
    [Fact]
    public void The_README_is_read_beside_the_rest_and_after_it()
    {
        Write(".agents/knowledge/kept.md", "Kept here.\n");
        Write("docs/DECISIONS.md", "## D1 — one\n\nA.\n");
        Write("README.md", "# Tool\n\nWhat it is.\n");

        Assert.Equal(
            ["kept @ .agents/knowledge/kept.md", "D1 — one @ docs/DECISIONS.md#D1 — one", "README @ README.md"],
            Read(Scan()));
    }
}
