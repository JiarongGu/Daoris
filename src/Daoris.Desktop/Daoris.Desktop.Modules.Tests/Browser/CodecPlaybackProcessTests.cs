using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's browser plays H.264, AAC and HEVC video (D234). The real browser is started as the shell starts
/// it, the application's own executable with the browser's arguments, and shown a page on loopback with a
/// clip of each: each must reach <c>readyState</c> 2 and move <c>currentTime</c>, read over CDP. The engine
/// plays HEVC through the machine's decoder, so that half is skipped on a machine with none.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class CodecPlaybackProcessTests : Bridge
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    [Fact]
    public Task H264_with_AAC_plays() => PlaysAsync("h264-aac.mp4");

    [HevcDecoderFact]
    public Task HEVC_plays() => PlaysAsync("hevc.mp4");

    private async Task PlaysAsync(string clip)
    {
        var exe = ApplicationExecutable();
        using var host = new LoopbackHost();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules.Tests", "fixtures", "media", clip));
        host.Serve("/" + clip, bytes);
        var page = host.Serve("/page.html", System.Text.Encoding.UTF8.GetBytes(
            $"<!doctype html><title>clip</title><video id=v src=\"/{clip}\" muted autoplay playsinline loop></video>"));

        var port = InAppBrowser.FreePort();
        var options = new EngineBrowserOptions(Path.Combine(Home, "engine"), port, Environment.ProcessId, Background: true);
        using var browser = Process.Start(new ProcessStartInfo(exe, options.ToArguments()) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! })!;
        try
        {
            using var cts = new CancellationTokenSource(Limit);
            using var engine = new EngineCdp(port);
            await engine.FirstWindowAsync(background: true, TimeSpan.FromSeconds(30), cts.Token);
            await engine.NewWindowAsync(page, background: true, cts.Token);

            PageCdp? tab = null;
            while (tab is null)
            {
                cts.Token.ThrowIfCancellationRequested();
                tab = await PageCdp.OpenAsync(port, page, cts.Token);
                if (tab is null) await Task.Delay(200, cts.Token);
            }

            using (tab)
            {
                string state = "";
                while (true)
                {
                    state = await tab.EvaluateAsync(
                        "JSON.stringify({ready: v.readyState, time: v.currentTime, error: v.error && v.error.code})", cts.Token);
                    using var read = JsonDocument.Parse(JsonDocument.Parse(state).RootElement.GetString()!);
                    if (read.RootElement.GetProperty("ready").GetInt32() >= 2 && read.RootElement.GetProperty("time").GetDouble() > 0) break;
                    if (cts.IsCancellationRequested) break;
                    await Task.Delay(250, CancellationToken.None);
                }

                using var final = JsonDocument.Parse(JsonDocument.Parse(state).RootElement.GetString()!);
                Assert.True(final.RootElement.GetProperty("ready").GetInt32() >= 2, $"{clip} never had data to play: {state}");
                Assert.True(final.RootElement.GetProperty("time").GetDouble() > 0, $"{clip} never advanced: {state}");
            }
        }
        finally
        {
            if (!browser.HasExited) browser.Kill(entireProcessTree: true);
        }
    }

    /// <summary>The application the browser is, built beside this project: the test starts it and builds nothing.</summary>
    private static string ApplicationExecutable()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");
        var built = Directory.Exists(app)
            ? Directory.EnumerateFiles(app, "Daoris.Desktop.exe", SearchOption.AllDirectories)
                .Where(file => file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        return built ?? throw new FileNotFoundException("Daoris.Desktop.App is not built: build it first, the test starts its executable as the browser");
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
