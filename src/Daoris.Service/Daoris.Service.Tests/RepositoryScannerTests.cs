using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public sealed class RepositoryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-scanner-" + Guid.NewGuid().ToString("N")[..8]);

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private void WriteLock(params string[] targets)
    {
        Write("daoris.json", """{"source":"s","packs":[],"target":".claude"}""");
        var entries = targets.Select(t => new { pack = "core", target = t, sha256 = "x" });
        Write("daoris.lock", JsonSerializer.Serialize(new { version = 1, entries }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// The distinction the whole index rests on. Canonical content is identical in every adopting
    /// repository, so indexing it per repository would produce a dozen copies of one rule and call
    /// that a corpus. What varies — and therefore what is worth searching across repositories — is
    /// the local material.
    /// </summary>
    [Fact]
    public void Classifies_provenance_from_the_lock()
    {
        Write(".claude/rules/sensitive-info.md", "# Canonical rule\n\nBody.");
        Write(".claude/rules/repo-mechanics.md", "# Our own rule\n\nBody.");
        WriteLock("rules/sensitive-info.md");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Equal(Provenance.Canonical, Single(entries, "sensitive-info").Provenance);
        Assert.Equal(Provenance.Local, Single(entries, "repo-mechanics").Provenance);
    }

    [Fact]
    public void A_repository_that_never_adopted_daoris_is_entirely_local()
    {
        Write(".claude/rules/house-style.md", "# House style\n\nBody.");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.All(entries, e => Assert.Equal(Provenance.Local, e.Provenance));
    }

    [Fact]
    public void Reads_skills_by_directory_name()
    {
        Write(".claude/skills/doc-loader/SKILL.md", "---\nname: doc-loader\n---\n\nSteps.");
        Write(".claude/skills/doc-loader/reference.md", "Supporting detail, not separately indexed.");

        var entries = new RepositoryScanner().Scan(_root);

        var skill = Assert.Single(entries, e => e.Kind == EntryKind.Skill);
        Assert.Equal("doc-loader", skill.Title);
        Assert.Equal(".claude/skills/doc-loader/SKILL.md", skill.RelativePath);
    }

    /// <summary>
    /// A decisions log is one file and many decisions. Returning the file for a query about one of
    /// them buries the answer in every other decision ever made.
    /// </summary>
    [Fact]
    public void Splits_logs_into_one_entry_per_section()
    {
        Write("docs/DECISIONS.md", """
            # Decisions

            Preamble that belongs to no entry.

            ## D1 — the tier is the directory

            Because the harness decides by path.

            ## D2 — drift is measured against the lock

            Because otherwise an improved rule cannot propagate.
            """);

        var entries = new RepositoryScanner().Scan(_root);
        var decisions = entries.Where(e => e.Kind == EntryKind.Decision).ToList();

        Assert.Equal(2, decisions.Count);
        Assert.Contains(decisions, d => d.Title == "D1 — the tier is the directory");
        Assert.Contains(decisions, d => d.Body.Contains("cannot propagate"));
        Assert.DoesNotContain(decisions, d => d.Body.Contains("Preamble"));
    }

    [Fact]
    public void Entry_ids_are_stable_and_distinguish_sections_of_one_file()
    {
        Write("docs/DECISIONS.md", "## D1 — one\n\nA.\n\n## D2 — two\n\nB.\n");

        var ids = new RepositoryScanner().Scan(_root)
            .Where(e => e.Kind == EntryKind.Decision)
            .Select(e => e.Id)
            .ToList();

        Assert.Equal(2, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Contains("docs/DECISIONS.md#", id));
    }

    [Fact]
    public void The_generated_index_is_not_indexed()
    {
        Write(".claude/rules/RULES_INDEX.md", "# RULES_INDEX\n\nA table of contents.");
        Write(".claude/rules/real-rule.md", "# Real\n\nBody.");

        var entries = new RepositoryScanner().Scan(_root);

        Assert.Single(entries);
        Assert.Equal("real-rule", entries[0].Title);
    }

    [Fact]
    public void A_missing_repository_yields_nothing_rather_than_throwing()
    {
        Assert.Empty(new RepositoryScanner().Scan(Path.Combine(_root, "does-not-exist")));
    }

    private static KnowledgeEntry Single(IReadOnlyList<KnowledgeEntry> entries, string title) =>
        entries.Single(e => e.Title == title);
}
