using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// This machine's agents, the page's `bridge/agents.ts` (MOD5): the roster and the accounts each runs as
/// (D49 §4), what a start would run on (MAP1b), a harness's explicit actions and their console, an
/// account's own model and effort (AGT6), and what sessions consumed (TOOL3).
/// </summary>
public sealed partial class DriverModule
{
    // The harness action running now, by `harness:action` — one at a time by construction, and the
    // two things a screen may do to it while it runs: answer the prompt it printed, or stop it.
    private readonly ConcurrentDictionary<string, HarnessRun> _actions = new();

    // Which `harness:action` holds the one slot, from its start until `RunActionAsync` ends it (REV3).
    private string? _acting;

    // When each account's sign-in state was last read (UX6e, D150 §5.3), kept as the application asks and is answered,
    // never by asking: what the Agents place says beside each state.
    private readonly RosterReads _reads = new();

    // This machine's harnesses, and the accounts they run as (D49 §4). The roster's report is cached
    // after its first look, so a page asking again starts nothing; `refresh` is the person pressing
    // *Read again*: every agent's, or with `agent` one agent's accounts only (UX6e). Each account and
    // the tool's own sign-in say when they were last read.
    [DriverRoute("HARNESSES")]
    private async Task<object?> HarnessesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var refresh = Flag(request, "refresh");
        var agent = refresh ? Optional(request, "agent") : null;

        var config = DriverConfig.Load(_loop.ConfigPath);
        IReadOnlyList<HarnessReport> roster;
        HashSet<string> asked;
        if (agent is not null)
        {
            // One agent's accounts read again, one at a time, and its own sign-in (UX6e, D150 §5.3): through its
            // account-owning door, which every door onto it shares (AGT7), so each account is asked once, not once a door.
            var door = AccountDoor(agent)
                ?? throw new DriverException($"no agent `{agent}` on this machine has accounts to read.");
            await _loop.Harnesses.ReportAsync(door, config, refresh: true, cancellationToken, own: true);
            roster = await _loop.Harnesses.RosterAsync(config, refresh: false, cancellationToken);
            asked = new HashSet<string>([door], StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            roster = await _loop.Harnesses.RosterAsync(config, refresh, cancellationToken);
            asked = refresh ? roster.Select(report => report.Adapter).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
        }

        // Whom a session of Daoris's runs on, asked only where a door was just read: the probe keeps those accounts' words.
        var running = asked.Count > 0 ? await RunningAsync(cancellationToken) : null;
        var now = _loop.Harnesses.Clock();
        var settings = _loop.Harnesses.Settings;

        return new
        {
            _loop.Harnesses.SettingsPath,
            Adapter = config.Adapter,
            Harnesses = roster.Select(report =>
            {
                // Asked once per harness: every field below reads the same two answers.
                var toolchain = _loop.Harnesses.Toolchain(report.Adapter);
                var stamps = _reads.Observe(
                    report, now, asked.Contains(report.Adapter), BusyOn(running, toolchain?.Owner(report.Adapter) ?? report.Adapter));
                var pinned = settings.ResolveVersion(report.Adapter, null, null);
                // The tool's own settings file under an account (AGT6, D98), where this build
                // knows it: a door's is its owner's (AGT7), and null offers nothing.
                var settingsFile = _loop.Harnesses.AccountToolchain(report.Adapter)?.SettingsFile;
                return new
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
                    Pinned = pinned,
                    Managed = HarnessSettings.ManagedBinary(
                        _loop.Harnesses.Home, report.Adapter,
                        pinned,
                        toolchain?.Binary ?? []),
                    // Whether this harness CAN be pinned at all. A harness that declares neither
                    // a package nor a maker's channel (AGT2b) has no version for Daoris to fetch,
                    // and a surface offering the control anyway would be a button whose only
                    // outcome is a refusal.
                    Pinnable = toolchain is { } pinnable
                        && (pinnable.Package is { Length: > 0 } || pinnable.Channel is { Length: > 0 }),
                    // Whether this door can run a sign-in at all — the same rule: a harness that
                    // declares no login flow gets no "Sign in" whose only outcome is a refusal.
                    SignsIn = toolchain?.LoginArguments is { Count: > 0 },
                    // Which Update this door has (USE1a): "pin" moves the pin to the newest
                    // release, "tool" runs the tool's own updater, and null offers none — the
                    // same rule again, after Update on a pinned door answered only a refusal.
                    Updates = toolchain is null ? null : HarnessActions.UpdateOf(toolchain, pinned),
                    // 🔴 Which TOOL's account this entry runs as, and which door it holds a
                    // session over. Both were already declared and neither reached the page,
                    // which is why the surface listed `claude-code` and `claude-code-acp` as two
                    // things a person has to have opinions about. They are one tool and one
                    // account; the second is a way in. The page groups on these two fields.
                    AccountOf = toolchain?.AccountOf,
                    // What a person calls the tool, and whose it is (AGT1) — `dsh` meant nothing
                    // to the owner until it said.
                    toolchain?.Product,
                    toolchain?.Maker,
                    Wire = _loop.Harnesses.Wire(report.Adapter).ToString().ToLowerInvariant(),
                    // Whether a session on this door keeps a conversation (D76 §1) — what the page
                    // reads an empty record by, rather than guessing from the emptiness (CONV3b).
                    Structured = _loop.Harnesses.Structured(report.Adapter),
                    // Whether a session on this door is handed the rules file (PERM1, D72): the agent's
                    // page has *What it may do* only where Daoris hands its agent one (UX6e, D150 §5.1).
                    TakesRules = _loop.Harnesses.Adapters.Resolve(report.Adapter).TakesSettings,
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
                        // The account's own model and effort, read fresh from the tool's file
                        // each time (AGT6): the file is the truth, and the terminal edits it too.
                        Settings = settingsFile is null
                            ? null
                            : AccountSettings(AgentSettings.Read(Path.Combine(profile.Home, settingsFile))),
                        // When its state was last read (UX6e), or null where it never was: absent is never a reading.
                        Read = stamps.Accounts.GetValueOrDefault(profile.Name),
                    }).ToArray(),
                    // What the tool itself offers for those two keys (AGT6, D98): its aliases and
                    // the efforts its settings keep. Null where Daoris does not know its settings,
                    // and the page then offers nothing and says so.
                    SettingsChoices = settingsFile is null
                        ? null
                        : new { AgentSettings.Models, AgentSettings.Efforts },
                    // Whether this agent takes an API key at all — the control is absent where
                    // it does not, by the rule `Pinnable` and `SignsIn` follow.
                    TakesKey = _loop.Harnesses.AccountToolchain(report.Adapter)?.KeyVariable is { Length: > 0 },
                    // 🔴 The account a person actually HAS — the tool's own configuration home —
                    // answered beside the profiles rather than left out, which read as "No
                    // accounts" to an owner who was logged in.
                    OwnLogin = report.OwnLogin.ToString().ToLowerInvariant(),
                    report.OwnAccount,
                    // When the tool's own sign-in was last read: only at a person's press (TOOL6g), so null before one.
                    OwnRead = stamps.Own,
                    // Which circles run this harness as which account (D49 §4): the terminal
                    // could set it and the page could not even see it. A door's are its
                    // owner's (AGT7).
                    WorkspaceDefaults = settings.Workspaces
                        .Select(circle => (Workspace: circle.Key, Map: circle.Value,
                            Owner: toolchain?.Owner(report.Adapter) ?? report.Adapter))
                        .Where(circle => circle.Map.TryGetValue(circle.Owner, out var chosen)
                            && !string.IsNullOrWhiteSpace(chosen))
                        .OrderBy(circle => circle.Workspace, StringComparer.Ordinal)
                        .Select(circle => new { circle.Workspace, Profile = circle.Map[circle.Owner] })
                        .ToArray(),
                };
            }).ToArray(),
        };
    }

    /// <summary>
    /// The door an agent's accounts are read through (AGT7, UX6e): the one named for it, else the first door onto its
    /// accounts, as the page lists an agent's doors. Null where no door this build knows runs as that agent's accounts.
    /// </summary>
    private string? AccountDoor(string agent)
    {
        string? first = null;
        foreach (var name in _loop.Harnesses.Known)
        {
            HarnessToolchain? toolchain;
            try
            {
                toolchain = _loop.Harnesses.Toolchain(name);
            }
            catch (DriverException)
            {
                continue;
            }

            if (toolchain is null || !string.Equals(toolchain.Owner(name), agent, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, agent, StringComparison.OrdinalIgnoreCase)) return name;
            first ??= name;
        }

        return first;
    }

    /// <summary>
    /// Whether a session of Daoris's runs on one of <paramref name="owner"/>'s accounts now, by the live records
    /// (<see cref="RunningAsync"/>); null where they were not read, and then none is taken for busy.
    /// </summary>
    private static Func<string, bool>? BusyOn(IReadOnlyDictionary<string, int>? running, string owner) =>
        running is null ? null : account => running.GetValueOrDefault(Key(owner, account)) > 0;

    /// <summary>
    /// The roster asked again because something the person pressed ran (a sign-in's end, a key, an account's edit), each
    /// door's accounts dated as read then (UX6e): what this machine has has probably changed.
    /// </summary>
    private async Task<IReadOnlyList<HarnessReport>> LookAgainAsync(DriverConfig config, CancellationToken cancellationToken)
    {
        var roster = await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
        var running = await RunningAsync(cancellationToken);
        var now = _loop.Harnesses.Clock();
        foreach (var report in roster)
        {
            _reads.Observe(report, now, asked: true, BusyOn(running, _loop.Harnesses.Toolchain(report.Adapter)?.Owner(report.Adapter) ?? report.Adapter));
        }

        return roster;
    }

    // What a driven start in each workspace would run on, and where each part came from
    // (MAP1b): the driver's own `SelectAsync`, read through `WiringAsync`, so the panel cannot
    // show a start the loop would not make. The page names the circles it shows; this answers
    // for those and no others. Names and versions only — no home, no binary, no key. Named
    // for what it answers rather than "wiring", which the page already calls the remotes map.
    [DriverRoute("STARTS")]
    private async Task<object?> StartsAsync(IpcRequest request, CancellationToken cancellationToken)
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
            // The work: a driven session, which is also what a conversation started without a
            // pick takes.
            starts.Add(Start("work", workspace,
                await _loop.Harnesses.WiringAsync(config.Adapter, config, workspace, cancellationToken)));

            // The intake (INT4b, AGT6), once an agent is named for it: the same `SelectAsync`
            // the intake takes for an ask in this circle. 🔴 An agent this build has no adapter
            // for is a held row in the driver's own sentence — the one the intake would hold
            // with — never a refusal of the whole answer, which would take the work's rows too.
            if (config.IntakeAdapter is { Length: > 0 } intake)
            {
                StartWiring wiring;
                try
                {
                    wiring = await _loop.Harnesses.WiringAsync(intake, config, workspace, cancellationToken);
                }
                catch (DriverException error)
                {
                    wiring = new StartWiring(
                        intake, intake, null, ChoiceFrom.Unset, null, ChoiceFrom.Unset, false, error.Message);
                }

                starts.Add(Start("intake", workspace, wiring));
            }
        }

        return new { Adapter = config.Adapter, Starts = starts };

        object Start(string job, string workspace, StartWiring wiring) => new
        {
            Job = job,
            Workspace = workspace,
            wiring.Adapter,
            wiring.Owner,
            // Asked defensively: a held row may be held BECAUSE the name resolves to nothing.
            Known(wiring.Adapter)?.Product,
            wiring.Profile,
            ProfileFrom = wiring.ProfileFrom.ToString().ToLowerInvariant(),
            wiring.Version,
            VersionFrom = wiring.VersionFrom.ToString().ToLowerInvariant(),
            wiring.Commanded,
            wiring.Refusal,
        };

        HarnessToolchain? Known(string adapter)
        {
            try
            {
                return _loop.Harnesses.Toolchain(adapter);
            }
            catch (DriverException)
            {
                return null;
            }
        }
    }

    // The person's explicit action on a harness (D49 §4): its own installer, its own updater,
    // its own login flow. Never automatic, never mid-session, never unasked — and streamed
    // line by line through the console, because it is a process like any other.
    [DriverRoute("HARNESS_ACTION")]
    private async Task<object?> HarnessActionAsync(IpcRequest request, CancellationToken cancellationToken)
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
                    $"`{harness}` takes no API key from Daoris — sign it in instead.");
            }

            var account = HarnessKeys.Add(
                _loop.Harnesses.Home, owner, PayloadHelper.GetRequiredValue<string>(request.Payload, "key"));
            await LookAgainAsync(config, cancellationToken);
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
            "profile-default" => ProfileDefault(owner, Optional(request, "profile"), Optional(request, "workspace")),
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
            await LookAgainAsync(config, cancellationToken);
            // A default's edit says what sessions there run as now (LOOK2c), the fact the terminal's verb prints: a
            // workspace cleared on the tool's own row runs as the machine's default where one is set.
            return action == "profile-default"
                ? new
                {
                    Harness = harness, Action = action, ExitCode = code,
                    Default = DefaultStanding(_loop.Harnesses.Settings, owner, Optional(request, "workspace")),
                }
                : (object)new { Harness = harness, Action = action, ExitCode = code };
        }

        // A pin's version is asked for before anything starts: a pin without one is a malformed call.
        var version = action == "pin" ? PayloadHelper.GetRequiredValue<string>(request.Payload, "version") : null;
        await StartProcessActionAsync(harness, action, profile, version, toolchain, command, stream, config);
        return new { Harness = harness, Action = action, Started = true };
    }

    // The two things a screen may do to a harness action while it runs (2026-09-23): answer
    // the prompt it printed — a login asks for the code the browser shows, and waits — and
    // stop it. Either is refused, naming the action, when nothing runs under that name: an
    // answer that went nowhere must not look delivered.
    [DriverRoute("HARNESS_INPUT")]
    private object? HarnessInput(IpcRequest request)
    {
        Running(request).Send(PayloadHelper.GetRequiredValue<string>(request.Payload, "text"));
        return new { Sent = true };
    }

    [DriverRoute("HARNESS_CANCEL")]
    private object? HarnessCancel(IpcRequest request)
    {
        Running(request).Cancel();
        return new { Cancelled = true };
    }

    // An account's own model and effort (AGT6, D98): keys in the tool's own settings file under
    // that account, the same file `daoris agent settings` edits (D50). A key sent as null is
    // cleared and one left out is untouched. Never the tool's own configuration home, which the
    // roster says Daoris never touches, and never an account a setting would bring into being.
    [DriverRoute("SET_AGENT_SETTINGS")]
    private object? SetAgentSettings(IpcRequest request)
    {
        var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
        var (owner, profile, read) = WriteAgentSettings(
            harness, Optional(request, "profile"), () => (Edit(request, "model"), Edit(request, "effort"), PerModel(request)));
        return new { Harness = owner, Profile = profile, read.Model, read.Effort, PerModel = PerModelOf(read), read.Problem };
    }

    // What sessions consumed (TOOL3/D57 §4) — measured before it is managed.
    //
    // 🔴 Over this bridge and nowhere else. Per-account usage names a credential profile, and
    // a profile name is already served only over loopback (`ToSession`), so this inherits
    // that boundary rather than arguing for its own. There is no HTTP route onto it.
    [DriverRoute("USAGE")]
    private async Task<object?> UsageAsync(IpcRequest request, CancellationToken cancellationToken)
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

    /// <summary>
    /// One setting's change as the page sent it (AGT6): a text sets it, null clears it, and a key left out
    /// is no change at all — the three a person can mean, kept apart.
    /// </summary>
    private static AgentSettingEdit? Edit(IpcRequest request, string name) =>
        request.Payload is { } payload && payload.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Null => new AgentSettingEdit(null),
                JsonValueKind.String => new AgentSettingEdit(value.GetString()),
                _ => throw new DriverException($"`{name}` is set with a name, or cleared with null."),
            }
            : null;

    /// <summary>An effort per model as the page sent it: the model's name, then a value or null.</summary>
    private static IReadOnlyDictionary<string, AgentSettingEdit>? PerModel(IpcRequest request)
    {
        if (request.Payload is not { } payload || !payload.TryGetProperty("perModel", out var models)
            || models.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return models.EnumerateObject().ToDictionary(
            model => model.Name,
            model => model.Value.ValueKind switch
            {
                JsonValueKind.Null => new AgentSettingEdit(null),
                JsonValueKind.String => new AgentSettingEdit(model.Value.GetString()),
                _ => throw new DriverException($"the effort for `{model.Name}` is set with a name, or cleared with null."),
            },
            StringComparer.Ordinal);
    }

    /// <summary>An account's settings as the page reads them (AGT6): the two keys, each model's own effort, and why not.</summary>
    private static object AccountSettings(AgentSettingsRead read) =>
        new { read.Model, read.Effort, PerModel = PerModelOf(read), read.Problem };

    private static object[] PerModelOf(AgentSettingsRead read) =>
        [.. read.PerModel.Select(entry => (object)new { entry.Model, entry.Effort })];

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
        return line => DriverLoop.EmitOutput(
            _events, $"{harness}:{action}", [new ConsoleLine(Interlocked.Increment(ref sequence), line)]);
    }

    /// <summary>
    /// A process action on a harness, answered once it has started — <c>HARNESS_ACTION</c>'s own start,
    /// and the one Ask Daoris's Apply of an update or a pin calls too (HELP6), so the two cannot differ.
    /// </summary>
    /// <remarks>
    /// 🔴 A process action is answered when the process has STARTED, and its end is news
    /// (HARNESS_ENDED) — the same way a conversation's ending is (D49 §3). It waits on a network, or on
    /// a person in a browser, and a request that waited with it timed out on the bridge at thirty seconds
    /// while the login ran on: the page closed its panel, the row said nothing had changed, and the
    /// process kept waiting for a browser nobody was told about (measured on the installed shell,
    /// 2026-09-23). While it runs the page may answer it or stop it (HARNESS_INPUT, HARNESS_CANCEL).
    /// </remarks>
    /// <param name="ended">Told the exit code and any problem once the process ends, after the news goes out.</param>
    private async Task StartProcessActionAsync(
        string harness, string action, string? profile, string? version, HarnessToolchain toolchain,
        IReadOnlyList<string>? command, Action<string> stream, DriverConfig config, Action<int, string?>? ended = null)
    {
        var owner = toolchain.Owner(harness);
        var key = $"{harness}:{action}";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<HarnessRun> track = run =>
        {
            _actions[key] = run;
            started.TrySetResult();
        };
        // 🔴 Signing in to ANOTHER account (D66 §3): the account is made by the sign-in, not named
        // before it. It opens under the next free `account-N` — nobody knows whose it is yet — and the
        // tool's own answer names who, on the roster, once it ends.
        var fresh = action == "login-new"
            ? HarnessSettings.NextAccount(_loop.Harnesses.Home, owner)
            : null;
        var profileHome = HarnessSettings.ProfileHome(
            _loop.Harnesses.Home, owner,
            fresh ?? profile ?? _loop.Harnesses.Settings.Resolve(owner, null, null) ?? "default");
        Func<Task<int>> run = action switch
        {
            "install" => () => HarnessActions.InstallAsync(toolchain, stream, CancellationToken.None, track),
            "update" => () => UpdateAsync(harness, toolchain, command, stream, CancellationToken.None, track),
            "login" => () => HarnessActions.LoginAsync(toolchain, command, profileHome, stream, CancellationToken.None, track),
            "login-new" => () => SignInAsync(harness, fresh!, toolchain, command, profileHome, stream, config, track),
            // The managed toolchain (TOOL2/D57) — the desktop's half of `daoris agent pin|unpin`, over
            // the same file.
            _ => () => PinAsync(harness, toolchain, stream, version!, CancellationToken.None, track),
        };

        // 🔴 One at a time on this machine, claimed as the action starts and released by
        // `RunActionAsync` however it ends (REV3). The page re-enabled its buttons once the request
        // answered `started`, and a second login under the same name took the first's place in
        // `_actions`: the first could no longer be answered or stopped, and its end removed the second's
        // entry. Claimed last, so nothing above can throw with it held.
        if (Interlocked.CompareExchange(ref _acting, key, null) is { } busy)
        {
            throw Refusals.Because(
                Refusals.HarnessActionBusy,
                $"{busy} is still running — wait for it to end, or stop it, before starting another.",
                ("running", busy));
        }

        var work = RunActionAsync(key, harness, action, fresh ?? profile, run, started.Task, config, ended);

        await Task.WhenAny(started.Task, work);
        // A refusal before the process started — no installer, no login flow, a binary that did not
        // start — travels as a refusal, exactly as it did when the request waited.
        if (work.IsCompleted) await work;
    }

    /// <summary>
    /// A process action to its end, and the end announced — after the request that started it has
    /// been answered. A failure before the process started is the caller's to refuse; one after it
    /// is news like any other end, because nobody awaits this any more.
    /// </summary>
    private async Task RunActionAsync(
        string key, string harness, string action, string? profile, Func<Task<int>> run, Task started,
        DriverConfig config, Action<int, string?>? ended = null)
    {
        int code;
        try
        {
            code = await run();
        }
        catch (Exception error) when (started.IsCompletedSuccessfully)
        {
            await AnnounceAsync(harness, action, profile, -1, error.Message, config);
            Tell(-1, error.Message);
            return;
        }
        finally
        {
            _actions.TryRemove(key, out _);
            // The slot goes with the action, however it ended — before it started included.
            Volatile.Write(ref _acting, null);
        }

        await AnnounceAsync(harness, action, profile, code, null, config);
        Tell(code, null);

        // Whoever asked to hear the end (HELP6: Ask Daoris's conversation) hears it after the news; a
        // listener that fails costs its own view, never the end the screen was told.
        void Tell(int exit, string? problem)
        {
            try
            {
                ended?.Invoke(exit, problem);
            }
            catch (Exception)
            {
                // The news already went out.
            }
        }
    }

    private async Task AnnounceAsync(
        string harness, string action, string? profile, int code, string? problem, DriverConfig config)
    {
        // Whatever it did, what this machine HAS has probably changed — so the next question asks
        // the tool again rather than answering from before, and the news arrives after the roster.
        IReadOnlyList<HarnessReport> roster = [];
        try
        {
            roster = await LookAgainAsync(config, CancellationToken.None);
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
        string harness, HarnessToolchain toolchain, Action<string> stream, string version,
        CancellationToken ct, Action<HarnessRun> started)
    {
        var code = await HarnessActions.PinAsync(
            toolchain, _loop.Harnesses.Home, harness, version, stream, ct, started);

        if (code == 0) _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath);
        return code;
    }

    /// <summary>
    /// Update a door (USE1a): a pinned one moves its pin to the newest release, written the way
    /// <see cref="PinAsync"/> writes one — only after that version is installed — and an unpinned one
    /// runs its own updater. The machine's pin, the one the roster shows.
    /// </summary>
    private Task<int> UpdateAsync(
        string harness, HarnessToolchain toolchain, IReadOnlyList<string>? command, Action<string> stream,
        CancellationToken ct, Action<HarnessRun> started) =>
        HarnessActions.UpdateAsync(
            toolchain, command, _loop.Harnesses.Home, harness,
            _loop.Harnesses.Settings.ResolveVersion(harness, null, null),
            version => _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath),
            stream, ct, started);

    /// <summary>
    /// An account's own model and effort, written — <c>SET_AGENT_SETTINGS</c>'s own write, and the one Ask
    /// Daoris's Apply makes too (HELP6). Never the tool's own configuration home, and never an account a
    /// setting would bring into being.
    /// </summary>
    /// <param name="edits">The keys to change, read only once the account is known to be one Daoris keeps.</param>
    private (string Owner, string Profile, AgentSettingsRead Read) WriteAgentSettings(
        string harness, string? profile,
        Func<(AgentSettingEdit? Model, AgentSettingEdit? Effort, IReadOnlyDictionary<string, AgentSettingEdit>? PerModel)> edits)
    {
        var toolchain = _loop.Harnesses.Toolchain(harness)
            ?? throw new DriverException(
                $"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
        var owner = toolchain.Owner(harness);
        var file = _loop.Harnesses.AccountToolchain(harness)?.SettingsFile
            ?? throw new DriverException(
                $"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, "
                + "so Daoris offers none — set its model with the tool itself.");
        var named = profile
            ?? throw new DriverException(
                $"name the account these settings are for — `{owner}`'s own configuration home is the tool's, "
                + "and Daoris never touches it.");
        var accounts = HarnessSettings.Profiles(_loop.Harnesses.Home, owner);
        if (!accounts.Contains(named, StringComparer.Ordinal))
        {
            throw new DriverException(
                $"`{owner}` has no account `{named}` on this machine — accounts that exist: "
                + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
        }

        var (model, effort, perModel) = edits();
        var read = AgentSettings.Write(
            Path.Combine(HarnessSettings.ProfileHome(_loop.Harnesses.Home, owner, named), file), model, effort, perModel);
        return (owner, named, read);
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

        // No default, list or kept account names it afterwards (TOOL4g, TOOL4e's note), as the terminal's `profile remove`
        // leaves the wiring: the next account made takes the first free name, which may be this one's.
        _loop.Harnesses.Settings.WithoutAccount(harness, profile).Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    /// <summary>
    /// Which profile this harness runs as — the machine's, or one workspace's (D49 §4). 🔴 No profile
    /// named CLEARS it: "use the tool's own home again" is a choice a person makes, not an argument
    /// they forgot, and the file's own rule is that absence means the harness's own home.
    /// </summary>
    /// <remarks>
    /// One method the route and Ask Daoris's door both call (HELP10), so the two cannot drift. A default outside its scope's
    /// own list is refused before anything is written, as the terminal refuses it since TOOL6a (TOOL4g, D130 §3.1).
    /// </remarks>
    private int ProfileDefault(string harness, string? profile, string? workspace)
    {
        var settings = _loop.Harnesses.Settings;
        DefaultAllowed(settings, harness, profile, workspace);
        DefaultEdited(settings, harness, profile, workspace).Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    /// <summary>
    /// An account's default set, or cleared by naming none (D49 §4): the machine's, or one workspace's — what
    /// <c>profile-default</c> writes. A workspace left naming no account is dropped.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A twin</b> of <c>daoris agent profile default … [--clear]</c> (LEFT3), the CLI's <c>withDefault</c>: each side's
    /// table holds the same rows (<c>ProfileDefaultTwinTests</c>, <c>toolchain.test.ts</c>). Public for that table, which
    /// the fast half runs without the roster the route asks again.
    /// </remarks>
    public static HarnessSettings DefaultEdited(HarnessSettings settings, string owner, string? profile, string? workspace) =>
        workspace is { Length: > 0 }
            ? settings.WithWorkspaceDefault(workspace, owner, profile)
            : settings.WithDefault(owner, profile);

    /// <summary>
    /// What sessions run as where a default was just edited (LOOK2c): the machine's, or one workspace's — an account, the
    /// machine's default a workspace naming none falls back to, or the agent's own configuration home. The screen says it
    /// after its press, as <c>daoris agent profile default … [--clear]</c> prints it, from the same resolution a start
    /// takes (<see cref="HarnessSettings.ResolveFrom"/>).
    /// </summary>
    /// <remarks>
    /// Found by LEFT3: the tool's own row's *use for a workspace* clears the workspace's entry, and with a machine default
    /// set its sessions then run as that default, not in the tool's own home the row names.
    /// </remarks>
    public static DefaultStandingAnswer DefaultStanding(HarnessSettings settings, string owner, string? workspace)
    {
        var scope = workspace is { Length: > 0 } ? workspace : null;
        var (account, from) = settings.ResolveFrom(owner, scope, null);
        return new DefaultStandingAnswer(scope, account, from switch
        {
            ChoiceFrom.Workspace => DefaultFrom.Workspace,
            ChoiceFrom.Machine => DefaultFrom.Machine,
            _ => DefaultFrom.Own,
        });
    }

    /// <summary>The profile a profile verb is about. Absent is a refusal, never a guess.</summary>
    private static string Named(IpcRequest request) =>
        Optional(request, "profile") is { Length: > 0 } profile
            ? profile
            : throw Refusals.Because(
                Refusals.HarnessProfileNeeded,
                "that action needs an account name.",
                ("action", "profile"));

    /// <summary>Back to `PATH`. Nothing is deleted — re-pinning that version needs no download.</summary>
    private int Unpin(string harness)
    {
        _loop.Harnesses.Settings.WithVersion(harness, null).Save(_loop.Harnesses.SettingsPath);
        return 0;
    }
}

/// <summary>
/// What sessions run as where a default was edited (LOOK2c): the workspace, or null for the machine; the account, or
/// null for the agent's own configuration home; and which rung answered, <see cref="DefaultFrom"/>.
/// </summary>
public sealed record DefaultStandingAnswer(string? Workspace, string? Account, string From);

/// <summary>Which rung a default's standing came from, as the page spells it.</summary>
public static class DefaultFrom
{
    /// <summary>The workspace names an account of its own.</summary>
    public const string Workspace = "workspace";

    /// <summary>The machine's default: the machine's own edit, or a workspace naming none that falls back to it.</summary>
    public const string Machine = "machine";

    /// <summary>No account at all: the agent's own configuration home.</summary>
    public const string Own = "own";
}
