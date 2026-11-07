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
    [DriverRoute("HARNESSES", PipeClass.Open)]
    private Task<object?> HarnessesAsync(NoPayload _, CancellationToken cancellationToken) =>
        ReadHarnessesAsync(null, null, refresh: false, cancellationToken);

    // The person pressing *Read again* (HARNESSREFRESH1, D169): every agent's accounts, with `agent` one agent's accounts, or
    // with `profile` too that one account alone; never an agent's own sign-in (POOL1a3, D197 point 3). Its own route so it can
    // declare what it moved, which the read above must not: it answers the roster as the read does, and every window asks its
    // roster again.
    [DriverRoute("HARNESSES_REFRESH", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("HARNESSES_REFRESH", None = OwnerNone.MachineSetting)]
    [GroundRead("HARNESSES_REFRESH", nameof(PipeReads.HarnessToRefresh))]
    [Says("HARNESSES_REFRESH", "agent:agent")]
    [Publishes("HARNESSES_REFRESH", StateScopes.Harnesses)]
    private Task<object?> RefreshHarnessesAsync(HarnessesRefreshPress press, CancellationToken cancellationToken) =>
        ReadHarnessesAsync(press.Agent, press.Profile, refresh: true, cancellationToken);

    private async Task<object?> ReadHarnessesAsync(string? agent, string? profile, bool refresh, CancellationToken cancellationToken)
    {
        var account = agent is null ? null : profile;

        var config = DriverConfig.Load(_loop.ConfigPath);
        IReadOnlyList<HarnessReport> roster;
        if (agent is not null)
        {
            // Through the agent's account-owning door, which every door onto it shares (AGT7), so each account is asked
            // once, not once a door: one at a time; or that one account alone (ROSTER1).
            var door = AccountDoor(agent)
                ?? throw new DriverException($"no agent `{agent}` on this machine has accounts to read.");
            await _loop.Harnesses.ReportAsync(door, config, refresh: true, cancellationToken, account: account);
            roster = await _loop.Harnesses.RosterAsync(config, refresh: false, cancellationToken);
        }
        else
        {
            roster = await _loop.Harnesses.RosterAsync(config, refresh, cancellationToken);
        }

        return new
        {
            _loop.Harnesses.SettingsPath,
            Adapter = ActorChoices.BuildAgent(config),
            Harnesses = Doors(_loop.Harnesses, roster),
            // The sheet's half (COAGENT1e, the agent-provider design §6): each agent once, from its registration, with its account
            // acts and each door's sheet. The page reads every agent fact here, never a door standing for its agent.
            Agents = _loop.Harnesses.Adapters.Facts.Agents.Where(agent => agent.Doors.Count > 0).ToArray(),
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
                var toolchain = harnesses.AgentOf(report.Adapter);
                var pinned = settings.ResolveVersion(report.Adapter, null, null);
                // The tool's own settings file under an account (AGT6, D98), where this build
                // knows it: a door's is its owner's (AGT7), and null offers nothing.
                var settingsFile = harnesses.AccountAgentOf(report.Adapter)?.Accounts.SettingsFile;
                var accountActs = AccountActs(harnesses, report.Adapter);
                return new
                {
                    Door = report.Adapter,
                    report.Present,
                    report.Version,
                    report.Problem,
                    // The managed toolchain (TOOL2/D57). `Pinned` is what the machine asked for
                    // and `Managed` is what is actually there — they differ exactly when a pin
                    // names a version nobody installed, which the surface must say rather than
                    // imply the pin is in force.
                    Pinned = pinned,
                    Managed = HarnessSettings.ManagedBinary(
                        harnesses.Home, report.Adapter,
                        pinned,
                        toolchain?.Program.Binary ?? []),
                    // Whether this harness CAN be pinned at all. A harness that declares neither
                    // a package nor a maker's channel (AGT2b) has no version for Daoris to fetch,
                    // and a surface offering the control anyway would be a button whose only
                    // outcome is a refusal.
                    Pinnable = toolchain is { } pinnable
                        && (pinnable.Program.Package is { Length: > 0 } || pinnable.Program.Channel is { Length: > 0 }),
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
                    Installs = toolchain?.Program.Install is { Count: > 0 },
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
                    // Its transport, as its sheet says it (COAGENT1e).
                    Transport = harnesses.Adapters.Facts.Of(report.Adapter)?.Door(report.Adapter) is { Transport: var transport } ? transport : null,
                    // Whether a session on this door keeps a conversation (D76 §1) — what the page
                    // reads an empty record by, rather than guessing from the emptiness (CONV3b).
                    Structured = harnesses.Structured(report.Adapter),
                    // Whether a session on this door is handed the rules file (PERM1, D72): the agent's
                    // page has *What it may do* only where Daoris hands its agent one (UX6e, D150 §5.1).
                    TakesRules = harnesses.Adapters.Door(report.Adapter).Can.TakesSettings,
                    // The plugin this harness came from (D64), or null for one this build carries
                    // — shown beside it, so a person knows which folder to look in.
                    Plugin = harnesses.Adapters.DeclaredBy(report.Adapter),
                    // The profile HOME is a machine path, and this bridge is the one surface
                    // allowed to carry one (D47 §4) — the page renders it so a person can find
                    // the directory they were told Daoris owns.
                    Profiles = report.Profiles.Select(profile => ProfileShown(profile, settingsFile)).ToArray(),
                    // What the tool itself offers for those two keys (AGT6, D98): its aliases and
                    // the efforts its settings keep. Null where Daoris does not know its settings,
                    // and the page then offers nothing and says so.
                    SettingsChoices = settingsFile is null
                        ? null
                        : new { AgentSettings.Models, AgentSettings.Efforts },
                    // What a session on this door may carry (the agent-provider design, row n): the scope the page's actors offer, apart
                    // from the account's settings above, with `max` only where a session may carry it.
                    SessionChoices = SessionChoices.Of(harnesses.Home, harnesses.Adapters, report.Adapter),
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
                    // No machine's default and no workspace's (POOL1a2, the account-pool design §5.2): the order's first says where
                    // starts begin, and `ACCOUNTS` answers the order.
                };
            })];
    }

    /// <summary>
    /// One account on the roster (<c>HARNESSES</c>): its id and the person's name for it, its home, its state as last read, who
    /// signed in, its key's handle and its own model and effort. No places (POOL1a2, the account-pool design §5.2): every account
    /// of Daoris's serves every workspace. Public for the fast half's table, which reads it without the roster the route asks.
    /// </summary>
    /// <param name="settingsFile">The tool's own settings file under an account (AGT6), or null where this build knows none.</param>
    public static object ProfileShown(ProfileReport profile, string? settingsFile) => new
    {
        // The account's stable id (ACCT2): its folder's name, and what every list, default, reading and record names.
        // `DisplayName` is the person's name for it, null where they gave none and it reads as its id.
        profile.Name,
        profile.DisplayName,
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
        harnesses.AccountAgentOf(adapter) is { } agent
            ? (agent.Accounts.LoginArguments is { Count: > 0 }, agent.Accounts.KeyVariable is { Length: > 0 },
               agent.Accounts.LoginArguments is { Count: > 0 } && agent.Accounts.DeviceLoginArguments is { Count: > 0 })
            : (false, false, false);

    /// <summary>
    /// The door an agent's accounts are read through (AGT7, UX6e): the one named for it, else the first door onto its
    /// accounts, as the page lists an agent's doors. Null where no door this build knows runs as that agent's accounts.
    /// </summary>
    private string? AccountDoor(string agent)
    {
        string? first = null;
        foreach (var name in _loop.Harnesses.Known)
        {
            AccountAgent? toolchain;
            try
            {
                toolchain = _loop.Harnesses.AgentOf(name);
            }
            catch (DriverException)
            {
                continue;
            }

            if (toolchain is null || !string.Equals(_loop.Harnesses.Adapters.OwnerOfAgent(name).Value, agent, StringComparison.OrdinalIgnoreCase)) continue;
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
    [DriverRoute("STARTS", PipeClass.Open)]
    private async Task<object?> StartsAsync(StartsRead press, CancellationToken cancellationToken)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        var named = (press.Workspaces ?? []).Where(circle => circle.Length > 0).ToList();

        var starts = new List<object>();
        foreach (var workspace in named.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            // The work: a driven session, which is also what a conversation started without a
            // pick takes.
            starts.Add(Start("work", workspace,
                await _loop.Harnesses.WiringAsync(ActorChoices.BuildAgent(config), config, workspace, cancellationToken)));

            // The intake (INT4b, AGT6), once an agent is named for it: the same `SelectAsync`
            // the intake takes for an ask in this circle. 🔴 An agent this build has no adapter
            // for is a held row in the driver's own sentence — the one the intake would hold
            // with — never a refusal of the whole answer, which would take the work's rows too.
            if (ActorKey.FirstAgent("intake", config) is { Length: > 0 } intake)
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

        return new { Adapter = ActorChoices.BuildAgent(config), Starts = starts };

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

        AccountAgent? Known(string adapter)
        {
            try
            {
                return _loop.Harnesses.AgentOf(adapter);
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
    [DriverRoute("HARNESS_ACTION", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("HARNESS_ACTION", None = OwnerNone.MachineSetting)]
    [GroundRead("HARNESS_ACTION", nameof(PipeReads.HarnessToAct))]
    [Says("HARNESS_ACTION", "door:agent")]
    [Publishes("HARNESS_ACTION", StateScopes.Harnesses)]
    private async Task<object?> HarnessActionAsync(HarnessActionPress press, CancellationToken cancellationToken)
    {
        var (action, harness) = (press.Action, press.Door);
        var config = DriverConfig.Load(_loop.ConfigPath);
        // A DriverException, so it travels the same way `Toolchain` already refuses an adapter
        // name it does not know: one mapping, one shape, and the driver's own sentence intact.
        var toolchain = ManagedAgents.Of(_loop.Harnesses, harness);

        var command = config.Commands.GetValueOrDefault(harness);
        var stream = Actions.Relay(harness, action);
        var profile = press.Profile;
        // 🔴 Whose accounts an action on this door touches (AGT7): a door's accounts, defaults
        // and keys are its OWNER's. Its pin and its installer stay its own.
        var owner = _loop.Harnesses.Adapters.OwnerOfAgent(harness).Value;

        // 🔴 An account that is an API key (AGT3, D67 §1). The key crosses this bridge once,
        // inward, and is answered only by its handle — here, on the roster, and in any event.
        if (action == "key-add")
        {
            if (_loop.Harnesses.KeyVariableOf(harness) is null)
            {
                throw new DriverException(
                    $"`{harness}` takes no API key from Daoris — sign it in instead.");
            }

            var account = HarnessKeys.Add(
                _loop.Harnesses.Home, owner, ActField.Needs(press.Key, "key"));
            // Not read: a key account is said by its handle, never signed in, since the tool says yes for any key (AGT3).
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            return new
            {
                Door = harness, Action = action, ExitCode = 0, Profile = account,
                Key = HarnessKeys.Handle(HarnessKeys.Of(_loop.Harnesses.Home, owner, account)!),
            };
        }

        // A new key into the account that holds a refused one (KEYREPLACE1a): the same id, so its name, lists, defaults and
        // order stand; what marked it refused is cleared here and, through `LookedAgain`, in the roster. Answered by the
        // handle only, as `key-add` is. The terminal's twin is `agent key <agent> --account`.
        if (action == "key-replace")
        {
            if (_loop.Harnesses.KeyVariableOf(harness) is null)
            {
                throw new DriverException(
                    $"`{harness}` takes no API key from Daoris — sign it in instead.");
            }

            var replaced = HarnessKeys.Replace(
                _loop.Harnesses.Home, owner, Named(press.Profile), ActField.Needs(press.Key, "key"));
            // The roster's own memory of the account: its last word and what it was owed, forgotten as a removal forgets them.
            _loop.Harnesses.Removed(owner, replaced);
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            return new
            {
                Door = harness, Action = action, ExitCode = 0, Profile = replaced,
                Key = HarnessKeys.Handle(HarnessKeys.Of(_loop.Harnesses.Home, owner, replaced)!),
            };
        }

        // An account's name (ACCT2), the person's word, kept beside it by its id, so every list, default, reading and
        // record keeps it; naming it its own id, or none, gives it none. The terminal's twin is `profile rename`.
        if (action == "profile-rename")
        {
            var renamed = Named(press.Profile);
            var name = AccountNames.Rename(
                _loop.Harnesses.Home, owner, HarnessSettings.Profiles(_loop.Harnesses.Home, owner), renamed, press.Name);
            await ChangedAsync(_loop, harness, action, config, cancellationToken);
            return new { Door = harness, Action = action, ExitCode = 0, Profile = renamed, Name = name };
        }

        // One set of Daoris's accounts serves every workspace (POOL1a, D197; the account-pool design §5.2): there is no list to join
        // and no default beside the order, so the page's `profile-join` and `profile-default` are refused; their presses leave with POOL1c.
        if (action is "profile-join" or "profile-default") throw AccountsRoutes.OneSet(owner, action == "profile-join" ? "a list to join" : "a default");

        // The file edits answer at once, with the exit code.
        int? edited = action switch
        {
            "unpin" => Unpin(harness),
            // 🔴 The credential profiles, from a SCREEN (DEPLOY3). They existed only as
            // `daoris agent account add|remove|default`, so the Machine view could list a
            // profile and log into one and never make one — D50 violated in the direction
            // nothing tests, since the rule is written "whatever a screen can set, a
            // terminal can" and the converse had no check.
            //
            // A sign-in stays the tool's: adding one MAKES A DIRECTORY and nothing else,
            // and what lands inside it is the harness's own.
            "profile-add" => ProfileAdd(owner, press.Profile),
            "profile-remove" => ProfileRemove(owner, press.Profile, stream),
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
            return new { Door = harness, Action = action, ExitCode = code };
        }

        // A pin's version is asked for before anything starts: a pin without one is a malformed call.
        var version = action == "pin" ? ActField.Needs(press.Version, "version") : null;
        // A sign-in's `join` and a new account's `name` (ACCT1, ACCT2), the terminal's `--join` and `--name`.
        var joins = action is "login" or "login-new" ? Joins(press.Join) : [];
        var named = action == "login-new" ? press.Name : null;
        // A sign-in's way, its browser first, and whether it switches from the one running (CODEXLOGIN1): the terminal's
        // `--device-code`. A word that is no way is refused before anything starts.
        var signsIn = action is "login" or "login-new";
        var way = signsIn ? SignInWays.Of(press.Way) : SignInWay.Browser;
        var switching = signsIn && press.Switch;
        var reached = await Actions.StartProcessActionAsync(
            harness, action, profile, version, toolchain, command, stream, config, joins: joins, name: named, way: way, switching: switching);
        return new { Door = harness, Action = action, Started = true, Profile = reached };
    }

    /// <summary>
    /// The lists a sign-in or <c>profile-join</c> names (ACCT1): <c>join</c>, each a workspace's name or null for this
    /// machine's list, in the order named, each once. A value that is neither is a malformed call.
    /// </summary>
    private static IReadOnlyList<string?> Joins(JsonElement? sent)
    {
        if (sent is not { } join || join.ValueKind == JsonValueKind.Null) return [];
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
    [DriverRoute("HARNESS_INPUT", PipeClass.Window)]
    [Owner("HARNESS_INPUT", None = OwnerNone.NamesNothing)]
    private object? HarnessInput(HarnessInputPress press)
    {
        Actions.Running(press.Door, press.Action).Send(press.Text);
        return new { Sent = true };
    }

    [DriverRoute("HARNESS_CANCEL", PipeClass.Window)]
    [Owner("HARNESS_CANCEL", None = OwnerNone.NamesNothing)]
    private object? HarnessCancel(HarnessCancelPress press)
    {
        Actions.Cancel(press.Door, press.Action);
        return new { Cancelled = true };
    }

    // An account's own model and effort (AGT6, D98): keys in the tool's own settings file under
    // that account, the same file `daoris agent settings` edits (D50). A key sent as null is
    // cleared and one left out is untouched. Never the tool's own configuration home, which the
    // roster says Daoris never touches, and never an account a setting would bring into being.
    [DriverRoute("SET_AGENT_SETTINGS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_AGENT_SETTINGS", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_AGENT_SETTINGS", nameof(PipeReads.ProfileSettings))]
    [Says("SET_AGENT_SETTINGS", "door:agent")]
    [NotAlready("SET_AGENT_SETTINGS", "merges into a profile's settings")]
    [Publishes("SET_AGENT_SETTINGS", StateScopes.Harnesses)]
    private object? SetAgentSettings(AgentSettingsPress press)
    {
        var (owner, profile, read) = Daoris.Driver.AccountSettings.Write(
            _loop.Harnesses, press.Door, press.Profile, () => (Edit(press.Model, "model"), Edit(press.Effort, "effort"), PerModel(press.PerModel)));
        return new { Door = owner, Profile = profile, read.Model, read.Effort, PerModel = PerModelOf(read), read.Problem };
    }

    // What sessions consumed (TOOL3/D57 §4) — measured before it is managed.
    //
    // 🔴 Over this bridge and nowhere else. Per-account usage names a credential profile, and
    // a profile name is already served only over loopback (`ToSession`), so this inherits
    // that boundary rather than arguing for its own. There is no HTTP route onto it. Its builder is the pushed slice's
    // (`UsageStateSlice`, PUSH1e), so the pull an older page makes and the push cannot drift.
    [DriverRoute("USAGE", PipeClass.Open)]
    private Task<object?> UsageAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(UsageStateSlice.Answer(_loop.Usage, UsageStateSlice.AgentsOf(_loop)));

    /// <summary>
    /// One setting's change as the page sent it (AGT6): a text sets it, null clears it, and a key left out
    /// is no change at all — the three a person can mean, kept apart.
    /// </summary>
    private static AgentSettingEdit? Edit(JsonElement? sent, string name) =>
        sent is { } value
            ? value.ValueKind switch
            {
                JsonValueKind.Null => new AgentSettingEdit(null),
                JsonValueKind.String => new AgentSettingEdit(value.GetString()),
                _ => throw new DriverException($"`{name}` is set with a name, or cleared with null."),
            }
            : null;

    /// <summary>An effort per model as the page sent it: the model's name, then a value or null.</summary>
    private static IReadOnlyDictionary<string, AgentSettingEdit>? PerModel(JsonElement? sent)
    {
        if (sent is not { ValueKind: JsonValueKind.Object } models)
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

    /// <summary>The sign-in's end sentence (ACCTNAME1, ACCOUNTID1c): the library's, which the terminal's sign-in says too (PUSH1f2e6).</summary>
    public static string SignedInSentence(string? account, string owner, string? called, string fresh, string? apart = null) =>
        AccountNames.SignedInSentence(account, owner, called, fresh, apart);

    /// <summary>
    /// Make a credential profile under a name the caller chose: a directory, and nothing else (DEPLOY3).
    /// </summary>
    /// <remarks>
    /// Idempotent, exactly as the CLI verb is — asking for one that exists is an answer, not a
    /// failure. The page makes accounts by signing in (<c>login-new</c>, D66 §3); this is the
    /// bridge's half of <c>daoris agent account add</c>, for a name a person picks before signing in.
    /// </remarks>
    private int ProfileAdd(string harness, string? named)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_loop.Harnesses.Home, harness, Named(named)));
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
    /// is <c>daoris agent account remove</c>.</para>
    ///
    /// <para>Only ever a profile: the tool's own configuration home is not under Daoris's directory,
    /// and no name reaches it.</para>
    ///
    /// <para>A delete that fails — a running session holding a file open in it — says so and leaves
    /// the wiring as it was, because the account is still there to point at.</para>
    /// </remarks>
    private int ProfileRemove(string harness, string? named, Action<string> stream)
    {
        var profile = Named(named);
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
        HarnessSettings.Change(
            _loop.Harnesses.SettingsPath, read => read.WithoutAccount(harness, profile), account: new AccountChange(harness, profile, Added: false),
            agents: _loop.Harnesses.Agents);
        _loop.Harnesses.Removed(harness, profile);
        return 0;
    }

    /// <summary>Back to `PATH`. Nothing is deleted — re-pinning that version needs no download.</summary>
    private int Unpin(string harness)
    {
        HarnessSettings.Change(_loop.Harnesses.SettingsPath, read => read.WithVersion(harness, null), agents: _loop.Harnesses.Agents);
        return 0;
    }
}
