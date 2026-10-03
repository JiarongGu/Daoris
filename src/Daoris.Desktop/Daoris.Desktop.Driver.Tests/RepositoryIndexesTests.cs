using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KNOWUSE1c (D135 §4): the look before asking names the indexes a repository keeps for its own documents, found in the
/// tree the session runs in. Found generically, since the instruction travels to repositories that know nothing of this
/// one: the router its manifest declares, then every file named as an index where documents and doctrine are kept. The
/// sessions that put readings to the owner had a cross-cutting index and one per part of the code, which their
/// instructions named and the driver's did not (`docs/2026-10-03-knowledge-use-evidence.md` §2).
/// </summary>
public sealed class RepositoryIndexesTests : IDisposable
{
    private readonly string _tree = Path.Combine(
        Path.GetTempPath(), "daoris-repository-indexes-" + Guid.NewGuid().ToString("N")[..8]);

    public RepositoryIndexesTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Keep(string relative, string text = "# an index\n")
    {
        var path = Path.Combine(_tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void Declare(string documents) => Keep("daoris.json", $"{{ \"source\": \"daoris@0.0.1\", \"documents\": {documents} }}\n");

    [Fact]
    public void A_tree_that_keeps_none_names_none()
    {
        Keep("README.md");
        Keep("docs/design.md");

        Assert.Empty(RepositoryIndexes.Find(_tree));
    }

    /// <summary>
    /// Every file named as an index in the root, the documents folder, and the two doctrine roots with their rules and
    /// knowledge tiers: in that order, and by name within a folder.
    /// </summary>
    [Fact]
    public void A_file_named_as_an_index_is_found_where_documents_and_doctrine_are_kept()
    {
        foreach (var path in new[]
                 {
                     ".agents/knowledge/index.md", ".agents/INDEX.md", ".claude/knowledge/knowledge-index.md",
                     ".claude/rules/RULES_INDEX_CROSS.md", ".claude/rules/RULES_INDEX.md", ".claude/rules/RULES_INDEX_V3.md",
                     ".claude/INDEX.md", "docs/INDEX.md", "INDEX.md",
                 })
        {
            Keep(path);
        }

        Assert.Equal(
            [
                "INDEX.md", "docs/INDEX.md", ".claude/INDEX.md", ".claude/rules/RULES_INDEX.md", ".claude/rules/RULES_INDEX_CROSS.md",
                ".claude/rules/RULES_INDEX_V3.md", ".claude/knowledge/knowledge-index.md", ".agents/INDEX.md",
                ".agents/knowledge/index.md",
            ],
            RepositoryIndexes.Find(_tree));
    }

    /// <summary>
    /// One level deep in those folders and nowhere else: the code's own folders, a documents subfolder and a dependency's
    /// files are not the repository's indexes, and a walk of the whole tree is not a price a start pays.
    /// </summary>
    [Fact]
    public void Nothing_deeper_and_nowhere_else_is_looked_in()
    {
        Keep("src/INDEX.md");
        Keep("docs/archive/INDEX.md");
        Keep("node_modules/some-package/INDEX.md");
        Keep(".claude/skills/doc-loader/INDEX.md");

        Assert.Empty(RepositoryIndexes.Find(_tree));
    }

    /// <summary>An index is a markdown file with <c>index</c> as a word of its name, in any case: not inside another word.</summary>
    [Theory]
    [InlineData("INDEX.md", true)]
    [InlineData("index.md", true)]
    [InlineData("Index.MD", true)]
    [InlineData("RULES_INDEX.md", true)]
    [InlineData("RULES_INDEX_CROSS.md", true)]
    [InlineData("knowledge-index.md", true)]
    [InlineData("docs.index.md", true)]
    [InlineData("README.md", false)]
    [InlineData("reindex.md", false)]
    [InlineData("indexing.md", false)]
    [InlineData("INDEXES.md", false)]
    [InlineData("INDEX.txt", false)]
    [InlineData("INDEX", false)]
    [InlineData(".md", false)]
    public void A_name_is_an_index_only_where_index_is_a_word_of_it(string name, bool index)
    {
        Assert.Equal(index, RepositoryIndexes.IsIndex(name));
    }

    /// <summary>The router the manifest declares comes first, as a path or as an object's <c>path</c>, then the indexes.</summary>
    [Theory]
    [InlineData("{ \"router\": \"docs/README.md\" }")]
    [InlineData("{ \"router\": { \"path\": \"docs/README.md\", \"words\": 3000 } }")]
    [InlineData("{ \"router\": \"./docs/README.md\" }")]
    [InlineData("{ \"router\": \"docs\\\\README.md\" }")]
    public void The_router_the_manifest_declares_comes_first(string documents)
    {
        Keep("docs/README.md", "| Document | Kind |\n");
        Keep(".claude/INDEX.md");
        Declare(documents);

        Assert.Equal(["docs/README.md", ".claude/INDEX.md"], RepositoryIndexes.Find(_tree));
    }

    /// <summary>A router that is also named as an index is named once, first, as the manifest spells it.</summary>
    [Fact]
    public void A_router_named_as_an_index_is_named_once()
    {
        Keep(".claude/rules/RULES_INDEX.md");
        Keep(".claude/rules/RULES_INDEX_CROSS.md");
        Declare("{ \"router\": \".claude/rules/RULES_INDEX.md\" }");

        Assert.Equal([".claude/rules/RULES_INDEX.md", ".claude/rules/RULES_INDEX_CROSS.md"], RepositoryIndexes.Find(_tree));
    }

    /// <summary>
    /// The router is a pointer to a file this tree holds, so a declaration that leaves the tree, is rooted, climbs, names a
    /// folder or nothing there, or is not a path at all names nothing; the indexes beside it are still found.
    /// </summary>
    [Theory]
    [InlineData("{ \"router\": \"../elsewhere/README.md\" }")]
    [InlineData("{ \"router\": \"docs/../README.md\" }")]
    [InlineData("{ \"router\": \"/docs/README.md\" }")]
    [InlineData("{ \"router\": \"C:/docs/README.md\" }")]
    [InlineData("{ \"router\": \"docs/missing.md\" }")]
    [InlineData("{ \"router\": \"docs\" }")]
    [InlineData("{ \"router\": \"\" }")]
    [InlineData("{ \"router\": 42 }")]
    [InlineData("{ \"router\": null }")]
    [InlineData("{ \"router\": { \"words\": 3000 } }")]
    [InlineData("{ \"decisions\": \"docs/README.md\" }")]
    [InlineData("[ \"docs/README.md\" ]")]
    public void A_router_that_is_not_a_plain_path_to_a_file_here_names_nothing(string documents)
    {
        Keep("README.md");
        Keep("docs/README.md");
        Keep("docs/INDEX.md");
        Declare(documents);

        Assert.Equal(["docs/INDEX.md"], RepositoryIndexes.Find(_tree));
    }

    /// <summary>A manifest that does not read declares nothing, and the indexes beside it are still found.</summary>
    [Fact]
    public void A_manifest_that_does_not_read_declares_no_router()
    {
        Keep("docs/README.md");
        Keep("docs/INDEX.md");
        Keep("daoris.json", "{ \"documents\": { \"router\": \"docs/README.md\" ");

        Assert.Equal(["docs/INDEX.md"], RepositoryIndexes.Find(_tree));
    }

    /// <summary>
    /// The target a quest's session is handed names the indexes in the TREE it runs in, its own tree where the repository
    /// opted in (D51), which carries the committed files as the root does; a tree that keeps none names none.
    /// </summary>
    [Fact]
    public void A_quests_target_names_the_indexes_its_tree_keeps()
    {
        Keep("docs/README.md");
        Keep(".claude/INDEX.md");
        Declare("{ \"router\": \"docs/README.md\" }");
        var quest = new QuestView("abc123", "Platform", "Game", "Split the loader", "Two jobs.", "Open");

        Assert.Equal(["docs/README.md", ".claude/INDEX.md"], SessionTarget.ForQuest(quest, _tree, "http://localhost:5177").Indexes);
        var bare = Directory.CreateDirectory(Path.Combine(_tree, "a-tree-that-keeps-none")).FullName;
        Assert.Empty(SessionTarget.ForQuest(quest, bare, "http://localhost:5177").Indexes);
    }
}
