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
    /// The same document indexed from a Windows checkout and a Linux one must be the same document.
    /// Line endings and a BOM are checkout artefacts, not content, and a body that carries them makes
    /// a repository's doctrine depend on which machine happened to read it.
    /// </summary>
    [Fact]
    public void A_CRLF_checkout_with_a_BOM_yields_the_same_body_as_an_LF_one()
    {
        WriteLock("rules/hygiene.md", "rules/plain.md");
        Write(".claude/rules/hygiene.md", "﻿line one\r\nline two\r\n");
        Write(".claude/rules/plain.md", "line one\nline two\n");

        var entries = new RepositoryScanner().Scan(_root);

        var crlf = entries.Single(e => e.Title == "hygiene");
        var lf = entries.Single(e => e.Title == "plain");
        Assert.Equal("line one\nline two", crlf.Body);
        Assert.Equal(lf.Body, crlf.Body);
        Assert.DoesNotContain('\r', crlf.Body);
        Assert.DoesNotContain('﻿', crlf.Body);
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

    /// <summary>
    /// 🔴 REV3: two sections under one heading — date-only fix headings do it — shared an id, and the
    /// SQLite store's primary key threw on the second, failing the WHOLE refresh for every repository
    /// sorted after this one. The in-memory store the tests used took the duplicate without a word. Every
    /// section keeps an id of its own; the second is told apart, the first keeps the id it always had.
    /// </summary>
    [Fact]
    public void Two_sections_under_one_heading_are_two_entries_with_two_ids()
    {
        Write("docs/FIX-LOG.md", "## 2026-09-25\n\nThe first fix.\n\n## 2026-09-25\n\nThe second fix.\n");

        var fixes = new RepositoryScanner().Scan(_root).Where(e => e.RelativePath == "docs/FIX-LOG.md").ToList();

        Assert.Equal(2, fixes.Count);
        Assert.Equal(2, fixes.Select(e => e.Id).Distinct().Count());
        Assert.EndsWith("#2026-09-25", fixes[0].Id);
        Assert.All(fixes, e => Assert.Equal("2026-09-25", e.Title));
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

    // ── the region (CANON8e / D59) ────────────────────────────────────────────────────────────────
    //
    // 🔴 Measured after the migration, not imagined: `.claude/rules/` is empty now, so the eight
    // canonical rules fell straight out of the index and a search for one returned only a
    // repository's OWN local rule. Cross-repository search and convergence are what the service
    // exists for, and they had quietly lost the always-loaded tier — with every gate green, because
    // none asserted that a canonical RULE was searchable.
    //
    // The twin contract is what failed: the CLI moved a tier, and this artefact reads the same layout
    // from another language where nothing breaks at compile time.

    /// <summary>A repository on the new layout: the rules in a region, the lock saying where.</summary>
    private void WriteRegion(params (string Name, string Body)[] rules)
    {
        Write("daoris.json", """{"source":"s","packs":[],"target":".claude"}""");
        var entries = rules.Select(r => new
        {
            pack = "core",
            source = $"core/rules/{r.Name}.md",
            target = $"rules/{r.Name}.md",
            sha256 = "x",
            @in = "AGENTS.md",
        });
        Write("daoris.lock", JsonSerializer.Serialize(new { version = 1, entries }));

        var body = string.Join("\n", rules.Select(r =>
            $"\n<!-- daoris: core/core/rules/{r.Name}.md @ 0.0.1 — canonical; edit via `daoris upstream` -->\n\n{r.Body}"));
        Write("AGENTS.md",
            $"# Ours\n\nOur own doctrine.\n\n<!-- daoris:rules — generated; edit the canon, not this -->\n"
            + $"# Doctrine\n{body}\n<!-- /daoris:rules -->\n");
    }

    [Fact]
    public void A_canonical_rule_in_the_region_is_indexed()
    {
        WriteRegion(
            ("repository-owns-its-work", "# Never write into another repository\n\nPublish the request."),
            ("sensitive-info", "# Sensitive info\n\nNo machine paths."));

        var entries = new RepositoryScanner().Scan(_root);

        var rule = Single(entries, "repository-owns-its-work");
        Assert.Equal(EntryKind.Rule, rule.Kind);
        Assert.Equal(Provenance.Canonical, rule.Provenance);
        Assert.Contains("Never write into another repository", rule.Body);
        // Each rule is its OWN entry, not one blob: a search that returned the whole tier for a
        // question about one rule buries the answer in every other rule, which is the same reason
        // the decision log is split at its headings.
        Assert.Contains("No machine paths", Single(entries, "sensitive-info").Body);
        Assert.DoesNotContain("No machine paths", rule.Body);
    }

    /// <summary>
    /// The adopter's own text around the region is theirs, and is not doctrine this service indexes
    /// as a rule. Their file, their words — only the span daoris owns is canonical.
    /// </summary>
    [Fact]
    public void The_text_around_the_region_is_not_swept_in()
    {
        WriteRegion(("sensitive-info", "# Sensitive info\n\nNo machine paths."));

        var entries = new RepositoryScanner().Scan(_root);

        Assert.DoesNotContain(entries, e => e.Body.Contains("Our own doctrine"));
    }

    /// <summary>A repository still on the old layout keeps working — nothing about files changed.</summary>
    [Fact]
    public void A_rule_that_is_still_a_file_is_indexed_as_before()
    {
        WriteLock("rules/sensitive-info.md");
        Write(".claude/rules/sensitive-info.md", "# Sensitive info\n\nNo machine paths.");

        var rule = Single(new RepositoryScanner().Scan(_root), "sensitive-info");

        Assert.Equal(Provenance.Canonical, rule.Provenance);
    }

    /// <summary>
    /// LAYOUT4: a repository on the older layout indexes exactly as before the service learned the
    /// agents layout — the same entries, in the same order, with the same provenance, read at the
    /// manifest's root alone. Written against the scanner before LAYOUT4 and run against both.
    /// </summary>
    [Fact]
    public void A_repository_on_the_older_layout_indexes_exactly_as_before()
    {
        WriteRegion(("sensitive-info", "# Sensitive info\n\nNo machine paths."));
        var withFiles = File.ReadAllText(Path.Combine(_root, "daoris.lock")).Replace(
            "]}", """,{"pack":"core","source":"core/knowledge/storage.md","target":"knowledge/storage.md","sha256":"x"},{"pack":"core","source":"core/skills/finder/SKILL.md","target":"skills/finder/SKILL.md","sha256":"x"}]}""");
        Write("daoris.lock", withFiles);
        Write(".claude/rules/house.md", "# House\n\nOur own rule.");
        Write(".claude/knowledge/storage.md", "# Storage\n\nCanonical.");
        Write(".claude/knowledge/ours.md", "# Ours\n\nLocal.");
        Write(".claude/skills/finder/SKILL.md", "---\nname: finder\n---\n\nSteps.");
        Write(".claude/skills/house/SKILL.md", "---\nname: house\n---\n\nOur steps.");
        Write("docs/DECISIONS.md", "## D1 — one\n\nA.\n");
        // Nothing on the older layout names these, and before LAYOUT4 nothing read them.
        Write(".agents/knowledge/elsewhere.md", "# Elsewhere\n\nNot this layout's.");
        Write("pkg/AGENTS.md", "# A package\n\nUndeclared.");

        var entries = new RepositoryScanner().Scan(_root)
            .Select(e => $"{e.Kind} {e.Provenance} {e.Title} {e.RelativePath}")
            .ToList();

        Assert.Equal(
            [
                "Rule Local house .claude/rules/house.md",
                "Rule Canonical sensitive-info AGENTS.md",
                "Knowledge Local ours .claude/knowledge/ours.md",
                "Knowledge Canonical storage .claude/knowledge/storage.md",
                "Skill Canonical finder .claude/skills/finder/SKILL.md",
                "Skill Local house .claude/skills/house/SKILL.md",
                "Decision Local D1 — one docs/DECISIONS.md",
            ],
            entries);
    }

    private static KnowledgeEntry Single(IReadOnlyList<KnowledgeEntry> entries, string title) =>
        entries.Single(e => e.Title == title);
}
