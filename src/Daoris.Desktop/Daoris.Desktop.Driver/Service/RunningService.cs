using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a Daoris host's status door answers that discovery reads: its search tier, and the home it serves.</summary>
/// <param name="Tier">The search tier it names, as every version has.</param>
/// <param name="Home">The id of the home it was started with (HOMEID1, <see cref="HomeIdentity"/>), or null for a shared host or one from before.</param>
public sealed record ServiceStatus(string Tier, string? Home);

/// <summary>
/// The service a program the install carries reaches when nobody named one (FINDDAORIS1, D171 point 1): the address the
/// running desktop left in the home (<see cref="RunningAddress"/>), taken only while the process it names lives and the
/// address answers, on this machine, as this home's Daoris host. <see cref="ServiceHttp.FromEnvironment()"/> asks it after the
/// variable.
/// </summary>
/// <remarks>
/// <para><b>Only on this machine.</b> A terminal's verbs send a checkout's path and read this machine's records, which go only
/// to a local host (D46), so a file naming another machine is not followed: the variable is how another is reached.</para>
///
/// <para><b>Only this home's host</b> (HOMEID1, the terminal RPC design §3.2). The status door names its search tier, as every
/// version has, and a local host now also its home's id. A tier alone was taken before, so another install's service at the
/// address, or a process id the system gave to another process since, passed; the id makes the process check a courtesy. A
/// status naming another home, or none (a host from before, or a shared one), is not taken, and nor is any where this home has
/// no id. Asked with redirects off: where an address sends its asker is no answer.</para>
///
/// <para>Discovery writes nothing: it reads the home's id and never mints one (the desktop and the hosts do at their start).</para>
/// </remarks>
public static class RunningService
{
    /// <summary>How long the status door is given to answer.</summary>
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The address the running desktop holds for <paramref name="home"/>, or null with the sentence that says why there is none:
    /// that Daoris is not running there and how to start it, or what was found and why it was not taken.
    /// </summary>
    /// <param name="install">The install the asking program runs from, whose launcher starts Daoris.</param>
    /// <param name="status">What an address's status door answers as a Daoris host, or null: <see cref="Status"/>, or a test's.</param>
    public static string? Find(string home, string install, Func<string, ServiceStatus?> status, out string why)
    {
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

        if (status(entry.Url) is not { } answered)
        {
            why = $"Daoris is running on {home} (pid {entry.Pid}), and nothing answered as its service at {entry.Url}: run this "
                + $"again once its window is up, or start it again ({start}).";
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
    /// What <paramref name="url"/>'s status door answers as a Daoris host, within two seconds and with redirects off, or null
    /// where nothing answers as one.
    /// </summary>
    /// <param name="handler">A test's host; null asks over the network.</param>
    public static ServiceStatus? Status(string url, HttpMessageHandler? handler = null)
    {
        // 🔴 HOMEID1: redirects off. An address on this machine that sends its asker elsewhere has not answered.
        using var http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            : new HttpClient(handler, disposeHandler: false);
        http.Timeout = Within;
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
