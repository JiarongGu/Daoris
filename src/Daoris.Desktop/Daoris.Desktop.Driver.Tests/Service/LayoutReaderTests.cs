using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAYOUT7 (D117 §6.2, §6.5): what a repository's line holds, read as git objects, for the set-up quest. Fast: the
/// reader is handed the listing and the texts git would answer, which the scratch repositories below stand for. The
/// git half is <c>SetupLineProcessTests</c>'s, in the Process half.
/// </summary>
public sealed class LayoutReaderTests
{
    /// <summary>
    /// What <c>git ls-tree -r -z</c> printed for a scratch repository with a mode-120000 <c>CLAUDE.md</c> made under
    /// <c>core.symlinks=false</c> (git 2.53, measured while writing this): mode, type, object, a tab, the path, a NUL.
    /// </summary>
    private const string Listed =
        "100644 blob 8d0303a0b80c2f77a944eb375d38c09fce93eb18\t.claude/skills/x/SKILL.md\0"
        + "100644 blob 185f3252c85e926a7151fd0f3a4184e9183b4d60\tAGENTS.md\0"
        + "120000 blob 47dc3e3d863cfb5727b87d785d09abf9743c0a72\tCLAUDE.md\0"
        + "100644 blob 6efb3e7893f6a49cc4c3da2c9331a811fc6fecf8\tsrc/api/AGENTS.md\0";

    [Fact]
    public void A_listing_is_read_as_git_prints_it_the_mode_saying_which_entry_is_a_link()
    {
        var entries = LineEntry.Parse(Listed);

        Assert.Equal([".claude/skills/x/SKILL.md", "AGENTS.md", "CLAUDE.md", "src/api/AGENTS.md"], entries.Select(e => e.Path));
        var link = Assert.Single(entries, entry => entry.IsLink);
        Assert.Equal("CLAUDE.md", link.Path);
        Assert.Equal("47dc3e3d863cfb5727b87d785d09abf9743c0a72", link.Object);
        Assert.All(entries.Where(entry => !entry.IsLink), entry => Assert.True(entry.IsFile));
    }

    /// <summary>A submodule is a commit in the tree, not a file of this repository; a name with a space or a tab is kept whole.</summary>
    [Fact]
    public void A_submodule_is_no_file_and_a_name_is_kept_whole()
    {
        var entries = LineEntry.Parse(
            "160000 commit 1111111111111111111111111111111111111111\tvendor/lib\0"
            + "100755 blob 2222222222222222222222222222222222222222\tscripts/run me.sh\0"
            + "100644 blob 3333333333333333333333333333333333333333\tdocs/a\tb.md\0"
            + "garbage\0");

        Assert.False(entries.Single(entry => entry.Path == "vendor/lib").IsFile);
        Assert.True(entries.Single(entry => entry.Path == "scripts/run me.sh").IsFile);
        Assert.True(entries.Single(entry => entry.Path == "docs/a\tb.md").IsFile);
        Assert.Equal(3, entries.Count);
    }

    /// <summary>An unadopted repository with nothing for agents: no manifest, no instruction file, the layout <c>none</c>.</summary>
    [Fact]
    public void An_unadopted_repository_with_nothing_for_agents_reads_none()
    {
        var facts = new Scratch().File("README.md", "# Reports\n").File("src/app.cs", "class A {}").Read();

        Assert.False(facts.Adopted);
        Assert.Equal(AgentLayout.None, facts.Layout);
        Assert.Equal(InstructionState.Absent, facts.Agents.State);
        Assert.Equal(InstructionState.Absent, facts.Claude.State);
        Assert.Empty(facts.Rooms);
        Assert.False(facts.Declares);
        Assert.Equal("main", facts.Line);
        Assert.Equal("abc1234", facts.Commit);
    }

    /// <summary>An adopter on the older layout: the region, the import, its tiers under <c>.claude/</c>, and not clean.</summary>
    [Fact]
    public void An_adopter_on_claude_code_reads_its_region_its_import_and_its_tiers()
    {
        var facts = new Scratch()
            .File("daoris.json", """{ "packs": [], "domain": { "summary": "The engine.", "owns": [], "accepts": [] } }""")
            .File("daoris.lock", """{ "version": 1, "entries": [] }""")
            .File("AGENTS.md", Region("rules"))
            .File("CLAUDE.md", "# Engine\n\nOne line of its own.\n\n" + Region("import", "@AGENTS.md"))
            .File(".claude/knowledge/engine-mechanics.md", "# Mechanics\n")
            .File(".claude/skills/doc-loader/SKILL.md", "---\nname: doc-loader\n---\n")
            .File(".claude/rules/old.md", "# Old\n")
            .Read();

        Assert.True(facts.Adopted);
        Assert.True(facts.Declares);
        Assert.Equal(AgentLayout.Claude, facts.Layout);
        Assert.False(facts.Clean);
        Assert.Equal(new InstructionFile(InstructionState.File, OwnLines: 0, Region: true), facts.Agents);
        Assert.Equal(InstructionState.File, facts.Claude.State);
        Assert.True(facts.Claude.Region);
        Assert.True(facts.Claude.Imports);
        Assert.Equal(2, facts.Claude.OwnLines);
        Assert.Equal(["engine-mechanics.md"], facts.ClaudeKnowledge);
        Assert.Equal(["doc-loader"], facts.ClaudeSkills);
        Assert.Equal(["old.md"], facts.Rules);
    }

    /// <summary>
    /// An adopter on the agents layout, moved and clean: the lock and the manifest both say <c>agents</c>, the skills
    /// under <c>.claude/skills/</c> are the lock's mirrors, and the declared room holds its instructions.
    /// </summary>
    [Fact]
    public void An_adopter_on_agents_reads_clean_with_its_mirrors_and_its_room()
    {
        var facts = AgentsAdopter().Read();

        Assert.Equal(AgentLayout.Agents, facts.Layout);
        Assert.True(facts.Clean);
        Assert.True(facts.Declares);
        Assert.Equal(["finder"], facts.AgentsSkills);
        Assert.Equal(["finder"], facts.ClaudeSkills);
        Assert.Equal(["finder"], facts.Mirrors);
        Assert.Equal(["src/api"], facts.DeclaredRooms);
        Assert.Equal(["src/api"], facts.Rooms);
        Assert.Empty(facts.RoomsWithout);
    }

    /// <summary>A declared room with no instructions on the line, or a document left in the old tier, is not clean.</summary>
    [Fact]
    public void An_agents_adopter_with_a_room_lacking_instructions_or_a_document_left_behind_is_not_clean()
    {
        var roomless = AgentsAdopter()
            .File("daoris.json", """{ "harness": "agents", "target": ".agents", "rooms": ["src/api", "src/web"], "domain": { "summary": "x" } }""")
            .Read();
        Assert.Equal(["src/web"], roomless.RoomsWithout);
        Assert.False(roomless.Clean);

        var left = AgentsAdopter().File(".claude/knowledge/house.md", "# House\n").Read();
        Assert.Equal(["house.md"], left.ClaudeKnowledge);
        Assert.False(left.Clean);
    }

    /// <summary>A repository with instruction files of its own and no doctrine: its lines counted, its rooms found.</summary>
    [Fact]
    public void A_repository_with_instruction_files_of_its_own_reads_its_own()
    {
        var facts = new Scratch()
            .File("AGENTS.md", "# Brief\n\nBuild with make.\n")
            .File("CLAUDE.md", "# Claude\n\nRun the tests.\nNever push.\n")
            .File(".claude/rules/house.md", "# House\n")
            .File(".agents/skills/release/SKILL.md", "---\nname: release\n---\n")
            .File("packages/api/AGENTS.md", "# The api\n")
            .File("packages/web/AGENTS.md", "# The web\n")
            .File(".agents/skills/release/AGENTS.md", "# not a room: inside a tier\n")
            .Read();

        Assert.False(facts.Adopted);
        Assert.Equal(AgentLayout.Own, facts.Layout);
        Assert.Equal(new InstructionFile(InstructionState.File, OwnLines: 2), facts.Agents);
        Assert.Equal(new InstructionFile(InstructionState.File, OwnLines: 3), facts.Claude);
        Assert.Equal(["house.md"], facts.Rules);
        Assert.Equal(["release"], facts.AgentsSkills);
        Assert.Equal(["packages/api", "packages/web"], facts.Rooms);
    }

    /// <summary>
    /// 🔴 The reference's shape on a checkout without links: <c>CLAUDE.md</c> a link (mode 120000) to <c>AGENTS.md</c>,
    /// and <c>.claude/skills</c> a link to <c>../.agents/skills</c>. Read from the mode first, and the checkout's own
    /// <c>core.symlinks</c> says an agent here reads the path.
    /// </summary>
    [Fact]
    public void Links_are_read_from_the_mode_and_the_checkout_says_whether_it_holds_them_as_text()
    {
        var facts = new Scratch()
            .File("AGENTS.md", "# Brief\n")
            .Link("CLAUDE.md", "AGENTS.md")
            .Link(".claude/skills", "../.agents/skills")
            .File(".agents/skills/x/SKILL.md", "---\nname: x\n---\n")
            .Read(symlinks: false);

        Assert.Equal(new InstructionFile(InstructionState.Link, Target: "AGENTS.md"), facts.Claude);
        Assert.Equal(new InstructionFile(InstructionState.Link, Target: "../.agents/skills"), facts.ClaudeSkillsRoot);
        Assert.True(facts.Symlinks is false);
        Assert.True(facts.LinksHeldAsText);
        Assert.Empty(facts.ClaudeSkills);

        var linked = new Scratch().File("AGENTS.md", "# Brief\n").Link("CLAUDE.md", "AGENTS.md").Read(symlinks: null);
        Assert.False(linked.LinksHeldAsText);
    }

    /// <summary>A link checked out as text and committed so: a file, mode 100644, whose whole content is its partner's name.</summary>
    [Fact]
    public void A_link_committed_as_text_is_held_as_text()
    {
        var facts = new Scratch().File("AGENTS.md", "# Brief\n").File("CLAUDE.md", "AGENTS.md").Read();

        Assert.Equal(new InstructionFile(InstructionState.HeldAsText, Target: "AGENTS.md"), facts.Claude);
    }

    /// <summary>
    /// What a link held as text is: the CLI's <c>heldAsText</c> (<c>links.ts</c>), the cases the service's
    /// <c>RepositoryLayoutTests</c> holds in the CLI's order, with what sits beside read from the line's tree.
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
    [InlineData("pkg/AGENTS.md", "﻿CLAUDE.md\r\n", null, true)]
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
    // The line's own edge: a FOLDER of that name beside it is something beside it too.
    [InlineData("CLAUDE.md", "docs", "docs/index.md", true)]
    public void What_a_link_held_as_text_is(string relative, string text, string? beside, bool held)
    {
        if (text == "../256") text = "../" + new string('a', 253);
        var tree = beside is null ? [] : new Scratch().File(beside, "# Beside\n").Entries;

        Assert.Equal(held, LayoutReader.HeldAsText(relative, text, tree));
    }

    /// <summary>
    /// The layout, by the order the CLI's <c>lockLayout</c> and the service's reader keep: the lock, then the
    /// manifest, then <c>.claude</c>. A lock naming a layout this build does not know is read as none, as the
    /// service reads it. <c>null</c> for the lock is none on the line.
    /// </summary>
    [Theory]
    [InlineData("""{"entries":[]}""", """{}""", AgentLayout.Claude)]
    [InlineData("""{"entries":[]}""", """{"harness":"agents","target":".agents"}""", AgentLayout.Claude)]
    [InlineData(null, """{"harness":"agents","target":".agents"}""", AgentLayout.Agents)]
    [InlineData("""{"entries":[],"harness":"agents","target":".agents"}""", """{"harness":"agents"}""", AgentLayout.Agents)]
    [InlineData("""{"entries":[],"harness":"agents"}""", """{}""", AgentLayout.Agents)]
    [InlineData("""{"entries":[],"harness":"claude-code","target":"docs/x"}""", """{}""", AgentLayout.Claude)]
    [InlineData("""{"entries":[],"harness":"unknown-harness"}""", """{"harness":"agents"}""", AgentLayout.Agents)]
    [InlineData(null, """{"harness":"unknown-harness"}""", AgentLayout.Claude)]
    [InlineData(null, """not json""", AgentLayout.Claude)]
    public void The_layout_is_the_locks_then_the_manifests_then_the_older_one(string? lockText, string manifest, string layout)
    {
        var scratch = new Scratch().File("daoris.json", manifest);
        if (lockText is not null) scratch.File("daoris.lock", lockText);

        Assert.Equal(layout, scratch.Read().Layout);
    }

    /// <summary>A move half made — the manifest flipped, the lock still on the older layout, or no lock — is not clean.</summary>
    [Theory]
    [InlineData("""{"entries":[]}""")]
    [InlineData(null)]
    public void A_move_the_lock_has_not_followed_is_not_clean(string? lockText)
    {
        var scratch = AgentsAdopter().Without("daoris.lock");
        if (lockText is not null) scratch.File("daoris.lock", lockText);

        Assert.False(scratch.Read().Clean);
    }

    /// <summary>What declares something is <c>connect</c>'s <c>isDeclared</c>: a summary, an area owned, or a kind of work accepted.</summary>
    [Theory]
    [InlineData("""{}""", false)]
    [InlineData("""{"domain":{"summary":"  ","owns":[],"accepts":[]}}""", false)]
    [InlineData("""{"domain":{"summary":"The engine."}}""", true)]
    [InlineData("""{"domain":{"owns":["rendering"]}}""", true)]
    [InlineData("""{"domain":{"accepts":["bugs"]}}""", true)]
    [InlineData("""{"domain":"not an object"}""", false)]
    public void Declaring_something_is_connects_rule(string manifest, bool declares) =>
        Assert.Equal(declares, new Scratch().File("daoris.json", manifest).Read().Declares);

    /// <summary>The reader asks git only for the few texts it reads: never a whole tree's blobs.</summary>
    [Fact]
    public void Only_the_layouts_own_files_are_read_as_text()
    {
        var entries = AgentsAdopter().File("src/big.bin", "…").Link(".claude/skills", "../.agents/skills").Entries;

        Assert.Equal(
            [".claude/skills", "AGENTS.md", "CLAUDE.md", "daoris.json", "daoris.lock"],
            LayoutReader.Wanted(entries).Order(StringComparer.Ordinal));
    }

    private static Scratch AgentsAdopter() => new Scratch()
        .File("daoris.json", """{ "harness": "agents", "target": ".agents", "rooms": ["src/api"], "domain": { "summary": "The engine." } }""")
        .File("daoris.lock", """
            { "version": 1, "harness": "agents", "target": ".agents", "entries": [],
              "mirrors": [ { "path": ".claude/skills/finder/SKILL.md", "of": ".agents/skills/finder/SKILL.md", "sha256": "x" } ] }
            """)
        .File("AGENTS.md", Region("rules"))
        .File("CLAUDE.md", Region("import", "@AGENTS.md"))
        .File(".agents/skills/finder/SKILL.md", "---\nname: finder\n---\n")
        .File(".claude/skills/finder/SKILL.md", "---\nname: finder\n---\n")
        .File("src/api/AGENTS.md", "# The api\n")
        .File("src/api/CLAUDE.md", Region("import", "@AGENTS.md"));

    private static string Region(string name, string body = "# Doctrine\n\nGenerated.") =>
        $"<!-- daoris:{name} — generated; edit the canon, not this -->\n{body}\n<!-- /daoris:{name} -->\n";

    /// <summary>A repository's line as git would list it: each file a blob, each link mode 120000 holding its target.</summary>
    internal sealed class Scratch
    {
        private readonly Dictionary<string, (string Mode, string Text)> _files = new(StringComparer.Ordinal);

        public IReadOnlyList<LineEntry> Entries =>
            [.. _files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => new LineEntry(file.Value.Mode, "blob", Hash(file.Value.Text), file.Key))];

        public Scratch File(string path, string text)
        {
            _files[path] = ("100644", text);
            return this;
        }

        public Scratch Link(string path, string target)
        {
            _files[path] = ("120000", target);
            return this;
        }

        public Scratch Without(string path)
        {
            _files.Remove(path);
            return this;
        }

        /// <summary>The facts, handed only the texts the reader asked for, as the git half reads them.</summary>
        public LayoutFacts Read(bool? symlinks = null)
        {
            var entries = Entries;
            var texts = LayoutReader.Wanted(entries).ToDictionary(path => path, path => _files[path].Text, StringComparer.Ordinal);
            return LayoutReader.Read("main", "abc1234", entries, texts, symlinks);
        }

        private static string Hash(string text) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
