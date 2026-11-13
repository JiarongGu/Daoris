using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The index a repository declares in its manifest, which every checkout writes again: where the line commits nothing in its
/// folder, the folder is output an unforced removal takes with a tree; else the index file alone is. The pure halves; the real-git
/// rows are in <c>GeneratedIndexGitTests</c>.
/// </summary>
public sealed class GeneratedIndexTests
{
    [Theory]
    [InlineData("""{ "documents": { "index": "docs/index/README.md" } }""", "docs/index/README.md")]
    [InlineData("""{ "documents": { "index": { "path": "docs/index/README.md", "words": 10 } } }""", "docs/index/README.md")]
    [InlineData("""{ "documents": { "index": " docs\\index\\README.md " } }""", "docs/index/README.md")]
    [InlineData("""{ "documents": { "router": "docs/README.md" } }""", null)]
    [InlineData("""{ "documents": { "index": 7 } }""", null)]
    [InlineData("""{ "documents": "docs" }""", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    public void The_manifest_names_its_index_or_nothing(string manifest, string? index) =>
        Assert.Equal(index, GeneratedIndex.Declared(manifest));

    [Fact]
    public void An_index_whose_folder_the_line_never_committed_declares_the_folder() =>
        Assert.Equal("docs/index/**", GeneratedIndex.Output("docs/index/README.md", folderCommitted: false));

    [Fact]
    public void An_index_in_a_committed_folder_declares_the_file_alone() =>
        Assert.Equal("docs/INDEX.md", GeneratedIndex.Output("docs/INDEX.md", folderCommitted: true));

    [Fact]
    public void An_index_at_the_root_declares_the_file_alone() =>
        Assert.Equal("INDEX.md", GeneratedIndex.Output("INDEX.md", folderCommitted: false));

    [Theory]
    [InlineData("docs/index/", true)]
    [InlineData("docs/index/outlines/driver.md", true)]
    [InlineData("docs/", false)]
    [InlineData("docs/indexes/README.md", false)]
    [InlineData("local/notes.md", false)]
    public void The_folder_it_declares_covers_what_git_names_under_it(string path, bool covered) =>
        Assert.Equal(covered, TreeScratch.IsDeclared(TreeScratch.Globs([GeneratedIndex.Output("docs/index/README.md", folderCommitted: false)]), path));

    [Theory]
    [InlineData("../outside/README.md")]
    [InlineData("/docs/index/README.md")]
    [InlineData("C:/docs/index/README.md")]
    [InlineData("*/index/README.md")]
    public void An_index_the_pattern_rules_refuse_declares_nothing(string index) =>
        Assert.Empty(TreeScratch.Globs([GeneratedIndex.Output(index, folderCommitted: false)]));
}
