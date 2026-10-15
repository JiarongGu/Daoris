using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The service a program the install carries reaches when nobody named one (FINDDAORIS1, D171 point 1): the address the
/// running desktop left in the home (<see cref="RunningAddress"/>), taken only while the process it names lives and the
/// address answers, on this machine, as a Daoris host. <see cref="ServiceHttp.FromEnvironment()"/> asks it after the variable.
/// </summary>
/// <remarks>
/// <para><b>Only on this machine.</b> A terminal's verbs send a checkout's path and read this machine's records, which go only
/// to a local host (D46), so a file naming another machine is not followed: the variable is how another is reached.</para>
///
/// <para><b>Only a Daoris host's answer.</b> The status door names its search tier, as every version has (the desktop's
/// <c>HostSupervisor</c> tells its own host from another program on the port by the same answer).</para>
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
    /// <param name="answers">Whether an address answers as a Daoris host: <see cref="AnswersAsDaoris(string, HttpMessageHandler?)"/>, or a test's.</param>
    public static string? Find(string home, string install, Func<string, bool> answers, out string why)
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

        if (!answers(entry.Url))
        {
            why = $"Daoris is running on {home} (pid {entry.Pid}), and nothing answered as its service at {entry.Url}: run this "
                + $"again once its window is up, or start it again ({start}).";
            return null;
        }

        why = "";
        return entry.Url;
    }

    /// <summary>Whether the process an address names still runs as the process it named: the driver lock's test.</summary>
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

    /// <summary>Whether <paramref name="url"/>'s status door answers as a Daoris host's, within two seconds.</summary>
    /// <param name="handler">A test's host; null asks over the network.</param>
    public static bool AnswersAsDaoris(string url, HttpMessageHandler? handler = null)
    {
        using var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        http.Timeout = Within;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/api/status");
            using var response = http.Send(request);
            if (!response.IsSuccessStatusCode) return false;
            using var reader = new StreamReader(response.Content.ReadAsStream());
            return IsDaorisStatus(reader.ReadToEnd());
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException
                                          or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Whether a status body is a Daoris host's: an object naming its search tier.</summary>
    public static bool IsDaorisStatus(string body)
    {
        try
        {
            using var status = JsonDocument.Parse(body);
            return status.RootElement.ValueKind == JsonValueKind.Object
                && status.RootElement.TryGetProperty("tier", out var tier)
                && tier.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
