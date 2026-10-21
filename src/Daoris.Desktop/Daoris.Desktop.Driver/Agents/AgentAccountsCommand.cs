using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver agent unpin</c> and <c>agent profile list|add|remove|default|order|ready|rename|join</c> (PUSH1f2e2, D194, the
/// alone-path design §5): a pin taken back and an agent's accounts from a terminal, the words <c>daoris agent …</c> hands this program.
/// Each change is the Agents or Accounts place's own press while Daoris runs (<c>HARNESS_ACTION</c>'s <c>unpin</c> and
/// <c>profile-*</c>, <c>ACCOUNT_USE</c>'s <c>order</c> and <c>ready</c>), else the same write on the terminal's word; a workspace's
/// unpin has no route and acts alone. Every check the window would refuse by is said here first, in today's words
/// (<c>fixtures/agents-text.json</c>), so a refusal costs nothing on either path. <c>profile use</c> stays the CLI's until the rest of
/// PUSH1f2e.
/// </summary>
internal static class AgentAccountsCommand
{
    private const string Prefix = "daoris";

    /// <summary>The profile verbs this program has; <c>use</c> is still the CLI's.</summary>
    public static IReadOnlyList<string> ProfileVerbs { get; } = ["list", "add", "remove", "default", "order", "ready", "rename", "join"];

    /// <summary>The profile verbs that change a home, each held from the top: all but <c>list</c>.</summary>
    public static bool ProfileChanges(string verb) => verb != "list" && ProfileVerbs.Contains(verb, StringComparer.Ordinal);

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
                HarnessSettings.Change(at.Path, read => read.WithoutAccount(owner, profile));
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
