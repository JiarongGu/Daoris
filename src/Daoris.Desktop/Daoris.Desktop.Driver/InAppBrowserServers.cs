namespace Daoris.Driver;

/// <summary>
/// A plugin server that drives Daoris's own browser (D78): `${browser}` in its command or environment is
/// the in-app browser's CDP endpoint, filled in when a session is handed its servers — never when the
/// manifest is read, because the endpoint exists only while the shell runs.
/// </summary>
/// <remarks>
/// <para><b>Asked only when needed.</b> The shell is asked to bring the browser up only for a session
/// whose servers name it, so a machine with no such plugin never opens a window it did not ask for.</para>
///
/// <para><b>Withheld, and said.</b> Where no shell answers (the headless driver, a gate) or the browser
/// would not come up, the server is not handed. A server handed with a placeholder instead of an address
/// would fail inside the harness with nobody told why, so the session's transcript says it.</para>
/// </remarks>
public static class InAppBrowserServers
{
    public const string Placeholder = "${browser}";

    /// <summary>Whether this server attaches to the in-app browser.</summary>
    public static bool Needs(AcpMcpServer server) =>
        server.Command.Contains(Placeholder, StringComparison.Ordinal)
        || server.Arguments.Any(argument => argument.Contains(Placeholder, StringComparison.Ordinal))
        || server.Environment.Values.Any(value => value.Contains(Placeholder, StringComparison.Ordinal));

    /// <summary>
    /// The servers as a session is handed them: every one that needs the browser with the endpoint in
    /// place, or — where there is none — left out, by name.
    /// </summary>
    public static (IReadOnlyList<AcpMcpServer> Handed, IReadOnlyList<string> Withheld) Resolve(
        IReadOnlyList<AcpMcpServer> servers, string? endpoint)
    {
        var handed = new List<AcpMcpServer>();
        var withheld = new List<string>();
        foreach (var server in servers)
        {
            if (!Needs(server))
            {
                handed.Add(server);
                continue;
            }

            if (endpoint is null)
            {
                withheld.Add(server.Name);
                continue;
            }

            string Fill(string text) => text.Replace(Placeholder, endpoint, StringComparison.Ordinal);
            handed.Add(server with
            {
                Command = Fill(server.Command),
                Arguments = [.. server.Arguments.Select(Fill)],
                Environment = server.Environment.ToDictionary(pair => pair.Key, pair => Fill(pair.Value), StringComparer.Ordinal),
            });
        }

        return (handed, withheld);
    }

    /// <summary>
    /// Resolve against the shell's browser, bringing it up first when a server needs it — and the
    /// sentence the transcript carries when one could not be handed, or null when nothing was withheld.
    /// </summary>
    /// <returns>
    /// And <c>Drives</c>: whether a server that drives Daoris's browser was handed, which puts the session's
    /// hands on the page from its start until it ends (BRW8). The registry keeps it beside the process,
    /// and the page says who is driving from it — what was handed, since whether the agent has touched a
    /// page yet is its own and never reaches the driver.
    /// </returns>
    public static async Task<(IReadOnlyList<AcpMcpServer> Handed, string? Notice, bool Drives)> HandAsync(
        IReadOnlyList<AcpMcpServer> servers, IInAppBrowser? browser, CancellationToken ct)
    {
        if (!servers.Any(Needs)) return (servers, null, false);

        string? endpoint = null;
        string? why = null;
        if (browser is null)
        {
            why = "this host carries no in-app browser — only the desktop shell does";
        }
        else
        {
            try
            {
                endpoint = await browser.EnsureAsync(ct).ConfigureAwait(false);
                if (endpoint is null) why = "the shell answered that it has no browser to bring up";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                why = error.Message;
            }
        }

        var (handed, withheld) = Resolve(servers, endpoint);
        return (handed, withheld.Count == 0
            ? null
            : $"— {string.Join(", ", withheld.Select(name => $"`{name}`"))} drives Daoris's own browser, and was "
              + $"not handed: {why} (D78).",
            // With an endpoint every server that needs it is handed; without one, none is.
            endpoint is not null);
    }
}
