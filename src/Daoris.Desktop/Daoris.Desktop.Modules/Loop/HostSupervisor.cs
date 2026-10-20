using System.Buffers.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    /// <summary>What the host this supervisor started printed last on its standard error (HOSTSTART1).</summary>
    private LastLines? _said;

    /// <summary>How long a host whose input was closed is given to stop by itself before it is killed.</summary>
    public TimeSpan StopWithin { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long, once the host has exited, its standard error is given to reach its end before the trouble is said: a
    /// child of the host that inherited the stream may hold it open after the host is gone.
    /// </summary>
    private static readonly TimeSpan ReadToEndWithin = TimeSpan.FromSeconds(2);

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

    /// <summary>
    /// Whether this window started its host, adopted one already answering, or has none (WIRING1b, the this-Daoris design
    /// §3.2): a <see cref="WindowWiring"/> word. An adopted host's environment is its starter's, which This Daoris says.
    /// </summary>
    public string Held { get; private set; } = WindowWiring.None;

    /// <summary>
    /// What the last probe heard, with when (WIRING1b): This Daoris shows it rather than asking again, so the page's open asks
    /// no network. Null before the first probe.
    /// </summary>
    public ServiceProbe? Last { get; private set; }

    /// <summary>
    /// The variable that asks the host this supervisor starts to read its person key from the first line of its standard
    /// input (PERSONDOOR1a), set to <c>1</c>. A twin of the host's <c>PersonKey.InputVariable</c>, duplicated on purpose since
    /// the artefacts share no code; each side's tests hold the same spelling.
    /// </summary>
    public const string PersonKeyOnInput = "DAORIS_PERSON_KEY_ON_INPUT";

    /// <summary>
    /// The person key handed to the host this supervisor starts (the person-door design §2.1): 32 bytes in base64url
    /// without padding, written on the first line of its input and never anywhere else. Null hands none, and the host keeps
    /// today's trust. Minting one for each start is PERSONDOOR1g's; an adopted host is handed nothing (§2.3).
    /// </summary>
    public string? PersonKey { get; init; }

    /// <summary>
    /// Whether the host this supervisor started proved it holds <see cref="PersonKey"/> (PROVED1, the this-Daoris design §3.2):
    /// its readiness probe asks <c>/api/status?prove=</c> a fresh nonce and checks the answer's <c>proof</c>. Null where nothing
    /// was asked: no key was handed, or the host was adopted.
    /// </summary>
    public bool? Proved { get; private set; }

    /// <summary>True when the host answers — found running, or started here and now answering.</summary>
    public async Task<bool> EnsureAsync(CancellationToken ct = default)
    {
        Proved = null;
        var found = await ProbeAsync(null, ct).ConfigureAwait(false);

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
            Held = WindowWiring.Adopted;

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
            Trouble = NoHost(serviceUrl);
            Last = Last! with { Why = "no-host" };
            return false;
        }

        try
        {
            // Through the start gate (PROCSTART2), as every start of the desktop's is.
            _owned = await ProcessStarts.Shared.StartAsync(
                StartInfo(location, asksKey: PersonKey is not null), CancellationToken.None).ConfigureAwait(false);
            // HOSTSTART1: read from the start, so a host that prints more than a pipe holds never waits on it.
            if (_owned is not null) _said = LastLines.Drain(_owned.StandardError);
            if (_owned is not null && PersonKey is not null) HandKey(_owned, PersonKey);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Located but unstartable — corrupt, blocked, or permissions. An unhandled throw here used
            // to fault the loop's task before HostReady completed, leaving the splash waiting forever.
            Trouble = $"the service host at {location.Executable} could not be started: {error.Message}";
            Last = Last! with { Why = "unstartable" };
            return false;
        }

        // A nonce for this start's proof (design §2.3), asked on every readiness probe, so the answer that makes the host
        // ready is the one that says whether it holds the key.
        var nonce = PersonKey is null ? null : Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        for (var attempt = 0; attempt < 100; attempt += 1)
        {
            if (await ProbeAsync(nonce, ct).ConfigureAwait(false) == Answer.Daoris)
            {
                Held = WindowWiring.Hosted;
                return true;
            }

            if (_owned is null || _owned.HasExited)
            {
                Trouble = await WithWhatItSaidAsync(
                    $"the service host at {location.Executable} exited before it answered.", exited: true, ct).ConfigureAwait(false);
                Last = Last! with { Why = "exited" };
                return false;
            }

            await Task.Delay(300, ct).ConfigureAwait(false);
        }

        Trouble = await WithWhatItSaidAsync(
            $"the service host was started from {location.Executable} but never answered at {serviceUrl}.", exited: false, ct)
            .ConfigureAwait(false);
        Last = Last! with { Why = "never-answered" };
        return false;
    }

    /// <summary>
    /// The trouble's sentence, followed by the host's last lines on its standard error where it printed any
    /// (HOSTSTART1): a host that dies at start says why there, and the window showed only that it exited. Bounded and as
    /// printed (<see cref="LastLines"/>); the window shows them as text, never as markup.
    /// </summary>
    private async Task<string> WithWhatItSaidAsync(string sentence, bool exited, CancellationToken ct)
    {
        if (_said is null) return sentence;
        if (exited)
        {
            try
            {
                await _said.Ended.WaitAsync(ReadToEndWithin, ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Something else still holds the stream: what was read by now is what is said.
            }
        }

        return _said.Said is { } said ? $"{sentence} Its last lines on standard error:\n{said}" : sentence;
    }

    /// <summary>
    /// Why a shell with no host answering and none to start has none — a sentence for the person, naming the two
    /// commands that lay one down in the words the driver's no-connector sentences use (CONNECTOR1c): the install's
    /// first, as <see cref="ServiceHostLocator.Candidates"/> ranks them after the person's own path.
    /// </summary>
    public static string NoHost(string serviceUrl) =>
        $"no service host is running at {serviceUrl}, and no {ServiceHostLocator.ExecutableName} was found. "
        + "`npm run publish:desktop -- --to <install> --service` lays one beside the application, and "
        + "`npm run publish:service -- --install` lands one in the home's `bin/`; or set "
        + $"{ServiceHostLocator.PathVariable} to the path of one.";

    /// <summary>
    /// How a host this supervisor starts is started (LOG2a): its standard input redirected and
    /// <see cref="StopOnInputEnd"/> set, so closing that input is its stop. Its standard error is redirected
    /// too, and read for as long as it runs, so a host that dies at start says why (HOSTSTART1); its output
    /// is left alone. The working directory is the location's, not the binary's — a dev host must run from
    /// its project so the platform bundle in its wwwroot is what gets served (see HostLocation).
    /// </summary>
    /// <param name="location">Where the host is.</param>
    /// <param name="asksKey">Whether the host is asked to read its person key on its input's first line (<see cref="PersonKeyOnInput"/>).</param>
    public static ProcessStartInfo StartInfo(HostLocation location, bool asksKey = false)
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
            RedirectStandardError = true,
            // In the console's code page, an em dash the host printed would reach the window as `鈥?` (the first
            // deployment's 4c).
            StandardErrorEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        start.Environment[StopOnInputEnd] = "1";
        // Only the ask is in the environment, never the key: an environment is copied into every child (design §2.1).
        if (asksKey) start.Environment[PersonKeyOnInput] = "1";
        return start;
    }

    /// <summary>The key written as the input's first line; a host gone already says so as it exits, which the loop reads.</summary>
    private static void HandKey(Process host, string key)
    {
        try
        {
            host.StandardInput.WriteLine(key);
            host.StandardInput.Flush();
        }
        catch (IOException)
        {
            // The pipe broke: the host has exited, and the readiness loop below says why.
        }
    }

    /// <summary>
    /// The proof of possession of <paramref name="key"/> for <paramref name="nonce"/> (the person-door design §2.3): HMAC-SHA-256
    /// under the key's 32 bytes, of the nonce's UTF-8, in base64url without padding. A twin of the host's <c>PersonKey.Prove</c>,
    /// held to the same vectors; null for a key that is not one.
    /// </summary>
    public static string? ProofOf(string key, string nonce)
    {
        byte[] secret;
        try
        {
            secret = Base64Url.DecodeFromChars(key.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        return secret.Length == 32 ? Base64Url.EncodeToString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(nonce))) : null;
    }

    /// <summary>Whether a status body's <c>proof</c> is this key's for <paramref name="nonce"/>, compared in constant time.</summary>
    private bool Proves(string body, string nonce)
    {
        if (PersonKey is null || ProofOf(PersonKey, nonce) is not { } expected) return false;
        try
        {
            using var status = JsonDocument.Parse(body);
            return status.RootElement.ValueKind == JsonValueKind.Object
                && status.RootElement.TryGetProperty("proof", out var proof)
                && proof.ValueKind == JsonValueKind.String
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(proof.GetString()!), Encoding.UTF8.GetBytes(expected));
        }
        catch (JsonException)
        {
            return false;
        }
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
    /// <param name="nonce">The proof asked of a host this supervisor started and handed a key (PROVED1); null asks none.</param>
    /// <param name="ct">The loop's stop.</param>
    private async Task<Answer> ProbeAsync(string? nonce, CancellationToken ct)
    {
        var asked = DateTimeOffset.UtcNow;
        var prove = nonce is null ? string.Empty : $"?prove={Uri.EscapeDataString(nonce)}";
        try
        {
            using var response = await _probe.GetAsync($"{serviceUrl.TrimEnd('/')}/api/status{prove}", ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Last = new ServiceProbe(false, asked, "foreign", null, null);
                return Answer.Foreign;
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            // What it said, its tier and version, kept for This Daoris (WIRING1b), read by the same rule as a Daoris host's.
            Last = ServiceProbe.Of(body, asked);
            if (!IsDaorisStatus(body)) return Answer.Foreign;
            if (nonce is not null) Proved = Proves(body, nonce);
            return Answer.Daoris;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            Last = new ServiceProbe(false, asked, "no-answer", null, null);
            return Answer.None;
        }
    }

    /// <summary>
    /// Whether a status body is a Daoris host's: an object naming its search tier. The driver's own reading, which a terminal
    /// finding the running desktop's service asks too (FINDDAORIS1), so the two never disagree on what a Daoris host is.
    /// </summary>
    internal static bool IsDaorisStatus(string body) => RunningService.IsDaorisStatus(body);

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
