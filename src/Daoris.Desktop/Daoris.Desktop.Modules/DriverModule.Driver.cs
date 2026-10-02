using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The driver's own state and standing choices, the page's `bridge/driver.ts` (MOD5): the state every
/// control answers with; the drivable set, holds and session trees; notifications; the strike limit and
/// a retry; the intake's and Ask Daoris's agents; a folder's trust; a nudge; and *Sync now*. Each standing
/// choice is an edit to `driver.json` and a nudge, the file the terminal's `daoris driver` edits (D50).
/// </summary>
public sealed partial class DriverModule
{
    [DriverRoute("STATE")]
    private object? GetState(IpcRequest request) => State();

    [DriverRoute("SET_DRIVABLE")]
    private object? SetDrivable(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var drivable = PayloadHelper.GetRequiredValue<bool>(request.Payload, "drivable");
        Change(config => config.WithDrivable(repository, drivable));
        return State();
    }

    [DriverRoute("SET_HOLD")]
    private object? SetHold(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var held = PayloadHelper.GetRequiredValue<bool>(request.Payload, "held");
        Change(config => config.WithHold(repository, held));
        return State();
    }

    // Session trees (D51): the desktop's half of the standing opt-in, over the same file
    // `daoris driver trees <repo> on|off` edits — two editors, one truth (D50).
    [DriverRoute("SET_TREES")]
    private object? SetTrees(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var ownTree = PayloadHelper.GetRequiredValue<bool>(request.Payload, "ownTree");
        Change(config => config.WithTrees(repository, ownTree));
        return State();
    }

    // Whether this machine interrupts the person at all (SURF5b). The same file
    // `daoris driver notify on|off` edits — one truth, two doors (D50).
    [DriverRoute("SET_NOTIFY")]
    private object? SetNotify(IpcRequest request)
    {
        var notify = PayloadHelper.GetRequiredValue<bool>(request.Payload, "notify");
        Change(config => config.WithNotify(notify));
        return State();
    }

    // How many failed sessions park a quest (DRV6/D58), and the person's way back in. Both
    // edit what `daoris driver strikes|retry` edits — one truth, two doors (D50).
    [DriverRoute("SET_STRIKES")]
    private object? SetStrikes(IpcRequest request)
    {
        var strikes = PayloadHelper.GetRequiredValue<int>(request.Payload, "strikes");
        Change(config => config.WithStrikes(strikes));
        return State();
    }

    // Which harness answers asks with an intake session (INT4b), or null for none — the same
    // file `daoris driver intake <adapter>|off` edits: one truth, two doors (D50).
    [DriverRoute("SET_INTAKE")]
    private object? SetIntake(IpcRequest request)
    {
        var adapter = Optional(request, "adapter");
        Change(config => config.WithIntake(adapter));
        return State();
    }

    // The agent Ask Daoris runs on (HELP1, D89), or null for none — the same file
    // `daoris driver helper <adapter>|off` edits: one truth, two doors (D50).
    [DriverRoute("SET_HELPER")]
    private object? SetHelper(IpcRequest request)
    {
        var adapter = Optional(request, "adapter");
        Change(config => config.WithHelper(adapter));
        return State();
    }

    // The person's grant of a folder the driver is holding for the harness's trust (D73) — the
    // screen's half of `daoris agent trust`. Only a pair this machine's last tick held is
    // granted, in the file that tick read; the terminal is the door that names any folder.
    [DriverRoute("TRUST_FOLDER")]
    private object? TrustFolder(IpcRequest request)
    {
        var folder = PayloadHelper.GetRequiredValue<string>(request.Payload, "folder");
        var trustFile = PayloadHelper.GetRequiredValue<string>(request.Payload, "trustFile");
        var hold = _loop.Trust.Holding(folder, trustFile)
            ?? throw new DriverException(
                $"the driver is not holding `{folder}` for this agent's trust, so there is nothing "
                + "here to confirm — only a folder it is holding can be trusted from this screen. "
                + "`daoris agent trust claude-code <folder> --yes` names any folder.");

        var grant = ClaudeTrust.Grant(hold.TrustFile, hold.Folder);
        _loop.Nudge();
        return new
        {
            hold.Folder,
            grant.Key,
            grant.Changed,
            grant.Verified,
            Message = grant.Verified
                ? $"Trusted `{hold.Folder}` for this agent: its own `permissions.allow` applies there "
                  + "now, and the driver looks again."
                : $"Wrote the grant for `{hold.Folder}`, but reading `{hold.TrustFile}` back does not "
                  + "show it — a running Claude Code may have saved over it. If so, the driver will "
                  + "hold the same folder again.",
        };
    }

    /// <summary>
    /// *Try again* (RETRY1, as SESSUX1b extends it, D126 §3.4): one act that does whichever applies to the quest, read from
    /// the planner's verdict, and says which. A quest parked on its failed sessions is marked at the strike limit; one the
    /// person's stop holds is released from the session the verdict names. Anything else is refused, and nothing written.
    /// </summary>
    /// <remarks>
    /// The verdict is the loop's last look where it has looked, and a fresh plan where it has not (another loop holds the
    /// home, DRV8a), as Sessions' groups read it, so the press and the list cannot disagree. The answer is still the state,
    /// which the page's toast reads the strikes from, with <c>retried</c> saying what was done.
    /// </remarks>
    [DriverRoute("RETRY_QUEST")]
    private async Task<object?> RetryQuestAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var quest = PayloadHelper.GetRequiredValue<string>(request.Payload, "quest").Trim().TrimStart('#');
        var config = DriverConfig.Load(_loop.ConfigPath);
        var considered = _loop.Look.Latest
            ?? await SessionGroups.VerdictsAsync(_loop.Service ?? throw NotReady(), config, Door(config), lastLook: null, cancellationToken)
                .ConfigureAwait(false);
        var verdict = considered.FirstOrDefault(c => string.Equals(c.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));

        switch (verdict)
        {
            case { Verdict: StartVerdict.Exhausted }:
                // Marked at the limit rather than erased, so the records still read true and the next
                // `strikes` failures park it again.
                Change(config => config.WithForgiven(quest, config.Strikes));
                return State(new { Quest = quest, Did = "marked", Session = (string?)null });
            case { Verdict: StartVerdict.Stopped, HeldBy: { } stop }:
                // Released from that stop, and no mark: a stop is not a strike (D58). A later stop holds it again.
                Change(config => config.WithReleased(quest, stop.Session));
                return State(new { Quest = quest, Did = "released", Session = (string?)stop.Session });
            default:
                throw Refusals.Because(
                    Refusals.QuestNotHeld,
                    $"#{quest} is neither parked nor held by your stop on this machine, so there is nothing to try again.",
                    ("id", quest));
        }
    }

    // "Look now": a person who just published a quest should not watch a poll countdown.
    [DriverRoute("NUDGE")]
    private async Task<object?> NudgeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        _loop.Nudge();
        // The one verb with nothing to answer — and `await` keeps this method honestly async.
        await Task.CompletedTask;
        return null;
    }

    // *Sync now* (SYNC6b): the tick's own pass for one circle, through the loop's own set —
    // `daoris-driver sync` is the other door to the same pass (D50). What it says comes back
    // in the driver's words; a circle with no remote is its refusal, mapped above.
    [DriverRoute("SYNC_NOW")]
    private async Task<object?> SyncNowAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var workspace = RemoteTarget.Workspace(
            PayloadHelper.GetRequiredValue<string>(request.Payload, "workspace"));
        var pass = _loop.SyncNowAsync(workspace, cancellationToken)
            ?? throw NotReady();
        var report = await pass;
        return new { Workspace = workspace, report.Problem, report.Notes };
    }

    /// <param name="retried">What *Try again* did (SESSUX1b): the quest, <c>marked</c> or <c>released</c>, and the stop's
    /// session for a release. Null for every other answer, which the bridge leaves out.</param>
    private object State(object? retried = null)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        return new
        {
            Retried = retried,
            // Whether the loop's service is up (LOOK2a): until it is, every route that reads it refuses *still coming up*,
            // so the status bar says starting rather than ready, which this file alone would claim.
            Ready = _loop.Service is not null,
            _loop.ConfigPath,
            // Where this machine's Daoris lives (D63), and what establishing it did this start — a
            // machine-local path, answered only over this bridge, like every path here.
            _loop.Home,
            _loop.HomeNotice,
            // How that home stands to the account's DAORIS_HOME (LEFT2): what the page's hint says of a terminal.
            _loop.HomeAccount,
            _loop.HostNotice,
            config.Drivable,
            config.Holds,
            config.Trees,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            config.Notify,
            config.Strikes,
            config.Forgiven,
            // The stops the person released (SESSUX1b), the quest against the session, as the terminal lists them.
            config.Released,
            // 🔴 Reported so the page can show it, and modelled on the record so no toggle deletes it.
            // Off is "" on the wire, never null: the bridge leaves a null out, and the page tells a
            // shell older than the intake by this field's absence (AGT6, seen on the window).
            IntakeAdapter = config.IntakeAdapter ?? "",
            // Off is "" on the wire for the intake's reason: a shell older than Ask Daoris sends no field.
            HelperAdapter = config.HelperAdapter ?? "",
            // The lines as set (WSR2), as rows rather than an object's keys: a key policy on the
            // bridge would respell a repository's name.
            Lines = config.Lines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Branch = p.Value }).ToArray(),
            WorkspaceLines = config.WorkspaceLines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Branch = p.Value }).ToArray(),
            // The landing rules as set (WSR1), as rows for the same reason.
            Landings = config.Landings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, p.Value.Form, p.Value.Pattern, p.Value.Tidy, p.Value.Plugin }).ToArray(),
            WorkspaceLandings = config.WorkspaceLandings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, p.Value.Form, p.Value.Pattern, p.Value.Tidy, p.Value.Plugin }).ToArray(),
            // Reading and writing across as set (D107), as rows for the same reason.
            ReadAcross = config.ReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Read = p.Value }).ToArray(),
            WorkspaceReadAcross = config.WorkspaceReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Read = p.Value }).ToArray(),
            WriteAcross = config.WriteAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, To = p.Value }).ToArray(),
            Running = _loop.Processes.Running,
            // Who is driving Daoris's browser (BRW8): the running sessions handed a server that drives it.
            DrivingBrowser = _loop.Processes.DrivingBrowser,
        };
    }
}
