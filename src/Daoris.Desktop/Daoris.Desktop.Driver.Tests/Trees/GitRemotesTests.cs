using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one reader of a remote's address (GITAUTH1b; the git-auth design §4.4): it takes Daoris's own prefix off, once, and no other
/// code of the desktop's reads <c>remote get-url</c> or <c>remote -v</c>, so no address Daoris keeps, says or judges carries the prefix.
/// </summary>
public sealed class GitRemotesTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    /// <summary>Files that name the words and read no address: the reader itself, and the read-only classifier of git's commands.</summary>
    private static readonly string[] Excused = ["GitRemotes.cs", "RefSnapshot.cs"];

    [Theory]
    [InlineData("daoris::https://github.com/org/engine.git", "https://github.com/org/engine.git")]
    [InlineData("daoris::http://127.0.0.1:8080/org/engine.git", "http://127.0.0.1:8080/org/engine.git")]
    [InlineData("daoris::daoris::https://host/x", "daoris::https://host/x")]
    [InlineData("git@github.com:org/engine.git", "git@github.com:org/engine.git")]
    [InlineData("https://github.com/org/engine.git", "https://github.com/org/engine.git")]
    [InlineData("other::https://host/x", "other::https://host/x")]
    public void The_reader_takes_Daoriss_prefix_off_once_and_nothing_else(string shown, string address) =>
        Assert.Equal(address, GitRemotes.Unprefixed(shown));

    [Fact]
    public void No_other_code_reads_a_remotes_address_from_git()
    {
        var root = EveryChildIsHandedTheToolsTests.SourceRoot();
        var readers = new List<string>();
        foreach (var project in Projects)
        {
            var folder = Path.Combine(root, project);
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || Excused.Contains(Path.GetFileName(file)))
                {
                    continue;
                }

                var source = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");
                if (Regex.IsMatch(source, @"""get-url""|""remote""\s*,\s*""-v""|remote -v|remote get-url")) readers.Add(Path.GetRelativePath(root, file));
            }
        }

        Assert.True(readers.Count == 0, "read a remote's address through GitRemotes.AddressOf, which takes Daoris's prefix off:\n" + string.Join('\n', readers));
    }

    [Fact]
    public void Daoriss_fetch_of_the_line_reads_origin_through_the_reader()
    {
        var source = File.ReadAllText(SourceFiles.Driver("TreeRefs.cs"));

        Assert.Contains("GitRemotes.AddressOf(root, \"origin\"", source, StringComparison.Ordinal);
    }
}
