using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris home proof</c> through the host as built, and a staged build proved by its own driver as <see cref="HomeProbe"/> starts it:
/// the verb reads and writes nothing in the home (it holds no lease and opens no machine log), and a home holding what the table no longer
/// names is refused by the build's own start, a clean one is not.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class HomeProofProcessTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-proofproc-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeProofProcessTests()
    {
        Directory.CreateDirectory(Path.Combine(_install, "data"));
        File.WriteAllText(Path.Combine(_install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-a"));
    }

    private string Home => Path.Combine(_install, "data");

    public void Dispose()
    {
        if (Directory.Exists(_install)) Directory.Delete(_install, recursive: true);
    }

    [Fact]
    public async Task The_verb_prints_the_report_and_writes_nothing_under_the_home()
    {
        Directory.CreateDirectory(Path.Combine(_install, "data", "harnesses", "claude-code", "old-acct"));
        var before = Snapshot();

        var (code, said) = await RunAsync(Home, HostBuild.Dll(), "home", "proof");

        Assert.Equal(0, code);
        var report = HomeProof.Parse(said);
        Assert.NotNull(report);
        Assert.Equal(["acct-a"], report!.Accounts["claude-code"]);
        Assert.Contains(report.Problems, problem => problem.Contains("data/harnesses", StringComparison.Ordinal));
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void A_staged_build_that_reads_the_copy_clean_is_not_refused_and_one_that_reads_a_stray_is()
    {
        StageDriver();

        Assert.Null(HomeProbe.Refused(_install, Home, "0.0.9"));

        // The install holds an account where the build's table no longer names a place: the staged build's own start says so.
        Directory.CreateDirectory(Path.Combine(_install, "data", "harnesses", "claude-code", "old-acct"));
        var refusal = HomeProbe.Refused(_install, Home, "0.0.9");

        Assert.Equal(HomeProbe.Code, refusal?.Code);
        Assert.Contains("data/harnesses", refusal?.Sentence);
        Assert.Contains("keeps running", refusal?.Sentence);
        Assert.False(Directory.Exists(Path.Combine(_install, StagedBuild.Folder, HomeProbe.Scratch))
            && Directory.EnumerateFileSystemEntries(Path.Combine(_install, StagedBuild.Folder, HomeProbe.Scratch)).Any());
    }

    /// <summary>The host's build as the staged build's driver, in the folder a staged build keeps it.</summary>
    private void StageDriver()
    {
        var from = Path.GetDirectoryName(HostBuild.Dll())!;
        var to = Path.Combine(StagedBuild.StagedOf(_install), StagedBuild.AppFolder, "daoris-driver");
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private List<string> Snapshot() =>
        [.. Directory.EnumerateFileSystemEntries(_install, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_install, path) + (File.Exists(path) ? ":" + new FileInfo(path).Length : "/"))
            .Order(StringComparer.Ordinal)];

    private static async Task<(int Code, string Said)> RunAsync(string home, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = home,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment[DaorisHome.Variable] = home;
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"daoris-driver {string.Join(' ', args)} did not end: {await output}{await error}");
        }

        return (process.ExitCode, await output + await error);
    }
}
