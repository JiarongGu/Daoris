using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A manifest or lock an editor holds is read as none: it stops neither the reading of its repository nor the watch of its checkout.</summary>
public sealed class UnreadableDeclarationTests
{
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
