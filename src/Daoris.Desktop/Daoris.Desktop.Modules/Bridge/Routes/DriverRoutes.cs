using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The driver's own state and standing choices, the page's `bridge/driver.ts` (MOD5): the state every
/// control answers with; the drivable set, holds and session trees; notifications; the strike limit and
/// a retry; the intake's and Ask Daoris's agents; a folder's trust; a nudge; and *Sync now*. Each standing
/// choice is an edit to `driver.json` and a nudge, the file the terminal's `daoris driver` edits (D50).
/// </summary>
/// <remarks>
/// Each standing choice changes the driver's state every window shows, and declares it (STATEHUB1h, D169): the pressing window
/// writes the state it was answered, and every window reads it again. A nudge declares nothing: the tick says what it moved.
/// </remarks>
public sealed class DriverRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    [DriverRoute("STATE")]
    private object? GetState(IpcRequest request) => State(_loop);

    /// <summary>
    /// What is under way (STATEHUB1c2, D169's STATEHUB1c2 note): the presses the publisher holds, whole and numbered as its last
    /// <c>UNDER_WAY</c> telling, for a window opened or reloaded mid-press, which heard none of them; the window keeps whichever
    /// is newer. A read: it declares nothing, is never under way, and tells nothing.
    /// </summary>
    [DriverRoute("UNDER_WAY")]
    private object? GetUnderWay(IpcRequest request) => _loop.UnderWay.Now();

    /// <summary>
    /// A slice's snapshot (PUSH1a, D182 point 3), which a window asks once it hears the slice's pushes: every partition as last
    /// judged, read from memory, or not judged yet, never a judgement made for the asking, so a second window judges nothing. A slice
    /// this Daoris does not push answers nothing, and the page keeps that slice's pulls (capability per slice). A read: it declares
    /// nothing and tells nothing.
    /// </summary>
    [DriverRoute("STATE_SLICE")]
    private object? GetStateSlice(IpcRequest request) =>
        _loop.Slices.Find(PayloadHelper.GetRequiredValue<string>(request.Payload, "slice"))?.Snapshot();

    [DriverRoute("SET_DRIVABLE")]
    [Publishes("SET_DRIVABLE", StateScopes.Driver)]
    private object? SetDrivable(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var drivable = PayloadHelper.GetRequiredValue<bool>(request.Payload, "drivable");
        _context.Change(config => config.WithDrivable(repository, drivable));
        return State(_loop);
    }

    [DriverRoute("SET_HOLD")]
    [Publishes("SET_HOLD", StateScopes.Driver)]
    private object? SetHold(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var held = PayloadHelper.GetRequiredValue<bool>(request.Payload, "held");
        _context.Change(config => config.WithHold(repository, held));
        return State(_loop);
    }

    // Session trees (D51): the desktop's half of the standing opt-in, over the same file
    // `daoris driver trees <repo> on|off` edits — two editors, one truth (D50).
    [DriverRoute("SET_TREES")]
    [Publishes("SET_TREES", StateScopes.Driver)]
    private object? SetTrees(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var ownTree = PayloadHelper.GetRequiredValue<bool>(request.Payload, "ownTree");
        _context.Change(config => config.WithTrees(repository, ownTree));
        return State(_loop);
    }

    // A repository's standing answer (KNOWUSE1b, D135 §3): the person's words, handed to every session there, over the same
    // file `daoris driver standing <repo> "…"|--clear` edits — one truth, two doors (D50). No words clears it; blank words or
    // words past the bound are the driver's refusal.
    [DriverRoute("SET_STANDING")]
    [Publishes("SET_STANDING", StateScopes.Driver)]
    private object? SetStanding(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var says = Optional(request, "says");
        _context.Change(config => config.WithStanding(repository, says, DateTimeOffset.UtcNow));
        return State(_loop);
    }

    /// <summary>
    /// This machine's heavy gates for a repository whose own <c>daoris.gates.json</c> marks none (HEAVYRUN1b2b, D180's HEAVYRUN1b2a
    /// note), the setup page's half of <c>daoris-driver heavy</c> over the same key (D50): <c>repository</c>, and <c>gates</c>, the whole
    /// list in the order its gates run, each <c>{name, run, cwd?}</c>, or none or null to clear it. Judged by
    /// <see cref="HeavyMapping.Apply"/> against the file before anything is written, so a list the mapping would not read is refused
    /// whole in its words. Never an edit to the repository; one gate at a time is not offered, since the list's order is the point.
    /// </summary>
    [DriverRoute("SET_HEAVY_GATES")]
    [Publishes("SET_HEAVY_GATES", StateScopes.Driver)]
    private object? SetHeavyGates(IpcRequest request)
    {
        var repository = Optional(request, "repository") ?? "";
        var gates = HeavyGatesOf(request);
        // The refusals come first, before anything is written.
        HeavyMapping.Apply(DriverConfig.Load(_loop.ConfigPath), repository, gates);
        _context.Change(config => config.WithHeavyGates(repository, gates));
        return State(_loop);
    }

    /// <summary>
    /// The list as the page sends it: null where it sends none or null; each gate's name, command and folder as written, a field that is
    /// no text read as none, so the mapping's own rule says what is missing.
    /// </summary>
    private static List<HeavyGate>? HeavyGatesOf(IpcRequest request)
    {
        if (request.Payload is not { } payload
            || !payload.TryGetProperty("gates", out var gates)
            || gates.ValueKind == JsonValueKind.Null) return null;
        if (gates.ValueKind != JsonValueKind.Array)
        {
            throw new DriverException("a repository's heavy gates are a list of gates, each its name, its command and its folder, in the order they run.");
        }

        static string? Text(JsonElement gate, string name) =>
            gate.ValueKind == JsonValueKind.Object && gate.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        return [.. gates.EnumerateArray().Select(gate => new HeavyGate(Text(gate, "name") ?? "", Text(gate, "run") ?? "", Text(gate, "cwd"), Heavy: true))];
    }

    // Whether this machine interrupts the person at all (SURF5b). The same file
    // `daoris driver notify on|off` edits — one truth, two doors (D50).
    [DriverRoute("SET_NOTIFY")]
    [Publishes("SET_NOTIFY", StateScopes.Driver)]
    private object? SetNotify(IpcRequest request)
    {
        var notify = PayloadHelper.GetRequiredValue<bool>(request.Payload, "notify");
        _context.Change(config => config.WithNotify(notify));
        return State(_loop);
    }

    // How many failed sessions park a quest (DRV6/D58), and the person's way back in. Both
    // edit what `daoris driver strikes|retry` edits — one truth, two doors (D50).
    [DriverRoute("SET_STRIKES")]
    [Publishes("SET_STRIKES", StateScopes.Driver)]
    private object? SetStrikes(IpcRequest request)
    {
        var strikes = PayloadHelper.GetRequiredValue<int>(request.Payload, "strikes");
        _context.Change(config => config.WithStrikes(strikes));
        return State(_loop);
    }

    // How long an account cools when its agent names no time (TOOL4g, D125 §2.2, §6): whole minutes, at least one, the same
    // `cooloff` `daoris driver cooloff <minutes>` edits — one truth, two doors (D50). Less than one is the driver's refusal.
    [DriverRoute("SET_COOLOFF")]
    [Publishes("SET_COOLOFF", StateScopes.Driver)]
    private object? SetCoolOff(IpcRequest request)
    {
        var minutes = Number(request, "minutes")
            ?? throw new DriverException(DriverConfig.CoolOffRefusal);
        _context.Change(config => config.WithCoolOff((int)Math.Clamp(minutes, int.MinValue, int.MaxValue)));
        return State(_loop);
    }

    // Which harness answers asks with an intake session (INT4b), or null for none — the same
    // file `daoris driver intake <adapter>|off` edits: one truth, two doors (D50). What an intake in each circle would run on
    // moves with it, which the roster's key holds.
    [DriverRoute("SET_INTAKE")]
    [Publishes("SET_INTAKE", StateScopes.Driver | StateScopes.Harnesses)]
    private object? SetIntake(IpcRequest request)
    {
        var adapter = Optional(request, "adapter");
        _context.Change(config => config.WithIntake(adapter));
        return State(_loop);
    }

    // The agent Ask Daoris runs on (HELP1, D89), or null for none — the same file
    // `daoris driver helper <adapter>|off` edits: one truth, two doors (D50).
    [DriverRoute("SET_HELPER")]
    [Publishes("SET_HELPER", StateScopes.Driver)]
    private object? SetHelper(IpcRequest request)
    {
        var adapter = Optional(request, "adapter");
        _context.Change(config => config.WithHelper(adapter));
        return State(_loop);
    }

    // Ask Daoris's access mode (HELPMODE1a2, D183 point 2): `restricted` or `full`, the person's press alone, over the same key
    // `daoris-driver help mode` sets: one truth, two doors (D50). Each start reads it; a conversation running under another is told on
    // its console that the switch takes its next start, since nothing it runs with changes while it runs. A mode it does not know, or
    // none, is the driver's refusal, with nothing written.
    [DriverRoute("SET_HELP_MODE")]
    [Publishes("SET_HELP_MODE", StateScopes.Driver)]
    private object? SetHelpMode(IpcRequest request)
    {
        var mode = Optional(request, "mode")
            ?? throw new DriverException($"Ask Daoris's mode is `{HelpModes.Restricted}` or `{HelpModes.Full}`; the press named none.");
        _context.Change(config => config.WithHelpMode(mode));
        _loop.Chat?.HelpModeChosen(HelpModes.Named(mode)!);
        return State(_loop);
    }

    // The person's grant of a folder the driver is holding for the harness's trust (D73) — the
    // screen's half of `daoris agent trust`. Only a pair this machine's last tick held is
    // granted, in the file that tick read; the terminal is the door that names any folder. The holds themselves are the tick's
    // to tell again, as it looks at once.
    [DriverRoute("TRUST_FOLDER")]
    [Publishes("TRUST_FOLDER", StateScopes.Driver)]
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
    /// the planner's verdict, and says which. A quest parked on its failed sessions is marked at its failures as the records
    /// count them, which the verdict carries (RETRY1b); one the person's stop holds is released from the session the verdict
    /// names. Anything else is refused, and nothing written.
    /// </summary>
    /// <remarks>
    /// The verdict is the loop's last look where it has looked, and a fresh plan where it has not (another loop holds the
    /// home, DRV8a), as Sessions' groups read it, so the press and the list cannot disagree. The answer is still the state,
    /// which the page's toast reads the strikes from, with <c>retried</c> saying what was done. What it writes moves the verdict
    /// Sessions' groups read, so it declares the sessions with the state (STATEHUB1h).
    /// </remarks>
    [DriverRoute("RETRY_QUEST")]
    [Publishes("RETRY_QUEST", StateScopes.Sessions | StateScopes.Driver)]
    private async Task<object?> RetryQuestAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var quest = PayloadHelper.GetRequiredValue<string>(request.Payload, "quest").Trim().TrimStart('#');
        var config = DriverConfig.Load(_loop.ConfigPath);
        var considered = _loop.Look.Latest
            ?? await SessionGroups.VerdictsAsync(_loop.Service ?? throw NotReady(), config, _context.Door(config), lastLook: null, cancellationToken)
                .ConfigureAwait(false);
        var verdict = considered.FirstOrDefault(c => string.Equals(c.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));

        switch (verdict)
        {
            case { Verdict: StartVerdict.Exhausted } exhausted:
                // Marked where it stands rather than erased, so the records still read true and the next `strikes`
                // failures park it again. At its failures, not the limit (RETRY1b): a quest parked a second time has
                // more failures than the limit, and a mark at the limit left it parked. A verdict carrying no count is
                // marked at the least count the planner parks at.
                _context.Change(config => config.WithForgiven(quest, exhausted.Failures ?? config.ForgivenAt(quest) + config.Strikes));
                return State(_loop, new { Quest = quest, Did = "marked", Session = (string?)null });
            case { Verdict: StartVerdict.Stopped, HeldBy: { } stop }:
                // Released from that stop, and no mark: a stop is not a strike (D58). A later stop holds it again.
                _context.Change(config => config.WithReleased(quest, stop.Session));
                return State(_loop, new { Quest = quest, Did = "released", Session = (string?)stop.Session });
            case { Verdict: StartVerdict.Paused, PausedBy: { } pause }:
                // A pause is the reason before a stop and the strikes (PAUSE1b, D132 §6.1): Resume moves it, and nothing is written.
                throw pause.Scope == WorkScope.Ask
                    ? Refusals.Because(
                        Refusals.QuestPaused, $"#{quest} is paused with ask #{pause.Id}; Resume carries it on.", ("id", quest), ("pause", pause.Id))
                    : Refusals.Because(
                        Refusals.QuestPaused, $"#{quest} is paused on its own; Resume carries it on.", ("id", quest), ("pause", pause.Id),
                        ("context", "quest"));
            default:
                throw Refusals.Because(
                    Refusals.QuestNotHeld,
                    $"#{quest} is neither parked nor held by your stop on this machine, so there is nothing to try again.",
                    ("id", quest));
        }
    }

    /// <summary>
    /// *Show it again* on the strip's review chip (REVIEWENV1d, D154 point 5; the review environment design §3.3): the set-up
    /// step's newest set-up served to its tab again, the tab opened where the person closed it and brought forward where the
    /// set-up left it, with no session and no model. Refused in the driver's words wherever it would show anything but that build
    /// at the rule's address. Answers the state, with what was said. The verdict is never a route here: it is the person's alone,
    /// beside the browser and never inside it (REVIEWENV1g). A tab served again is what every window's chip reads off the
    /// state (<c>inReview</c>'s <c>served</c>), so it declares the state (STATEHUB1h).
    /// </summary>
    [DriverRoute("SHOW_REVIEW_AGAIN")]
    [Publishes("SHOW_REVIEW_AGAIN", StateScopes.Driver)]
    private async Task<object?> ShowReviewAgainAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var quest = PayloadHelper.GetRequiredValue<string>(request.Payload, "quest").Trim().TrimStart('#');
        var desk = _loop.Reviews
                   ?? throw new DriverException("this window carries no browser to show a review in, so nothing was shown again.");
        var service = _loop.Service ?? throw NotReady();
        var (ok, message) = await desk.ShowAgainAsync(service, DriverConfig.Load(_loop.ConfigPath), quest, cancellationToken);
        if (!ok) throw new DriverException(message);
        return State(_loop, shownAgain: new { Quest = quest, Message = message });
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
    // in the driver's words; a circle with no remote is its refusal, mapped above. A pass can move where each circle stands,
    // the quests and sessions it fed, and the registry, and a wall it hit is answered as its report (STATEHUB1d, D169).
    [DriverRoute("SYNC_NOW")]
    [Publishes("SYNC_NOW", StateScopes.Sessions | StateScopes.Quests | StateScopes.Sync | StateScopes.Registry)]
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
    /// <param name="reviewed">What a review's procedure look found (REVIEWENV1a), on the answer to <c>SET_REVIEW</c> alone.</param>
    /// <param name="shownAgain">What *Show it again* said (REVIEWENV1d), on the answer to <c>SHOW_REVIEW_AGAIN</c> alone.</param>
    /// <remarks>
    /// A read of the loop alone, so the domains whose standing choices answer with it (Across, Lines) reach it by name over
    /// their loop, holding no other domain's class (PART10d).
    /// </remarks>
    internal static object State(DriverLoop loop, object? retried = null, object? reviewed = null, object? shownAgain = null)
    {
        var config = DriverConfig.Load(loop.ConfigPath);
        return new
        {
            Retried = retried,
            Reviewed = reviewed,
            ShownAgain = shownAgain,
            // Whether the loop's service is up (LOOK2a): until it is, every route that reads it refuses *still coming up*,
            // so the status bar says starting rather than ready, which this file alone would claim.
            Ready = loop.Service is not null,
            loop.ConfigPath,
            // Where this machine's Daoris lives (D63), and what establishing it did this start — a
            // machine-local path, answered only over this bridge, like every path here.
            loop.Home,
            loop.HomeNotice,
            // How that home stands to the account's DAORIS_HOME (LEFT2): what the page's hint says of a terminal.
            loop.HomeAccount,
            loop.HostNotice,
            config.Drivable,
            config.Holds,
            config.Trees,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            config.Notify,
            config.Strikes,
            // The cool-off a limit naming no time takes, in minutes (TOOL4g): the file's, or the hour when it sets none.
            CoolOff = (int)config.CoolOff.TotalMinutes,
            config.Forgiven,
            // The stops the person released (SESSUX1b), the quest against the session, as the terminal lists them.
            config.Released,
            // 🔴 Reported so the page can show it, and modelled on the record so no toggle deletes it.
            // Off is "" on the wire, never null: the bridge leaves a null out, and the page tells a
            // shell older than the intake by this field's absence (AGT6, seen on the window).
            IntakeAdapter = config.IntakeAdapter ?? "",
            // Off is "" on the wire for the intake's reason: a shell older than Ask Daoris sends no field.
            HelperAdapter = config.HelperAdapter ?? "",
            // Ask Daoris's access mode as the person chose it (HELPMODE1a2): `restricted` or `full`, what its next start reads. A
            // conversation's own, the mode its start got, is its record's (`HELP_CONVERSATIONS`' `mode`).
            config.HelpMode,
            // The lines as set (WSR2), as rows rather than an object's keys: a key policy on the
            // bridge would respell a repository's name.
            Lines = config.Lines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Branch = p.Value }).ToArray(),
            WorkspaceLines = config.WorkspaceLines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Branch = p.Value }).ToArray(),
            // The landing rules as set (WSR1), as rows for the same reason, with the ticket's key and the squash (DOORS1b).
            Landings = config.Landings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new
                {
                    Repository = p.Key, p.Value.Form, p.Value.Pattern, p.Value.Tidy, p.Value.Plugin, p.Value.AutoAccept, p.Value.Key,
                    p.Value.Squash,
                }).ToArray(),
            WorkspaceLandings = config.WorkspaceLandings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new
                {
                    Workspace = p.Key, p.Value.Form, p.Value.Pattern, p.Value.Tidy, p.Value.Plugin, p.Value.AutoAccept, p.Value.Key,
                    p.Value.Squash,
                }).ToArray(),
            // Reading and writing across as set (D107), as rows for the same reason.
            ReadAcross = config.ReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Read = p.Value }).ToArray(),
            WorkspaceReadAcross = config.WorkspaceReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Read = p.Value }).ToArray(),
            WriteAcross = config.WriteAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, To = p.Value }).ToArray(),
            // Each repository's standing answer (KNOWUSE1b), as rows for the same reason; `at` in UTC, or null where unknown.
            Standing = config.Standing.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, p.Value.Says, p.Value.At }).ToArray(),
            // This machine's heavy gates for each repository that marks none (HEAVYRUN1b2b), as rows for the same reason, each list in
            // the order its gates run; a gate's folder left out where it runs in the checkout itself.
            HeavyGates = config.HeavyGates.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new
                {
                    Repository = p.Key,
                    Gates = p.Value.Select(gate => new { gate.Name, gate.Run, Cwd = gate.WorkingDirectory }).ToArray(),
                }).ToArray(),
            // The session languages as set (LANG1c), as rows for the same reason, and the closed table each screen names its
            // options from, so adding a language is a row in the driver's table and needs no window catalogue.
            Languages = config.Languages.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Language = p.Value }).ToArray(),
            WorkspaceLanguages = config.WorkspaceLanguages.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Language = p.Value }).ToArray(),
            LanguageTable = SessionLanguages.Table.Select(row => new { row.Code, row.Name }).ToArray(),
            // The review rules as set (REVIEWENV1a), as rows for the same reason: a repository's may be none here.
            Reviews = config.Reviews.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.ReviewWire(p.Value) }).ToArray(),
            WorkspaceReviews = config.WorkspaceReviews.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.ReviewWire(p.Value) }).ToArray(),
            // The second-opinion rules as set (XAGENT1a), as rows for the same reason, each naming the working agent's family
            // over the loop's agents, a plugin's among them (XAGENT1b3).
            Opinions = config.Opinions.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.OpinionWire(p.Value, config.Adapter, loop.Harnesses.Adapters) }).ToArray(),
            WorkspaceOpinions = config.WorkspaceOpinions.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.OpinionWire(p.Value, config.Adapter, loop.Harnesses.Adapters) }).ToArray(),
            // The helpers and commit rules as set (DOORS1b), as rows for the same reason: a workspace's Defaults reads its own here.
            Helpers = config.Helpers.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.HelperWire(p.Value) }).ToArray(),
            WorkspaceHelpers = config.WorkspaceHelpers.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.HelperWire(p.Value) }).ToArray(),
            Commits = config.Commits.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.CommitWire(p.Value) }).ToArray(),
            WorkspaceCommits = config.WorkspaceCommits.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.CommitWire(p.Value) }).ToArray(),
            // The access setups as set (AUTONOMY1a), as rows for the same reason, and this machine's, none set being no pause: a
            // workspace's Defaults and Settings read their own here, a repository's setup reads what it resolves to off LINES.
            Access = config.Access.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Setting = LinesRoutes.AccessWire(p.Value) }).ToArray(),
            WorkspaceAccess = config.WorkspaceAccess.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Setting = LinesRoutes.AccessWire(p.Value) }).ToArray(),
            MachineAccess = LinesRoutes.AccessWire(config.MachineAccess),
            Running = loop.Processes.Running,
            // Who is driving Daoris's browser (BRW8): the running sessions handed a server that drives it.
            DrivingBrowser = loop.Processes.DrivingBrowser,
            // The set-ups waiting for the person here (REVIEWENV1d, design §3.3), each with whether Daoris serves its tab now: the
            // strip's chip beside the browser's door. Left out where the loop carries no browser, which shows nothing.
            InReview = loop.Reviews?.Waiting
                .Select(row => new { row.Quest, row.Title, row.Environment, row.Look, row.Shows, row.Served })
                .ToArray(),
        };
    }
}
