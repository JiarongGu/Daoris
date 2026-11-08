using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A file an editor holds is left out of its repository's reading; it stops neither that repository nor the others.</summary>
public sealed class UnreadableFileTests
{
    [Fact]
    public void A_file_another_process_holds_is_skipped_and_its_neighbours_are_read()
    {
        var root = Path.Combine(Path.GetTempPath(), $"daoris-held-{Guid.NewGuid():N}");
        var rules = Path.Combine(root, "repo", ".claude", "rules");
        Directory.CreateDirectory(rules);
        File.WriteAllText(Path.Combine(rules, "open.md"), "# open\n\nReadable.\n");
        File.WriteAllText(Path.Combine(rules, "held.md"), "# held\n\nAn editor has it.\n");

        try
        {
            using var held = new FileStream(Path.Combine(rules, "held.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var entries = new RepositoryScanner().Scan(Path.Combine(root, "repo"));

            Assert.Contains(entries, e => e.RelativePath.EndsWith("open.md", StringComparison.Ordinal));
            Assert.DoesNotContain(entries, e => e.RelativePath.EndsWith("held.md", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
