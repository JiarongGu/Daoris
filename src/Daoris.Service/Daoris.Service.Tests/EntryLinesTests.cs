using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Every entry the scanner reads as a run of its file's lines keeps them (ORIENT2e; the orientation design §3.2):
/// a hit names <c>path:first-last</c>, and a session reads that range rather than the file. ORIENT1 found decision
/// files read whole 21 times because a search named the file and not the note.
/// </summary>
/// <remarks>
/// The lines are the body's: line <c>i</c> of the body is line <c>first + i</c> of the file, so a heading the body
/// leaves out is outside them, and an entry whose body is not its file's lines as written has none.
/// </remarks>
public sealed class EntryLinesTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-lines-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static List<string> Of(IEnumerable<KnowledgeEntry> entries, EntryKind kind) =>
        entries.Where(e => e.Kind == kind).Select(e => $"{e.Title} @ {e.RelativePath}:{e.Lines?.ToString() ?? "-"}").ToList();

    /// <summary>Each body is its file's lines as written, from its first line to its last.</summary>
    private void AssertVerbatim(IEnumerable<KnowledgeEntry> entries)
    {
        foreach (var entry in entries.Where(e => e.Lines is not null))
        {
            var file = File.ReadAllText(Path.Combine(_root, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
                .Replace("\r\n", "\n").Split('\n');
            var span = entry.Lines!.Value;
            Assert.Equal(
                string.Join('\n', file[(span.First - 1)..span.Last]).Trim(),
                entry.Body);
        }
    }

    [Fact]
    public void A_log_s_sections_keep_the_lines_of_their_bodies()
    {
        Write("docs/DECISIONS.md",
            "# Decisions\n\n## D1 — one\n\nBecause.\nIt held.\n\n## D2 — two\n\nAnd because.\n");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Equal(["D1 — one @ docs/DECISIONS.md:5-6", "D2 — two @ docs/DECISIONS.md:10"], Of(entries, EntryKind.Decision));
        AssertVerbatim(entries);
    }

    /// <summary>A decision in a folder is its own text, heading first, then each dated note with the note's lines.</summary>
    [Fact]
    public void A_decision_and_each_of_its_notes_keep_their_lines()
    {
        Write("daoris.json", """{"source":"s","packs":[],"documents":{"decisions":"docs/decisions"}}""");
        Write("docs/decisions/D7.md",
            "## D7 — A choice (2026-10-01)\n\n**Decision.** We chose it.\n\n**AB1, built 2026-10-02: it landed.** The build.\nIt works.\n");
        Write("docs/decisions/D8.md", "## D8 — Another (2026-10-03)\n\n**Decision.** No notes.\n");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Equal(
            [
                "D7 — A choice (2026-10-01) @ docs/decisions/D7.md:1-3",
                "D7 › AB1, built 2026-10-02: it landed. @ docs/decisions/D7.md:5-6",
                "D8 — Another (2026-10-03) @ docs/decisions/D8.md:1-3",
            ],
            Of(entries, EntryKind.Decision));
        AssertVerbatim(entries);
    }

    [Fact]
    public void A_whole_document_keeps_every_line_its_frontmatter_s_too()
    {
        Write(".claude/knowledge/note.md", "---\nname: note\n---\n\n# Note\n\nBody.\n");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Equal(["note @ .claude/knowledge/note.md:1-7"], Of(entries, EntryKind.Knowledge));
        AssertVerbatim(entries);
    }

    /// <summary>A README, until the repository adopts: its opening and each section, by their lines.</summary>
    [Fact]
    public void A_readme_s_opening_and_sections_keep_their_lines()
    {
        Write("README.md", "\n# Atlas\n\nWhat it is.\n\n## Build\n\nRun it.\n");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Equal(["README @ README.md:2-4", "Build @ README.md:8"], Of(entries, EntryKind.Knowledge));
        AssertVerbatim(entries);
    }

    /// <summary>A deployment's documents folder (ORIENT1c): the opening and each section, by their lines.</summary>
    [Fact]
    public void A_deployment_s_documents_keep_their_lines()
    {
        Write("daoris.json", """{"source":"s","packs":[]}""");
        Write("docs/design.md", "# The probe lock design\n\nWhy the probe holds a lock.\n\n## The lock\n\nOne probe at a time.\n");

        var entries = new RepositoryScanner(documents: "docs").Scan(_root);

        Assert.Equal(
            ["The probe lock design @ docs/design.md:1-3", "The lock @ docs/design.md:7"],
            Of(entries, EntryKind.Knowledge));
        AssertVerbatim(entries);
    }

    /// <summary>
    /// A rule out of the region keeps the lines between its provenance line and the next: its markers are the
    /// boundary, and the adopter's own text around the region is never among them.
    /// </summary>
    [Fact]
    public void A_rule_in_the_region_keeps_its_lines()
    {
        Write("daoris.json", """{"source":"s","packs":[],"target":".claude"}""");
        Write("daoris.lock", JsonSerializer.Serialize(new
        {
            version = 1,
            entries = new[]
            {
                new { pack = "core", source = "core/rules/sensitive-info.md", target = "rules/sensitive-info.md", sha256 = "x", @in = "AGENTS.md" },
            },
        }));
        Write("AGENTS.md",
            "# Ours\n\n<!-- daoris:rules — generated; edit the canon, not this -->\n# Doctrine\n\n"
            + "<!-- daoris: core/core/rules/sensitive-info.md @ 0.0.1 — canonical; edit via `daoris upstream` -->\n\n"
            + "# Sensitive info\n\nNo machine paths.\n<!-- /daoris:rules -->\n");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Equal(["sensitive-info @ AGENTS.md:8-10"], Of(entries, EntryKind.Rule));
        AssertVerbatim(entries);
    }

    /// <summary>
    /// A deployment's index row is its cells labelled by their columns (ORIENT1c), which is not its file's line as
    /// written, so it names no lines: absent, never a line whose text a range would misreport.
    /// </summary>
    [Fact]
    public void A_deployment_s_index_row_is_not_its_file_s_lines_and_names_none()
    {
        Write("docs/index/routes.md", "# Routes\n\n| Route | Handler |\n|---|---|\n| `STATE` | `Module.cs:38` |\n");

        var entries = new RepositoryScanner(index: "docs/index").Scan(_root);

        Assert.Contains(entries, e => e.Title == "STATE");
        Assert.All(entries.Where(e => e.RelativePath == "docs/index/routes.md"), e => Assert.Null(e.Lines));
    }
}
