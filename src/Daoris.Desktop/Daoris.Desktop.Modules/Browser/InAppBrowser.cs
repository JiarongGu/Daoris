using System.Net;
using System.Net.Sockets;

namespace Daoris.Desktop;

/// <summary>
/// What Daoris's own browser is reached by, whichever engine shows it: the endpoint and the port.
/// Where it starts and what it runs on is
/// <see cref="EngineBrowser"/>'s.
/// </summary>
/// <remarks>
/// <para><b>Loopback, and said.</b> Any process on this machine can drive the browser while it
/// runs, as with any browser started with a debugging port. The design says so.</para>
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

}
