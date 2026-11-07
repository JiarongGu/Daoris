using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// This repository's own rooms (its lanes file's <c>builds</c>) and classes (its gates file's <c>trees</c>) pass the room
/// check over its tracked files, and the report says its claim groups: nearly everything one group, since the contracts, the web's
/// output, the generated types, the shared props and the doctrine join the desktop, service, web, tools, CLI and documents rooms, and
/// the records beside it.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class RoomsDeclaredCensusTests(ITestOutputHelper output)
{
    [Fact]
    public void This_repositorys_declaration_passes_and_says_its_claim_groups()
    {
        var root = GitSeed.RepoRoot();
        var report = RoomCheck.Of(
            File.ReadAllText(Path.Combine(root, LineRegistration.LanesFile)),
            File.ReadAllText(Path.Combine(root, GeneratedWriters.DeclarationFile)),
            Tracked(root));
        output.WriteLine(report.Said());

        Assert.Empty(report.Problems);
        Assert.Equal(
            [["web", "driver", "modules", "service", "cli", "tools", "docs"], ["records"]],
            report.Groups.Select(group => group.ToArray()).ToArray());
    }

    private static IReadOnlyList<string> Tracked(string root)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var each in new[] { "ls-files", "-z" }) start.ArgumentList.Add(each);
        using var git = Process.Start(TestGit.Past(start)) ?? throw new InvalidOperationException("git did not start");
        var listed = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
