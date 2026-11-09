using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A manifest, lock or code map an editor holds is read as none or as a problem said: it stops neither the reading of its repository,
/// the watch of its checkout, nor an import.
/// </summary>
public sealed class UnreadableDeclarationTests
{
    [Fact]
    public void A_manifest_another_process_holds_still_proposes_its_repository()
    {
        var root = Path.Combine(Path.GetTempPath(), $"daoris-decl-{Guid.NewGuid():N}");
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(repository);
        File.WriteAllText(Path.Combine(repository, "daoris.json"), "{}");

        try
        {
            using var stream = new FileStream(Path.Combine(repository, "daoris.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var proposed = Assert.Single(RegistryImport.Propose(root));

            Assert.Equal(("repo", true), (proposed.Repository, proposed.Adopted));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_code_map_another_process_holds_is_a_problem_said_and_not_a_failure()
    {
        var root = Path.Combine(Path.GetTempPath(), $"daoris-decl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs", "code-map.json"), "{}");

        try
        {
            using var stream = new FileStream(Path.Combine(root, "docs", "code-map.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var read = CodeMapReader.Read(root);

            Assert.Null(read.Map);
            Assert.Equal("docs/code-map.json", read.File);
            Assert.False(string.IsNullOrWhiteSpace(read.Problem));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("daoris.json")]
    [InlineData("daoris.lock")]
    public void A_manifest_or_lock_another_process_holds_leaves_the_rest_of_the_repository_read(string held)
    {
        var root = Path.Combine(Path.GetTempPath(), $"daoris-decl-{Guid.NewGuid():N}");
        var repository = Path.Combine(root, "repo");
        var rules = Path.Combine(repository, ".claude", "rules");
        Directory.CreateDirectory(rules);
        File.WriteAllText(Path.Combine(rules, "open.md"), "# open\n\nReadable.\n");
        File.WriteAllText(Path.Combine(repository, held), "{}");

        try
        {
            using var stream = new FileStream(Path.Combine(repository, held), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var entries = new RepositoryScanner().Scan(repository);

            Assert.Contains(entries, e => e.RelativePath.EndsWith("open.md", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("daoris.json")]
    [InlineData("daoris.lock")]
    public void A_manifest_or_lock_another_process_holds_does_not_end_the_watch_of_its_checkout(string held)
    {
        var root = Path.Combine(Path.GetTempPath(), $"daoris-decl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, held), "{}");

        try
        {
            using var stream = new FileStream(Path.Combine(root, held), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var watch = new CheckoutFreshness(root, documents: null, index: null);

            Assert.True(watch.Watching);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
