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
    /// <summary>
    /// The variable that asks the host this supervisor starts to stop when its standard input ends
    /// (LOG2a), set to <c>1</c>. A twin of the host's <c>InputEndStop.Variable</c>, duplicated on purpose
    /// since the artefacts share no code; each side's tests hold the same spelling.
    /// </summary>
    public const string StopOnInputEnd = "DAORIS_STOP_ON_INPUT_END";

    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? _owned;

    /// <summary>How long a host whose input was closed is given to stop by itself before it is killed.</summary>
    public TimeSpan StopWithin { get; init; } = TimeSpan.FromSeconds(5);

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
        var found = await ProbeAsync(ct).ConfigureAwait(false);

        // 🔴 HOSTID1: an answer is adopted only when it is a Daoris host's. Something else on the port —
        // another program that happened to take it — is not this machine's host: its answers would be
        // read as the machine's, and starting ours beside it would only fail to bind.
        if (found == Answer.Foreign)
        {
            Trouble =
                $"something is answering at {serviceUrl}, and it is not a Daoris host: its status is not the "
                + "service's. Stop what holds that port, or set DAORIS_SERVICE_URL to another.";
            return false;
        }

        if (found == Answer.Daoris)
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

        try
        {
            _owned = Process.Start(StartInfo(location));
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
            if (await ProbeAsync(ct).ConfigureAwait(false) == Answer.Daoris) return true;
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

    /// <summary>
    /// How a host this supervisor starts is started (LOG2a): its standard input redirected and
    /// <see cref="StopOnInputEnd"/> set, so closing that input is its stop. The working directory is the
    /// location's, not the binary's — a dev host must run from its project so the platform bundle in its
    /// wwwroot is what gets served (see HostLocation).
    /// </summary>
    public static ProcessStartInfo StartInfo(HostLocation location)
    {
        // Not the tools' environment (TOOLS5): the host is Daoris's own program, found by its own locator (D60, D93),
        // and it starts no process (D121 §1.1); the knowledge feed's git is the checkout's driver's.
        var start = new ProcessStartInfo
        {
            FileName = location.Executable,
            WorkingDirectory = location.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            // Nothing is written on it, only closed, but every redirected stream names UTF-8 (a source
            // scan holds it), and one with no byte-order mark writes nothing ahead of a first byte.
            StandardInputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        start.Environment[StopOnInputEnd] = "1";
        return start;
    }

    /// <summary>
    /// Ends the host — but only one this supervisor started. Its input is closed first, which the host
    /// takes as its stop, and only a host still running <see cref="StopWithin"/> later is killed.
    /// </summary>
    /// <remarks>
    /// 🔴 LOG2a: the first real machine log held an <c>app.started</c> from the host for every start and
    /// no <c>app.stopped</c> at all. The shell killed the host it started, so nothing the host does on a
    /// clean stop ever ran. Closing the input asks for that stop without a door: a route to stop the host
    /// would be one any caller on the machine could press. The kill stays as the backstop, for a host
    /// that is wedged, or one whose input something else still holds open.
    /// </remarks>
    public HostStop Stop()
    {
        var owned = Interlocked.Exchange(ref _owned, null);
        if (owned is null) return HostStop.NotOwned;
        using (owned)
        {
            try
            {
                if (owned.HasExited) return HostStop.NotOwned;
                try
                {
                    owned.StandardInput.Close();
                }
                catch (IOException)
                {
                    // The pipe had broken already: the host is going, or the kill below ends it.
                }

                if (owned.WaitForExit(StopWithin)) return HostStop.Exited;
                owned.Kill(entireProcessTree: true);
                return HostStop.Killed;
            }
            catch (InvalidOperationException)
            {
                // It went between the look and the kill.
                return HostStop.Exited;
            }
        }
    }

    /// <summary>What answers the status probe: nothing, a Daoris host, or something else.</summary>
    private enum Answer { None, Daoris, Foreign }

    /// <summary>
    /// Ask the status door. A Daoris host's status names its search tier (`tier`), as every version
    /// has, so that is what tells one from another program on the port. Nothing more is claimed: two
    /// Daoris hosts are told apart by the page each serves (<see cref="Notice"/>), never by this.
    /// </summary>
    private async Task<Answer> ProbeAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _probe.GetAsync($"{serviceUrl.TrimEnd('/')}/api/status", ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return Answer.Foreign;
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return IsDaorisStatus(body) ? Answer.Daoris : Answer.Foreign;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return Answer.None;
        }
    }

    /// <summary>Whether a status body is a Daoris host's: an object naming its search tier.</summary>
    internal static bool IsDaorisStatus(string body)
    {
        try
        {
            using var status = System.Text.Json.JsonDocument.Parse(body);
            return status.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && status.RootElement.TryGetProperty("tier", out var tier)
                && tier.ValueKind == System.Text.Json.JsonValueKind.String;
        }
        catch (System.Text.Json.JsonException)
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

/// <summary>What <see cref="HostSupervisor.Stop"/> did.</summary>
public enum HostStop
{
    /// <summary>Nothing this supervisor started was running: a host it adopted, or none at all.</summary>
    NotOwned,

    /// <summary>The host went by itself once its input was closed, and wrote its own stop.</summary>
    Exited,

    /// <summary>It was still running after <see cref="HostSupervisor.StopWithin"/>, and was killed.</summary>
    Killed,
}
