using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The service reads the layout the CLI writes (LAYOUT4; D117, `docs/2026-10-01-agent-layout-design.md`
/// §5.5): the lock's root before the manifest's, mirrors skipped, rooms indexed, both roots for a
/// repository with no lock, and a link or a link held as text skipped, never followed.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *the layout*): the CLI's <c>lockLayout</c>
/// (<c>src/Daoris.Cli/src/layout.ts</c>) answers where a lock's files are, and its <c>heldAsText</c>
/// (<c>links.ts</c>) what a link checked out as text is. The two sides share no code, so each carries
/// the table: <see cref="The_layout_a_lock_was_written_under"/> holds the CLI's
/// <c>layout-descriptor.test.ts</c> rows, and <see cref="What_a_link_held_as_text_is"/> its
/// <c>layout-links.test.ts</c> cases, in their order, before this side's own edges.</para>
///
/// <para>Where the answers differ, they differ because one side writes and the other reads, and the
/// row says so: the CLI refuses a lock or a room that leaves the repository, which the service reads as
/// no lock and no room — never outside the repository, and never costing the whole corpus.</para>
/// </remarks>
public sealed class RepositoryLayoutTests : IDisposable
{
    // One folder above the repository, so a path that climbs out of it lands somewhere this test owns.
    private readonly string _parent = Path.Combine(
        Path.GetTempPath(), "daoris-layout-" + Guid.NewGuid().ToString("N")[..8]);

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

    private IReadOnlyList<KnowledgeEntry> Scan() => new RepositoryScanner().Scan(Root);

    private static string Skill(string name) => $"---\nname: {name}\ndescription: Use when {name} is wanted.\n---\n\nSteps of {name}.\n";

    /// <summary>The manifest and lock an `agents` sync writes: a canonical knowledge document and skill, and the mirrors.</summary>
    private void AgentsRepository(string manifest = """{"source":"s","packs":[],"harness":"agents","target":".agents"}""")
    {
        Write("daoris.json", manifest);
        Write("daoris.lock", """
            {
              "version": 1,
              "canonVersion": "0.1.0",
              "source": "s",
              "entries": [
                { "pack": "core", "source": "core/knowledge/storage.md", "target": "knowledge/storage.md", "sha256": "x" },
                { "pack": "core", "source": "core/skills/finder/SKILL.md", "target": "skills/finder/SKILL.md", "sha256": "x" },
                { "pack": "core", "source": "core/skills/finder/run.sh", "target": "skills/finder/run.sh", "sha256": "x" }
              ],
              "harness": "agents",
              "target": ".agents",
              "mirrors": [
                { "path": ".claude/skills/finder/run.sh", "of": ".agents/skills/finder/run.sh", "sha256": "x" },
                { "path": ".claude/skills/finder/SKILL.md", "of": ".agents/skills/finder/SKILL.md", "sha256": "x" },
                { "path": ".claude/skills/house/SKILL.md", "of": ".agents/skills/house/SKILL.md", "sha256": "x" }
              ]
            }
            """);
        Write(".agents/knowledge/storage.md", "---\nname: storage\n---\n\nWhere the data lives.\n");
        Write(".agents/skills/finder/SKILL.md", Skill("finder"));
        Write(".agents/skills/finder/run.sh", "#!/bin/sh\necho finder\n");
        Write(".agents/skills/house/SKILL.md", Skill("house"));
        const string header = "<!-- daoris: mirror of .agents/skills/{0}/SKILL.md for agents that read only .claude/skills — edit that file, not this; daoris sync rewrites this one -->";
        Write(".claude/skills/finder/SKILL.md", Skill("finder").Replace("---\n\n", "---\n" + string.Format(header, "finder") + "\n\n"));
        Write(".claude/skills/finder/run.sh", "#!/bin/sh\necho finder\n");
        Write(".claude/skills/house/SKILL.md", Skill("house").Replace("---\n\n", "---\n" + string.Format(header, "house") + "\n\n"));
    }

    // ── the root: the lock's, then the manifest's, then .claude ─────────────────────────────────────

    /// <summary>
    /// 🔴 The headline of §5.5: the mirror is a copy of its source, so indexing it would put every
    /// skill in a search twice per repository. The lock says which files are mirrors.
    /// </summary>
    [Fact]
    public void A_skill_with_a_mirror_is_found_once_at_its_source()
    {
        AgentsRepository();

        var skills = Scan().Where(e => e.Kind == EntryKind.Skill).OrderBy(e => e.Title, StringComparer.Ordinal).ToList();

        Assert.Equal(["finder", "house"], skills.Select(s => s.Title));
        Assert.Equal(".agents/skills/finder/SKILL.md", skills[0].RelativePath);
        Assert.Equal(Provenance.Canonical, skills[0].Provenance);
        // The repository's own skill is mirrored too, and is still its own at the source.
        Assert.Equal(".agents/skills/house/SKILL.md", skills[1].RelativePath);
        Assert.Equal(Provenance.Local, skills[1].Provenance);
    }

    [Fact]
    public void The_agents_layout_reads_its_knowledge_at_the_lock_root_as_canonical()
    {
        AgentsRepository();

        var storage = Assert.Single(Scan(), e => e.Title == "storage");

        Assert.Equal(EntryKind.Knowledge, storage.Kind);
        Assert.Equal(Provenance.Canonical, storage.Provenance);
        Assert.Equal(".agents/knowledge/storage.md", storage.RelativePath);
    }

    /// <summary>
    /// §2.3: `.claude/skills/` holds Daoris's mirrors beside a skill the repository keeps for Claude
    /// Code alone, and the lock tells them apart. The second is the repository's own, and indexed.
    /// </summary>
    [Fact]
    public void A_skill_the_repository_keeps_for_one_agent_beside_the_mirrors_is_its_own()
    {
        AgentsRepository();
        Write(".claude/skills/solo/SKILL.md", Skill("solo"));

        var solo = Assert.Single(Scan(), e => e.Title == "solo");

        Assert.Equal(Provenance.Local, solo.Provenance);
        Assert.Equal(".claude/skills/solo/SKILL.md", solo.RelativePath);
    }

    /// <summary>
    /// 🔴 §5.1: the lock, not the manifest, says where the files are. Between a manifest's flip and the
    /// sync that moves them, the manifest names the new root and the files are still at the old.
    /// </summary>
    [Fact]
    public void The_lock_root_comes_before_the_manifest()
    {
        Write("daoris.json", """{"source":"s","packs":[],"harness":"agents","target":".agents"}""");
        Write("daoris.lock", """{"version":1,"entries":[{"pack":"core","source":"core/knowledge/storage.md","target":"knowledge/storage.md","sha256":"x"}]}""");
        Write(".claude/knowledge/storage.md", "Where the data lives.\n");
        Write(".agents/knowledge/stray.md", "Not where the lock says anything is.\n");

        var entries = Scan();

        var storage = Assert.Single(entries, e => e.Title == "storage");
        Assert.Equal(".claude/knowledge/storage.md", storage.RelativePath);
        Assert.Equal(Provenance.Canonical, storage.Provenance);
        Assert.DoesNotContain(entries, e => e.Title == "stray");
    }

    [Fact]
    public void A_lock_on_the_agents_layout_is_read_there_whatever_the_manifest_says()
    {
        AgentsRepository(manifest: """{"source":"s","packs":[]}""");

        var storage = Assert.Single(Scan(), e => e.Title == "storage");

        Assert.Equal(".agents/knowledge/storage.md", storage.RelativePath);
        Assert.Equal(Provenance.Canonical, storage.Provenance);
    }

    /// <summary>
    /// §5.5: an unadopted repository in the reference's shape keeps its skills in `.agents/`, and one
    /// in the older shape in `.claude/`. With no lock, nothing says which, so both are read.
    /// </summary>
    [Fact]
    public void A_repository_with_no_lock_is_read_at_both_roots()
    {
        Write(".agents/skills/alpha/SKILL.md", Skill("alpha"));
        Write(".claude/skills/beta/SKILL.md", Skill("beta"));
        Write(".agents/knowledge/kept.md", "Kept here.\n");
        Write(".claude/knowledge/also.md", "And here.\n");

        var entries = Scan();

        Assert.Equal(
            [".agents/knowledge/kept.md", ".agents/skills/alpha/SKILL.md", ".claude/knowledge/also.md", ".claude/skills/beta/SKILL.md"],
            entries.Select(e => e.RelativePath).Order(StringComparer.Ordinal));
        Assert.All(entries, e => Assert.Equal(Provenance.Local, e.Provenance));
    }

    /// <summary>
    /// The reference on the owner's checkout (§0.2): `core.symlinks=false` turns `.claude/skills` into
    /// a file holding `../.agents/skills` and `CLAUDE.md` into nine bytes. Each skill is read once, at
    /// its source, and the files that were links are not documents.
    /// </summary>
    [Fact]
    public void The_reference_layout_on_a_checkout_without_links_is_read_once()
    {
        Write(".claude/skills", "../.agents/skills");
        Write("CLAUDE.md", "AGENTS.md");
        Write("AGENTS.md", "# The brief\n\nOurs.\n");
        Write(".agents/skills/probe/SKILL.md", Skill("probe"));

        var entries = Scan();

        var skill = Assert.Single(entries);
        Assert.Equal(".agents/skills/probe/SKILL.md", skill.RelativePath);
    }

    /// <summary>
    /// §5.5: a link held as text is skipped as a document. Its whole content is a path, so indexing it
    /// would put a path in a search where a document should be.
    /// </summary>
    [Fact]
    public void A_link_held_as_text_is_skipped_as_a_document()
    {
        Write(".agents/knowledge/pointer.md", "../../docs/guide.md");
        Write(".agents/knowledge/beside.md", "real.md");
        Write(".agents/knowledge/real.md", "# Real\n\nA document.\n");
        Write(".agents/knowledge/todo.md", "TODO");
        Write(".agents/skills/linked/SKILL.md", "../../../.claude/skills/linked/SKILL.md");

        var titles = Scan().Select(e => e.Title).Order(StringComparer.Ordinal);

        // One word that names nothing beside it is prose, however short (the CLI's rule).
        Assert.Equal(["real", "todo"], titles);
    }

    /// <summary>A real link is never followed: what it points at is some other folder's, maybe another repository's.</summary>
    [Fact]
    public void A_real_link_is_never_followed()
    {
        var elsewhere = Path.Combine(_parent, "elsewhere");
        Write("skills/far/SKILL.md", Skill("far"), under: elsewhere);
        Write("knowledge/far.md", "Somebody else's.\n", under: elsewhere);
        Write(".agents/skills/near/SKILL.md", Skill("near"));
        Directory.CreateDirectory(Path.Combine(Root, ".agents", "knowledge"));
        Directory.CreateDirectory(Path.Combine(Root, ".claude"));

        try
        {
            // A folder link at a tier, a skill's folder, and a document.
            Directory.CreateSymbolicLink(Path.Combine(Root, ".claude", "skills"), Path.Combine(elsewhere, "skills"));
            Directory.CreateSymbolicLink(Path.Combine(Root, ".agents", "skills", "far"), Path.Combine(elsewhere, "skills", "far"));
            File.CreateSymbolicLink(Path.Combine(Root, ".agents", "knowledge", "far.md"), Path.Combine(elsewhere, "knowledge", "far.md"));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            return; // this machine makes no links (xunit v2 has no dynamic skip); the held-as-text cells hold the rule
        }

        var entries = Scan();

        var only = Assert.Single(entries);
        Assert.Equal(".agents/skills/near/SKILL.md", only.RelativePath);
    }

    /// <summary>
    /// The logs and the region's file are read the same way: a decisions log or an `AGENTS.md` that is a
    /// link is some other folder's, and never followed.
    /// </summary>
    [Fact]
    public void A_log_or_a_region_file_that_is_a_link_is_not_read()
    {
        var elsewhere = Path.Combine(_parent, "elsewhere");
        Write("DECISIONS.md", "## D1 — somebody else's\n\nNot ours.\n", under: elsewhere);
        Write("AGENTS.md", "<!-- daoris:rules -->\n<!-- daoris: core/core/rules/far.md @ 0.0.1 -->\n\n# Far\n\nNot ours.\n<!-- /daoris:rules -->\n", under: elsewhere);
        Write("daoris.lock", """{"version":1,"entries":[{"pack":"core","source":"core/rules/far.md","target":"rules/far.md","sha256":"x","in":"AGENTS.md"}]}""");
        Directory.CreateDirectory(Path.Combine(Root, "docs"));

        try
        {
            File.CreateSymbolicLink(Path.Combine(Root, "docs", "DECISIONS.md"), Path.Combine(elsewhere, "DECISIONS.md"));
            File.CreateSymbolicLink(Path.Combine(Root, "AGENTS.md"), Path.Combine(elsewhere, "AGENTS.md"));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            return; // this machine makes no links (xunit v2 has no dynamic skip)
        }

        Assert.Empty(Scan());
    }

    /// <summary>D18 for the region: the file a lock entry names is read inside the repository or not at all.</summary>
    [Fact]
    public void A_region_file_the_lock_names_outside_the_repository_is_not_read()
    {
        Write("AGENTS.md", "<!-- daoris:rules -->\n<!-- daoris: core/core/rules/far.md @ 0.0.1 -->\n\n# Far\n\nNot ours.\n<!-- /daoris:rules -->\n", under: Path.Combine(_parent, "outside"));
        Write("daoris.lock", """{"version":1,"entries":[{"pack":"core","source":"core/rules/far.md","target":"rules/far.md","sha256":"x","in":"../outside/AGENTS.md"}]}""");

        Assert.Empty(Scan());
    }

    /// <summary>
    /// D18, read side: the CLI refuses a lock whose target leaves the repository. The service reads it
    /// as no lock, everything local, and never reads outside the repository.
    /// </summary>
    [Fact]
    public void A_lock_whose_target_leaves_the_repository_is_read_as_no_lock()
    {
        Write("daoris.lock", """{"version":1,"harness":"agents","target":"../outside","entries":[{"pack":"core","target":"knowledge/own.md","sha256":"x"}]}""");
        Write("knowledge/secret.md", "Not this repository's.\n", under: Path.Combine(_parent, "outside"));
        Write(".claude/knowledge/own.md", "Ours.\n");

        var entries = Scan();

        var own = Assert.Single(entries);
        Assert.Equal(".claude/knowledge/own.md", own.RelativePath);
        Assert.Equal(Provenance.Local, own.Provenance);
    }

    // ── rooms ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// §5.5: each declared room's `AGENTS.md` is a folder's own reference, which is what the search
    /// exists to find. The repository's own text, always: Daoris never writes a room's instructions.
    /// Declared, never found: an undeclared folder's `AGENTS.md` is not a room.
    /// </summary>
    [Fact]
    public void Each_declared_room_is_indexed_as_the_repository_own_knowledge()
    {
        Write("daoris.json", """{"source":"s","packs":[],"harness":"agents","target":".agents","rooms":["src/app","docs\\guide/","empty"]}""");
        Write("src/app/AGENTS.md", "# The app\n\nRun its gate before landing.\n");
        Write("docs/guide/AGENTS.md", "# The guide\n\nKeep it short.\n");
        Write("tools/AGENTS.md", "# Undeclared\n\nNot a room.\n");
        Write("AGENTS.md", "# The brief\n\nThe root's own text.\n");

        var rooms = Scan().OrderBy(e => e.RelativePath, StringComparer.Ordinal).ToList();

        Assert.Equal(["docs/guide/AGENTS.md", "src/app/AGENTS.md"], rooms.Select(r => r.RelativePath));
        Assert.All(rooms, r => Assert.Equal(EntryKind.Knowledge, r.Kind));
        Assert.All(rooms, r => Assert.Equal(Provenance.Local, r.Provenance));
        // Named by its folder: every room's file is called AGENTS.md, so the folder is what tells them apart.
        Assert.Equal(["docs/guide", "src/app"], rooms.Select(r => r.Title));
        Assert.Contains("Run its gate", rooms[1].Body);
    }

    /// <summary>
    /// The CLI's table of refused rooms (`layout-descriptor.test.ts`), in its order: a room that leaves
    /// the repository, is its root, or sits in the target or the mirror root. The CLI refuses the
    /// manifest; the service reads that room as no room, and still reads the good one beside it.
    /// </summary>
    [Theory]
    [InlineData("../sibling")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData(".agents/skills")]
    [InlineData(".agents")]
    [InlineData(".claude/skills/x")]
    [InlineData("C:/elsewhere")]
    [InlineData("/abs")]
    public void A_room_the_CLI_refuses_is_never_read(string room)
    {
        Write("daoris.json", $$"""{"source":"s","packs":[],"harness":"agents","target":".agents","rooms":["pkg",{{System.Text.Json.JsonSerializer.Serialize(room)}}]}""");
        Write("pkg/AGENTS.md", "# A package\n\nIts own.\n");
        Write("sibling/AGENTS.md", "# Another repository\n\nNot ours.\n", under: _parent);
        Write("AGENTS.md", "# The brief\n\nThe root's own text.\n");
        Write(".agents/AGENTS.md", "# Inside the target\n");
        Write(".agents/skills/AGENTS.md", "# Inside a tier\n");
        Write(".claude/skills/x/AGENTS.md", "# Inside the mirror root\n");

        var entries = Scan();

        var only = Assert.Single(entries);
        Assert.Equal("pkg/AGENTS.md", only.RelativePath);
    }

    [Fact]
    public void A_room_whose_instructions_are_a_link_held_as_text_is_skipped()
    {
        Write("daoris.json", """{"source":"s","packs":[],"harness":"agents","target":".agents","rooms":["pkg","lib"]}""");
        Write("pkg/AGENTS.md", "CLAUDE.md");
        Write("lib/AGENTS.md", "# The library\n\nIts own.\n");

        var only = Assert.Single(Scan());

        Assert.Equal("lib/AGENTS.md", only.RelativePath);
    }

    /// <summary>
    /// The CLI accepts a room in the knowledge folder of the root a layout moved from, which the
    /// scanner also reads as a tier. One file is one entry.
    /// </summary>
    [Fact]
    public void A_room_whose_file_a_tier_already_read_is_one_entry()
    {
        AgentsRepository(manifest: """{"source":"s","packs":[],"harness":"agents","target":".agents","rooms":[".claude/knowledge"]}""");
        Write(".claude/knowledge/AGENTS.md", "# Left here\n\nOurs.\n");

        var found = Assert.Single(Scan(), e => e.RelativePath == ".claude/knowledge/AGENTS.md");

        Assert.Equal(EntryKind.Knowledge, found.Kind);
        Assert.Equal(Provenance.Local, found.Provenance);
    }

    /// <summary>A room is declared on either descriptor (the CLI's LAYOUT3 choice 2), so it is read on either.</summary>
    [Fact]
    public void A_room_declared_on_the_older_layout_is_indexed_too()
    {
        Write("daoris.json", """{"source":"s","packs":[],"rooms":["pkg"]}""");
        Write("daoris.lock", """{"version":1,"entries":[]}""");
        Write("pkg/AGENTS.md", "# A package\n\nIts own.\n");

        var only = Assert.Single(Scan());

        Assert.Equal("pkg/AGENTS.md", only.RelativePath);
    }

    // ── the twin tables ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The layout a lock was written under: the CLI's <c>lockLayout</c>, row for row, then the roots
    /// this side reads there. <c>null</c> for the lock means none is on disk; a <c>-</c> target means
    /// the service reads the lock as none.
    /// </summary>
    [Theory]
    // The CLI's rows (layout-descriptor.test.ts): absent fields mean claude-code under .claude, whatever the manifest says.
    [InlineData("""{"entries":[]}""", """{}""", ".claude", ".claude")]
    [InlineData("""{"entries":[]}""", """{"harness":"agents","target":".agents"}""", ".claude", ".claude")]
    // No lock: the CLI answers the manifest's own layout; the service reads it and both roots (§5.5).
    [InlineData(null, """{"harness":"agents","target":".agents"}""", "-", ".agents")]
    // A target that leaves the repository: the CLI refuses; the service reads the lock as none.
    [InlineData("""{"entries":[],"target":"../outside"}""", """{"harness":"agents"}""", "-", ".agents")]
    [InlineData("""{"entries":[],"harness":"agents","target":".agents"}""", """{"harness":"agents"}""", ".agents", ".agents,.claude")]
    // lockLayout's other branches: an older build wrote at the manifest's own target; a lock that names
    // its harness and no target is at that harness's default; one the CLI does not know is refused there.
    [InlineData("""{"entries":[]}""", """{"target":"docs/agent"}""", "docs/agent", "docs/agent")]
    [InlineData("""{"entries":[]}""", """{"target":"./.claude/"}""", ".claude", ".claude")]
    [InlineData("""{"entries":[],"harness":"agents"}""", """{}""", ".agents", ".agents,.claude")]
    [InlineData("""{"entries":[],"harness":"claude-code","target":"docs/x"}""", """{}""", "docs/x", "docs/x")]
    [InlineData("""{"entries":[],"harness":"unknown-harness"}""", """{}""", "-", ".claude")]
    // A manifest target the CLI would refuse is never a root.
    [InlineData("""{"entries":[]}""", """{"target":"../elsewhere"}""", ".claude", ".claude")]
    public void The_layout_a_lock_was_written_under(string? lockText, string manifest, string target, string firstRoots)
    {
        Write("daoris.json", manifest);
        if (lockText is not null) Write("daoris.lock", lockText);

        var daorisLock = DaorisLock.Read(Root);
        var layout = RepositoryLayout.Of(Root, daorisLock);

        Assert.Equal(target == "-" ? null : target, daorisLock.Target);
        if (target == "-")
        {
            // Read as no lock: the manifest's root first, then both roots, each once.
            Assert.StartsWith(firstRoots, string.Join(",", layout.Roots));
            Assert.Contains(".agents", layout.Roots);
            Assert.Contains(".claude", layout.Roots);
        }
        else
        {
            Assert.Equal(firstRoots, string.Join(",", layout.Roots));
        }
        Assert.Equal(layout.Roots.Count, layout.Roots.Distinct().Count());
    }

    [Fact]
    public void The_lock_mirrors_are_read_as_paths_in_the_repository()
    {
        AgentsRepository();

        var daorisLock = DaorisLock.Read(Root);

        Assert.True(daorisLock.IsMirror(".claude/skills/finder/SKILL.md"));
        Assert.True(daorisLock.IsMirror(".claude\\skills\\house\\SKILL.md"));
        Assert.False(daorisLock.IsMirror(".agents/skills/finder/SKILL.md"));
        Assert.False(DaorisLock.Empty.IsMirror(".claude/skills/finder/SKILL.md"));
    }

    /// <summary>
    /// What a link checked out as text is: the CLI's <c>heldAsText</c>, the cases of
    /// <c>layout-links.test.ts</c> first, in its order, then the rule's own edges.
    /// </summary>
    [Theory]
    // The CLI's cases.
    [InlineData("CLAUDE.md", "AGENTS.md", null, true)]
    [InlineData(".claude/skills", "../.agents/skills", null, true)]
    [InlineData("pkg/CLAUDE.md", "AGENTS.md\n", null, true)]
    [InlineData("CLAUDE.md", "@AGENTS.md\n", null, false)]
    [InlineData("AGENTS.md", "Hello.\n", null, false)]
    // The rule's edges: the partner either way, a relative path, a name of something beside it, a BOM.
    [InlineData("AGENTS.md", "CLAUDE.md", null, true)]
    [InlineData("pkg/AGENTS.md", "\uFEFFCLAUDE.md\r\n", null, true)]
    [InlineData(".agents/knowledge/x.md", "./y.md", null, true)]
    [InlineData(".agents/knowledge/x.md", "y.md", ".agents/knowledge/y.md", true)]
    [InlineData(".agents/knowledge/x.md", "y.md", null, false)]
    [InlineData(".agents/knowledge/x.md", "TODO", null, false)]
    // Not a token: nothing, prose, an import, a heading, markup, or longer than a path is.
    [InlineData(".agents/knowledge/x.md", "", null, false)]
    [InlineData(".agents/knowledge/x.md", "../y z", null, false)]
    [InlineData(".agents/knowledge/x.md", "# ../y.md", null, false)]
    [InlineData(".agents/knowledge/x.md", "-../y.md", null, false)]
    [InlineData(".agents/knowledge/x.md", "`../y.md`", null, false)]
    [InlineData(".agents/knowledge/x.md", "[../y.md]", null, false)]
    [InlineData(".agents/knowledge/x.md", "../256", null, false)]
    public void What_a_link_held_as_text_is(string relative, string text, string? beside, bool held)
    {
        if (beside is not null) Write(beside, "# Beside\n");
        // A token longer than 255 characters is not a path the rule reads as one.
        if (text == "../256") text = "../" + new string('a', 253);

        Assert.Equal(held, RepositoryLinks.HeldAsText(Root, relative, text));
    }
}
