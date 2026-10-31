namespace Daoris.Driver;

/// <summary>
/// A plugin server that drives Daoris's own browser (D78): `${browser}` in its command or environment is
/// the in-app browser's CDP endpoint, filled in when a session is handed its servers — never when the
/// manifest is read, because the endpoint exists only while the shell runs.
/// </summary>
/// <remarks>
/// <para><b>Asked only when needed.</b> The shell is asked for the endpoint only for a session whose servers name
/// it, and the browser itself comes up at the person's press or the first connection to the endpoint
/// (BROWSERSTART1), never at a session's start, so a machine with no such plugin, and a restart that takes
/// sessions up, never open a window nobody asked for.</para>
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
    /// Resolve against the shell's browser endpoint, asked for when a server needs it — and the
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
        var (handed, notice, drives, _) = await HandNotedAsync(servers, browser, ct).ConfigureAwait(false);
        return (handed, notice?.Note, drives);
    }

    /// <summary>
    /// <see cref="HandAsync"/> with its sentence as a note (CONVNOTE2c): the lead, the servers withheld a fact of it, and why as
    /// a code where the host knows it, or the exception's words as written; the English is what it always was.
    /// </summary>
    /// <returns>
    /// And <c>Drivers</c>: the names of the servers handed that drive Daoris's browser, found by <see cref="Needs"/> before the
    /// endpoint was filled in (BRWHINT1a), for the line a chat's agent is told — never a literal name, which another plugin's
    /// server may also carry.
    /// </returns>
    public static async Task<(IReadOnlyList<AcpMcpServer> Handed, Noted? Notice, bool Drives, IReadOnlyList<string> Drivers)> HandNotedAsync(
        IReadOnlyList<AcpMcpServer> servers, IInAppBrowser? browser, CancellationToken ct)
    {
        if (!servers.Any(Needs)) return (servers, null, false, []);

        string? endpoint = null;
        Noted? why = null;
        if (browser is null)
        {
            why = Noted.Of(NoteCodes.BrowserNoHost, "this host carries no in-app browser — only the desktop shell does");
        }
        else
        {
            try
            {
                // The endpoint only: the browser comes up at a press or the first connection, never at a start (BROWSERSTART1).
                endpoint = await browser.EndpointAsync(ct).ConfigureAwait(false);
                if (endpoint is null) why = Noted.Of(NoteCodes.BrowserNoneUp, "the shell answered that it has no browser to bring up");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                // An exception's message may hold a path: the program's words, never a value.
                why = Noted.Said(error.Message, NoteBy.Program);
            }
        }

        var (handed, withheld) = Resolve(servers, endpoint);
        Noted? notice = withheld.Count == 0
            ? null
            : Noted.Of(
                    NoteCodes.BrowserWithheld,
                    $"— {string.Join(", ", withheld.Select(name => $"`{name}`"))} drives Daoris's own browser, and was not handed:",
                    ("servers", (IReadOnlyList<string>)[.. withheld]))
                .Then(" ", why ?? new Noted("", []))
                .Then(" (D78).");
        // With an endpoint every server that needs it is handed; without one, none is.
        return (handed, notice, endpoint is not null, endpoint is null ? [] : [.. servers.Where(Needs).Select(server => server.Name)]);
    }

    /// <summary>
    /// What a chat's agent is told of the browser it was handed (BRWHINT1a): each server that drives it by its own name, and
    /// what the browser is. Daoris's one-copy English (D142 point 6). Null where none was handed, so nothing is said.
    /// </summary>
    public static string? Told(IReadOnlyList<string> drivers)
    {
        if (drivers.Count == 0) return null;
        var quoted = drivers.Select(name => $"`{name}`").ToList();
        var names = quoted.Count == 1 ? quoted[0] : $"{string.Join(", ", quoted.Take(quoted.Count - 1))} and {quoted[^1]}";
        var servers = drivers.Count == 1 ? $"the MCP server {names}" : $"the MCP servers {names}";
        return $"Daoris has handed you its own browser as {servers}: it drives the browser window the person signs in to and watches, so use its tools for any web page, including one behind the person's sign-in.";
    }
}
