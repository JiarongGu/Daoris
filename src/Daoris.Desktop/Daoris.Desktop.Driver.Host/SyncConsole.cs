namespace Daoris.Driver.Host;

/// <summary>
/// The sync, from a terminal (SYNC6a, D50): `daoris-driver sync [status] [--workspace &lt;name&gt;]`.
/// </summary>
/// <remarks>
/// <para><b>`sync` runs the pass the tick runs</b> — the registrations, knowledge and code map up by
/// ancestry, the retires owed, the team's rows down, then the host's fetch, rebase and push of quests
/// and records — for every circle with a remote, or the one named. The screen's *Sync now* is the same
/// pass through the other door. <b>`sync status`</b> asks this machine's host where each circle stands,
/// and reaches no remote at all.</para>
///
/// <para>There is no `pull` or `push`. A pass pushes what it rebased on what it fetched, and the tick
/// runs the whole pass within seconds, so holding back either half would be undone before anyone
/// relied on it.</para>
///
/// <para>Exit codes keep the family contract: 0 clean · 2 tool error, which includes a pass that
/// could not reach its remote — a script that syncs must be able to tell.</para>
/// </remarks>
internal static class SyncConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? workspace = null;
        var status = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "status": status = true; break;
                case "--workspace" when i + 1 < args.Length: workspace = args[++i]; break;
                default: return Usage();
            }
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();
            if (status) return await StatusAsync(service, workspace).ConfigureAwait(false);

            using var sync = RemoteSyncSet.FromEnvironment(
                service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
            if (sync.Workspaces.Count == 0)
            {
                Console.WriteLine("sync: no remote is wired on this machine — `daoris remote add` wires one; until then nothing leaves it.");
                return 0;
            }

            var report = await sync.RunOnceAsync(workspace).ConfigureAwait(false);
            foreach (var note in report.Notes) Console.WriteLine($"  note  {note}");
            if (report.Problem is not null) Console.WriteLine($"sync  {report.Problem}");

            // Where each circle stands after the pass, from the host that holds it.
            IReadOnlyList<string> circles = workspace is null ? sync.Workspaces : [RemoteTarget.Workspace(workspace)];
            foreach (var circle in circles)
            {
                foreach (var line in (await service.SyncStandingAsync(circle).ConfigureAwait(false)).Describe())
                {
                    Console.WriteLine(line);
                }
            }

            return report.Problem is null ? 0 : 2;
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"sync: {error.Message}");
            return 2;
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"sync: the service could not be reached — {error.Message}");
            return 2;
        }
    }

    /// <summary>Every circle this machine has a remote for, or the one named — as its host says it stands.</summary>
    private static async Task<int> StatusAsync(ServiceClient service, string? workspace)
    {
        IReadOnlyList<string> circles = workspace is not null
            ? [RemoteTarget.Workspace(workspace)]
            : RemoteTarget.Load().Keys.Order(StringComparer.Ordinal).ToList();
        if (circles.Count == 0)
        {
            Console.WriteLine("sync: no remote is wired on this machine — every circle is local, and nothing is ahead of anything.");
            return 0;
        }

        foreach (var circle in circles)
        {
            foreach (var line in (await service.SyncStandingAsync(circle).ConfigureAwait(false)).Describe())
            {
                Console.WriteLine(line);
            }
        }

        return 0;
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: daoris-driver sync [status] [--workspace <name>]");
        Console.Error.WriteLine("  sync          one pass now, for every circle with a remote or the one named");
        Console.Error.WriteLine("  sync status   where each circle stands on this machine — ahead, behind, in conflict,");
        Console.Error.WriteLine("                and when it last reached its remote; reaches no remote itself");
        return 2;
    }
}
