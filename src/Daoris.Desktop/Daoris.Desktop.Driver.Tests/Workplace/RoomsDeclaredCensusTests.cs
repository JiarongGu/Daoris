using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// This repository's own rooms (its lanes file's <c>builds</c>) and rules (its <c>daoris.files.json</c>) pass the room
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
        var checkout = GitSeed.RepoRoot();
        var report = RoomCheck.Of(
            File.ReadAllText(Path.Combine(checkout, LineRegistration.LanesFile)),
            File.ReadAllText(Path.Combine(checkout, RepositoryFilesReader.FileName)),
            Tracked(checkout));
        output.WriteLine(report.Said());

        Assert.Empty(report.Problems);
        Assert.Equal(
            [["web", "driver", "modules", "service", "cli", "tools", "docs"], ["records"]],
            report.Groups.Select(group => group.ToArray()).ToArray());
    }

    [Fact]
    public void This_repositorys_rule_file_reads_clean_with_the_web_s_dependency_before_the_root_s()
    {
        var checkout = GitSeed.RepoRoot();
        var read = RepositoryFilesReader.Read(File.ReadAllText(Path.Combine(checkout, RepositoryFilesReader.FileName)));

        Assert.Empty(read.Problems);
        Assert.Equal(
            [FileKind.Local, FileKind.Local, FileKind.Local, FileKind.Scratch, FileKind.Dependency, FileKind.Dependency, FileKind.Intermediate, FileKind.Output, FileKind.Output, FileKind.Output],
            read.Rules.Select(rule => rule.Kind));
        Assert.Equal(["/local/lyntai-local.props", "!/local/lyntai-local.props/"], read.Rules[1].Paths);
        Assert.Equal("copy", read.Rules[1].Reach);
        Assert.Equal(["/local/shenora-local.props", "!/local/shenora-local.props/"], read.Rules[2].Paths);
        Assert.Equal("copy", read.Rules[2].Reach);
        Assert.Equal(["/src/Daoris.Web/node_modules/"], read.Rules[4].Paths);
        Assert.Equal(["/node_modules/"], read.Rules[5].Paths);
        Assert.DoesNotContain("trees", File.ReadAllText(Path.Combine(checkout, GeneratedWriters.DeclarationFile)).Split('"'));
    }

    private static IReadOnlyList<string> Tracked(string checkout)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = checkout, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var each in new[] { "ls-files", "-z" }) start.ArgumentList.Add(each);
        using var git = Process.Start(TestGit.Past(start)) ?? throw new InvalidOperationException("git did not start");
        var listed = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
