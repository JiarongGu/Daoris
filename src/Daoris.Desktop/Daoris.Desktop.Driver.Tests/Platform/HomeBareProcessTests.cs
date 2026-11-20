using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A bare <c>daoris home</c>, through the host as built: it lists the paths no entry names and changes nothing under the home, so it
/// holds no writer lease and opens no machine log, whose open prunes old files.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class HomeBareProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-homebare-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeBareProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public async Task A_bare_home_lists_a_stray_and_prunes_nothing_and_writes_nothing()
    {
        Put("data/old/thing.bin", 2048);
        Put("logs/2000-01-01.driver.jsonl", 10);
        var before = Snapshot();

        var (code, said) = await RunAsync("home");

        Assert.Equal(0, code);
        Assert.Contains("Not in the table", said);
        Assert.Contains("data/old", said);
        Assert.Equal(before, Snapshot());
    }

    private void Put(string relative, int bytes)
    {
        var path = Path.Combine(_home, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    /// <summary>Every file under the home with its size, and every folder: what a run must leave as it found it.</summary>
    private List<string> Snapshot() =>
        [.. Directory.EnumerateFileSystemEntries(_home, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_home, path) + (File.Exists(path) ? ":" + new FileInfo(path).Length : "/"))
            .Order(StringComparer.Ordinal)];

    private async Task<(int Code, string Said)> RunAsync(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _home,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment[DaorisHome.Variable] = _home;
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
