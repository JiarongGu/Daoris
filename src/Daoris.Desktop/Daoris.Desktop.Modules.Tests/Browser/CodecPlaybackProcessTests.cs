using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's browser plays H.264, AAC and HEVC video (D234). The real browser is started as the shell starts
/// it, the application's own executable with the browser's arguments, and shown a page on loopback with a
/// clip of each: each must reach <c>readyState</c> 2 and move <c>currentTime</c>, read over CDP. The engine
/// plays HEVC through the machine's decoder, so that half is skipped on a machine with none.
/// </summary>
/// <remarks>
/// Each wait is named, and fails saying what it waited for and the last it saw: the browser's port, the page's
/// window, the page among the engine's targets, the clip. A clip the engine refuses fails at once with the media
/// error, what the engine says it can play, and which engine it is.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CodecPlaybackProcessTests : Bridge
{
    private static readonly TimeSpan Limit = Poll.Patience;

    private const string ClipState =
        "({page: document.readyState, shown: document.visibilityState, ready: v.readyState, network: v.networkState, time: v.currentTime, paused: v.paused," +
        " error: v.error && v.error.code, message: v.error && v.error.message})";

    private const string CanPlay =
        "(() => { const v = document.createElement('video'); return {" +
        " h264: v.canPlayType('video/mp4; codecs=\"avc1.42E01E\"'), aac: v.canPlayType('audio/mp4; codecs=\"mp4a.40.2\"')," +
        " hevc: v.canPlayType('video/mp4; codecs=\"hvc1.1.6.L93.B0\"') }; })()";

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
        using var browser = Process.Start(new ProcessStartInfo(exe, options.ToArguments()) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)! })!;
        var engineAt = Engine(exe);
        try
        {
            using var engine = new EngineCdp(port);
            await UntilAsync($"the browser's CDP port {port} to answer", browser, engineAt,
                async ct => await engine.AnswersAsync(ct) ? "" : null);

            await UntilAsync("the engine to open the page's window", browser, engineAt,
                async ct => (await engine.NewWindowAsync(page, background: true, ct)).GetRawText());

            using var tab = await UntilAsync($"{page} among the engine's targets", browser, engineAt,
                ct => PageCdp.OpenAsync(port, page, ct));

            string seen = "nothing yet";
            string? plays = null;
            await UntilAsync($"{clip} to play", browser, engineAt, async ct =>
            {
                plays ??= await tab.EvaluateAsync(CanPlay, ct);
                seen = await tab.EvaluateAsync(ClipState, ct);
                using var state = JsonDocument.Parse(seen);
                var read = state.RootElement;
                if (read.GetProperty("error").ValueKind == JsonValueKind.Number)
                {
                    Assert.Fail($"the engine refused {clip}: {seen}; it says it plays {plays}; {engineAt}");
                }

                return read.GetProperty("ready").GetInt32() >= 2 && read.GetProperty("time").GetDouble() > 0 ? seen : null;
            }, () => $"the clip: {seen}; it says it plays {plays ?? "(not asked)"}");
        }
        finally
        {
            if (!browser.HasExited) browser.Kill(entireProcessTree: true);
        }
    }

    /// <summary>
    /// Waits up to <see cref="Limit"/> for <paramref name="look"/> to find what it looks for. A look that fails on the
    /// way (a port not yet open, a client's own timeout) is a look that found nothing yet, kept for the failure.
    /// </summary>
    private static async Task<T> UntilAsync<T>(string what, Process browser, string engineAt, Func<CancellationToken, Task<T?>> look, Func<string>? seen = null)
        where T : class
    {
        using var cts = new CancellationTokenSource(Limit);
        string last = "no answer yet";
        while (true)
        {
            if (browser.HasExited)
            {
                Assert.Fail($"the browser exited with code {browser.ExitCode} while the test waited for {what}; last: {last}; {seen?.Invoke()}; {engineAt}");
            }

            try
            {
                if (await look(cts.Token) is { } found) return found;
            }
            catch (Exception error) when (!cts.IsCancellationRequested && error is HttpRequestException or WebSocketException or OperationCanceledException or InvalidOperationException)
            {
                last = $"{error.GetType().Name}: {error.Message}";
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }

            if (cts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"waited {Limit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s for {what}; last: {last}; {seen?.Invoke()}; {engineAt}");
            }

            try
            {
                await Task.Delay(250, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    /// <summary>Which engine the executable lays beside it, by its library's size: the standard CEF's and the codecs package's differ.</summary>
    private static string Engine(string exe)
    {
        var library = new FileInfo(Path.Combine(Path.GetDirectoryName(exe)!, "libcef.dll"));
        return library.Exists
            ? $"the engine is {exe}, beside libcef.dll of {library.Length.ToString(CultureInfo.InvariantCulture)} bytes written {library.LastWriteTimeUtc:u}"
            : $"the engine is {exe}, with no libcef.dll beside it";
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
