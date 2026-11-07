using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a Daoris host's status door answers that discovery reads: its search tier, and the home it serves.</summary>
/// <param name="Tier">The search tier it names, as every version has.</param>
/// <param name="Home">The id of the home it started with (<see cref="HomeIdentity"/>), or null for a shared host or an older one.</param>
public sealed record ServiceStatus(string Tier, string? Home);

/// <summary>
/// The service a program the install carries reaches when nobody named one: the address the
/// running desktop left in the home (<see cref="RunningAddress"/>), taken only while the process it names lives and the
/// address answers, on this machine, as this home's Daoris host. <see cref="ServiceHttp.FromEnvironment()"/> asks it after the
/// variable.
/// </summary>
/// <remarks>
/// <para><b>Only on this machine.</b> A terminal's verbs send a checkout's path and read this machine's records, which go only
/// to a local host, so a file naming another machine is not followed: the variable is how another is reached.</para>
///
/// <para><b>Only this home's host.</b> The status door names its search tier, as every
/// version has, and a local host also its home's id. A tier alone would pass another install's service at the
/// address, or a process id the system gave to another process since; the id makes the process check a courtesy. A
/// status naming another home, or none (an older host, or a shared one), is not taken, and nor is any where this home has
/// no id. Asked with redirects off: where an address sends its asker is no answer.</para>
///
/// <para>Discovery writes nothing: it reads the home's id and never mints one (the desktop and the hosts do at their start).</para>
/// </remarks>
public static class RunningService
{
    /// <summary>How long the status door is given to answer.</summary>
    public static readonly TimeSpan Within = TimeSpan.FromSeconds(2);

    /// <summary>The wait before a status door that did not answer is asked a second time.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(1);

    /// <summary>How long a service whose process runs is waited for in all, once the first ask goes unanswered.</summary>
    public static readonly TimeSpan WaitFor = TimeSpan.FromSeconds(30);

    /// <summary>The bound of each ask after the first, so a service slow for seconds still answers one.</summary>
    public static readonly TimeSpan LongWithin = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The address the running desktop holds for <paramref name="home"/>, or null with the sentence that says why there is none:
    /// that Daoris is not running there and how to start it, or what is found and why it is not taken. Where its process runs
    /// and the status door does not answer, it is asked again for <paramref name="total"/>.
    /// </summary>
    /// <param name="install">The install the asking program runs from, whose launcher starts Daoris.</param>
    /// <param name="status">What an address's status door answers as a Daoris host within the bound it is given, or null: <see cref="Status"/>, or a test's.</param>
    /// <param name="unanswered">Whether the address came from the home's record and nothing answered there, which the refusal does not blame on the variable.</param>
    /// <param name="retryAfter">The wait between asks, so a service busy for a moment is not taken for a missing one.</param>
    /// <param name="waiting">Told once, when the first ask goes unanswered and the process runs.</param>
    /// <param name="total">How long to wait in all; null is <see cref="WaitFor"/>.</param>
    public static string? Find(
        string home, string install, Func<string, TimeSpan, ServiceStatus?> status, out string why, out bool unanswered,
        TimeSpan retryAfter, Action<string>? waiting = null, TimeSpan? total = null)
    {
        unanswered = false;
        var start = Path.Combine(install, "Daoris.exe");
        if (RunningAddress.Read(home) is not { } entry || !Lives(entry.Pid, entry.Started))
        {
            why = $"Daoris is not running on {home}: start it ({start}), and run this again once its window is up.";
            return null;
        }

        if (!IsLoopback(entry.Url))
        {
            why = $"the address Daoris left in {home} ({entry.Url}) is not on this machine, and only this machine's own service "
                + $"is taken from it; set {ServiceClient.UrlVariable} to reach another.";
            return null;
        }

        var bound = total ?? WaitFor;
        var clock = Stopwatch.StartNew();
        var answered = status(entry.Url, Within);
        var asks = 1;
        if (answered is null && bound > TimeSpan.Zero)
        {
            // The process runs: a service busy past the bound (a landing, a machine at full load) answers a longer ask.
            waiting?.Invoke($"Daoris is running on {home} but has not answered yet; waiting up to {Seconds(bound)}.");
            while (answered is null && clock.Elapsed < bound)
            {
                var left = bound - clock.Elapsed;
                Thread.Sleep(retryAfter < left ? retryAfter : left);
                left = bound - clock.Elapsed;
                if (left <= TimeSpan.Zero) break;
                answered = status(entry.Url, left < LongWithin ? left : LongWithin);
                asks++;
            }
        }

        if (answered is null)
        {
            unanswered = true;
            why = $"Daoris is running on {home} (pid {entry.Pid}), and nothing answered as its service at {entry.Url} within "
                + $"{Seconds(bound)}, asked {asks} times. The address is the one Daoris "
                + $"left in {home}, so {ServiceClient.UrlVariable} is not the cause: the service may be busy. Run this again in a moment, "
                + $"or start Daoris again ({start}).";
            return null;
        }

        if (HomeIdentity.Read(home) is not { } mine || answered.Home != mine)
        {
            why = $"Daoris is running on {home} (pid {entry.Pid}), and the service at {entry.Url} answered as "
                + $"{(answered.Home is null ? "no Daoris home's" : "another Daoris home's")}, so nothing was sent to it: start Daoris "
                + $"on this home ({start}), or set {ServiceClient.UrlVariable} to the service you mean.";
            return null;
        }

        why = "";
        return entry.Url;
    }

    /// <summary>A bound as a person reads it: <c>2 seconds</c>, <c>1 second</c>.</summary>
    internal static string Seconds(TimeSpan span) =>
        span.TotalSeconds == 1 ? "1 second" : $"{span.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} seconds";

    /// <summary>Whether the process an address names still runs as the process it named: the home lease's test.</summary>
    public static bool Lives(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException)
        {
            // No process has that id now.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Exited as we looked.
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Its start time is not ours to read: cannot tell, and the status door decides.
            return true;
        }
    }

    /// <summary>Whether an address is this machine's own: its host by name, not a substring (`localhost.example.com` is not).</summary>
    public static bool IsLoopback(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var address)
        && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
        && address.IsLoopback;

    /// <summary>
    /// What <paramref name="url"/>'s status door answers as a Daoris host, within <paramref name="timeout"/> (<see cref="Within"/>
    /// when null) and with redirects off, or null where nothing answers as one.
    /// </summary>
    /// <param name="handler">A test's host; null asks over the network.</param>
    public static ServiceStatus? Status(string url, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        // 🔴 Redirects off. An address on this machine that sends its asker elsewhere has not answered.
        using var http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            : new HttpClient(handler, disposeHandler: false);
        http.Timeout = timeout ?? Within;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/api/status");
            using var response = http.Send(request);
            if (!response.IsSuccessStatusCode) return null;
            using var reader = new StreamReader(response.Content.ReadAsStream());
            return ReadStatus(reader.ReadToEnd());
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException
                                          or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A status body as a Daoris host's (an object naming its search tier), with the home it names where that is an id.</summary>
    public static ServiceStatus? ReadStatus(string body)
    {
        try
        {
            using var status = JsonDocument.Parse(body);
            var root = status.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("tier", out var tier) || tier.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var home = root.TryGetProperty("home", out var named) && named.ValueKind == JsonValueKind.String
                && HomeIdentity.IsId(named.GetString())
                    ? named.GetString()
                    : null;
            return new ServiceStatus(tier.GetString()!, home);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether a status body is a Daoris host's: an object naming its search tier.</summary>
    public static bool IsDaorisStatus(string body) => ReadStatus(body) is not null;
}
