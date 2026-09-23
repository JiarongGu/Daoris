using System.Collections.Concurrent;
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

    // The harness action running now, by `harness:action` — one at a time by construction, and the
    // two things a screen may do to it while it runs: answer the prompt it printed, or stop it.
    private readonly ConcurrentDictionary<string, HarnessRun> _actions = new();

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

            // How many failed sessions park a quest (DRV6/D58), and the person's way back in. Both
            // edit what `daoris driver strikes|retry` edits — one truth, two doors (D50).
            case "SET_STRIKES":
            {
                var strikes = PayloadHelper.GetRequiredValue<int>(request.Payload, "strikes");
                Change(config => config.WithStrikes(strikes));
                return State();
            }

            // Which harness answers asks with an intake session (INT4b), or null for none — the same
            // file `daoris driver intake <adapter>|off` edits: one truth, two doors (D50).
            case "SET_INTAKE":
            {
                var adapter = Optional(request, "adapter");
                Change(config => config.WithIntake(adapter));
                return State();
            }

            case "RETRY_QUEST":
            {
                var quest = PayloadHelper.GetRequiredValue<string>(request.Payload, "quest");
                // Marked at the limit rather than erased, so the records still read true and the next
                // `strikes` failures park it again.
                Change(config => config.WithForgiven(quest, config.Strikes));
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
                        // Whether this harness CAN be pinned at all. A harness that declares neither
                        // a package nor a maker's channel (AGT2b) has no version for Daoris to fetch,
                        // and a surface offering the control anyway would be a button whose only
                        // outcome is a refusal.
                        Pinnable = _loop.Harnesses.Toolchain(report.Adapter) is { } pinnable
                            && (pinnable.Package is { Length: > 0 } || pinnable.Channel is { Length: > 0 }),
                        // Whether this door can run a sign-in at all — the same rule: a harness that
                        // declares no login flow gets no "Sign in" whose only outcome is a refusal.
                        SignsIn = _loop.Harnesses.Toolchain(report.Adapter)?.LoginArguments is { Count: > 0 },
                        // 🔴 Which TOOL's account this entry runs as, and which door it holds a
                        // session over. Both were already declared and neither reached the page,
                        // which is why the surface listed `claude-code` and `claude-code-acp` as two
                        // things a person has to have opinions about. They are one tool and one
                        // account; the second is a way in. The page groups on these two fields.
                        AccountOf = _loop.Harnesses.Toolchain(report.Adapter)?.AccountOf,
                        // What a person calls the tool, and whose it is (AGT1) — `dsh` meant nothing
                        // to the owner until it said.
                        _loop.Harnesses.Toolchain(report.Adapter)?.Product,
                        _loop.Harnesses.Toolchain(report.Adapter)?.Maker,
                        Wire = _loop.Harnesses.Wire(report.Adapter).ToString().ToLowerInvariant(),
                        // The plugin this harness came from (D64), or null for one this build carries
                        // — shown beside it, so a person knows which folder to look in.
                        Plugin = _loop.Harnesses.Adapters.DeclaredBy(report.Adapter),
                        // The profile HOME is a machine path, and this bridge is the one surface
                        // allowed to carry one (D47 §4) — the page renders it so a person can find
                        // the directory they were told Daoris owns.
                        Profiles = report.Profiles.Select(profile => new
                        {
                            profile.Name,
                            profile.Home,
                            Login = profile.Login.ToString().ToLowerInvariant(),
                            // Who is signed in there, by the tool's own answer (D66 §3) — the name
                            // a person knows the account by, where the directory's is `account-2`.
                            profile.Account,
                            // An account that is a key, by its handle only (AGT3). Never the key.
                            profile.Key,
                        }).ToArray(),
                        // Whether this agent takes an API key at all — the control is absent where
                        // it does not, by the rule `Pinnable` and `SignsIn` follow.
                        TakesKey = _loop.Harnesses.AccountToolchain(report.Adapter)?.KeyVariable is { Length: > 0 },
                        // 🔴 The account a person actually HAS — the tool's own configuration home —
                        // answered beside the profiles rather than left out, which read as "No
                        // accounts" to an owner who was logged in.
                        OwnLogin = report.OwnLogin.ToString().ToLowerInvariant(),
                        report.OwnAccount,
                        // Which circles run this harness as which account (D49 §4): the terminal
                        // could set it and the page could not even see it. A door's are its
                        // owner's (AGT7).
                        WorkspaceDefaults = settings.Workspaces
                            .Select(circle => (Workspace: circle.Key, Map: circle.Value,
                                Owner: _loop.Harnesses.Toolchain(report.Adapter)?.Owner(report.Adapter) ?? report.Adapter))
                            .Where(circle => circle.Map.TryGetValue(circle.Owner, out var chosen)
                                && !string.IsNullOrWhiteSpace(chosen))
                            .OrderBy(circle => circle.Workspace, StringComparer.Ordinal)
                            .Select(circle => new { circle.Workspace, Profile = circle.Map[circle.Owner] })
                            .ToArray(),
                    }).ToArray(),
                };
            }

            // What a driven start in each workspace would run on, and where each part came from
            // (MAP1b): the driver's own `SelectAsync`, read through `WiringAsync`, so the panel cannot
            // show a start the loop would not make. The page names the circles it shows; this answers
            // for those and no others. Names and versions only — no home, no binary, no key. Named
            // for what it answers rather than "wiring", which the page already calls the remotes map.
            case "STARTS":
            {
                var config = DriverConfig.Load(_loop.ConfigPath);
                var named = new List<string>();
                if (request.Payload is { } payload
                    && payload.TryGetProperty("workspaces", out var circles)
                    && circles.ValueKind == JsonValueKind.Array)
                {
                    named.AddRange(circles.EnumerateArray()
                        .Where(circle => circle.ValueKind == JsonValueKind.String)
                        .Select(circle => circle.GetString()!)
                        .Where(circle => circle.Length > 0));
                }

                var starts = new List<object>();
                foreach (var workspace in named.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
                {
                    var wiring = await _loop.Harnesses.WiringAsync(config.Adapter, config, workspace, cancellationToken);
                    starts.Add(new
                    {
                        // The one job the loop runs today: a driven session, which is also what a
                        // conversation started without a pick takes. The intake joins with INT4b.
                        Job = "work",
                        Workspace = workspace,
                        wiring.Adapter,
                        wiring.Owner,
                        _loop.Harnesses.Toolchain(wiring.Adapter)?.Product,
                        wiring.Profile,
                        ProfileFrom = wiring.ProfileFrom.ToString().ToLowerInvariant(),
                        wiring.Version,
                        VersionFrom = wiring.VersionFrom.ToString().ToLowerInvariant(),
                        wiring.Commanded,
                        wiring.Refusal,
                    });
                }

                return new { Adapter = config.Adapter, Starts = starts };
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
                var profile = Optional(request, "profile");
                // 🔴 Whose accounts an action on this door touches (AGT7): a door's accounts, defaults
                // and keys are its OWNER's. Its pin and its installer stay its own.
                var owner = toolchain.Owner(harness);

                // 🔴 An account that is an API key (AGT3, D67 §1). The key crosses this bridge once,
                // inward, and is answered only by its handle — here, on the roster, and in any event.
                if (action == "key-add")
                {
                    if (_loop.Harnesses.AccountToolchain(harness)?.KeyVariable is not { Length: > 0 })
                    {
                        throw new DriverException(
                            $"`{harness}` takes no API key from Daoris — sign in with its own login instead.");
                    }

                    var account = HarnessKeys.Add(
                        _loop.Harnesses.Home, owner, PayloadHelper.GetRequiredValue<string>(request.Payload, "key"));
                    await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
                    return new
                    {
                        Harness = harness, Action = action, ExitCode = 0, Profile = account,
                        Key = HarnessKeys.Handle(HarnessKeys.Of(_loop.Harnesses.Home, owner, account)!),
                    };
                }

                // The file edits answer at once, with the exit code.
                int? edited = action switch
                {
                    "unpin" => Unpin(harness),
                    // 🔴 The credential profiles, from a SCREEN (DEPLOY3). They existed only as
                    // `daoris agent profile add|remove|default`, so the Machine view could list a
                    // profile and log into one and never make one — D50 violated in the direction
                    // nothing tests, since the rule is written "whatever a screen can set, a
                    // terminal can" and the converse had no check.
                    //
                    // A sign-in stays the tool's: adding one MAKES A DIRECTORY and nothing else,
                    // and what lands inside it is the harness's own.
                    "profile-add" => ProfileAdd(owner, request),
                    "profile-remove" => ProfileRemove(owner, request, stream),
                    "profile-default" => ProfileDefault(owner, request),
                    "install" or "update" or "login" or "login-new" or "pin" => null,
                    _ => throw Refusals.Because(
                        Refusals.HarnessActionUnknown,
                        $"unknown agent action '{action}' — one of: install, update, login, login-new, "
                        + "key-add, pin, unpin, profile-add, profile-remove, profile-default",
                        ("action", action)),
                };
                if (edited is { } code)
                {
                    // Whatever it did, what this machine HAS has probably changed — so the next question
                    // asks the tool again rather than answering from before.
                    await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
                    return new { Harness = harness, Action = action, ExitCode = code };
                }

                // 🔴 A process action is answered when the process has STARTED, and its end is news
                // (HARNESS_ENDED) — the same way a conversation's ending is (D49 §3). It waits on a
                // network, or on a person in a browser, and a request that waited with it timed out
                // on the bridge at thirty seconds while the login ran on: the page closed its panel,
                // the row said nothing had changed, and the process kept waiting for a browser nobody
                // was told about (measured on the installed shell, 2026-09-23). While it runs the
                // page may answer it or stop it (HARNESS_INPUT, HARNESS_CANCEL).
                var key = $"{harness}:{action}";
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Action<HarnessRun> track = run =>
                {
                    _actions[key] = run;
                    started.TrySetResult();
                };
                // 🔴 Signing in to ANOTHER account (D66 §3): the account is made by the sign-in, not
                // named before it. It opens under the next free `account-N` — nobody knows whose it
                // is yet — and the tool's own answer names who, on the roster, once it ends.
                var fresh = action == "login-new"
                    ? HarnessSettings.NextAccount(_loop.Harnesses.Home, owner)
                    : null;
                var profileHome = HarnessSettings.ProfileHome(
                    _loop.Harnesses.Home, owner,
                    fresh ?? profile ?? _loop.Harnesses.Settings.Resolve(owner, null, null) ?? "default");
                Func<Task<int>> run = action switch
                {
                    "install" => () => HarnessActions.InstallAsync(toolchain, stream, CancellationToken.None, track),
                    "update" => () => HarnessActions.UpdateAsync(toolchain, command, stream, CancellationToken.None, track),
                    "login" => () => HarnessActions.LoginAsync(toolchain, command, profileHome, stream, CancellationToken.None, track),
                    "login-new" => () => SignInAsync(harness, fresh!, toolchain, command, profileHome, stream, config, track),
                    // The managed toolchain (TOOL2/D57) — the desktop's half of
                    // `daoris agent pin|unpin`, over the same file.
                    _ => () => PinAsync(harness, toolchain, stream, request, CancellationToken.None, track),
                };
                var work = RunActionAsync(key, harness, action, fresh ?? profile, run, started.Task, config);

                await Task.WhenAny(started.Task, work);
                // A refusal before the process started — no installer, no login flow, a binary that
                // did not start — travels as a refusal, exactly as it did when the request waited.
                if (work.IsCompleted) await work;
                return new { Harness = harness, Action = action, Started = true };
            }

            // The two things a screen may do to a harness action while it runs (2026-09-23): answer
            // the prompt it printed — a login asks for the code the browser shows, and waits — and
            // stop it. Either is refused, naming the action, when nothing runs under that name: an
            // answer that went nowhere must not look delivered.
            case "HARNESS_INPUT":
            {
                Running(request).Send(PayloadHelper.GetRequiredValue<string>(request.Payload, "text"));
                return new { Sent = true };
            }

            case "HARNESS_CANCEL":
            {
                Running(request).Cancel();
                return new { Cancelled = true };
            }

            // This machine's plugins (D64): the catalogue as the driver reads it, each with what it
            // declares, what it speaks on, whether it is running, and why it contributes nothing when
            // it does not. Machine paths ride this bridge like every path here.
            case "PLUGINS":
            {
                await Task.CompletedTask;
                var catalog = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
                var running = new HashSet<string>(_loop.RunningPlugins, StringComparer.Ordinal);
                return new
                {
                    Folder = Path.Combine(_loop.Home, PluginCatalog.Folder),
                    Plugins = catalog.Plugins.Select(plugin => new
                    {
                        plugin.Manifest.Id,
                        plugin.Manifest.Name,
                        plugin.Manifest.Version,
                        plugin.Manifest.Description,
                        plugin.Enabled,
                        plugin.Problem,
                        Harnesses = plugin.Manifest.Harnesses.Select(h => h.Name).ToArray(),
                        Points = plugin.Manifest.Hooks?.Points ?? [],
                        Running = running.Contains(plugin.Manifest.Id),
                        plugin.Folder,
                        plugin.Data,
                    }).ToArray(),
                };
            }

            // The screen's half of `daoris plugin enable|disable|remove` (D50): a row in
            // `plugins.json`, or the install folder gone with the data folder named and kept.
            case "PLUGIN_ACTION":
            {
                await Task.CompletedTask;
                var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var entry = PluginCatalog.Load(_loop.Home).Plugins
                    .FirstOrDefault(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
                    ?? throw Refusals.Because(
                        Refusals.PluginUnknown, $"no plugin `{id}` on this machine.", ("id", id));

                switch (action)
                {
                    case "enable":
                        PluginState.Enable(_loop.Home, entry.Manifest.Id);
                        break;
                    case "disable":
                        PluginState.Disable(_loop.Home, entry.Manifest.Id);
                        break;
                    case "remove":
                        // The install folder goes; what the plugin kept is NAMED and stays — it is
                        // the person's to throw away, the same judgement Forget makes for an account.
                        Directory.Delete(entry.Folder, recursive: true);
                        PluginState.Enable(_loop.Home, entry.Manifest.Id);
                        break;
                    default:
                        throw Refusals.Because(
                            Refusals.PluginActionUnknown,
                            $"unknown plugin action '{action}' — one of: enable, disable, remove",
                            ("action", action));
                }

                // The loop reconciles its hook processes against the catalogue each tick; asked to
                // look now, so a plugin switched off stops before the person has finished reading.
                _loop.Nudge();
                return new
                {
                    Id = entry.Manifest.Id,
                    Action = action,
                    Data = Directory.Exists(entry.Data) ? entry.Data : null,
                };
            }

            // What sessions consumed (TOOL3/D57 §4) — measured before it is managed.
            //
            // 🔴 Over this bridge and nowhere else. Per-account usage names a credential profile, and
            // a profile name is already served only over loopback (`ToSession`), so this inherits
            // that boundary rather than arguing for its own. There is no HTTP route onto it.
            case "USAGE":
            {
                await Task.CompletedTask;
                var sessions = _loop.Usage.Sessions;
                return new
                {
                    // Newest first: a person looking at this is asking about recent work.
                    Sessions = sessions
                        .OrderByDescending(entry => entry.When)
                        .Select(entry => new
                        {
                            entry.Session,
                            entry.Repository,
                            entry.Harness,
                            entry.Profile,
                            entry.Used,
                            entry.Size,
                            entry.When,
                        })
                        .ToArray(),
                    Accounts = _loop.Usage.ByAccount().Select(account => new
                    {
                        account.Harness,
                        account.Profile,
                        account.Sessions,
                        account.Used,
                    }).ToArray(),
                };
            }

            // "Look now": a person who just published a quest should not watch a poll countdown.
            case "NUDGE":
                _loop.Nudge();
                // The one verb with nothing to answer — and `await` keeps this method honestly async.
                await Task.CompletedTask;
                return null;

            // *Sync now* (SYNC6b): the tick's own pass for one circle, through the loop's own set —
            // `daoris-driver sync` is the other door to the same pass (D50). What it says comes back
            // in the driver's words; a circle with no remote is its refusal, mapped above.
            case "SYNC_NOW":
            {
                var workspace = RemoteTarget.Workspace(
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "workspace"));
                var pass = _loop.SyncNowAsync(workspace, cancellationToken)
                    ?? throw Refusals.Because(
                        Refusals.DriverNotReady,
                        "the driver is still coming up — its service is not answering yet. A moment.");
                var report = await pass;
                return new { Workspace = workspace, report.Problem, report.Notes };
            }

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
    /// A process action to its end, and the end announced — after the request that started it has
    /// been answered. A failure before the process started is the caller's to refuse; one after it
    /// is news like any other end, because nobody awaits this any more.
    /// </summary>
    private async Task RunActionAsync(
        string key, string harness, string action, string? profile, Func<Task<int>> run, Task started,
        DriverConfig config)
    {
        int code;
        try
        {
            code = await run();
        }
        catch (Exception error) when (started.IsCompletedSuccessfully)
        {
            await AnnounceAsync(harness, action, profile, -1, error.Message, config);
            return;
        }
        finally
        {
            _actions.TryRemove(key, out _);
        }

        await AnnounceAsync(harness, action, profile, code, null, config);
    }

    private async Task AnnounceAsync(
        string harness, string action, string? profile, int code, string? problem, DriverConfig config)
    {
        // Whatever it did, what this machine HAS has probably changed — so the next question asks
        // the tool again rather than answering from before, and the news arrives after the roster.
        IReadOnlyList<HarnessReport> roster = [];
        try
        {
            roster = await _loop.Harnesses.RosterAsync(config, refresh: true, CancellationToken.None);
        }
        catch (Exception)
        {
            // The roster is asked again on the page's next question; the end is still news.
        }

        var signingIn = action is "login" or "login-new" && profile is not null;
        await _events.EmitAsync("DAORIS", "HARNESS_ENDED", new
        {
            Harness = harness, Action = action, Profile = profile, ExitCode = code, Problem = problem,
            // Who signed in (D66 §3) — the tool's own answer, from the roster just read, so the
            // sentence a person hears names the account the way they know it.
            Account = signingIn
                ? roster.FirstOrDefault(report => report.Adapter == harness)?.Profiles
                    .FirstOrDefault(each => each.Name == profile)?.Account
                : null,
            // Whether a sign-in to another account left one behind: it does only when it finished.
            Kept = action == "login-new" && profile is not null
                ? Directory.Exists(HarnessSettings.ProfileHome(
                    _loop.Harnesses.Home, _loop.Harnesses.Toolchain(harness)?.Owner(harness) ?? harness, profile))
                : (bool?)null,
        });
    }

    /// <summary>
    /// Sign in to another account (D66 §3): the tool's own login flow into a fresh directory, kept
    /// only when the sign-in finished.
    /// </summary>
    /// <remarks>
    /// "Finished" is the tool's exit code and then the tool's own word, asked of that one home: a
    /// zero exit whose home still reports signed OUT signed nobody in. An answer it cannot give is
    /// kept, by the rule every unknown login state follows (SES3) — the person watched the sign-in
    /// and can remove it. Otherwise the directory existed only for this sign-in, and goes: the list
    /// is exactly what it was before the press, whether the tool failed, was stopped, or never
    /// started.
    /// </remarks>
    private async Task<int> SignInAsync(
        string harness, string fresh, HarnessToolchain toolchain, IReadOnlyList<string>? command,
        string profileHome, Action<string> stream, DriverConfig config, Action<HarnessRun> track)
    {
        var code = -1;
        try
        {
            code = await HarnessActions.LoginAsync(
                toolchain, command, profileHome, stream, CancellationToken.None, track);
            return code;
        }
        finally
        {
            var (login, account) = code == 0
                ? await _loop.Harnesses.LoginOfAsync(harness, config, fresh, CancellationToken.None)
                : (LoginState.Out, null);

            if (code != 0 || login == LoginState.Out)
            {
                try
                {
                    HarnessSettings.RemoveProfile(_loop.Harnesses.Home, toolchain.Owner(harness), fresh);
                    stream("nothing was signed in, so nothing was kept — the account opened for it is gone again.");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    stream($"nothing was signed in, and {profileHome} could not be removed — {error.Message}");
                }
            }
            else
            {
                stream(account is { Length: > 0 }
                    ? $"signed in as {account} — this machine lists it as `{fresh}`."
                    : $"signed in — `{harness}` did not say who, so this machine lists it as `{fresh}`.");
            }
        }
    }

    /// <summary>The action running under `harness:action`, or the refusal that names it.</summary>
    private HarnessRun Running(IpcRequest request)
    {
        var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        return _actions.TryGetValue($"{harness}:{action}", out var run)
            ? run
            : throw Refusals.Because(
                Refusals.HarnessActionIdle,
                $"nothing is running for `{harness}` {action} — it finished, or was never started.",
                ("harness", harness), ("action", action));
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
        CancellationToken ct, Action<HarnessRun> started)
    {
        var version = PayloadHelper.GetRequiredValue<string>(request.Payload, "version");
        var code = await HarnessActions.PinAsync(
            toolchain, _loop.Harnesses.Home, harness, version, stream, ct, started);

        if (code == 0) _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath);
        return code;
    }

    /// <summary>
    /// Make a credential profile under a name the caller chose: a directory, and nothing else (DEPLOY3).
    /// </summary>
    /// <remarks>
    /// Idempotent, exactly as the CLI verb is — asking for one that exists is an answer, not a
    /// failure. The page makes accounts by signing in (<c>login-new</c>, D66 §3); this is the
    /// bridge's half of <c>daoris agent profile add</c>, for a name a person picks before signing in.
    /// </remarks>
    private int ProfileAdd(string harness, IpcRequest request)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_loop.Harnesses.Home, harness, Named(request)));
        return 0;
    }

    /// <summary>
    /// Remove an account — 🔴 <b>its directory with it, credentials included</b> (D66 §3).
    /// </summary>
    /// <remarks>
    /// <para>This amends SES3's "removing one deletes nothing", on the owner's word: Forget did not
    /// delete the account. The old rule un-pointed it and kept any directory the tool would not
    /// call signed out, so a removed account stayed listed and signed in — the leftover the person
    /// pressed the button to be rid of. The page asks twice before it sends this; the terminal twin
    /// is <c>daoris agent profile remove</c>.</para>
    ///
    /// <para>Only ever a profile: the tool's own configuration home is not under Daoris's directory,
    /// and no name reaches it.</para>
    ///
    /// <para>A delete that fails — a running session holding a file open in it — says so and leaves
    /// the wiring as it was, because the account is still there to point at.</para>
    /// </remarks>
    private int ProfileRemove(string harness, IpcRequest request, Action<string> stream)
    {
        var profile = Named(request);
        var directory = HarnessSettings.ProfileHome(_loop.Harnesses.Home, harness, profile);
        try
        {
            stream(HarnessSettings.RemoveProfile(_loop.Harnesses.Home, harness, profile)
                ? $"removed {directory} — the account and its sign-in are gone from this machine."
                : $"`{profile}` is not on this machine — there was nothing to remove.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            stream($"could not remove {directory} — {error.Message} A session running as this "
                + "account may hold a file open in it; stop it and remove again. Part of it may already be gone.");
            return 1;
        }

        var settings = _loop.Harnesses.Settings;
        if (settings.Defaults.TryGetValue(harness, out var machine) && machine == profile)
        {
            settings = settings.WithDefault(harness, null);
        }

        foreach (var workspace in settings.Workspaces.Keys.ToList())
        {
            if (settings.Workspaces[workspace].TryGetValue(harness, out var held) && held == profile)
            {
                settings = settings.WithWorkspaceDefault(workspace, harness, null);
            }
        }

        settings.Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    /// <summary>
    /// Which profile this harness runs as — the machine's, or one workspace's (D49 §4). 🔴 No profile
    /// named CLEARS it: "use the tool's own home again" is a choice a person makes, not an argument
    /// they forgot, and the file's own rule is that absence means the harness's own home.
    /// </summary>
    private int ProfileDefault(string harness, IpcRequest request)
    {
        var profile = Optional(request, "profile");
        var workspace = Optional(request, "workspace");
        var settings = _loop.Harnesses.Settings;

        settings = workspace is { Length: > 0 }
            ? settings.WithWorkspaceDefault(workspace, harness, profile)
            : settings.WithDefault(harness, profile);

        settings.Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    /// <summary>The profile a profile verb is about. Absent is a refusal, never a guess.</summary>
    private static string Named(IpcRequest request) =>
        Optional(request, "profile") is { Length: > 0 } profile
            ? profile
            : throw Refusals.Because(
                Refusals.HarnessActionUnknown,
                "that action needs a profile name.",
                ("action", "profile"));

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
            // Where this machine's Daoris lives (D63), and what establishing it did this start — a
            // machine-local path, answered only over this bridge, like every path here.
            _loop.Home,
            _loop.HomeNotice,
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
            // 🔴 Reported so the page can show it, and modelled on the record so no toggle deletes it.
            config.IntakeAdapter,
            Running = _loop.Processes.Running,
        };
    }

    private void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(_loop.ConfigPath)).Save(_loop.ConfigPath);
        _loop.Nudge();
    }
}
