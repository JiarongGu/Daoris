using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// This machine's agents, the page's `bridge/agents.ts` (MOD5): the roster and the accounts each runs as
/// (D49 §4), what a start would run on (MAP1b), a harness's explicit actions and their console, an
/// account's own model and effort (AGT6), and what sessions consumed (TOOL3).
/// </summary>
public sealed class AgentsRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // The harness action running now is the context's, shared with Ask Daoris's doors (PART10f).
    private AgentActions Actions => _context.Actions;

    // This machine's harnesses, and the accounts they run as (D49 §4). Each account and the tool's own
    // sign-in say what was last read of them and when, the driver's report kept under the home, so a page
    // asking, the first time after a start included, asks no account (ROSTER1, D150 §5.3). It is a read and
    // declares nothing, so a window's read is never published, which every window would read again without end.
    [DriverRoute("HARNESSES")]
    private Task<object?> HarnessesAsync(IpcRequest request, CancellationToken cancellationToken) =>
        ReadHarnessesAsync(request, refresh: false, cancellationToken);

    // The person pressing *Read again* (HARNESSREFRESH1, D169): every agent's accounts, with `agent` one agent's accounts and
    // its own sign-in, or with `profile` too that one account alone. Its own route so it can declare what it moved, which the
    // read above must not: it answers the roster as the read does, and every window asks its roster again.
    [DriverRoute("HARNESSES_REFRESH")]
    [Publishes("HARNESSES_REFRESH", StateScopes.Harnesses)]
    private Task<object?> RefreshHarnessesAsync(IpcRequest request, CancellationToken cancellationToken) =>
        ReadHarnessesAsync(request, refresh: true, cancellationToken);

    private async Task<object?> ReadHarnessesAsync(IpcRequest request, bool refresh, CancellationToken cancellationToken)
    {
        var agent = refresh ? Optional(request, "agent") : null;
        var account = agent is null ? null : Optional(request, "profile");

        var config = DriverConfig.Load(_loop.ConfigPath);
        IReadOnlyList<HarnessReport> roster;
        if (agent is not null)
        {
            // Through the agent's account-owning door, which every door onto it shares (AGT7), so each account is asked
            // once, not once a door: one at a time, and its own sign-in; or that one account alone (ROSTER1).
            var door = AccountDoor(agent)
                ?? throw new DriverException($"no agent `{agent}` on this machine has accounts to read.");
            await _loop.Harnesses.ReportAsync(door, config, refresh: true, cancellationToken, own: account is null, account: account);
            roster = await _loop.Harnesses.RosterAsync(config, refresh: false, cancellationToken);
        }
        else
        {
            roster = await _loop.Harnesses.RosterAsync(config, refresh, cancellationToken);
        }

        return new
        {
            _loop.Harnesses.SettingsPath,
            Adapter = config.Adapter,
            Harnesses = Doors(_loop.Harnesses, roster),
        };
    }

    /// <summary>
    /// Each door of the roster as <c>HARNESSES</c> answers it, and as <i>What needs you</i>'s judge reads the accounts' readings (PUSH1d4):
    /// one builder for both, from the roster's reports and the harnesses' settings, asking nothing of an account.
    /// </summary>
    public static object[] Doors(HarnessRoster harnesses, IReadOnlyList<HarnessReport> roster)
    {
        var settings = harnesses.Settings;
        return [.. roster.Select(report =>
            {
                // Asked once per harness: every field below reads the same two answers.
                var toolchain = harnesses.Toolchain(report.Adapter);
                var pinned = settings.ResolveVersion(report.Adapter, null, null);
                // The tool's own settings file under an account (AGT6, D98), where this build
                // knows it: a door's is its owner's (AGT7), and null offers nothing.
                var settingsFile = harnesses.AccountToolchain(report.Adapter)?.SettingsFile;
                var owner = toolchain?.Owner(report.Adapter) ?? report.Adapter;
                var accountActs = AccountActs(harnesses, report.Adapter);
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
                        harnesses.Home, report.Adapter,
                        pinned,
                        toolchain?.Binary ?? []),
                    // Whether this harness CAN be pinned at all. A harness that declares neither
                    // a package nor a maker's channel (AGT2b) has no version for Daoris to fetch,
                    // and a surface offering the control anyway would be a button whose only
                    // outcome is a refusal.
                    Pinnable = toolchain is { } pinnable
                        && (pinnable.Package is { Length: > 0 } || pinnable.Channel is { Length: > 0 }),
                    // Whether this door can run a sign-in at all — the same rule: an agent that
                    // declares no login flow gets no "Sign in" whose only outcome is a refusal. Its
                    // agent's flow (AGT7, CODEXACCT1), as `TakesKey` reads its key: `codex-acp` has no
                    // sign-in of its own and runs `codex login`, so Agents → Codex offered no account.
                    SignsIn = accountActs.SignsIn,
                    // Whether its agent signs in by a device code beside its own sign-in (CODEXLOGIN1): the sign-in's
                    // panel offers the other way while one runs and after one fails. Absent from an older shell.
                    DeviceCode = accountActs.DeviceCode,
                    // Which Update this door has (USE1a): "pin" moves the pin to the newest
                    // release, "tool" runs the tool's own updater, and null offers none — the
                    // same rule again, after Update on a pinned door answered only a refusal.
                    Updates = toolchain is null ? null : HarnessActions.UpdateOf(toolchain, pinned),
                    // Whether Daoris has an installer for this door (AGENTMARK1) — the test `InstallAsync` makes, so a
                    // surface offering Install anyway is a button whose only outcome is a refusal; the page says
                    // How to install where this is false.
                    Installs = toolchain?.Install is { Count: > 0 },
                    // The mark this tool wears on the page's strip (AGENTMARK1), declared by the harness; null leaves the
                    // page to derive one.
                    Mark = toolchain?.Mark,
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
                    Wire = harnesses.Wire(report.Adapter).ToString().ToLowerInvariant(),
                    // Whether a session on this door keeps a conversation (D76 §1) — what the page
                    // reads an empty record by, rather than guessing from the emptiness (CONV3b).
                    Structured = harnesses.Structured(report.Adapter),
                    // Whether a session on this door is handed the rules file (PERM1, D72): the agent's
                    // page has *What it may do* only where Daoris hands its agent one (UX6e, D150 §5.1).
                    TakesRules = harnesses.Adapters.Resolve(report.Adapter).TakesSettings,
                    // The plugin this harness came from (D64), or null for one this build carries
                    // — shown beside it, so a person knows which folder to look in.
                    Plugin = harnesses.Adapters.DeclaredBy(report.Adapter),
                    // The profile HOME is a machine path, and this bridge is the one surface
                    // allowed to carry one (D47 §4) — the page renders it so a person can find
                    // the directory they were told Daoris owns.
                    Profiles = report.Profiles.Select(profile => ProfileShown(profile, settings.PlacesOf(owner, profile.Name), settingsFile)).ToArray(),
                    // What the tool itself offers for those two keys (AGT6, D98): its aliases and
                    // the efforts its settings keep. Null where Daoris does not know its settings,
                    // and the page then offers nothing and says so.
                    SettingsChoices = settingsFile is null
                        ? null
                        : new { AgentSettings.Models, AgentSettings.Efforts },
                    // Whether this agent takes an API key at all — the control is absent where
                    // it does not, by the rule `Pinnable` and `SignsIn` follow.
                    TakesKey = accountActs.TakesKey,
                    // 🔴 The account a person actually HAS — the tool's own configuration home —
                    // answered beside the profiles rather than left out, which read as "No
                    // accounts" to an owner who was logged in.
                    OwnLogin = report.OwnLogin.ToString().ToLowerInvariant(),
                    report.OwnAccount,
                    // Its organisation and plan (ACCOUNTID1), as an account's.
                    report.OwnOrg,
                    report.OwnPlan,
                    // When the tool's own sign-in was last read: only at a person's press (TOOL6g), so null before one.
                    report.OwnRead,
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
            })];
    }

    /// <summary>
    /// One account on the roster (<c>HARNESSES</c>): its id and the person's name for it, where it runs, its home, its state
    /// as last read, who signed in, its key's handle and its own model and effort. Public for the fast half's table, which
    /// reads it without the roster the route asks.
    /// </summary>
    /// <param name="places">Where it runs (ACCT1, <see cref="HarnessSettings.PlacesOf"/>).</param>
    /// <param name="settingsFile">The tool's own settings file under an account (AGT6), or null where this build knows none.</param>
    public static object ProfileShown(ProfileReport profile, IReadOnlyList<AccountPlace> places, string? settingsFile) => new
    {
        // The account's stable id (ACCT2): its folder's name, and what every list, default, reading and record names.
        // `DisplayName` is the person's name for it, null where they gave none and it reads as its id.
        profile.Name,
        profile.DisplayName,
        // Where it runs (ACCT1): each scope whose own list holds it or whose own default names it, this machine (a null
        // workspace) first; `Nowhere` where none does. That is a fact about lists, not a claim that no start runs on it:
        // a scope naming nothing takes Daoris's accounts in turn (OWNSPACE1b-A's `inherited`), which the page says beside it.
        Places = PlacesShown(places),
        Nowhere = places.Count == 0,
        // The profile HOME is a machine path, and this bridge is the one surface allowed to carry one (D47 §4) — the page
        // renders it so a person can find the directory they were told Daoris owns.
        profile.Home,
        Login = profile.Login.ToString().ToLowerInvariant(),
        // Who is signed in there, by the tool's own answer (D66 §3), read fresh and written nowhere: what the page offers as
        // the account's name at a sign-in's end (ACCT2), never kept without the person.
        profile.Account,
        // The organisation and the plan the tool names beside who (ACCOUNTID1), read and kept as who is: what tells two
        // sign-ins of one email apart. Null where it names none.
        profile.Org,
        profile.Plan,
        // An account that is a key, by its handle only (AGT3). Never the key.
        profile.Key,
        // The account's own model and effort, read fresh from the tool's file each time (AGT6): the file is the truth, and
        // the terminal edits it too.
        Settings = settingsFile is null
            ? null
            : AccountSettings(AgentSettings.Read(Path.Combine(profile.Home, settingsFile))),
        // When its state was last read (UX6e, ROSTER1), or null where it never was: absent is never a reading.
        profile.Read,
    };

    /// <summary>
    /// Whether a door signs in its agent's accounts, takes a key into one, and has a device code beside its own sign-in, by
    /// that agent's own flows and key variable (AGT7, CODEXACCT1): <c>codex-acp</c> signs in with <c>codex login</c>, its
    /// browser first, and <c>codex login --device-auth</c> beside it (CODEXLOGIN1), as <c>claude-code-acp</c> with <c>claude
    /// auth login</c> alone. Public for the fast half's table, which reads it without the roster the route asks.
    /// </summary>
    public static (bool SignsIn, bool TakesKey, bool DeviceCode) AccountActs(HarnessRoster harnesses, string adapter) =>
        harnesses.AccountToolchain(adapter) is { } agent
            ? (agent.LoginArguments is { Count: > 0 }, agent.KeyVariable is { Length: > 0 },
               agent.LoginArguments is { Count: > 0 } && agent.DeviceLoginArguments is { Count: > 0 })
            : (false, false, false);

    /// <summary>Where an account runs, as the page reads it: each scope, a null workspace for this machine, its list and default.</summary>
    internal static object[] PlacesShown(IReadOnlyList<AccountPlace> places) =>
        [.. places.Select(place => (object)new { place.Workspace, place.List, place.Default })];

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
    /// Something the person pressed ran on an agent (ROSTER1): an account's edit, a key, a sign-in's end, an install, an
    /// update or a pin. A person looking again lets a refused account through (AGT3b) and ends the tool's own home's cool-off
    /// (D125 §3.7), as every such act always did; and what the act changed is read again, nothing else: the binary, where it
    /// changed which binary runs. An account's edit reads no account, and a sign-in's end reads its account itself.
    /// </summary>
    internal static async Task ChangedAsync(
        DriverLoop loop, string harness, string action, DriverConfig config, CancellationToken cancellationToken)
    {
        loop.Harnesses.LookedAgain();
        if (action is "install" or "update" or "pin" or "unpin")
        {
            await loop.Harnesses.BinaryChangedAsync(harness, config, cancellationToken);
        }
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
    // line by line through the console, because it is a process like any other. What it changed at once (a key, a name, a
    // join, a default) is the roster's, which it declares; an action that runs on says its end as `HARNESS_ENDED` (STATEHUB1h).
    [DriverRoute("HARNESS_ACTION")]
    [Publishes("HARNESS_ACTION", StateScopes.Harnesses)]
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
        var stream = Actions.Relay(harness, action);
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
            // Not read: a key account is said by its handle, never signed in, since the tool says yes for any key (AGT3).
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            return new
            {
                Harness = harness, Action = action, ExitCode = 0, Profile = account,
                Key = HarnessKeys.Handle(HarnessKeys.Of(_loop.Harnesses.Home, owner, account)!),
            };
        }

        // A new key into the account that holds a refused one (KEYREPLACE1a): the same id, so its name, lists, defaults and
        // order stand; what marked it refused is cleared here and, through `LookedAgain`, in the roster. Answered by the
        // handle only, as `key-add` is. The terminal's twin is `agent key <agent> --account`.
        if (action == "key-replace")
        {
            if (_loop.Harnesses.AccountToolchain(harness)?.KeyVariable is not { Length: > 0 })
            {
                throw new DriverException(
                    $"`{harness}` takes no API key from Daoris — sign it in instead.");
            }

            var replaced = HarnessKeys.Replace(
                _loop.Harnesses.Home, owner, Named(request), PayloadHelper.GetRequiredValue<string>(request.Payload, "key"));
            // The roster's own memory of the account: its last word and what it was owed, forgotten as a removal forgets them.
            _loop.Harnesses.Removed(owner, replaced);
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            return new
            {
                Harness = harness, Action = action, ExitCode = 0, Profile = replaced,
                Key = HarnessKeys.Handle(HarnessKeys.Of(_loop.Harnesses.Home, owner, replaced)!),
            };
        }

        // An account's name (ACCT2), the person's word, kept beside it by its id, so every list, default, reading and
        // record keeps it; naming it its own id, or none, gives it none. The terminal's twin is `profile rename`.
        if (action == "profile-rename")
        {
            var renamed = Named(request);
            var name = AccountNames.Rename(
                _loop.Harnesses.Home, owner, HarnessSettings.Profiles(_loop.Harnesses.Home, owner), renamed, Optional(request, "name"));
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            return new { Harness = harness, Action = action, ExitCode = 0, Profile = renamed, Name = name };
        }

        // An account put into the lists the page names (ACCT1): `join`, each a workspace's name or null for this machine's
        // list, each refused before anything is written where it cannot be joined. The terminal's twin is `profile join`.
        if (action == "profile-join")
        {
            var account = Named(request);
            if (!HarnessSettings.Profiles(_loop.Harnesses.Home, owner).Contains(account, StringComparer.Ordinal))
            {
                throw new DriverException($"`{owner}` has no account `{account}` on this machine, so it joins no list.");
            }

            var lists = Joins(request);
            if (lists.Count == 0) throw new DriverException("name the lists it joins: a workspace's, or this machine's.");
            var placed = AgentActions.Joined(_loop, owner, account, lists);
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            _loop.Nudge();
            return new { Harness = harness, Action = action, ExitCode = 0, Profile = account, Places = PlacesShown(placed) };
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
            "profile-default" => ProfileDefault(_loop, owner,Optional(request, "profile"), Optional(request, "workspace")),
            "install" or "update" or "login" or "login-new" or "pin" => null,
            _ => throw Refusals.Because(
                Refusals.HarnessActionUnknown,
                $"unknown agent action '{action}' — one of: {string.Join(", ", AgentActions.Words)}",
                ("action", action)),
        };
        if (edited is { } code)
        {
            // The report reads the wiring, the directories and the readings fresh at every question, so an edit reads
            // no account again (ROSTER1); an unpin asks its binary.
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
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
        // A sign-in's `join` and a new account's `name` (ACCT1, ACCT2), the terminal's `--join` and `--name`.
        var joins = action is "login" or "login-new" ? Joins(request) : [];
        var named = action == "login-new" ? Optional(request, "name") : null;
        // A sign-in's way, its browser first, and whether it switches from the one running (CODEXLOGIN1): the terminal's
        // `--device-code`. A word that is no way is refused before anything starts.
        var signsIn = action is "login" or "login-new";
        var way = signsIn ? SignInWays.Of(Optional(request, "way")) : SignInWay.Browser;
        var switching = signsIn && Flag(request, "switch");
        var reached = await Actions.StartProcessActionAsync(
            harness, action, profile, version, toolchain, command, stream, config, joins: joins, name: named, way: way, switching: switching);
        return new { Harness = harness, Action = action, Started = true, Profile = reached };
    }

    /// <summary>
    /// The lists a sign-in or <c>profile-join</c> names (ACCT1): <c>join</c>, each a workspace's name or null for this
    /// machine's list, in the order named, each once. A value that is neither is a malformed call.
    /// </summary>
    private static IReadOnlyList<string?> Joins(IpcRequest request)
    {
        if (request.Payload is not { } payload || !payload.TryGetProperty("join", out var join) || join.ValueKind == JsonValueKind.Null) return [];
        if (join.ValueKind != JsonValueKind.Array) throw new DriverException("`join` is a list of workspaces, null for this machine's list.");

        var lists = new List<string?>();
        foreach (var each in join.EnumerateArray())
        {
            var scope = each.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String when each.GetString()!.Trim() is { Length: > 0 } workspace => workspace,
                _ => throw new DriverException("`join` names a workspace by its name, or this machine's list by null."),
            };
            if (!lists.Contains(scope, StringComparer.OrdinalIgnoreCase)) lists.Add(scope);
        }

        return lists;
    }

    // The two things a screen may do to a harness action while it runs (2026-09-23): answer
    // the prompt it printed — a login asks for the code the browser shows, and waits — and
    // stop it. Either is refused, naming the action, when nothing runs under that name: an
    // answer that went nowhere must not look delivered.
    [DriverRoute("HARNESS_INPUT")]
    private object? HarnessInput(IpcRequest request)
    {
        Actions.Running(request).Send(PayloadHelper.GetRequiredValue<string>(request.Payload, "text"));
        return new { Sent = true };
    }

    [DriverRoute("HARNESS_CANCEL")]
    private object? HarnessCancel(IpcRequest request)
    {
        Actions.Cancel(request);
        return new { Cancelled = true };
    }

    // An account's own model and effort (AGT6, D98): keys in the tool's own settings file under
    // that account, the same file `daoris agent settings` edits (D50). A key sent as null is
    // cleared and one left out is untouched. Never the tool's own configuration home, which the
    // roster says Daoris never touches, and never an account a setting would bring into being.
    [DriverRoute("SET_AGENT_SETTINGS")]
    [Publishes("SET_AGENT_SETTINGS", StateScopes.Harnesses)]
    private object? SetAgentSettings(IpcRequest request)
    {
        var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
        var (owner, profile, read) = WriteAgentSettings(
            _loop, harness, Optional(request, "profile"), () => (Edit(request, "model"), Edit(request, "effort"), PerModel(request)));
        return new { Harness = owner, Profile = profile, read.Model, read.Effort, PerModel = PerModelOf(read), read.Problem };
    }

    // What sessions consumed (TOOL3/D57 §4) — measured before it is managed.
    //
    // 🔴 Over this bridge and nowhere else. Per-account usage names a credential profile, and
    // a profile name is already served only over loopback (`ToSession`), so this inherits
    // that boundary rather than arguing for its own. There is no HTTP route onto it. Its builder is the pushed slice's
    // (`UsageStateSlice`, PUSH1e), so the pull an older page makes and the push cannot drift.
    [DriverRoute("USAGE")]
    private Task<object?> UsageAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(UsageStateSlice.Answer(_loop.Usage));

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

    /// <summary>Who signed in, with what tells a second sign-in of the email apart after it (ACCOUNTID1c, <c>AccountNames.ApartWords</c>).</summary>
    private static string Told(string account, string? apart) => apart is null ? account : $"{account} · {apart}";

    /// <summary>
    /// The sign-in's end sentence (ACCTNAME1): the email offered as the name is said once, as the terminal's twin says it;
    /// where the email is another sign-in's, the organisation (else the plan) is said after it (ACCOUNTID1c), and a name that
    /// is the email with it beside is "that name".
    /// </summary>
    public static string SignedInSentence(string? account, string owner, string? called, string fresh, string? apart = null) =>
        account is { Length: > 0 }
            ? called is not null && (string.Equals(account, called, StringComparison.OrdinalIgnoreCase)
                    || (apart is not null && string.Equals(AccountNames.Beside(account, apart), called, StringComparison.OrdinalIgnoreCase)))
                ? $"signed in as {Told(account, apart)} — this machine lists it by that name."
                : $"signed in as {Told(account, apart)} — this machine lists it as `{called ?? fresh}`."
            : $"signed in — `{owner}` did not say who, so this machine lists it as `{called ?? fresh}`.";

    /// <summary>
    /// An account's own model and effort, written — <c>SET_AGENT_SETTINGS</c>'s own write, and the one Ask
    /// Daoris's Apply makes too (HELP6). Never the tool's own configuration home, and never an account a
    /// setting would bring into being.
    /// </summary>
    /// <param name="edits">The keys to change, read only once the account is known to be one Daoris keeps.</param>
    internal static (string Owner, string Profile, AgentSettingsRead Read) WriteAgentSettings(
        DriverLoop loop, string harness, string? profile,
        Func<(AgentSettingEdit? Model, AgentSettingEdit? Effort, IReadOnlyDictionary<string, AgentSettingEdit>? PerModel)> edits)
    {
        var toolchain = loop.Harnesses.Toolchain(harness)
            ?? throw new DriverException(
                $"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
        var owner = toolchain.Owner(harness);
        var file = loop.Harnesses.AccountToolchain(harness)?.SettingsFile
            ?? throw new DriverException(
                $"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, "
                + "so Daoris offers none — set its model with the tool itself.");
        var named = profile
            ?? throw new DriverException(
                $"name the account these settings are for — `{owner}`'s own configuration home is the tool's, "
                + "and Daoris never touches it.");
        var accounts = HarnessSettings.Profiles(loop.Harnesses.Home, owner);
        if (!accounts.Contains(named, StringComparer.Ordinal))
        {
            throw new DriverException(
                $"`{owner}` has no account `{named}` on this machine — accounts that exist: "
                + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
        }

        var (model, effort, perModel) = edits();
        var read = AgentSettings.Write(
            Path.Combine(HarnessSettings.ProfileHome(loop.Harnesses.Home, owner, named), file), model, effort, perModel);
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
        // leaves the wiring: the next account made takes the first free name, which may be this one's, and so starts never
        // read (ROSTER1).
        _loop.Harnesses.Settings.WithoutAccount(harness, profile).Save(_loop.Harnesses.SettingsPath);
        _loop.Harnesses.Removed(harness, profile);
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
    internal static int ProfileDefault(DriverLoop loop, string harness, string? profile, string? workspace)
    {
        var settings = loop.Harnesses.Settings;
        AccountsRoutes.DefaultAllowed(settings, harness, profile, workspace);
        DefaultEdited(settings, harness, profile, workspace).Save(loop.Harnesses.SettingsPath);
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
