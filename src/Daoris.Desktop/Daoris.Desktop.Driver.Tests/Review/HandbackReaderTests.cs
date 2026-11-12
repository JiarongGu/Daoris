using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The hand-back read from a real tree: the tip, the merge base with the line, the paths touched since and the gates they reach.</summary>
public sealed class HandbackReaderTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _tree = Path.Combine(AppContext.BaseDirectory, "handback-reader-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_tree, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_tree, recursive: true);
    }

    private async Task<string> GitAsync(params string[] arguments)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(
            _tree, ["-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. arguments], CancellationToken.None);
        Assert.True(code == 0, stderr);
        return stdout.Trim();
    }

    private async Task CommitAsync(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_tree, path))!);
        File.WriteAllText(Path.Combine(_tree, path), text);
        await GitAsync("add", "-A");
        await GitAsync("commit", "-m", path);
    }

    [Fact]
    public async Task A_tree_ahead_of_the_line_reads_its_base_its_touched_paths_and_the_gates_they_reach()
    {
        Directory.CreateDirectory(_tree);
        await GitAsync("init", "-q", "-b", "line");
        await CommitAsync("README.md", "one");
        var baseCommit = await GitAsync("rev-parse", "HEAD");
        await GitAsync("checkout", "-q", "-b", "work");
        await CommitAsync("src/Daoris.Web/a.ts", "a");
        var tip = await GitAsync("rev-parse", "HEAD");
        IReadOnlyList<DeclaredGate> gates = [new("web", "npm test", null, false) { Paths = ["src/Daoris.Web/"] }, new("driver", "dotnet test", null, true) { Paths = ["src/Daoris.Desktop/"] }];
        var asked = (new CheckAsk("a1", "s1", "web", null, CheckAsk.ByAgent, At), (CheckState?)new CheckState("a1", CheckStates.Reached, At) { Tip = tip });

        var seen = await HandbackReader.ReadAsync(_tree, "line", gates, [asked], [], CancellationToken.None);

        Assert.Equal((tip, baseCommit, 0), (seen!.Tip, seen.Base, seen.Behind));
        Assert.Equal(["src/Daoris.Web/a.ts"], seen.Touched);
        Assert.Equal([("web", CheckVerdicts.Reached)], seen.Checks.Select(check => (check.Gate, check.Verdict)));
        Assert.True(seen.Ready);

        await GitAsync("checkout", "-q", "line");
        await CommitAsync("later.md", "moved");
        await GitAsync("checkout", "-q", "work");
        var behind = await HandbackReader.ReadAsync(_tree, "line", gates, [asked], [], CancellationToken.None);
        Assert.Equal(1, behind!.Behind);
        Assert.False(behind.Ready);
        Assert.Null(await HandbackReader.ReadAsync(_tree, "no-such-line", gates, [], [], CancellationToken.None));
    }
}
