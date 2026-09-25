using System.Diagnostics;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Brings the local HTTP host up, and knows whether it owns it. Adopt, never double-start: a host
/// already answering belongs to whoever started it — another shell, a terminal, a rehearsal — and
/// killing someone else's server on exit would be the process version of writing into their tree.
/// </summary>
/// <param name="serviceUrl">Where the host answers, or should.</param>
/// <param name="locate">
/// Where this shell's own host is — the one it would start, and the one whose page it carries. The
/// real answer is <see cref="ServiceHostLocator"/>'s; a test hands in a location of its own.
/// </param>
public sealed partial class HostSupervisor(string serviceUrl, Func<HostLocation?>? locate = null) : IDisposable
{
    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? _owned;

    /// <summary>Why the last <see cref="EnsureAsync"/> answered false — a sentence for the person.</summary>
    public string? Trouble { get; private set; }

    /// <summary>
    /// Information rather than trouble: the host answers, and the page it serves is not the one this
    /// install carries. Set only on adoption, and only when both pages could be read.
    /// </summary>
    /// <remarks>
    /// 🔴 The first deployment's 4d: the shell adopted the machine's host, the window was new, the
    /// page was old, and no surface said so. Adoption stays the rule — a running host is somebody's —
    /// but the shell can say what it adopted, because the platform's page names its own bundle in a
    /// script tag and the install's `index.html` names the bundle it carries. Different names are a
    /// different page; nothing else about the host is claimed.
    /// </remarks>
    public string? Notice { get; private set; }

    /// <summary>True when the host answers — found running, or started here and now answering.</summary>
    public async Task<bool> EnsureAsync(CancellationToken ct = default)
    {
        if (await AnswersAsync(ct).ConfigureAwait(false))
        {
            // 🔴 The notice informs; it never decides (REV3). A throw while reading the install's own
            // page — unreadable, locked — used to escape here, and the loop counted a host that had
            // just answered as down, and never started.
            try
            {
                Notice = await AdoptionNoticeAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Notice = null;
            }

            return true;
        }

        var location = Locate();

        if (location is null)
        {
            Trouble =
                $"no service host is running at {serviceUrl}, and no {ServiceHostLocator.ExecutableName} was "
                + $"found — install one with `npm run publish:service -- --install`, or set "
                + $"{ServiceHostLocator.PathVariable}.";
            return false;
        }

        // The working directory is the location's, not the binary's — a dev host must run from its
        // project so the platform bundle in its wwwroot is what gets served (see HostLocation).
        try
        {
            _owned = Process.Start(new ProcessStartInfo
            {
                FileName = location.Executable,
                WorkingDirectory = location.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Located but unstartable — corrupt, blocked, or permissions. An unhandled throw here used
            // to fault the loop's task before HostReady completed, leaving the splash waiting forever.
            Trouble = $"the service host at {location.Executable} could not be started: {error.Message}";
            return false;
        }

        for (var attempt = 0; attempt < 100; attempt += 1)
        {
            if (await AnswersAsync(ct).ConfigureAwait(false)) return true;
            if (_owned is null || _owned.HasExited)
            {
                Trouble = $"the service host at {location.Executable} exited before it answered.";
                return false;
            }

            await Task.Delay(300, ct).ConfigureAwait(false);
        }

        Trouble = $"the service host was started from {location.Executable} but never answered at {serviceUrl}.";
        return false;
    }

    /// <summary>Ends the host — but only one this supervisor started.</summary>
    public void Stop()
    {
        if (_owned is { HasExited: false })
        {
            _owned.Kill(entireProcessTree: true);
        }

        _owned = null;
    }

    private async Task<bool> AnswersAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _probe.GetAsync($"{serviceUrl.TrimEnd('/')}/api/status", ct)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private HostLocation? Locate() =>
        locate is not null
            ? locate()
            : ServiceHostLocator.Locate(
                Environment.GetEnvironmentVariable(ServiceHostLocator.PathVariable),
                DaorisHome.Resolve(),
                AppContext.BaseDirectory);

    /// <summary>
    /// What an adopted host serves against what this install carries — a sentence when they differ,
    /// null when they agree or when either side cannot be read. Silence is never a claim of sameness.
    /// </summary>
    private async Task<string?> AdoptionNoticeAsync(CancellationToken ct)
    {
        var own = Locate();
        if (own is null) return null;

        // The same rule the host itself applies: the bundle lives in `wwwroot` beside the working
        // directory, which is why HostLocation carries one.
        var page = Path.Combine(own.WorkingDirectory, "wwwroot", "index.html");
        if (!File.Exists(page)) return null;
        var carried = BundleNamed(await File.ReadAllTextAsync(page, ct).ConfigureAwait(false));

        string? served;
        try
        {
            served = BundleNamed(await _probe.GetStringAsync($"{serviceUrl.TrimEnd('/')}/", ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        if (carried is null || served is null || carried == served) return null;
        return $"the service already running at {serviceUrl} serves {served}, and this application carries "
            + $"{carried} — something else started that host, and the page you are looking at is its. "
            + "Stop it and start Daoris again to see this install's page.";
    }

    /// <summary>The hashed bundle a platform page names in its script tag, or null for any other page.</summary>
    public static string? BundleNamed(string html)
    {
        var match = Bundle().Match(html);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"index-[A-Za-z0-9_-]+\.js")]
    private static partial Regex Bundle();

    public void Dispose()
    {
        Stop();
        _probe.Dispose();
    }
}
