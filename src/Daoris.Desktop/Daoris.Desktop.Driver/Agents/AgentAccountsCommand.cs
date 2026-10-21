using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver agent unpin</c> and <c>agent profile list|add|remove|default|order|ready|use|rename|join</c> (PUSH1f2e2, PUSH1f2e3, D194, the
/// alone-path design §5): a pin taken back and an agent's accounts from a terminal, the words <c>daoris agent …</c> hands this program.
/// Each change is the Agents or Accounts place's own press while Daoris runs (<c>HARNESS_ACTION</c>'s <c>unpin</c> and
/// <c>profile-*</c>, <c>ACCOUNT_USE</c>'s <c>order</c>, <c>ready</c> and <c>use</c>), else the same write on the terminal's word; a workspace's
/// unpin has no route and acts alone. Every check the window would refuse by is said here first, in today's words
/// (<c>fixtures/agents-text.json</c>), so a refusal costs nothing on either path.
/// </summary>
internal static class AgentAccountsCommand
{
    private const string Prefix = "daoris";

    /// <summary>The profile verbs this program has, every one the CLI's family had (<c>use</c> since PUSH1f2e3).</summary>
    public static IReadOnlyList<string> ProfileVerbs { get; } = ["list", "add", "remove", "default", "order", "ready", "use", "rename", "join"];

    /// <summary>
    /// Whether the words after <c>agent</c> are a profile verb that changes a home, held from the top: all but <c>list</c>, and of
    /// <c>use</c> one that names a setting or <c>--clear</c>; with none it prints the scope's settings, a read.
    /// </summary>
    public static bool ProfileChanges(IReadOnlyList<string> args)
    {
        var verb = args.Count > 1 ? args[1] : "list";
        if (verb == "use")
        {
            return AgentsCommand.Operands(args, AgentsCommand.Valued).Count > 3
                || args.Skip(2).Any(word => word.StartsWith("--", StringComparison.Ordinal) && word != "--workspace");
        }

        return verb != "list" && ProfileVerbs.Contains(verb, StringComparer.Ordinal);
    }

    private const string Written = "machine-local, tracked by nothing, like every wiring file here.";

    // ——— unpin

    /// <summary>
    /// <c>unpin &lt;agent&gt; [--workspace &lt;name&gt;]</c> (TOOL2, D57): back to <c>PATH</c>, the machine's or one workspace's pin
    /// taken off. Nothing is deleted. The machine's is <c>HARNESS_ACTION</c>'s <c>unpin</c>; a workspace's has no route.
    /// </summary>
    public static AgentPress Unpin(IReadOnlyList<string> args, AgentsWorld world)
    {
        var path = AgentsCommand.WiringPath(world);
        var (name, _, _, _) = AgentsCommand.Agent(args, "unpin", "<agent>", world, path);
        var workspace = AgentsCommand.FlagValue(args, "--workspace");
        var circle = workspace?.Trim() is { Length: > 0 } trimmed ? trimmed : null;

        int Said(object? _)
        {
            world.Instance.Out.WriteLine(workspace is not null
                ? $"{Prefix}: the `{workspace}` workspace no longer pins `{name}` on this machine."
                : $"{Prefix}: `{name}` runs from PATH again on this machine.");
            world.Instance.Out.WriteLine("  Nothing was deleted — the managed install stays where it is, and re-pinning that");
            world.Instance.Out.WriteLine("  version needs no download.");
            return 0;
        }

        return new AgentPress("unpin", circle is null ? AgentRoutes.Harness : null, AgentsCommand.Isolated(args, world.Instance.Environment),
            new JsonObject { ["action"] = "unpin", ["harness"] = name },
            _ =>
            {
                HarnessSettings.Change(path, read => circle is null ? read.WithVersion(name, null) : read.WithWorkspaceVersion(circle, name, null));
                return Task.FromResult<object?>(null);
            },
            _ => null, Said);
    }

    // ——— profile

    /// <summary><c>profile [list] | add | remove | default | order | ready | rename | join …</c>, the words after <c>agent</c>.</summary>
    public static AgentPress? Profile(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        var action = args.Count > 1 ? args[1] : "list";
        var path = AgentsCommand.WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var settings = HarnessSettings.Load(path);
        var context = new Context(args, world, path, home, settings, $"profile {action}");

        switch (action)
        {
            case "list":
                foreach (var name in AgentsCommand.Named(world, path))
                {
                    var existing = HarnessSettings.Profiles(home, name);
                    world.Instance.Out.WriteLine($"  {name,-14} {(existing.Count > 0 ? string.Join(", ", existing) : "(none)")}");
                }

                return null;
            case "add":
                return Add(context);
            case "remove":
                return Remove(context);
            case "default":
                return Default(context);
            case "order":
                return Order(context);
            case "ready":
                return Ready(context);
            case "use":
                return Use(context, out exit);
            case "rename":
                return Rename(context);
            case "join":
                return Join(context);
            default:
                throw new AgentWords(
                    $"unknown agent profile verb '{action}' — one of: list, add, remove, default, order, ready, use, rename, join");
        }
    }

    /// <summary>What every profile verb reads: its words, its terminal, the wiring file and its home, and the wiring as it stands.</summary>
    private sealed record Context(
        IReadOnlyList<string> Args, AgentsWorld World, string Path, string Home, HarnessSettings Settings, string Verb)
    {
        public void Say(string line) => World.Instance.Out.WriteLine(line);

        public bool Isolated => AgentsCommand.Isolated(Args, World.Instance.Environment);

        public string? Operand(int index) => AgentsCommand.Operand(Args, index);

        /// <summary>The operand at <paramref name="index"/>, refused where it is not there, as the CLI's <c>bare</c>.</summary>
        public string Bare(int index, string shape) =>
            Operand(index) ?? throw new AgentWords($"`agent {Verb}` needs {shape} — e.g. `daoris agent {Verb} claude-code`");

        /// <summary>
        /// The agent whose accounts this verb touches (AGT7): a door's are its owner's, and the verb says so rather than quietly landing
        /// somewhere else than was typed.
        /// </summary>
        public string Owner()
        {
            var given = Operand(2);
            var named = AgentsCommand.Named(World, Path);
            if (given is not { Length: > 0 }) throw new AgentWords($"`agent {Verb}` needs an agent — one of: {string.Join(", ", named)}");
            var (name, toolchain, _, _) = AgentsCommand.Agent(Args, Verb, "<agent>", World, Path, at: 2);
            var owner = toolchain.Owner(name);
            if (owner != name) Say($"{Prefix}: `{name}` runs as `{owner}`'s accounts — this is one of them.");
            return owner;
        }

        /// <summary>An account by its id or its name (ACCT2): its id, or what was given where it names none, for the verb to refuse.</summary>
        public string Account(string owner, string given) => AgentsCommand.AccountNamed(Home, owner, given);

        public string? Workspace() => AgentsCommand.FlagValue(Args, "--workspace")?.Trim() is { Length: > 0 } named ? named : null;

        /// <summary>A scope's own list: the machine's, or the workspace's own, never the machine's standing in for it.</summary>
        public IReadOnlyList<string> OwnList(string owner, string? workspace) =>
            workspace is null
                ? Settings.Rotation.GetValueOrDefault(owner) ?? []
                : Settings.WorkspaceRotation.TryGetValue(workspace, out var lists) ? lists.GetValueOrDefault(owner) ?? [] : [];

        public string? OwnDefault(string owner, string? workspace) =>
            (workspace is null
                ? Settings.Defaults.GetValueOrDefault(owner)
                : Settings.Workspaces.TryGetValue(workspace, out var defaults) ? defaults.GetValueOrDefault(owner) : null) is { Length: > 0 } named
                ? named.Trim()
                : null;

        public string? OwnKeep(string owner, string? workspace) =>
            RotationUse.Read(workspace is null
                ? Settings.Uses.GetValueOrDefault(owner)
                : Settings.WorkspaceUses.TryGetValue(workspace, out var uses) ? uses.GetValueOrDefault(owner) : null).Use.Keep;
    }

    private static string Where(string? workspace) => workspace is null ? "on this machine" : $"in `{workspace}`";

    private static string Scoped(string? workspace) =>
        workspace is null ? "" : $" --workspace {ShellWord.Of(workspace, ShellWord.Workspace)}";

    private static Task<object?> Done(object? done = null) => Task.FromResult(done);

    /// <summary>A name a profile's folder could not take, in the layout's own words, said before anything is sent.</summary>
    private static string Folder(string home, string owner, string profile)
    {
        try
        {
            return HarnessSettings.ProfileHome(home, owner, profile);
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }
    }

    /// <summary><c>profile add &lt;agent&gt; &lt;profile&gt;</c> (DEPLOY3): a folder, and nothing else; one that is there is an answer.</summary>
    private static AgentPress Add(Context at)
    {
        var owner = at.Owner();
        var profile = at.Bare(3, "<agent> <profile>");
        var where = Folder(at.Home, owner, profile);
        var existed = Directory.Exists(where);
        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject { ["action"] = "profile-add", ["harness"] = owner, ["profile"] = profile },
            _ =>
            {
                Directory.CreateDirectory(where);
                return Done();
            },
            _ => null,
            _ =>
            {
                at.Say(existed
                    ? $"{Prefix}: `{owner}` profile `{profile}` already exists — {where}"
                    : $"{Prefix}: `{owner}` profile `{profile}` — {where}");
                at.Say($"  It is empty until you log into it: `daoris agent login {owner} --profile {ShellWord.Of(profile, ShellWord.Account)}`.");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile remove &lt;agent&gt; &lt;profile&gt;</c> (D66 §3): the account's folder and the sign-in in it gone, and no default, list
    /// or kept account naming it after. A delete the window could not finish is said, exit 1.
    /// </summary>
    private static AgentPress Remove(Context at)
    {
        var owner = at.Owner();
        var profile = at.Account(owner, at.Bare(3, "<agent> <profile>"));
        var where = Folder(at.Home, owner, profile);
        var existed = Directory.Exists(where);
        const string failed = "failed";
        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject { ["action"] = "profile-remove", ["harness"] = owner, ["profile"] = profile },
            _ =>
            {
                var removed = HarnessSettings.RemoveProfile(at.Home, owner, profile);
                HarnessSettings.Change(at.Path, read => read.WithoutAccount(owner, profile), account: new AccountChange(owner, profile, Added: false));
                return Done(removed);
            },
            answer => Number(answer, "exitCode") == 1 ? failed : existed,
            done =>
            {
                if (done is failed)
                {
                    at.World.Instance.Error.WriteLine($"{Prefix}: {where} was not removed — a session running as this account may hold a file open "
                        + "in it; stop it and remove it again. Part of it may already be gone.");
                    return 1;
                }

                at.Say(done is true
                    ? $"{Prefix}: removed the `{owner}` account `{profile}` — the directory and the sign-in in it are gone: {where}"
                    : $"{Prefix}: `{owner}` has no account `{profile}` on this machine — there was nothing to remove.");
                at.Say($"  It is no longer a default for `{owner}` anywhere on this machine: no default and no order names it.");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile default &lt;agent&gt; &lt;profile&gt;|--clear [--workspace &lt;name&gt;]</c> (D49 §4, LEFT3): the account a scope's starts
    /// begin on, refused where it is not here or its scope's own list does not hold it (D130 §3.1); <c>--clear</c> names none again.
    /// </summary>
    private static AgentPress Default(Context at)
    {
        var owner = at.Owner();
        var workspace = at.Workspace();
        HarnessSettings Edited(HarnessSettings read, string? profile) =>
            workspace is not null ? read.WithWorkspaceDefault(workspace, owner, profile) : read.WithDefault(owner, profile);
        JsonObject Payload(string? profile)
        {
            var payload = new JsonObject { ["action"] = "profile-default", ["harness"] = owner };
            if (profile is not null) payload["profile"] = profile;
            if (workspace is not null) payload["workspace"] = workspace;
            return payload;
        }

        if (at.Args.Contains("--clear"))
        {
            if (at.Operand(3) is { } named)
            {
                throw new AgentWords($"`--clear` names no account — `daoris agent profile default {owner} --clear` clears the default, "
                    + $"and `daoris agent profile default {owner} {ShellWord.Of(named, ShellWord.Account)}` sets it.");
            }

            var after = Edited(at.Settings, null);
            return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated, Payload(null),
                _ =>
                {
                    HarnessSettings.Change(at.Path, read => Edited(read, null));
                    return Done();
                },
                _ => null,
                _ =>
                {
                    var fallback = after.ResolveFrom(owner, workspace, null).Value;
                    at.Say(workspace is null
                        ? $"{Prefix}: this machine names no default for `{owner}` now: Daoris takes its accounts in turn."
                        : fallback is not null
                            ? $"{Prefix}: `{workspace}` names no account for `{owner}` now: its sessions run as the machine's default, `{fallback}`."
                            : $"{Prefix}: `{workspace}` names no account for `{owner}` now: its sessions take Daoris's accounts in turn.");
                    at.Say($"  No account was deleted. Written to {at.Path} — {Written}");
                    return 0;
                });
        }

        var profile = at.Account(owner, at.Bare(3, "<agent> <profile>|--clear [--workspace <name>]"));
        // Refused rather than created: a default that does not exist is a typo every start in that scope would refuse.
        var existing = HarnessSettings.Profiles(at.Home, owner);
        if (!existing.Contains(profile, StringComparer.Ordinal))
        {
            throw new AgentWords($"`{owner}` has no profile `{profile}` on this machine — `daoris agent profile add {owner} "
                + $"{ShellWord.Of(profile, ShellWord.Account)}` creates it. Profiles that exist: "
                + (existing.Count > 0 ? string.Join(", ", existing) : "(none)"));
        }

        // A scope's default is where its starts begin within its list, so one the list does not hold is refused (D130 §3.1).
        var list = at.OwnList(owner, workspace);
        if (ScopeProblem.Of(profile, list, null) is { Kind: ScopeProblemKind.Default })
        {
            throw new AgentWords($"`{owner}`'s list {Where(workspace)} is {string.Join(", then ", list)}, and `{profile}` is not in it — the "
                + "list is every account its starts may run on, and the default is where they begin within it. "
                + $"`daoris agent profile order {owner} {ShellWord.Words([.. list, profile], ShellWord.Account)}{Scoped(workspace)}` adds it, "
                + "or make one of the list the default.");
        }

        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated, Payload(profile),
            _ =>
            {
                HarnessSettings.Change(at.Path, read => Edited(read, profile));
                return Done();
            },
            _ => null,
            _ =>
            {
                at.Say(workspace is not null
                    ? $"{Prefix}: sessions in `{workspace}` run `{owner}` as `{profile}`."
                    : $"{Prefix}: this machine runs `{owner}` as `{profile}` by default.");
                at.Say($"  Written to {at.Path} — {Written}");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile order &lt;agent&gt; &lt;profile&gt;…|--clear [--workspace &lt;name&gt;]</c> (TOOL4e, D125 §3.1): the accounts rotation may
    /// use, in order, each here and once, holding the scope's default and its kept account (D130 §3.1, §4.6); <c>--clear</c> names none.
    /// </summary>
    private static AgentPress Order(Context at)
    {
        var owner = at.Owner();
        var workspace = at.Workspace();
        // An account by its id or its name (ACCT2); the list holds ids, so a rename leaves it standing.
        string[] named = [.. AgentsCommand.Operands(at.Args, AgentsCommand.Valued).Skip(3).Select(given => at.Account(owner, given))];
        JsonObject Payload(IEnumerable<string> accounts)
        {
            var payload = new JsonObject
            {
                ["action"] = "order", ["harness"] = owner, ["accounts"] = new JsonArray([.. accounts.Select(each => (JsonNode?)each)]),
            };
            if (workspace is not null) payload["workspace"] = workspace;
            return payload;
        }

        if (at.Args.Contains("--clear"))
        {
            if (named.Length > 0)
            {
                throw new AgentWords($"`--clear` names no account — `daoris agent profile order {owner} --clear` clears the order, and "
                    + $"`daoris agent profile order {owner} {ShellWord.Words(named, ShellWord.Account)}` sets it.");
            }

            var after = at.Settings.WithRotation(owner, null, workspace);
            return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, Payload([]),
                _ =>
                {
                    HarnessSettings.Change(at.Path, read => read.WithRotation(owner, null, workspace));
                    return Done();
                },
                _ => null,
                _ =>
                {
                    if (workspace is not null)
                    {
                        var machine = after.Rotation.GetValueOrDefault(owner) ?? [];
                        at.Say(machine.Count > 0
                            ? $"{Prefix}: `{workspace}` names no order for `{owner}` now: the machine's applies there, {string.Join(", then ", machine)}."
                            : $"{Prefix}: `{workspace}` names no order for `{owner}` now, and neither does this machine.");
                    }
                    else
                    {
                        at.Say($"{Prefix}: `{owner}` has no order on this machine now.");
                        string[] scoped = [.. after.WorkspaceRotation.Where(pair => pair.Value.ContainsKey(owner)).Select(pair => pair.Key)];
                        if (scoped.Length > 0) at.Say($"  A workspace's own still applies there: {string.Join(", ", scoped)}.");
                    }

                    at.Say($"  No account was deleted. Written to {at.Path} — {Written}");
                    return 0;
                });
        }

        if (named.Length == 0)
        {
            throw new AgentWords($"`agent profile order` needs the accounts, in order, or --clear — e.g. `daoris agent profile order {owner} "
                + "account-1 account-2`.");
        }

        // Refused rather than written, as `profile default` refuses a name that does not exist: a typo in an order is an account
        // rotation would walk past without saying why.
        var existing = HarnessSettings.Profiles(at.Home, owner);
        if (HarnessSettings.OrderProblem(existing, named) is { } problem) throw new AgentWords(problem.Sentence(owner, existing));

        string[] list = [.. named.Select(account => account.Trim())];
        if (ScopeProblem.Of(at.OwnDefault(owner, workspace), list, at.OwnKeep(owner, workspace)) is { } bound)
        {
            throw new AgentWords(OrderRefusal(owner, workspace, bound, list));
        }

        return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, Payload(list),
            _ =>
            {
                HarnessSettings.Change(at.Path, read => read.WithRotation(owner, list, workspace));
                return Done();
            },
            _ => null,
            _ =>
            {
                var spelled = string.Join(", then ", list);
                at.Say(workspace is not null
                    ? $"{Prefix}: in `{workspace}`, `{owner}`'s rotation is {spelled}."
                    : $"{Prefix}: on this machine, `{owner}`'s rotation is {spelled}.");
                at.Say("  An account not in it is never rotated into, and work resolved to it never moves (D125 §3.1).");
                at.Say($"  Written to {at.Path} — {Written}");
                return 0;
            });
    }

    /// <summary><c>profile order</c>'s refusal for a list that leaves out its scope's default or kept account (D130 §3.1, §4.6).</summary>
    private static string OrderRefusal(string owner, string? workspace, ScopeProblem problem, IReadOnlyList<string> list)
    {
        var named = $"`daoris agent profile order {owner} {ShellWord.Words([.. list, problem.Account], ShellWord.Account)}{Scoped(workspace)}`";
        var noKeep = $"`daoris agent profile use {owner} --no-keep{Scoped(workspace)}`";
        return problem.Kind switch
        {
            ScopeProblemKind.Default =>
                $"{(workspace is not null ? $"`{workspace}`'s" : "this machine's")} default for `{owner}` is `{problem.Account}`, and "
                + "this list does not hold it — the list is every account its starts may run on, and the default is where they "
                + $"begin within it. Name it in the list ({named}), or first make one of the list the default "
                + $"(`daoris agent profile default {owner} {ShellWord.Of(list[0], ShellWord.Account)}{Scoped(workspace)}`).",
            ScopeProblemKind.Keep =>
                $"`{problem.Account}` is kept for conversations {Where(workspace)}, and this list does not hold it — the "
                + $"kept account is one of the list. Name it in the list ({named}), or first keep none ({noKeep}).",
            _ =>
                $"`{problem.Account}` is kept for conversations, and this list holds no other account, so driven work "
                + $"would have none — name another account in it, or first keep none ({noKeep}).",
        };
    }

    /// <summary>
    /// <c>profile ready &lt;agent&gt; &lt;profile&gt;|--own</c> (TOOL4e, D125 §2.3): an account's cool-off ended early, the terminal's
    /// <i>Try now</i>; <c>--own</c> is the tool's own sign-in.
    /// </summary>
    private static AgentPress Ready(Context at)
    {
        var owner = at.Owner();
        var own = at.Args.Contains("--own");
        var account = at.Operand(3) is { } given ? at.Account(owner, given) : null;
        if (own && account is not null)
        {
            throw new AgentWords($"`--own` names no account — `daoris agent profile ready {owner} --own` is the tool's own sign-in, and "
                + $"`daoris agent profile ready {owner} {ShellWord.Of(account, ShellWord.Account)}` is that account.");
        }

        if (!own && account is null)
        {
            throw new AgentWords($"`agent profile ready` needs <agent> <profile>|--own — e.g. `daoris agent profile ready {owner} account-1`, "
                + "or `--own` for the tool's own sign-in.");
        }

        var now = at.World.Now();
        var who = account is null ? $"`{owner}`'s own sign-in" : $"the `{owner}` account `{account}`";
        var payload = new JsonObject { ["action"] = "ready", ["harness"] = owner };
        if (account is null) payload["own"] = true;
        else payload["profile"] = account;
        return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, payload,
            _ => Done(AccountCooling.End(at.Home, owner, account, now)),
            answer => answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("ended", out var ended) && ended.ValueKind == JsonValueKind.True,
            done =>
            {
                if (done is true)
                {
                    at.Say($"{Prefix}: {who} is offered again: no start waits on its limit now.");
                    at.Say("  If the limit still holds, the agent refuses the next start at once, and the account cools again until");
                    at.Say("  the time it names.");
                    return 0;
                }

                string[] others = [.. AccountCooling.Read(at.Home, now)
                    .Where(entry => string.Equals(entry.Agent, owner, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.Account ?? "its own sign-in")];
                at.Say($"{Prefix}: {who} is not cooling, so nothing changed — "
                    + (others.Length > 0 ? $"cooling now: {string.Join(", ", others)}." : $"no account of `{owner}` is cooling."));
                return 0;
            });
    }

    // ——— profile use

    /// <summary><c>profile use</c>'s flags (TOOL6a, D130 §16.6; D165 §6, SWITCH1a); any other is refused rather than read as a choice.</summary>
    private static readonly HashSet<string> UseFlags = new(StringComparer.Ordinal)
    {
        "--keep", "--no-keep", "--switch", "--curve", "--no-curve", "--early", "--near", "--workspace", "--clear",
    };

    /// <summary>What the terminal says for each <i>use accounts</i> choice (D130 §16.6): its name, and what it costs where the screen says so.</summary>
    private static readonly IReadOnlyDictionary<string, (string Name, string? Cost)> UseWords = new Dictionary<string, (string, string?)>
    {
        ["goal"] = ("make the most of them", null),
        ["order"] = ("one by one, in order", "one limit stops every session on that account"),
    };

    /// <summary>The switch's values as the page names them (<c>harness.use.switch.*</c>), for the terminal to say the same.</summary>
    private static readonly IReadOnlyDictionary<string, string> SwitchWords = new Dictionary<string, string>
    {
        ["smart"] = "Smart", ["curve"] = "My own curve", ["off"] = "Off",
    };

    /// <summary><c>profile use</c>'s shape, as a refusal names it.</summary>
    private static readonly string UseShape =
        $"[{string.Join('|', RotationUse.Modes)}] [--keep <account>|--no-keep] [--switch {string.Join('|', RotationUse.Switches)}] "
        + "[--curve <window>=<elapsed>:<used>,…]… [--no-curve] [--workspace W], or --clear (--early on|off and --near <percent> "
        + "are the old names)";

    /// <summary>How a curve is given at the terminal (the switch design §5), as a refusal shows it.</summary>
    private const string CurveExample = "`--curve weekly=0:30,50:70,100:100`";

    /// <summary>Choices said as a person says them: <c>a, b or c</c>.</summary>
    private static string Either(IReadOnlyList<string> choices) =>
        choices.Count > 1 ? $"{string.Join(", ", choices.Take(choices.Count - 1))} or {choices[^1]}" : string.Concat(choices);

    /// <summary>
    /// <c>profile use &lt;agent&gt; [goal|order] [--keep &lt;account&gt;|--no-keep] [--switch smart|curve|off] [--curve
    /// &lt;window&gt;=&lt;elapsed&gt;:&lt;used&gt;,…]… [--no-curve] [--workspace &lt;name&gt;]</c>, or <c>--clear</c> (TOOL6a, D130 §16.6;
    /// D165 §6, the switch design §5): how a scope's list is used, the Accounts place's <c>ACCOUNT_USE</c> <c>use</c>. With no flag it
    /// prints the scope's settings, the step the next start would follow and what each account last said. A scope with no list of its
    /// own has nothing to use and is refused, naming the door that gives it one; a kept account is one of the list and leaves driven
    /// work another (§4.6); a curve is refused in the sentence the driver says. <c>--early</c> and <c>--near</c> are the old names.
    /// </summary>
    private static AgentPress? Use(Context at, out int exit)
    {
        exit = 0;
        var owner = at.Owner();
        var workspace = at.Workspace();
        if (at.Args.Skip(2).FirstOrDefault(word => word.StartsWith("--", StringComparison.Ordinal) && !UseFlags.Contains(word)) is { } stray)
        {
            throw new AgentWords($"`{stray}` is not a flag of `agent profile use` — it takes {UseShape}.");
        }

        string[] chosen = [.. AgentsCommand.Operands(at.Args, AgentsCommand.Valued).Skip(3)];
        string[] modes = [.. RotationUse.Modes.Select(mode => $"{mode} ({UseWords[mode].Name})")];
        if (chosen.Length > 1)
        {
            throw new AgentWords($"`agent profile use` takes one way to use accounts, and `{string.Join(' ', chosen)}` names more — {Either(modes)}.");
        }

        if (chosen.Length == 1 && !RotationUse.Modes.Contains(chosen[0], StringComparer.Ordinal))
        {
            throw new AgentWords($"`{chosen[0]}` is not a way to use accounts — {Either(modes)}. An account is kept for conversations with "
                + "`--keep <account>`.");
        }

        var (change, named) = UseChangeOf(at, owner);
        if (chosen.Length == 1)
        {
            change = change with { Use = chosen[0] };
            named = true;
        }

        JsonObject Payload()
        {
            var payload = new JsonObject { ["action"] = "use", ["harness"] = owner };
            if (workspace is not null) payload["workspace"] = workspace;
            return payload;
        }

        var clear = at.Args.Contains("--clear");
        if (clear && named)
        {
            throw new AgentWords($"`--clear` sets no setting beside it — `daoris agent profile use {owner} --clear` returns the scope to "
                + "today's defaults, and the setting is set on its own.");
        }

        if (clear)
        {
            var cleared = Payload();
            cleared["clear"] = true;
            return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, cleared,
                _ =>
                {
                    HarnessSettings.Change(at.Path, read => read.WithUse(owner, null, workspace));
                    return Done();
                },
                _ => null,
                _ =>
                {
                    at.Say($"{Prefix}: {Where(workspace)}, `{owner}` sets nothing of its own about how its list is used: today's defaults apply.");
                    at.Say($"  Written to {at.Path} — {Written}");
                    return 0;
                });
        }

        if (!named)
        {
            exit = UsePrinted(at, owner, workspace, at.Settings.ResolveScope(owner, workspace));
            return null;
        }

        var list = at.OwnList(owner, workspace);
        if (list.Count == 0)
        {
            var borrowed = workspace is not null && at.Settings.ResolveScope(owner, workspace).From == ChoiceFrom.Machine;
            throw new AgentWords(workspace is not null
                ? $"`{workspace}` has no list of its own for `{owner}`, so there is nothing to use there — `daoris agent profile order {owner} "
                    + $"<account>…{Scoped(workspace)}` gives it one"
                    + (borrowed ? ", and until then it uses this machine's, which is set without --workspace." : ".")
                : $"`{owner}` has no list on this machine, so there is nothing to use yet — `daoris agent profile order {owner} <account>…` "
                    + "sets one, and how it is used is set beside it.");
        }

        if (change.Keep is { } keep)
        {
            switch (ScopeProblem.Of(null, list, keep))
            {
                case { Kind: ScopeProblemKind.Keep } problem:
                    throw new AgentWords($"`{problem.Account}` is not in `{owner}`'s list {Where(workspace)} ({string.Join(", then ", list)}) — the "
                        + $"kept account is one of the list. Name it in the list first (`daoris agent profile order {owner} "
                        + $"{ShellWord.Words([.. list, problem.Account], ShellWord.Account)}{Scoped(workspace)}`), or keep one of it.");
                case { Kind: ScopeProblemKind.Alone } problem:
                    throw new AgentWords($"`{owner}`'s list {Where(workspace)} holds no account but `{problem.Account}`, so keeping it for "
                        + $"conversations would leave driven work none — `daoris agent profile order {owner} "
                        + $"{ShellWord.Words(list, ShellWord.Account)} <account>…{Scoped(workspace)}` adds one.");
            }
        }

        // As the Accounts place sends a change (`ACCOUNT_USE`'s `UseChangeOf`): a kept account or a curve taken out is its own flag.
        var sent = Payload();
        if (change.Use is { } use) sent["use"] = use;
        if (change.NoKeep) sent["noKeep"] = true;
        else if (change.Keep is { } kept) sent["keep"] = kept;
        if (change.Switch is { } switched) sent["switch"] = switched;
        if (change.NoCurve) sent["noCurve"] = true;
        else if (change.Curve is { } curve) sent["curve"] = CurveNode(curve);
        if (change.Early is { } early) sent["early"] = early;
        if (change.Near is { } near) sent["near"] = near;
        var after = at.Settings.WithUse(owner, change, workspace);
        return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, sent,
            _ =>
            {
                HarnessSettings.Change(at.Path, read => read.WithUse(owner, change, workspace));
                return Done();
            },
            _ => null,
            _ =>
            {
                UsePrinted(at, owner, workspace, after.ResolveScope(owner, workspace));
                at.Say($"  Written to {at.Path} — {Written}");
                return 0;
            });
    }

    /// <summary>A curve as the route reads one: each window it names, its points as <c>[elapsed, used]</c>.</summary>
    private static JsonObject CurveNode(UseCurve curve)
    {
        var node = new JsonObject();
        foreach (var window in UseCurve.Windows)
        {
            if (curve[window] is not { } points) continue;
            node[window] = new JsonArray([.. points.Select(point => (JsonNode?)new JsonArray(point.Elapsed, point.Used))]);
        }

        return node;
    }

    /// <summary>
    /// The settings <c>profile use</c>'s flags name, each refused unless it is one, and whether any was named: the switch, the curve,
    /// the old names, then the kept account, in the order the TypeScript verb judged them.
    /// </summary>
    private static (UseChange Change, bool Named) UseChangeOf(Context at, string owner)
    {
        var change = new UseChange();
        var named = false;
        IReadOnlyList<string> Values(string flag)
        {
            var values = new List<string>();
            for (var index = 0; index < at.Args.Count; index++)
            {
                if (at.Args[index] != flag) continue;
                var value = index + 1 < at.Args.Count ? at.Args[index + 1] : null;
                if (value is null || value.StartsWith("--", StringComparison.Ordinal) || value.Trim().Length == 0)
                {
                    throw new AgentWords($"`{flag}` needs a value — it takes {UseShape}.");
                }

                values.Add(value);
            }

            return values;
        }

        string? Value(string flag) => Values(flag) is [var first, ..] ? first : null;

        if (Value("--switch") is { } switched)
        {
            if (!RotationUse.Switches.Contains(switched, StringComparer.Ordinal))
            {
                throw new AgentWords("`--switch` is smart, curve or off — smart, Daoris's curve; curve, your own (`--curve`); off, no switch "
                    + "while a session runs.");
            }

            change = change with { Switch = switched };
            named = true;
        }

        var curves = Values("--curve");
        var noCurve = at.Args.Contains("--no-curve");
        if (curves.Count > 0 && noCurve)
        {
            throw new AgentWords("`--curve` and `--no-curve` together say two things — set a window of your curve, or take the curve out.");
        }

        if (curves.Count > 0) (change, named) = (change with { Curve = CurveOf(curves) }, true);
        if (noCurve) (change, named) = (change with { NoCurve = true }, true);

        if (Value("--early") is { } early)
        {
            if (early is not ("on" or "off")) throw new AgentWords("`--early` is on or off — switch before the limit, or not.");
            (change, named) = (change with { Early = early == "on" }, true);
        }

        if (Value("--near") is { } near)
        {
            var percent = near.Length > 0 && near.All(char.IsAsciiDigit) && int.TryParse(near, out var whole) ? whole : -1;
            if (percent is < RotationUse.NearLowest or > RotationUse.NearHighest)
            {
                throw new AgentWords($"`--near` is a whole percent from {RotationUse.NearLowest} to {RotationUse.NearHighest} — e.g. `--near 85`: "
                    + "the old name of your curve's five-hour window, flat at that share; a start passes an account whose use of that window "
                    + "is over it.");
            }

            (change, named) = (change with { Near = percent }, true);
        }

        var keep = Value("--keep");
        var noKeep = at.Args.Contains("--no-keep");
        if (keep is not null && noKeep)
        {
            throw new AgentWords("`--keep` and `--no-keep` together say two things — keep one account for conversations, or none.");
        }

        if (keep is not null) (change, named) = (change with { Keep = at.Account(owner, keep) }, true);
        if (noKeep) (change, named) = (change with { NoKeep = true }, true);
        return (change, named);
    }

    /// <summary>
    /// The curve <c>--curve &lt;window&gt;=&lt;elapsed&gt;:&lt;used&gt;,…</c> gives, once per window (the switch design §5): each window's
    /// points as whole numbers, then held to the curve's rules in the sentence the driver says (<see cref="UseCurve.Problem"/>).
    /// </summary>
    private static UseCurve CurveOf(IReadOnlyList<string> given)
    {
        var windows = new JsonObject();
        foreach (var value in given)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value, @"^([^=]+)=(\d+:\d+(?:,\d+:\d+)*)$");
            if (!match.Success)
            {
                throw new AgentWords($"`--curve {value}` is not a window and its points — it takes `<window>=<elapsed>:<used>,…`, each a whole "
                    + $"percent, e.g. {CurveExample}.");
            }

            var window = match.Groups[1].Value;
            if (windows.ContainsKey(window)) throw new AgentWords($"`--curve` names `{window}` twice — give each window once.");
            windows[window] = new JsonArray([.. match.Groups[2].Value.Split(',').Select(point =>
                (JsonNode?)new JsonArray([.. point.Split(':').Select(number => (JsonNode?)JsonValue.Create(double.Parse(number, System.Globalization.CultureInfo.InvariantCulture)))]))]);
        }

        var raw = JsonSerializer.SerializeToElement(windows);
        if (UseCurve.Problem(raw) is { } problem)
        {
            throw new AgentWords($"{problem} `--curve` takes <window>=<elapsed>:<used>,…, e.g. {CurveExample}.");
        }

        return UseCurve.Read(raw)!;
    }

    /// <summary>
    /// <c>profile use</c>'s print: whose scope it is, its list, how it is used, the step the next start would follow, and what each of
    /// its accounts last said.
    /// </summary>
    private static int UsePrinted(Context at, string owner, string? workspace, RotationScope scope)
    {
        var borrowed = workspace is not null && scope.From == ChoiceFrom.Machine;
        if (scope.List.Count == 0)
        {
            at.Say(workspace is null
                ? $"{Prefix}: `{owner}` has no list on this machine, so it has no settings — `daoris agent profile order {owner} <account>…` sets one."
                : borrowed
                    ? $"{Prefix}: `{workspace}` names no account of its own for `{owner}`, and this machine has no list, so there are no settings — "
                        + $"`daoris agent profile order {owner} <account>…` sets the machine's."
                    : $"{Prefix}: `{workspace}` names its own account for `{owner}`, `{scope.Default}`, and no list of its own, so it has no "
                        + $"settings — `daoris agent profile order {owner} {ShellWord.Of(scope.Default ?? "", ShellWord.Account)} <account>…"
                        + $"{Scoped(workspace)}` gives it a list.");
            return 0;
        }

        // Each account by the person's name for it, else its id (ACCT2).
        string Shown(string account) => AccountNames.Shown(at.Home, owner, account);
        var spelled = string.Join(", then ", scope.List.Select(Shown));
        at.Say(borrowed
            ? $"{Prefix}: `{workspace}` names no account of its own for `{owner}`, so it uses this machine's: {spelled}."
            : $"{Prefix}: {Where(workspace)}, `{owner}`'s list is {spelled}.");

        var toolchain = Toolchain(at, owner);
        var says = Says(at, owner);
        var now = at.World.Now();
        var zone = at.World.Zone;
        var said = scope.List.ToDictionary(account => account, account => AccountWindows.SaidOf(at.Home, owner, account, now), StringComparer.Ordinal);

        var (rows, notes) = UseLines(scope, toolchain?.Product ?? owner, says, Shown);
        foreach (var (label, value) in rows) at.Say($"  {label,-24} {value}");
        foreach (var note in notes) at.Say($"  {note}");
        var (row, next) = NextStartLines(scope, says, said.Values.Any(windows => windows is not null), Shown);
        at.Say($"  {"next start",-24} {row}");
        if (next is not null) at.Say($"  {next}");

        // Each account's cool-off, else its last reading and its age (TOOL6c), else that it has said nothing: unknown. A reading over the
        // scope's curve says so, as the start's line does (SWITCH1b-iii).
        var cooling = AccountCooling.Read(at.Home, now);
        at.Say("  what each account last said:");
        foreach (var account in scope.List)
        {
            var cooled = cooling.FirstOrDefault(entry => string.Equals(entry.Agent, owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Account ?? "", account, StringComparison.OrdinalIgnoreCase));
            at.Say($"    {Shown(account),-14} {(cooled is not null
                ? CoolingLine(cooled, now, zone)
                : said[account] is { } reading
                    ? SaidLine(reading, now, zone, scope.Use)
                    : "nothing said about what it has left")}");
        }

        return 0;
    }

    /// <summary>The toolchain an agent's accounts are said by: its door's, or the agent a door runs as.</summary>
    private static HarnessToolchain? Toolchain(Context at, string agent)
    {
        var set = at.World.Adapters(at.Home);
        return set.Names.Contains(agent, StringComparer.OrdinalIgnoreCase) ? set.Resolve(agent).Toolchain : set.Holder(agent);
    }

    /// <summary>
    /// Whether an agent's accounts say how much of each window is used (TOOL6c, CODEXUSE1b): a toolchain whose accounts are its own,
    /// its door's or a door onto it (AGT7), carries the frame's words (<c>Windows</c>) or asks its own server (<c>Usage</c>), as
    /// <c>codex-acp</c> asks for <c>codex</c>'s. The CLI's <c>windows</c>.
    /// </summary>
    private static bool Says(Context at, string owner) => Says(at.World.Adapters(at.Home), owner);

    /// <summary><see cref="Says(Context, string)"/> over the agents a home runs: <c>agent list</c>'s too (PUSH1f2e4).</summary>
    internal static bool Says(AdapterSet set, string owner)
    {
        IEnumerable<HarnessToolchain?> theirs = [set.Holder(owner), .. set.Names
            .Select(name => (Name: name, Toolchain: set.Resolve(name).Toolchain))
            .Where(each => each.Toolchain is { } toolchain && string.Equals(toolchain.Owner(each.Name), owner, StringComparison.OrdinalIgnoreCase))
            .Select(each => each.Toolchain)];
        return theirs.Any(toolchain => toolchain?.Windows is not null || toolchain?.Usage is not null);
    }

    /// <summary>
    /// How a scope's list is used, in the terminal's words (TOOL6a; D130 §3.2, §6, §16.6): each setting's label and value, and a note
    /// for each thing the person should know: a default outside the list, a kept account that leaves driven work none, and every value
    /// or setting this build does not know, said rather than dropped. <i>Switch accounts</i> says what passes an account at a start
    /// (D165 point 2, the switch design §3), only where the agent says how much of each window is used. <c>agent list</c> says it
    /// beneath each list too.
    /// </summary>
    internal static (List<(string Label, string Value)> Rows, List<string> Notes) UseLines(
        RotationScope scope, string product, bool says, Func<string, string> shown)
    {
        var use = scope.Use;
        var mode = UseWords[use.Use];
        var rows = new List<(string, string)>
        {
            ("use accounts", mode.Cost is { } cost ? $"{mode.Name} — {cost}" : mode.Name),
            ("kept for conversations", use.Keep is null ? "none" : shown(use.Keep)),
            ("switch accounts", !says
                ? $"{SwitchWords[use.Switch]} — {product}'s sessions here do not say how much of each window is used, so a start passes no account by its use"
                : $"{SwitchWords[use.Switch]} — {(use.Switch == "off" ? "no session moves at a turn's end; " : "a session moves at a turn's end when its use is over the curve; ")}"
                    + $"a start {(use.Switch == "off" ? "still " : "")}passes an account whose limit is reached, that draws on usage "
                    + $"credits, or whose use is over {(use.Switch == "curve" ? "your curve, Daoris's smart curve on a window yours leaves out" : "Daoris's smart curve")}"),
        };
        if (CurveLine(use) is { } curve) rows.Add(("curve", curve));

        var notes = new List<string>();
        switch (ScopeProblem.Of(scope.Default, scope.List, use.Keep))
        {
            case { Kind: ScopeProblemKind.Default } problem:
                notes.Add($"its default, `{shown(problem.Account)}`, is not in this list: name it in the list, or make one of the list the default");
                break;
            case { Kind: ScopeProblemKind.Alone } problem:
                notes.Add($"`{shown(problem.Account)}` is kept for conversations, and this list holds no other account for driven work");
                break;
        }

        foreach (var name in scope.Unknown)
        {
            notes.Add(RotationUse.Fields.Contains(name, StringComparer.Ordinal)
                ? $"`{name}` holds a value this build does not know, so it reads as today's default; it is kept as written"
                : RotationUse.OldNames.Contains(name, StringComparer.Ordinal)
                    ? $"`{name}` is an old name of the switch, and holds a value this build does not read: nothing reads it, and the next write takes it out"
                    : $"`{name}` is a setting this build does not know: nothing here reads it, and it is kept as written");
        }

        return (rows, notes);
    }

    /// <summary>
    /// The person's curve in the terminal's words (D165 point 2): each window it names, its points as <c>elapsed:used</c>, and where the
    /// switch is not <c>curve</c>, that it is kept and read only there. Null where there is none to show: none set, under another switch.
    /// </summary>
    private static string? CurveLine(RotationUse use)
    {
        string[] windows = [.. UseCurve.Windows.Where(window => use.Curve[window] is not null)];
        if (windows.Length == 0) return use.Switch == "curve" ? "none of your own — each window takes Daoris's smart curve" : null;
        var said = string.Join("; ", windows.Select(window =>
            $"{window} {string.Join(", ", use.Curve[window]!.Select(point => $"{point.Elapsed}:{point.Used}"))}"));
        return use.Switch == "curve" ? said : $"{said} — kept, and read only under curve";
    }

    /// <summary>
    /// The step the next start of a scope would follow (TOOL6b, TOOL6c; D130 §16.3, §16.4, §16.6), in the terminal's words: the goal's
    /// steps as the driver's walk takes them, or the list's order under <c>order</c>, and the kept account driven work passes; where the
    /// agent says how much of each window is used, the curve's steps (D165 point 2, the switch design §3). Prose about
    /// <see cref="AccountRotation.Order"/>: this reads no session record, so it names the steps rather than the account they would choose.
    /// </summary>
    internal static (string Row, string? Note) NextStartLines(RotationScope scope, bool says, bool anySaid, Func<string, string> shown)
    {
        if (scope.List.Count == 1) return ($"`{shown(scope.List[0])}`, the one account this list holds", null);

        var begins = scope.Begins is null ? null : shown(scope.Begins);
        var from = $"from `{begins}`{(scope.Default is not null && scope.Default == scope.Begins ? ", its default" : "")}";
        var keep = scope.Use.Keep;
        var passes = keep is not null && ScopeProblem.Of(null, scope.List, keep) is null
            ? $"; driven work passes `{shown(keep)}`, kept for conversations"
            : "";
        var off = scope.Use.Switch == "off";
        var last = says
            ? $"; one whose limit is reached, that draws on usage credits or whose use is over {(off ? "Daoris's smart curve" : "its curve")} goes last"
            : "";
        if (scope.Use.Use != "goal") return ($"the first ready account of this list, {from}{passes}{last}", null);

        // Step 4 weighs what would lapse (D165's USAGERESET1 note): only a read week's room, so an agent that says nothing has none.
        var lapses = !says ? "" : "then the one whose week would lapse the most: within its last day, its room under "
            + $"{(off ? "Daoris's smart curve" : "its curve")} per hour to its reset; ";
        var room = !says ? "" : off ? "then the one furthest behind its week's pace; " : "then the one with the most room under its curve; ";
        return (
            $"the ready account running the fewest of Daoris's sessions; {lapses}{room}then the one Daoris started on least recently; "
                + $"then this list's order, {from}{passes}{last}{(says ? ", the least over first" : "")}",
            anySaid ? null : "no account has said what it has left yet: Daoris spreads starts across them by its own sessions");
    }

    /// <summary>The line a cooling account is given (D125 §2.4): until when, how long that is, why, and how to try it now.</summary>
    internal static string CoolingLine(CoolingEntry entry, DateTimeOffset now, TimeZoneInfo zone)
    {
        var ready = $"`daoris agent profile ready {entry.Agent} {(entry.Account is null ? "--own" : ShellWord.Of(entry.Account, ShellWord.Account))}`";
        return $"cooling until {CoolingWords.When(entry.Until, zone)}, in {Span(entry.Until - now)} — {CoolingWords.Why(entry)}; {ready} tries it now";
    }

    /// <summary>How long until then, to the minute and rounded up: <c>2 d 2 h</c>, <c>3 h 10 min</c>, <c>1 min</c>.</summary>
    private static string Span(TimeSpan left)
    {
        var minutes = Math.Max(1, (long)Math.Ceiling(left.TotalMinutes));
        var (days, hours) = (minutes / 1440, minutes % 1440 / 60);
        if (days > 0) return hours > 0 ? $"{days} d {hours} h" : $"{days} d";
        return hours > 0 ? $"{hours} h {minutes % 60} min" : $"{minutes} min";
    }

    /// <summary>
    /// What an account last said, as one line a person reads (TOOL6c, D130 §3.2, §9): how long ago, its own word where it gave one,
    /// credits, each window's use and reset in the machine's zone, and its tightest window over the scope's curve with the curve's value
    /// there now, as the start's line says it (SWITCH1b-iii), where a scope's <paramref name="use"/> is given (<c>agent list</c> gives
    /// none). Its pace warning is said in its own words, never as near (USAGEREAD3a).
    /// </summary>
    internal static string SaidLine(AccountSaid said, DateTimeOffset now, TimeZoneInfo zone, RotationUse? use)
    {
        var parts = new List<string>();
        if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.RefusedWord) is { } reached)
        {
            parts.Add($"its {reached.Window} limit reached, by its own word");
        }
        else if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.WarnedWord) is { } warned)
        {
            parts.Add($"ahead of its pace on its {warned.Window} window, by its own word");
        }

        if (said.Windows.Any(each => each.Credits)) parts.Add("drawing on usage credits");
        // The lead is the newest reading's source, a read winning a tie; a window from the other source says so, with its own age.
        var newest = said.Windows.Aggregate((best, each) =>
            each.Seen > best.Seen || (each.Seen == best.Seen && each.Source == AccountWindows.SourceRead) ? each : best);
        foreach (var each in said.Windows.OrderBy(each => each.Window.ToLowerInvariant() switch { "session" => 0, AccountWindows.Weekly => 1, _ => 2 }))
        {
            if (each.Used is not { } used) continue;
            var own = each.Source == newest.Source ? "" : $" ({each.Source} {RotationWords.Age(each.Seen, now)})";
            parts.Add($"{RotationWords.Percent(used)} of its {each.Window} limit used{own}, resetting {CoolingWords.When(each.Reset, zone)}");
        }

        if (use is not null && UsageCurves.TightestSaid(use, said, now) is { Room: < 0 } over)
        {
            parts.Add($"over its {over.Window.Window} curve's {UsageCurves.Points(over.Curve)}%");
        }

        // A reading with no number, no warning and no credits is its agent's clear word alone.
        if (parts.Count == 0) parts.Add("clear, by its own word");
        return $"{newest.Source} {RotationWords.Age(newest.Seen, now)}: {string.Join("; ", parts)}";
    }

    /// <summary>
    /// <c>profile rename &lt;agent&gt; &lt;account&gt; &lt;name&gt;</c> (ACCT2): an account's name, the person's word, kept by its id in
    /// <c>accounts.json</c>, so every list, default, reading and record keeps it; its own id gives it none.
    /// </summary>
    private static AgentPress Rename(Context at)
    {
        var owner = at.Owner();
        var account = at.Account(owner, at.Bare(3, "<agent> <account> <name>"));
        var wanted = at.Operand(4) ?? throw new AgentWords(
            $"`agent profile rename` needs <agent> <account> <name> — e.g. `daoris agent profile rename {owner} "
            + $"{ShellWord.Of(account, ShellWord.Account)} work`; naming it `{account}` gives it none.");
        var accounts = HarnessSettings.Profiles(at.Home, owner);
        var names = AccountNames.Of(at.Home, owner);
        if (AccountNames.Problem(accounts, names, account, wanted) is { } problem)
        {
            throw new AgentWords(AccountNames.Sentence(owner, problem, account, wanted, accounts, names));
        }

        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject { ["action"] = "profile-rename", ["harness"] = owner, ["profile"] = account, ["name"] = wanted },
            _ => Done(AccountNames.Rename(at.Home, owner, HarnessSettings.Profiles(at.Home, owner), account, wanted)),
            answer => AgentsCommand.Text(answer, "name") is { Length: > 0 } now ? now : null,
            done =>
            {
                at.Say(done is string now
                    ? $"{Prefix}: `{owner}`'s account `{account}` is called `{now}` now."
                    : $"{Prefix}: `{owner}`'s account `{account}` has no name of yours now: it reads as its id.");
                at.Say("  Its id stays, so every list, default, reading and record that names it still does.");
                at.Say($"  Written to {AccountNames.PathOf(at.Home)} — machine-local, tracked by nothing.");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile join &lt;agent&gt; &lt;account&gt; [&lt;workspace&gt;…] [--machine]</c> (ACCT1): an account put into the lists a start runs
    /// on, each workspace's own and with <c>--machine</c> this machine's; a workspace that takes this machine's list is refused, naming it.
    /// </summary>
    private static AgentPress Join(Context at)
    {
        var owner = at.Owner();
        var account = at.Account(owner, at.Bare(3, "<agent> <account> <workspace>…|--machine"));
        List<string?> joins = [.. at.Args.Contains("--machine") ? [null] : Array.Empty<string?>(),
            .. AgentsCommand.Operands(at.Args, AgentsCommand.Valued).Skip(4).Select(each => (string?)each.Trim())];
        var existing = HarnessSettings.Profiles(at.Home, owner);
        if (!existing.Contains(account, StringComparer.Ordinal))
        {
            throw new AgentWords($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (existing.Count > 0 ? string.Join(", ", existing.Select(id => AccountNames.Shown(at.Home, owner, id))) : "(none)"));
        }

        if (joins.Count == 0)
        {
            throw new AgentWords("`agent profile join` needs the lists — a workspace's name, or `--machine` for this machine's: e.g. "
                + $"`daoris agent profile join {owner} {ShellWord.Of(account, ShellWord.Account)} work`.");
        }

        foreach (var workspace in joins)
        {
            if (at.Settings.JoinProblemOf(owner, workspace) is { } problem) throw new AgentWords(problem.Sentence(owner));
        }

        HarnessSettings Joined(HarnessSettings read) => joins.Aggregate(read, (next, workspace) => next.WithJoined(owner, account, workspace));
        var after = Joined(at.Settings);
        var shown = AccountNames.Shown(at.Home, owner, account);
        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject
            {
                ["action"] = "profile-join", ["harness"] = owner, ["profile"] = account,
                ["join"] = new JsonArray([.. joins.Select(each => (JsonNode?)each)]),
            },
            _ =>
            {
                HarnessSettings.Change(at.Path, Joined);
                return Done();
            },
            _ => null,
            _ =>
            {
                at.Say($"{Prefix}: `{owner}`'s account `{shown}` is in "
                    + $"{string.Join(", ", joins.Select(workspace => workspace is null ? "this machine's list" : $"`{workspace}`'s list"))}.");
                at.Say($"  `{shown}` runs work in {string.Join(", ", after.PlacesOf(owner, account).Select(PlaceWords))}.");
                at.Say($"  Written to {at.Path} — {Written}");
                return 0;
            });
    }

    /// <summary>One scope an account runs in, as a person reads it: <c>this machine's list, as its default</c>, <c>`work`'s default</c>.</summary>
    private static string PlaceWords(AccountPlace place)
    {
        var scope = place.Workspace is null ? "this machine's" : $"`{place.Workspace}`'s";
        return place.List && place.Default ? $"{scope} list, as its default" : $"{scope} {(place.List ? "list" : "default")}";
    }

    private static int? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}
