namespace Daoris.Driver;

/// <summary>
/// The servers a session is handed, as the harness will start them: Daoris's browser filled in or withheld
/// (<see cref="InAppBrowserServers"/>), then every server a plugin declared through <c>npx</c> made node and its package's script,
/// the package installed once into the toolchain (<see cref="ServerPackages"/>). Both doors take what this hands, the protocol
/// door on <c>session/new</c> and the pipe door in its file, so the harness starts each server as one node under the session's job.
/// </summary>
/// <remarks>
/// A server whose package cannot be started is not handed, and the log says why: handed as written, it would start the chain this
/// replaces, or fail inside the harness with nobody told.
/// </remarks>
public static class SessionServers
{
    /// <inheritdoc cref="InAppBrowserServers.HandNotedAsync"/>
    public static async Task<(IReadOnlyList<ServerEntry> Handed, Noted? Notice, bool Drives, IReadOnlyList<string> Drivers)> HandNotedAsync(
        string home, IReadOnlyList<ServerEntry> servers, IInAppBrowser? browser, MachineLog? log, CancellationToken ct)
    {
        var (filled, notice, _, drivers) = await InAppBrowserServers.HandNotedAsync(servers, browser, ct).ConfigureAwait(false);
        var handed = await DirectAsync(home, filled, log, ct, kept: drivers).ConfigureAwait(false);
        IReadOnlyList<string> driving = [.. drivers.Where(name => handed.Any(server => server.Name == name))];
        return (handed, notice, driving.Count > 0, driving);
    }

    /// <inheritdoc cref="InAppBrowserServers.HandAsync"/>
    public static async Task<(IReadOnlyList<ServerEntry> Handed, string? Notice, bool Drives)> HandAsync(
        string home, IReadOnlyList<ServerEntry> servers, IInAppBrowser? browser, MachineLog? log, CancellationToken ct)
    {
        var (handed, notice, drives, _) = await HandNotedAsync(home, servers, browser, log, ct).ConfigureAwait(false);
        return (handed, notice?.Note, drives);
    }

    /// <summary>
    /// Every server running a package through <c>npx</c>, declared or after a node start's <c>--</c> (<see cref="ServerNpx"/>), given node and
    /// the package's script there, that start kept (<see cref="ServerKeeper"/>) where <paramref name="kept"/> names the server; any other as declared; one that cannot start left out, and logged.
    /// </summary>
    /// <param name="npm">The package manager an install runs; Tools' npm where null.</param>
    public static async Task<IReadOnlyList<ServerEntry>> DirectAsync(
        string home, IReadOnlyList<ServerEntry> servers, MachineLog? log, CancellationToken ct, IReadOnlyList<string>? npm = null,
        IReadOnlyCollection<string>? kept = null)
    {
        var handed = new List<ServerEntry>();
        string? node = null;
        foreach (var server in servers)
        {
            var (npx, refusal) = ServerNpx.Read(server.Command, server.Arguments);
            if (npx is null && refusal is null)
            {
                handed.Add(server);
                continue;
            }

            try
            {
                if (npx is null) throw new DriverException(refusal!);
                // The node the harness would find first on the PATH Daoris hands it, named whole, so the harness looks nothing up.
                node ??= PackageStarts.Node(null, Tools.ChildPath(Tools.Read(home), home, Environment.GetEnvironmentVariable(Tools.PathVariable)))
                    ?? throw new DriverException("node is not on this machine's PATH, nor managed by Daoris");
                var script = await ServerPackages.ScriptAsync(home, npx.Npx, log, ct, npm).ConfigureAwait(false);
                var start = new ServerEntry(server.Name, node, [script, .. npx.Npx.Arguments], server.Environment);
                if (kept?.Contains(server.Name) == true) start = ServerKeeper.Wrap(home, start);
                handed.Add(npx.Handed(server, node, start.Arguments));
            }
            catch (DriverException error)
            {
                log?.Warn(ServerPackages.Refused, ("server", server.Name), ("why", error.Message));
            }
        }

        return handed;
    }
}
