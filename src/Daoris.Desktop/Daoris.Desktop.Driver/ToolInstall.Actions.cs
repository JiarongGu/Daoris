using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>How a tool's download, use or update ended.</summary>
/// <param name="Version">The version it planned, or null when there was nothing to do.</param>
/// <param name="ExitCode">0 done; 1 refused by a check (<paramref name="Check"/>); 2 a tool error; -1 stopped.</param>
/// <param name="Problem">Why it did not do what it was asked.</param>
/// <param name="Stopped">The person stopped it: nothing was kept, and nothing switched.</param>
/// <param name="Nothing">Why there was nothing to do, when there was not.</param>
public sealed record ToolEnd(
    string Tool, string Action, string? Version, int ExitCode, string? Check, string? Problem, bool Stopped, string? Nothing);

/// <summary>
/// A tool's download, use or update while it runs (TOOLS4, D121 §3.6): followed by the page and stopped by the person,
/// as <see cref="HarnessRun"/> is for an agent's install.
/// </summary>
public sealed class ToolRun
{
    private readonly CancellationTokenSource _stop;

    internal ToolRun(string tool, string action, string? version, CancellationTokenSource stop, Task<ToolEnd> ended)
    {
        (Tool, Action, Version, _stop, Ended) = (tool, action, version, stop, ended);
    }

    public string Tool { get; }

    /// <summary><c>download</c>, <c>use</c> or <c>update</c>.</summary>
    public string Action { get; }

    /// <summary>The version it is downloading or using; null when there is nothing to do.</summary>
    public string? Version { get; }

    /// <summary>Its end, news after the answer that it started.</summary>
    public Task<ToolEnd> Ended { get; }

    /// <summary>The person's stop: the download ends, and its staging goes with it.</summary>
    public void Cancel()
    {
        try
        {
            _stop.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ended between the look and the stop.
        }
    }
}

/// <summary>
/// The driver's download as an action the page starts and follows (TOOLS4, D121 §3.6): <see cref="Start"/> answers once
/// the action has started, its end is news after, and the person's stop cancels it. It is never one call a bridge waits
/// on, because the bridge gives up after thirty seconds and a download outlives that (WSR7 a). The routes that carry it
/// to the page, <c>TOOLS_DOWNLOAD</c> and <c>TOOLS_USE</c>, are TOOLS7's.
/// </summary>
/// <remarks>
/// <para>🔴 <b>One at a time for each tool</b>, claimed as the action starts and released however it ends: two downloads
/// of one version would stage into one folder (REV3 learned the shape on agents' logins).</para>
/// <para>Planned first, from the lists as last fetched (<see cref="ToolInstall.Plan"/>), so a refusal is the answer and
/// nothing starts; <c>use</c> and <c>update</c> switch only once the version is downloaded and verified.</para>
/// </remarks>
/// <param name="baseDirectory">The running application's folder, where the list built in is looked for first.</param>
/// <param name="transport">How a host is reached — the network, unless a test holds its own.</param>
public sealed class ToolActions(string home, string baseDirectory, HttpMessageHandler? transport = null, MachineLog? log = null)
{
    private readonly ConcurrentDictionary<string, ToolRun> _running = new(StringComparer.Ordinal);

    /// <summary>What runs for a tool now, or null.</summary>
    public ToolRun? Running(string tool) => _running.TryGetValue(tool, out var run) ? run : null;

    /// <summary>Stop what runs for a tool. False when nothing runs: a stop that went nowhere must not look delivered.</summary>
    public bool Cancel(string tool)
    {
        if (!_running.TryGetValue(tool, out var run)) return false;
        run.Cancel();
        return true;
    }

    /// <summary>
    /// Start a download, a use or an update (§3.6, §3.7), answered once it has started.
    /// </summary>
    /// <param name="action"><c>download</c>, <c>use</c> or <c>update</c>.</param>
    /// <param name="version">The version named, or null for the newest the lists named at the last look.</param>
    /// <param name="write">Each line as it happens, for the page's console.</param>
    /// <param name="platform">This machine's platform, unless a test names another.</param>
    /// <exception cref="ToolRefusal">The plan's check (<c>unknown</c>, <c>version</c>, <c>conflict</c>, <c>machine</c>,
    /// <c>file</c>, <c>platform</c>), or <c>busy</c> when the tool already has one running.</exception>
    /// <exception cref="DriverException">An action or a tool this build does not know.</exception>
    public ToolRun Start(string tool, string action, string? version, Action<string> write, string? platform = null)
    {
        if (action is not ("download" or "use" or "update"))
        {
            throw new DriverException($"unknown tool action '{action}' — one of: download, use, update");
        }

        var declared = Tools.Find(tool) ?? throw new DriverException(
            $"`{tool}` is not a tool this build runs — one of: {string.Join(", ", Tools.Declared.Select(each => each.Id))}.");
        if (_running.TryGetValue(tool, out var busy))
        {
            throw new ToolRefusal("busy", $"{declared.Name}'s {busy.Action} is still running — wait for it to end, or stop it, before "
                + "starting another.");
        }

        var here = platform ?? ToolResources.Current;
        var plan = ToolInstall.Plan(home, ToolResources.Merge(ToolResources.ReadLists(home, baseDirectory), here), tool, action, version);
        if (plan.Problem is not null) throw new ToolRefusal(plan.Check!, plan.Problem);

        var stop = new CancellationTokenSource();
        if (plan.Nothing is not null || !plan.Fetch)
        {
            // Nothing to fetch: the answer is the end.
            var end = Finish(plan, write);
            stop.Dispose();
            return new ToolRun(tool, action, plan.Version, stop, Task.FromResult(end));
        }

        // Claimed before the work begins, and the work begins only once claimed: a start that loses the claim never ran.
        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ToolRun? self = null;
        var ended = Task.Run(async () =>
        {
            if (!await go.Task.ConfigureAwait(false))
            {
                stop.Dispose();
                return new ToolEnd(tool, action, plan.Version, -1, "busy", "another start held the tool", false, null);
            }

            try
            {
                await ToolInstall.DownloadAsync(home, tool, plan.Offered!, here!, write, stop.Token, transport, log).ConfigureAwait(false);
                return Finish(plan, write);
            }
            catch (ToolRefusal refusal)
            {
                return new ToolEnd(tool, action, plan.Version, 1, refusal.Check, refusal.Message, false, null);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                write("  stopped — nothing was kept, and nothing switched.");
                return new ToolEnd(tool, action, plan.Version, -1, null, "stopped — nothing was kept", true, null);
            }
            catch (Exception error)
            {
                // Nobody awaits the work but the follower: its end says what went wrong rather than faulting.
                return new ToolEnd(tool, action, plan.Version, 2, null, error.Message, false, null);
            }
            finally
            {
                // The slot goes with the action, however it ended — and only this action's slot.
                _running.TryRemove(KeyValuePair.Create(tool, self!));
                stop.Dispose();
            }
        });

        self = new ToolRun(tool, action, plan.Version, stop, ended);
        if (!_running.TryAdd(tool, self))
        {
            go.SetResult(false);
            throw new ToolRefusal("busy", $"{declared.Name} already has an action running — wait for it to end, or stop it, before "
                + "starting another.");
        }

        go.SetResult(true);
        return self;
    }

    /// <summary>What an action ends with: <c>use</c> and <c>update</c> switch to the version, <c>download</c> never does.</summary>
    private ToolEnd Finish(ToolPlan plan, Action<string> write)
    {
        if (plan.Nothing is not null)
        {
            write($"  {plan.Nothing}.");
            return new ToolEnd(plan.Tool, plan.Action, null, 0, null, null, false, plan.Nothing);
        }

        if (plan.Action != "download")
        {
            Tools.UseManaged(home, plan.Tool, plan.Version!);
            write($"  {Tools.Find(plan.Tool)!.Name} is set to managed {plan.Version}.");
        }

        return new ToolEnd(plan.Tool, plan.Action, plan.Version, 0, null, null, false, null);
    }
}
