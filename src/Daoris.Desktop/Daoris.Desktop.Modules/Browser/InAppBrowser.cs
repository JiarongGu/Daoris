using System.Net;
using System.Net.Sockets;

namespace Daoris.Desktop;

/// <summary>
/// What Daoris's own browser is reached by, whichever engine shows it (D78): the endpoint, the port,
/// and the address rule its favorites and history keep. Where it starts and what it runs on is
/// <see cref="EngineBrowser"/>'s (D85, CHR3).
/// </summary>
/// <remarks>
/// <para><b>Loopback, and said.</b> Any process on this machine can drive the browser while it
/// runs, as with any browser started with a debugging port. The design says so (D78 §3.4).</para>
/// </remarks>
public static class InAppBrowser
{
    /// <summary>The endpoint a server attaches to — `--cdp-endpoint` for Playwright MCP, `--browserUrl` for DevTools MCP.</summary>
    public static string Endpoint(int port) => $"http://127.0.0.1:{port}";

    /// <summary>A loopback port nothing holds right now — asked of the OS rather than guessed.</summary>
    public static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// What the person typed, as the page to go to — or null when it is not a web page. A host with no
    /// scheme is a site, over HTTPS unless it is this machine. A file, a script, data, or a credential
    /// written into the address goes nowhere: the bar is for pages, and an agent drives the page over
    /// CDP without ever passing through it.
    /// </summary>
    public static string? Address(string typed)
    {
        var text = typed.Trim();
        if (text.Length == 0 || text.Contains(' ', StringComparison.Ordinal)) return null;
        if (string.Equals(text, "about:blank", StringComparison.OrdinalIgnoreCase)) return "about:blank";

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            // No scheme: a web address, if anything. `javascript:`, `data:` and `file:` all carry one.
            if (text.Contains(':', StringComparison.Ordinal) && !LooksLikeHostAndPort(text)) return null;
            var local = text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                        || text.StartsWith("127.0.0.1", StringComparison.Ordinal)
                        || text.StartsWith("[::1]", StringComparison.Ordinal);
            text = (local ? "http://" : "https://") + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
               && string.IsNullOrEmpty(uri.UserInfo)
               && uri.Host.Length > 0
            ? uri.AbsoluteUri
            : null;
    }

    /// <summary>`host:port`, `host:port/path` — a colon that is a port, not a scheme.</summary>
    private static bool LooksLikeHostAndPort(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var rest = text[(colon + 1)..];
        var digits = rest.TakeWhile(char.IsAsciiDigit).Count();
        return digits > 0 && (digits == rest.Length || rest[digits] == '/');
    }
}
