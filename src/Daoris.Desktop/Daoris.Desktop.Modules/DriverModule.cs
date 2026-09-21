using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The session-control surface's host half (D46 §6): the page asks, this answers — and every mutation
/// is an edit to `driver.json` plus a nudge, because the file is the person's standing choices and the
/// loop already re-reads it every tick. Stopping a session is the one control that touches a process,
/// through the shared registry, and the driver records the end as the person's.
/// </summary>
public sealed class DriverModule : ModuleBase
{
    private readonly IEventBus _events;
    private readonly DriverLoop _loop;

    /// <remarks>
    /// The bus is held as well as handed to the base: this module both ANSWERS requests and, since
    /// D49 §3, raises one of its own — a conversation ending is news the page wants without asking.
    /// </remarks>
    public DriverModule(IEventBus events, DriverLoop loop) : base(events: events)
    {
        _events = events;
        _loop = loop;
    }

    public override string ModuleName => "DAORIS.DRIVER";

    /// <summary>
    /// Route, and let the driver's own refusals reach the person.
    /// </summary>
    /// <remarks>
    /// <b>An unhandled exception becomes a generic `UNKNOWN_ERROR` carrying only its TYPE</b>, so every
    /// sentence `DriverException` was written to deliver — "unknown adapter 'x' — one of: …", "that
    /// harness declares no installer" — used to be dropped on the floor and shown as a bare failure.
    /// Mapped once, here, rather than at each throw site: the driver library is where those sentences
    /// live, and a per-site wrapping is a list somebody eventually forgets to append to.
    /// </remarks>
    protected override async Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await RouteAsync(request, cancellationToken);
        }
        catch (DriverException error)
        {
            throw Refusals.Because(Refusals.DriverRefused, error.Message, ("message", error.Message));
        }
    }

    private async Task<object?> RouteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "STATE":
                return State();

            case "SET_DRIVABLE":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var drivable = PayloadHelper.GetRequiredValue<bool>(request.Payload, "drivable");
                Change(config => config.WithDrivable(repository, drivable));
                return State();
            }

            case "SET_HOLD":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var held = PayloadHelper.GetRequiredValue<bool>(request.Payload, "held");
                Change(config => config.WithHold(repository, held));
                return State();
            }

            // Session trees (D51): the desktop's half of the standing opt-in, over the same file
            // `daoris driver trees <repo> on|off` edits — two editors, one truth (D50).
            case "SET_TREES":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var ownTree = PayloadHelper.GetRequiredValue<bool>(request.Payload, "ownTree");
                Change(config => config.WithTrees(repository, ownTree));
                return State();
            }

            // Whether this machine interrupts the person at all (SURF5b). The same file
            // `daoris driver notify on|off` edits — one truth, two doors (D50).
            case "SET_NOTIFY":
            {
                var notify = PayloadHelper.GetRequiredValue<bool>(request.Payload, "notify");
                Change(config => config.WithNotify(notify));
                return State();
            }

            // The console's backlog (D49 §2): what this session has said, or what it has said since
            // the page last heard. Live lines arrive as `SESSION_OUTPUT` events; this is how a page
            // that just opened catches up, and how one that missed a batch closes the gap — the
            // sequence numbers are the driver's, so neither side has to remember the other.
            case "TAIL_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // Absent means "everything you have": a page opening a drawer has seen nothing, and
                // making it say so explicitly would be ceremony with a wrong default available.
                var after = request.Payload is { } payload
                    && payload.TryGetProperty("after", out var seen)
                    && seen.ValueKind == JsonValueKind.Number
                        ? seen.GetInt64()
                        : 0;
                var tail = _loop.Output.Tail(id, after);
                return new
                {
                    Session = id,
                    Lines = tail.Lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
                    tail.Sequence,
                    tail.Live,
                    tail.Dropped,
                };
            }

            // A conversation in a repository (D49 §3). The record is the service's and the lock is the
            // ledger's; what only this side can do is put a harness behind it — a process on this
            // machine, which never leaves it (D46 §7).
            case "START_CHAT":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                if (_loop.Chat is not { } chat)
                {
                    throw Refusals.Because(
                        Refusals.DriverNotReady,
                        "the driver is still coming up — its service is not answering yet. A moment.");
                }

                var config = DriverConfig.Load(_loop.ConfigPath);
                var adapter = request.Payload is { } payload
                    && payload.TryGetProperty("adapter", out var named)
                    && named.ValueKind == JsonValueKind.String
                        ? named.GetString()!
                        : config.Adapter;

                var start = await chat.StartAsync(
                    repository, adapter, config,
                    // The end of a conversation is news the page wants without asking: the drawer is
                    // probably open, and a record that moved silently reads as one that hung.
                    onEnded: (session, state) =>
                        _events.EmitAsync("DAORIS", "SESSION_ENDED", new { Session = session, State = state }),
                    // The per-session picker (D49 §4). Absent takes the workspace's default, then the
                    // machine's, then the harness's own configuration home.
                    profile: Optional(request, "profile"),
                    // The per-conversation tree choice (D51). Absent falls back to the repository's
                    // standing opt-in, which the runner reads from the same config.
                    ownTree: request.Payload is { } chosen
                        && chosen.TryGetProperty("ownTree", out var tree)
                        && tree.ValueKind == JsonValueKind.True,
                    ct: cancellationToken);

                _loop.Nudge();
                return new { start.SessionId, start.Message };
            }

            // The person's half of the turn-taking. False is an answer — the session ended while they
            // were typing — and never an error.
            case "SESSION_INPUT":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var text = PayloadHelper.GetRequiredValue<string>(request.Payload, "text");
                return new { Sent = _loop.Chat?.Say(id, text) ?? false };
            }

            // Finishing a conversation rather than cutting it off: the harness gets end-of-input, says
            // what it was going to say, and exits on its own. `STOP_SESSION` is the other verb.
            case "END_CHAT":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                return new { Ended = _loop.Processes.CloseInput(id) };
            }

            case "STOP_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // False is an answer, not an error: the session already finished, and its record says how.
                var stopped = _loop.Processes.Stop(id);
                _loop.Nudge();
                return new { Stopped = stopped };
            }

            // The person's answer to a session parked at a checkpoint (D52 §4). It goes through the
            // DRIVER rather than straight to the service because the two halves must move together:
            // this machine lets the process go, and only then does the record say it ended.
            case "RESOLVE_SESSION":
                return await ResolveAsync(request, cancellationToken);

            // What a session actually did (SURF6, design §5). The evidence string says that work
            // happened; this says what it was. Desktop-only by construction, like the console: it is
            // read off a checkout, and only the machine holding one can answer at all.
            case "SESSION_DIFF":
                return await DiffAsync(request, cancellationToken);

            // The two acts on a reviewed session (SURF6b, D51 rules 6–7). Both are the PERSON's —
            // nothing merges itself and nothing deletes itself — so both are their own route rather
            // than anything the diff route could do as a side effect.
            case "MERGE_SESSION_TREE":
            case "DISCARD_SESSION_TREE":
                return await ActOnTreeAsync(request, cancellationToken);

            // This machine's harnesses, and the accounts they run as (D49 §4). Detection is free and
            // read-only — it asks each tool its own version and each profile's own login state — so
            // the page may ask whenever it likes; `refresh` is the person pressing "look again".
            case "HARNESSES":
            {
                var refresh = request.Payload is { } payload
                    && payload.TryGetProperty("refresh", out var again)
                    && again.ValueKind == JsonValueKind.True;

                var config = DriverConfig.Load(_loop.ConfigPath);
                var roster = await _loop.Harnesses.RosterAsync(config, refresh, cancellationToken);
                var settings = _loop.Harnesses.Settings;

                return new
                {
                    _loop.Harnesses.SettingsPath,
                    Adapter = config.Adapter,
                    Harnesses = roster.Select(report => new
                    {
                        Harness = report.Adapter,
                        report.Present,
                        report.Version,
                        report.Problem,
                        report.MachineDefault,
                        // The managed toolchain (TOOL2/D57). `Pinned` is what the machine asked for
                        // and `Managed` is what is actually there — they differ exactly when a pin
                        // names a version nobody installed, which the surface must say rather than
                        // imply the pin is in force.
                        Pinned = settings.ResolveVersion(report.Adapter, null, null),
                        Managed = HarnessSettings.ManagedBinary(
                            _loop.Harnesses.Home, report.Adapter,
                            settings.ResolveVersion(report.Adapter, null, null),
                            _loop.Harnesses.Toolchain(report.Adapter)?.Binary ?? []),
                        // Whether this harness CAN be pinned at all. A harness that declares no
                        // package has no version for Daoris to fetch, and a surface offering the
                        // control anyway would be a button whose only outcome is a refusal.
                        Pinnable = _loop.Harnesses.Toolchain(report.Adapter)?.Package is { Length: > 0 },
                        // The profile HOME is a machine path, and this bridge is the one surface
                        // allowed to carry one (D47 §4) — the page renders it so a person can find
                        // the directory they were told Daoris owns.
                        Profiles = report.Profiles.Select(profile => new
                        {
                            profile.Name,
                            profile.Home,
                            Login = profile.Login.ToString().ToLowerInvariant(),
                        }).ToArray(),
                    }).ToArray(),
                };
            }

            // The person's explicit action on a harness (D49 §4): its own installer, its own updater,
            // its own login flow. Never automatic, never mid-session, never unasked — and streamed
            // line by line through the console, because it is a process like any other.
            case "HARNESS_ACTION":
            {
                var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
                var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
                var config = DriverConfig.Load(_loop.ConfigPath);
                // A DriverException, so it travels the same way `Toolchain` already refuses an adapter
                // name it does not know: one mapping, one shape, and the driver's own sentence intact.
                var toolchain = _loop.Harnesses.Toolchain(harness)
                    ?? throw new DriverException(
                        $"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");

                var command = config.Commands.GetValueOrDefault(harness);
                var stream = Relay(harness, action);

                var code = action switch
                {
                    "install" => await HarnessActions.InstallAsync(toolchain, stream, cancellationToken),
                    "update" => await HarnessActions.UpdateAsync(toolchain, command, stream, cancellationToken),
                    "login" => await HarnessActions.LoginAsync(
                        toolchain, command,
                        HarnessSettings.ProfileHome(
                            _loop.Harnesses.Home, harness,
                            Optional(request, "profile")
                            ?? _loop.Harnesses.Settings.Resolve(harness, null, null)
                            ?? "default"),
                        stream, cancellationToken),
                    // The managed toolchain (TOOL2/D57) — the desktop's half of
                    // `daoris harness pin|unpin`, over the same file.
                    "pin" => await PinAsync(harness, toolchain, stream, request, cancellationToken),
                    "unpin" => Unpin(harness),
                    _ => throw Refusals.Because(
                        Refusals.HarnessActionUnknown,
                        $"unknown harness action '{action}' — one of: install, update, login, pin, unpin",
                        ("action", action)),
                };

                // Whatever it did, what this machine HAS has probably changed — so the next question
                // asks the tool again rather than answering from before.
                await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
                return new { Harness = harness, Action = action, ExitCode = code };
            }

            // "Look now": a person who just published a quest should not watch a poll countdown.
            case "NUDGE":
                _loop.Nudge();
                // The one verb with nothing to answer — and `await` keeps this method honestly async.
                await Task.CompletedTask;
                return null;

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>An optional string on a request — absent and blank are the same answer: unstated.</summary>
    /// <summary>
    /// The three moves a person may make on a parked session — `completed`, `declined`, `stopped`,
    /// each carrying what they want the record to say (design §4).
    /// </summary>
    /// <remarks>
    /// <para><b>Narrowed here, not re-judged.</b> The ledger allows a fourth from `awaiting-person`
    /// — back to `working` — and that one is the DRIVER's observation, not a button: a person
    /// resumes a conversation by answering it, which is the composer's job. Narrowing the person's
    /// verbs is a surface rule and belongs on the surface; everything about whether the move is
    /// legal at all stays the ledger's (D36), and its refusal reaches the person verbatim.</para>
    ///
    /// <para><b>The process goes first.</b> A record that says `completed` while this machine still
    /// holds the process is exactly the lie the observed lifecycle exists to prevent — so the
    /// process is let go, and the record moves after. `Stop` answering false is not an error: the
    /// common case is a session parked with nothing of ours still running.</para>
    ///
    /// <para><b>Declining needs a reason</b>, the same rule the quest door already holds and for the
    /// same reason: the note is the part whoever reads the record can act on.</para>
    ///
    /// <para><b>A move with no note still writes one.</b> The store keeps the previous note when a
    /// move carries none (deliberately — a later move must not erase what an earlier one recorded),
    /// so a session finished at a checkpoint would otherwise read <i>reached completed</i> beside
    /// the analysis it was parked with, which says the opposite of what happened. The stamped
    /// sentence also carries the one fact the state cannot: `completed` normally means the session
    /// closed its own quest, and this one means a person decided it was done.</para>
    /// </remarks>
    /// <summary>
    /// One session's landed work, as a diff (SURF6).
    /// </summary>
    /// <remarks>
    /// <para><b>Read-only, and that is the whole of this route.</b> It runs `git diff` in the tree the
    /// record names and returns what git said. Nothing here writes, merges or removes anything — the
    /// acts that do are the person's and are their own routes, so a surface that only shows the work
    /// cannot accidentally change it.</para>
    ///
    /// <para><b>Unreviewable is INFORMATION, not a failure</b> (D48 §6's class): a record mirrored
    /// from another machine names no tree here, a record made before the base commit was written has
    /// no range, and a tree that has been discarded is gone. None of those is a fault, and each has a
    /// different sentence, so the page can say which.</para>
    /// </remarks>
    private async Task<object?> DiffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        if (_loop.Service is not { } service)
        {
            throw Refusals.Because(
                Refusals.DriverNotReady,
                "the driver is still coming up — its service is not answering yet. A moment.");
        }

        var (tree, baseCommit) = await service.SessionGroundAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to diff. That is what a record looks like when it travelled from the machine "
                + "that did the work.",
                ("session", id));
        }

        if (string.IsNullOrWhiteSpace(baseCommit))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record does not say which commit its tree stood at when it began, so "
                + "there is no range to measure. Records made before Daoris started writing that down "
                + "keep their evidence line and cannot gain a diff.",
                ("session", id));
        }

        var diff = await WorkingTree.DiffAsync(tree, baseCommit, cancellationToken);
        if (diff is null)
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "git could not read that range where the session ran — the tree has moved, been "
                + "discarded, or no longer holds the commit it started from.",
                ("session", id));
        }

        return new
        {
            Session = id,
            diff.Base,
            diff.Truncated,
            Files = diff.Files.Select(file => new
            {
                file.Path,
                file.Status,
                file.Added,
                file.Removed,
                file.Patch,
            }).ToArray(),
        };
    }

    /// <summary>
    /// Merge a session's tree into the canonical line, or discard it (SURF6b, D51 rules 6–7).
    /// </summary>
    /// <remarks>
    /// <para><b>A refusal here is an ANSWER, not an error</b> — the checkout is busy, the tree holds
    /// work nobody merged, there is nothing to merge. Each comes back as `{ done: false, message }`
    /// with the sentence the tree layer wrote, exactly as `START_CHAT` does, because the person's next
    /// move is different for each and a code would flatten them into one.</para>
    ///
    /// <para><b>Discard needs `force` said out loud.</b> The unforced call is what produces the
    /// refusal that names what would be lost, so the page asks, shows that sentence, and only then
    /// sends `force`. Destroying work is never a side effect of tidying (D51 rule 7).</para>
    /// </remarks>
    private async Task<object?> ActOnTreeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var merging = request.Type == "MERGE_SESSION_TREE";
        var force = request.Payload is { } payload
            && payload.TryGetProperty("force", out var meant)
            && meant.ValueKind == JsonValueKind.True;

        if (_loop.Service is not { } service)
        {
            throw Refusals.Because(
                Refusals.DriverNotReady,
                "the driver is still coming up — its service is not answering yet. A moment.");
        }

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to merge or discard.",
                ("session", id));
        }

        // The same home the loop derives for the chat runner and the watch: the directory holding
        // `driver.json`. Derived rather than stored twice, so one answer cannot drift from the other.
        var trees = new SessionTrees(Path.GetDirectoryName(Path.GetFullPath(_loop.ConfigPath))!);
        if (merging)
        {
            var merged = await trees.MergeAsync(tree, cancellationToken);
            // The rail's states do not change, but the tree's mergeability does — and the review the
            // person is looking at was computed before this.
            _loop.Nudge();
            return new { Session = id, Done = merged.Merged, merged.Message };
        }

        var removal = await trees.RemoveAsync(tree, force, cancellationToken);
        _loop.Nudge();
        return new { Session = id, Done = removal.Removed, removal.Message };
    }

    private async Task<object?> ResolveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var state = PayloadHelper.GetRequiredValue<string>(request.Payload, "state");
        var note = Optional(request, "note");

        if (state is not ("completed" or "declined" or "stopped"))
        {
            throw Refusals.Because(
                Refusals.SessionMoveNotYours, PersonMoves,
                ("state", state), ("moves", "completed, declined, stopped"));
        }

        if (state == "declined" && note is null)
        {
            throw Refusals.Because(Refusals.SessionDeclineNeedsReason, DeclineNeedsReason);
        }

        if (_loop.Service is not { } service)
        {
            throw Refusals.Because(
                Refusals.DriverNotReady,
                "the driver is still coming up — its service is not answering yet. A moment.");
        }

        // False is the common case, not a failure: a parked session usually has no process here.
        _loop.Processes.Stop(id);
        var message = await service.AdvanceAsync(
            id, state, note ?? ByThePerson(state), ct: cancellationToken);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";

    /// <summary>What the record says when the person wrote nothing — never translated: it is data.</summary>
    private static string ByThePerson(string state) => state switch
    {
        "completed" => "The person finished this at a checkpoint.",
        "stopped" => "The person stopped this at a checkpoint.",
        _ => "The person moved this at a checkpoint.",
    };

    private static string? Optional(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>
    /// A harness action's output, relayed as it happens — the same console the session drawer already
    /// renders (D49 §2), because an install that printed nothing until it finished is indistinguishable
    /// from one that hung.
    /// </summary>
    /// <remarks>
    /// <para>Keyed by `harness:action` rather than a session id: it is not a session, has no record,
    /// and must never look like one. The page subscribes to it exactly as it subscribes to a
    /// session's.</para>
    ///
    /// <para>The sequence still counts, because the page's merge rule is "drop anything already
    /// seen" — every line arriving as number 0 would render as one line repeatedly overwritten. There
    /// is no backlog to catch up on here (nothing buffers a harness action), so the counter starts at
    /// 1 and only has to be monotonic.</para>
    /// </remarks>
    private Action<string> Relay(string harness, string action)
    {
        var sequence = 0L;
        return line => _events.EmitAsync("DAORIS", "SESSION_OUTPUT", new
        {
            Session = $"{harness}:{action}",
            Lines = new[] { new { Sequence = Interlocked.Increment(ref sequence), Text = line } },
        });
    }

    /// <summary>
    /// Install a version into the directory Daoris owns, and pin to it — <b>in that order</b>
    /// (TOOL2/D57).
    /// </summary>
    /// <remarks>
    /// 🔴 The pin is written only after the install succeeded. A pin naming a version that is not
    /// there refuses every spawn, so writing it first would turn a failed download into a machine
    /// that cannot start a session.
    /// </remarks>
    private async Task<int> PinAsync(
        string harness, HarnessToolchain toolchain, Action<string> stream, IpcRequest request,
        CancellationToken ct)
    {
        var version = PayloadHelper.GetRequiredValue<string>(request.Payload, "version");
        var code = await HarnessActions.PinAsync(
            toolchain, _loop.Harnesses.Home, harness, version, stream, ct);

        if (code == 0) _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath);
        return code;
    }

    /// <summary>Back to `PATH`. Nothing is deleted — re-pinning that version needs no download.</summary>
    private int Unpin(string harness)
    {
        _loop.Harnesses.Settings.WithVersion(harness, null).Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    private object State()
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        return new
        {
            _loop.ConfigPath,
            config.Drivable,
            config.Holds,
            config.Trees,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            config.Notify,
            Running = _loop.Processes.Running,
        };
    }

    private void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(_loop.ConfigPath)).Save(_loop.ConfigPath);
        _loop.Nudge();
    }
}
