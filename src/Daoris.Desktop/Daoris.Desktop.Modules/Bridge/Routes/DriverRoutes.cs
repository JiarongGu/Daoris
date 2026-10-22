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
    /// Which checks a repository's sessions run under Current (FABRIC-PIPELINE1b, D192 point 4), Setup's half of <c>daoris-driver
    /// checks</c> over the same key (D50): <c>repository</c>, and <c>checks</c>, <c>light</c> or <c>heavy-at-end</c> in any case, or none
    /// or null to clear it, which is today's. Judged by <see cref="SessionChecks.Apply"/> against the file before anything is written, so a
    /// value that is no choice is refused in its words. The person's standing choice: light lowers their part, so no proposal names it.
    /// </summary>
    [DriverRoute("SET_SESSION_CHECKS")]
    [Publishes("SET_SESSION_CHECKS", StateScopes.Driver)]
    private object? SetSessionChecks(IpcRequest request)
    {
        var repository = Optional(request, "repository") ?? "";
        var checks = Optional(request, "checks");
        // The refusals come first, before anything is written.
        SessionChecks.Apply(DriverConfig.Load(_loop.ConfigPath), repository, checks);
        _context.Change(config => config.WithSessionChecks(repository, checks));
        return State(_loop);
    }

    /// <summary>
    /// Whether a repository's own <c>daoris.gates.json</c> wins over this machine's heavy gates (HEAVYRUN1b2c, D180's HEAVYRUN1b2c note):
    /// <c>{ repository, marks, names, refusal? }</c>, <c>marks</c> true where the file marks some (their names), false where it marks none or
    /// there is no file, null with <c>refusal</c> where it cannot be told (a file that does not read, or no checkout here). A read of one
    /// file in the checkout the registry holds, the first of the name that is on this disk as <c>RECOVERY_DROP</c> picks, with no git; it
    /// never resolves a run, which throws where there is no file and no mapping. The page judges nothing itself.
    /// </summary>
    [DriverRoute("HEAVY_MARKS")]
    private async Task<object?> HeavyMarksAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = (Optional(request, "repository") ?? "").Trim();
        var service = _loop.Service ?? throw NotReady();
        var root = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken), repository, null)
            .Select(row => row.Root).FirstOrDefault(path => Directory.Exists(path));
        if (root is null)
        {
            return new
            {
                repository, marks = (bool?)null, names = Array.Empty<string>(), light = Array.Empty<string>(),
                refusal = $"`{repository}` has no checkout on this machine to read.",
            };
        }

        // FABRIC-PIPELINE1b: and its light gates, those it declares and does not mark heavy, which Setup's sessions' checks name.
        var marks = HeavyGates.Marks(root);
        return new { repository, marks = marks.Marks, names = marks.Names, light = marks.Light, refusal = marks.Refusal };
    }

    /// <summary>
    /// Who holds a repository's heavy-run slot, at which gate, and who waits (HEAVYSLOT1, the UI/UX design §3.5):
    /// <c>{ repository, holder?, waiters, refusal? }</c>, the holder <c>{ session?, gates, since, now?: { gate, at, since }, failed?: { gate,
    /// exit } }</c> and each waiter <c>{ session?, since }</c>, the longest waiting first; a run with no <c>session</c> is one from a
    /// terminal. Read from the records the runs keep beside the slot (<see cref="HeavySlot.Look"/>), each only where its process still
    /// runs, never from a run's output; the slot keyed by the checkout the registry holds, as <c>HEAVY_MARKS</c> picks it, and
    /// <c>refusal</c> where there is none here or git's files do not say its repository. A read: it declares nothing.
    /// </summary>
    [DriverRoute("HEAVY_SLOT")]
    private async Task<object?> HeavySlotAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = (Optional(request, "repository") ?? "").Trim();
        var service = _loop.Service ?? throw NotReady();
        var registry = await service.RegistryAsync(cancellationToken);
        // Files and processes, off the window's thread (UIFREEZE1).
        return await Task.Run(() => HeavySlotOf(_loop.Home, repository, LinesRoutes.Checkouts(registry, repository, null).Select(row => row.Root)), cancellationToken);
    }

    /// <summary>The slot of the first of <paramref name="roots"/> on this disk, as <c>HEAVY_SLOT</c> answers it.</summary>
    internal static object HeavySlotOf(string home, string repository, IEnumerable<string> roots, Func<int, long, bool>? alive = null)
    {
        var root = roots.FirstOrDefault(Directory.Exists);
        if (root is null)
        {
            return new { repository, waiters = Array.Empty<object>(), refusal = $"`{repository}` has no checkout on this machine to read." };
        }

        if (HeavySlot.KeyOf(root) is not { } key)
        {
            return new { repository, waiters = Array.Empty<object>(), refusal = $"git's files in `{repository}`'s checkout do not say which repository it is." };
        }

        var look = HeavySlot.Look(home, key, alive);
        var holder = look.Holder is { } held
            ? new
            {
                session = held.Session,
                gates = held.Gates,
                since = held.Since,
                now = held.Now is { } now ? new { gate = now.Gate, at = now.At, since = now.Since } : null,
                failed = held.Failed is { } failed ? new { gate = failed.Gate, exit = failed.Exit } : null,
            }
            : null;
        return new
        {
            repository,
            holder,
            waiters = look.Waiters.Select(waiter => new { session = waiter.Session, since = waiter.Since }).ToArray(),
        };
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

    // One kind's keep rule (KEEP1d, D195 point 2; the cleanup design §2.2, §4.4): the set-aside folders' days, `whole` or not, the
    // downloads' days or `never`, the logs' days, or a kind's default again; the same `keep` `daoris driver keep` edits, judged by the
    // one judgement (`KeepRules.Apply`) before anything is written, a value out of its bounds refused as KEEP_OUT_OF_RANGE. What the
    // Disk space card shows moves with it, so it declares the wiring with the state.
    [DriverRoute("SET_KEEP")]
    [Publishes("SET_KEEP", StateScopes.Driver | StateScopes.Wiring)]
    private object? SetKeep(IpcRequest request)
    {
        var whole = request.Payload is { } payload && payload.TryGetProperty("whole", out var said)
            ? said.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => (bool?)false, _ => null }
            : null;
        var set = new KeepSet(
            PayloadHelper.GetRequiredValue<string>(request.Payload, "kind"),
            Number(request, "days") is { } days ? (int)Math.Clamp(days, int.MinValue, int.MaxValue) : null,
            Flag(request, "never"),
            whole,
            Flag(request, "default"),
            // KEEP1f: the floor's size in GB, its share, or `off`, a size judged against half the Daoris folder's volume as read here.
            Number(request, "gb") is { } gb ? (int)Math.Clamp(gb, int.MinValue, int.MaxValue) : null,
            Number(request, "percent") is { } percent ? (int)Math.Clamp(percent, int.MinValue, int.MaxValue) : null,
            Flag(request, "off"));
        if (KeepRules.IsFloor(set.Kind) && set.Gb is not null) set = set with { Volume = SpaceVolumes.Read(DaorisHome.FolderOf(_loop.Home).Folder)?.Total };
        _context.Change(config => KeepRules.Apply(config, set));
        return State(_loop);
    }

    // How many sessions Daoris starts at once on this machine (CAPSET1, D130's CAPCOUNT1 note): the person's dial, the same
    // `cap` `daoris driver cap <n>` edits — one truth, two doors (D50). Less than one is the driver's refusal.
    [DriverRoute("SET_CAP")]
    [Publishes("SET_CAP", StateScopes.Driver)]
    private object? SetCap(IpcRequest request)
    {
        var cap = Number(request, "cap")
            ?? throw new DriverException(DriverConfig.CapRefusal);
        _context.Change(config => config.WithCap((int)Math.Clamp(cap, int.MinValue, int.MaxValue)));
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
    // `daoris-driver help mode` sets: one truth, two doors (D50). Each start reads it, and a conversation running under another ends
    // at its turn's end, its next words resuming it under the mode chosen (ACCESSSWAP1): nothing it runs with changes while it runs.
    // A mode it does not know, or none, is the driver's refusal, with nothing written.
    [DriverRoute("SET_HELP_MODE")]
    [Publishes("SET_HELP_MODE", StateScopes.Driver)]
    private object? SetHelpMode(IpcRequest request)
    {
        var mode = Optional(request, "mode")
            ?? throw new DriverException($"Ask Daoris's mode is `{HelpModes.Restricted}` or `{HelpModes.Full}`; the press named none.");
        _context.Change(config => config.WithHelpMode(mode));
        _loop.Chat?.HelpAccessChosen(HelpModes.Named(mode)!);
        return State(_loop);
    }

    // Restricted mode's configured access (HELPMODE1c2b, D183's note): the person's choice of one housekeeping operation, or since
    // HELPMODE1c3 one chosen setting, or since HELPMODE1c4 one checkout turned off or on by its repository, set on Ask Daoris's screen,
    // written to the home's own file. Each start reads it onto its allow-list, receipt and room, and each tool call
    // is judged by it then, so a revoke refuses the next call; a conversation running with another choice ends at its turn's end, its
    // next words resuming it with this one (ACCESSSWAP1). No proposal names it: a helper never widens its own access. A grant or an
    // operation this build does not act on is the driver's refusal, with nothing written.
    [DriverRoute("SET_HELP_GRANT")]
    [Publishes("SET_HELP_GRANT", StateScopes.Driver)]
    private object? SetHelpGrant(IpcRequest request)
    {
        var grant = Optional(request, "grant") ?? ConfiguredAccess.HousekeepingGrant;
        var operation = Optional(request, "operation")
            ?? throw new DriverException("A grant's press names the operation it chooses; this one named none.");
        var chosen = request.Payload is { } payload && payload.TryGetProperty("chosen", out var value) && value.ValueKind == JsonValueKind.True;
        ConfiguredAccess.Choose(_loop.Home, grant, operation, chosen);
        _loop.Chat?.HelpAccessChosen(DriverConfig.Load(_loop.ConfigPath).HelpMode);
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

        // 🔴 Never the own sign-in's file (POOL1a3, D197 point 3; Fable's F3): its hold offers no press, and one sent anyway is
        // refused before anything is written. The person trusts the folder with the tool itself, at their terminal.
        if (hold.Own)
        {
            throw Refusals.Because(
                Refusals.TrustOwnHome,
                "That folder is held for your own sign-in, outside Daoris's accounts, so Daoris writes nothing in its file: trust it "
                + "with the tool itself, at your terminal.",
                ("folder", hold.Folder));
        }

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
        // PUSH1f2a: the verdict's switch is the library's operation, which `daoris-driver driver retry` acting alone calls too; a
        // pause or a quest neither parked nor held is its coded refusal (ChoiceCodes), raised here by the module's one mapping.
        var decision = StandingChoices.Retry(quest, considered);
        _context.Change(decision.Change);
        return State(_loop, new { Quest = quest, decision.Did, decision.Session });
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
        var access = ConfiguredAccess.Read(loop.Home);
        // HELPMODE1c3: the browser's grant is its plugin's approval, drawn here read only from that record.
        var browser = HelpGrants.Read(loop.Home, PluginCatalog.Load(loop.Home, AdapterSet.Built().Names)).Browser;
        // HELPMODE1c4: the checkouts D107 lets agents outside read, from the machine's facts as last judged, never a read of its own;
        // none before the facts are judged, and the loop's next look pushes them.
        string[] readable = loop.Facts.Current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is { } whole
            ? [.. AcrossRules.Readable(config, whole.Snapshot.Repositories).Select(checkout => checkout.Repository)]
            : [];
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
            // Restricted mode's configured access as the person chose it (HELPMODE1c2b): each grant the driver serves by its id, with the
            // operations chosen, housekeeping's from the home's record, what the next start and every tool call read, and those it acts
            // on, so the page offers no other; and whether a conversation running here was handed another choice of it, which its next
            // turn takes (ACCESSSWAP1, the grant's *pending* mark), false once its agent has ended. HELPMODE1c3: the chosen settings
            // the same way; and the browser's, read only from its plugin's approval (`plugin-approvals.json`): `chosen` holds `reads`
            // and each write accepted, by its tool's name, once approved; `changed` an update since; `plugin` whose tab changes it, none
            // where no plugin here attaches to Daoris's browser; no `built`, so the page offers nothing to choose. HELPMODE1c4: the
            // checkouts, on by default: `built` each readable checkout here by its repository, `chosen` those the person has not turned off.
            HelpGrants = new[]
            {
                Grant(loop, ConfiguredAccess.HousekeepingGrant, access, ConfiguredAccess.HousekeepingBuilt),
                Grant(loop, ConfiguredAccess.SettingsGrant, access, ConfiguredAccess.SettingsBuilt),
                new HelpGrantRow(
                    ConfiguredAccess.CheckoutsGrant, [.. readable.Where(access.Reads)], readable,
                    loop.Chat?.GrantPending(ConfiguredAccess.CheckoutsGrant, access) == true, false, null),
                new HelpGrantRow(
                    "browser",
                    browser is { State: HelpGrants.Approved } ? ["reads", .. browser.Accepted] : [],
                    null, false, browser?.Changed == true, browser?.Plugin),
            },
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
            // FABRIC-GRAPH1d: the rules the file keeps that read as no rule, which the rows above skip, so Setup names them: why
            // (`object`, `form`, `refused`) and the driver's sentence.
            UnreadLandings = config.UnreadLandings("landings")
                .Select(rule => new { Repository = rule.Name, rule.Why, rule.Said }).ToArray(),
            WorkspaceUnreadLandings = config.UnreadLandings("workspaceLandings")
                .Select(rule => new { Workspace = rule.Name, rule.Why, rule.Said }).ToArray(),
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
            // Which checks each repository's sessions run under Current (FABRIC-PIPELINE1b), as rows for the same reason.
            SessionChecks = config.SessionChecks.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Checks = p.Value }).ToArray(),
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
            // The hand-over rules as set (MIXAGENT1c), as rows for the same reason: a workspace's Defaults reads its own here.
            HandOver = config.HandOver.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = new { p.Value.Mode } }).ToArray(),
            WorkspaceHandOver = config.WorkspaceHandOver.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = new { p.Value.Mode } }).ToArray(),
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
            // The Ask Daoris conversations running under another access than the one chosen (ACCESSSWAP1): each ends at its turn's end,
            // and the page keeps it in view so its next words resume it under the new access.
            HelpSwapping = loop.Chat?.HelpSwapping(config.HelpMode) ?? [],
            // The set-ups waiting for the person here (REVIEWENV1d, design §3.3), each with whether Daoris serves its tab now: the
            // strip's chip beside the browser's door. Left out where the loop carries no browser, which shows nothing.
            InReview = loop.Reviews?.Waiting
                .Select(row => new { row.Quest, row.Title, row.Environment, row.Look, row.Shows, row.Served })
                .ToArray(),
        };
    }

    /// <summary>A grant kept in the home's record, as <c>helpGrants</c> answers it: chosen, built, and pending a running conversation's next turn.</summary>
    private static HelpGrantRow Grant(DriverLoop loop, string grant, ConfiguredAccess access, IReadOnlyList<string> built) =>
        new(grant, access.Of(grant), built, loop.Chat?.GrantPending(grant, access) == true, false, null);

    /// <summary>
    /// One row of <c>helpGrants</c> (HELPMODE1c2b, HELPMODE1c3): the grant, what is chosen, the operations the driver acts on (none for one
    /// set elsewhere, the browser's), whether a running conversation has yet to take it, whether its plugin's update changed it since
    /// approval, and the plugin whose tab sets it.
    /// </summary>
    private sealed record HelpGrantRow(string Id, IReadOnlyList<string> Chosen, IReadOnlyList<string>? Built, bool Pending, bool Changed, string? Plugin);
}
