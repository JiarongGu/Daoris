using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class LinksGateTests : IDisposable
{
    private readonly Fixture _fx = new("links");

    public void Dispose() => _fx.Dispose();

    private sealed class FakeGit(params string[] files) : IGit
    {
        public IReadOnlyList<string> StagedFiles() => files;

        public IReadOnlyList<string> TrackedFiles() => files;
    }

    private GateResult Run(params string[] tracked) =>
        new LinksGate(new FakeGit(tracked)).Run(new GateContext(_fx.Path, new GateDeclaration()));

    /// <summary>
    /// The failure this exists for: nothing compiles a link, the page still renders, and the reader
    /// concludes the thing it points at never mattered.
    /// </summary>
    [Fact]
    public void A_link_to_a_missing_file_is_reported_with_both_ends()
    {
        _fx.Write("README.md", "see [the design](docs/design.md) for why");

        var result = Run("README.md");

        Assert.False(result.Passed);
        Assert.Contains("README.md", result.Detail);
        Assert.Contains("docs/design.md", result.Detail);
    }

    /// <summary>A link cannot hold a space, so a file named with one is linked as `%20`, and is the file the link means.</summary>
    [Fact]
    public void A_percent_encoded_link_resolves_to_the_file_it_names()
    {
        _fx.Write("docs/the design.md", "the design");
        _fx.Write("README.md", "see [the design](docs/the%20design.md)");

        var result = Run("README.md", "docs/the design.md");

        Assert.True(result.Passed, result.Detail);
    }

    [Fact]
    public void Links_that_resolve_pass_and_the_count_is_reported()
    {
        _fx.Write("docs/design.md", "the design");
        _fx.Write("README.md", "see [the design](docs/design.md)");

        var result = Run("README.md", "docs/design.md");

        Assert.True(result.Passed, result.Detail);
        Assert.Contains("1 link", result.Detail);
    }

    [Fact]
    public void A_relative_link_resolves_from_the_linking_document_not_the_repository_root()
    {
        _fx.Write("docs/a.md", "see [b](b.md)");
        _fx.Write("docs/b.md", "b");

        Assert.True(Run("docs/a.md", "docs/b.md").Passed);
    }

    [Fact]
    public void A_parent_relative_link_resolves()
    {
        _fx.Write("README.md", "root");
        _fx.Write("docs/a.md", "back to [root](../README.md)");

        Assert.True(Run("docs/a.md", "README.md").Passed);
    }

    /// <summary>External links need the network, which no gate here may touch.</summary>
    [Theory]
    [InlineData("https://example.com/x.md")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("#a-heading")]
    [InlineData("/absolute/path.md")]
    public void Targets_that_are_not_relative_files_are_left_alone(string target)
    {
        _fx.Write("README.md", $"see [x]({target})");

        Assert.True(Run("README.md").Passed);
    }

    /// <summary>
    /// The fragment is dropped rather than verified: checking an anchor means reproducing a renderer's
    /// slug rules, which differ between renderers. A wrong file is always a defect; a wrong anchor is
    /// sometimes not.
    /// </summary>
    [Fact]
    public void A_fragment_after_the_path_does_not_break_resolution()
    {
        _fx.Write("docs/design.md", "# A heading");
        _fx.Write("README.md", "see [it](docs/design.md#a-heading)");

        Assert.True(Run("README.md", "docs/design.md").Passed);
    }

    /// <summary>🔴 A link quoted in a fence or a code span is followed by nobody, so it is not checked; a real one beside it is.</summary>
    [Fact]
    public void A_link_quoted_in_a_fence_or_a_code_span_is_not_checked_but_a_real_one_beside_it_is()
    {
        _fx.Write("README.md",
            "write `[x](docs/span.md)` and ``[y](docs/double `tick`.md)`` like this:\n\n"
            + "````md\n```\n[z](docs/inner.md)\n```\n[w](docs/outer.md)\n````\n\n"
            + "~~~\n[v](docs/tilde.md)\n~~~\n\n"
            + "- an item\n\n  ```\n  [u](docs/item.md)\n  ```\n\n"
            + "then [real](docs/real.md) and [`code` text](docs/linked.md)\n");

        var result = Run("README.md");

        Assert.False(result.Passed);
        Assert.Equal("README.md: 'docs/real.md' does not exist\nREADME.md: 'docs/linked.md' does not exist", result.Detail);
    }

    [Fact]
    public void A_link_to_a_directory_resolves()
    {
        _fx.Write("docs/archive/old.md", "old");
        _fx.Write("README.md", "see [the archive](docs/archive)");

        Assert.True(Run("README.md", "docs/archive/old.md").Passed);
    }

    [Fact]
    public void No_markdown_skips_rather_than_passing()
    {
        Assert.True(Run().Skipped);
    }
}
