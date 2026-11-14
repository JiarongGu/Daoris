using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class DoctrineToolTests
{
    [Fact]
    public void The_install_is_asked_for_its_bundle_by_its_files_from_its_program_or_its_home()
    {
        var install = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "daoris-tree-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var program = Path.Combine(install, "app", "daoris-driver");
            Directory.CreateDirectory(program);
            Assert.Null(DoctrineTool.BesideProgram(program));

            var home = Path.Combine(install, "data");
            File.WriteAllText(Path.Combine(install, "INSTALLED.md"), "# Daoris — installed desktop\n");
            Assert.Null(DoctrineTool.OfHome(home));

            var entry = Path.Combine(install, "app", "cli", DoctrineTool.Bundle);
            Directory.CreateDirectory(Path.GetDirectoryName(entry)!);
            File.WriteAllText(entry, "");
            Assert.Equal(Path.GetFullPath(entry), DoctrineTool.BesideProgram(program));
            Assert.Equal(Path.GetFullPath(entry), DoctrineTool.OfHome(home));
            Assert.Equal(Path.GetFullPath(entry), DoctrineTool.OfHome(install));
        }
        finally
        {
            GitSeed.Discard(install);
        }
    }

    [Fact]
    public void The_bundle_is_the_file_the_publish_lays()
    {
        Assert.Equal("daoris.mjs", DoctrineTool.Bundle);
    }
}
