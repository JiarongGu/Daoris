using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris agent unpin</c> and <c>agent profile list|add|remove|order|ready|use|rename</c>: a pin taken back and an agent's accounts from a terminal, the words <c>daoris agent …</c> hands this program.
/// Each change is the Agents or Accounts place's own press while Daoris runs (<c>HARNESS_ACTION</c>'s <c>unpin</c> and
/// <c>profile-*</c>, <c>ACCOUNT_USE</c>'s <c>order</c>, <c>ready</c> and <c>use</c>), else the same write on the terminal's word; a workspace's
/// unpin has no route and acts alone. Every check the window would refuse by is said here first, in the words
/// (<c>fixtures/agents-text.json</c>), so a refusal costs nothing on either path. The <c>use</c> verb is <see cref="AgentAccountUse"/>
/// and the words about an account's use are <see cref="AgentAccountLines"/>.
/// </summary>
/// <remarks>
/// An agent's accounts are one set: <c>order</c> and <c>use</c> act on it.
/// </remarks>
internal static class AgentAccountsCommand
{
    private const string Prefix = AccountVerbContext.Prefix;

    /// <summary>The profile verbs this program has.</summary>
    public static IReadOnlyList<string> ProfileVerbs { get; } = ["list", "add", "remove", "order", "ready", "use", "rename"];

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
                || args.Skip(2).Any(word => word.StartsWith("--", StringComparison.Ordinal));
        }

        return verb != "list" && ProfileVerbs.Contains(verb, StringComparer.Ordinal);
    }

    internal const string Written = "machine-local, tracked by nothing, like every wiring file here.";

    // ——— unpin

    /// <summary>
    /// <c>unpin &lt;agent&gt; [--workspace &lt;name&gt;]</c>: back to <c>PATH</c>, the machine's or one workspace's pin
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
            new JsonObject { ["action"] = "unpin", ["door"] = name },
            _ =>
            {
                HarnessSettings.Change(
                    path, read => circle is null ? read.WithVersion(name, null) : read.WithWorkspaceVersion(circle, name, null),
                    agents: AgentsCommand.Agents(world, path));
                return Task.FromResult<object?>(null);
            },
            _ => null, Said);
    }

    // ——— profile

    /// <summary><c>profile [list] | add | remove | order | ready | use | rename …</c>, the words after <c>agent</c>.</summary>
    public static AgentPress? Profile(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        var action = args.Count > 1 ? args[1] : "list";
        var path = AgentsCommand.WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        // Read as the set, so a file an older build wrote is the set here too.
        var settings = HarnessSettings.LoadSet(path, AgentsCommand.Agents(world, path));
        var context = new AccountVerbContext(args, world, path, home, settings, $"account {action}");

        switch (action)
        {
            case "list":
                foreach (var name in AgentsCommand.Named(world, path))
                {
                    var existing = HarnessSettings.Profiles(home, name);
                    world.Instance.Out.WriteLine($"  {name,-14} {(existing.Count > 0 ? string.Join(", ", existing) : "(none)")}");
                    foreach (var stray in HarnessSettings.Strays(home, name))
                    {
                        world.Instance.Out.WriteLine($"    left over: {stray} — no account; `daoris agent account remove {name} {ShellWord.Of(stray, ShellWord.Account)}` "
                            + "takes it, a sign-in in it included.");
                    }
                }

                return null;
            case "add":
                return Add(context);
            case "remove":
                return Remove(context);
            case "order":
                return Order(context);
            case "ready":
                return Ready(context);
            case "use":
                return AgentAccountUse.Use(context, out exit);
            case "rename":
                return Rename(context);
            default:
                throw new AgentWords($"unknown agent profile verb '{action}' — one of: {string.Join(", ", ProfileVerbs)}");
        }
    }

    internal static Task<object?> Done(object? done = null) => Task.FromResult(done);

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

    /// <summary><c>profile add &lt;agent&gt; &lt;profile&gt;</c>: a folder, and nothing else; one that is there is an answer.</summary>
    private static AgentPress Add(AccountVerbContext at)
    {
        var owner = at.Owner();
        var profile = at.Bare(3, "<agent> <profile>");
        var where = Folder(at.Home, owner, profile);
        var existed = Directory.Exists(where);
        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject { ["action"] = "profile-add", ["door"] = owner, ["profile"] = profile },
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
                at.Say($"  It is empty until you log into it: `daoris agent login {owner} --account {ShellWord.Of(profile, ShellWord.Account)}`.");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile remove &lt;agent&gt; &lt;profile&gt;</c>: the account's folder and the sign-in in it gone, and no default, list
    /// or kept account naming it after. A delete the window cannot finish is said, exit 1.
    /// </summary>
    private static AgentPress Remove(AccountVerbContext at)
    {
        var owner = at.Owner();
        var profile = at.Account(owner, at.Bare(3, "<agent> <profile>"));
        var where = Folder(at.Home, owner, profile);
        var existed = Directory.Exists(where);
        const string failed = "failed";
        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject { ["action"] = "profile-remove", ["door"] = owner, ["profile"] = profile },
            _ =>
            {
                bool removed;
                try
                {
                    removed = HarnessSettings.RemoveProfile(at.Home, owner, profile);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // The window's outcome for a delete that could not finish; the wiring stays.
                    return Done(failed);
                }

                HarnessSettings.Change(
                    at.Path, read => read.WithoutAccount(owner, profile), account: new AccountChange(owner, profile, Added: false), agents: at.Agents);
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
                at.Say($"  It is out of `{owner}`'s order: Daoris's accounts no longer hold it, and no start runs on it.");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile order &lt;agent&gt; &lt;account&gt;…|--clear</c>: the set's order, the
    /// accounts named first, in that order, each here and once, and the rest after in their order (<see cref="HarnessSettings.Change"/>
    /// writes it whole); <c>--clear</c> returns to the ids' order and clears no setting.
    /// </summary>
    private static AgentPress Order(AccountVerbContext at)
    {
        var owner = at.Owner();
        // An account by its id or its name; the order holds ids, so a rename leaves it standing.
        string[] named = [.. AgentsCommand.Operands(at.Args, AgentsCommand.Valued).Skip(3).Select(given => at.Account(owner, given))];
        JsonObject Payload(IEnumerable<string> accounts) => new()
        {
            ["action"] = "order", ["door"] = owner, ["accounts"] = new JsonArray([.. accounts.Select(each => (JsonNode?)each)]),
        };

        // Each account by the person's name for it, else its id.
        string Spelled(IEnumerable<string> order) => string.Join(", then ", order.Select(account => AccountNames.Shown(at.Home, owner, account)));
        if (at.Args.Contains("--clear"))
        {
            if (named.Length > 0)
            {
                throw new AgentWords($"`--clear` names no account — `daoris agent account order {owner} --clear` returns the order to the "
                    + $"accounts' ids, and `daoris agent account order {owner} {ShellWord.Words(named, ShellWord.Account)}` puts those first.");
            }

            return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, Payload([]),
                _ =>
                {
                    HarnessSettings.Change(at.Path, read => read.WithRotation(owner, null), agents: at.Agents);
                    return Done();
                },
                _ => null,
                _ =>
                {
                    var order = at.Order(owner);
                    at.Say(order.Count > 0
                        ? $"{Prefix}: `{owner}`'s order is its accounts' ids' again: {Spelled(order)}."
                        : $"{Prefix}: `{owner}` has no account of Daoris's, so its order names none.");
                    at.Say($"  No account was deleted and no setting cleared. Written to {at.Path} — {Written}");
                    return 0;
                });
        }

        if (named.Length == 0)
        {
            throw new AgentWords($"`agent account order` needs the accounts to put first, in order, or --clear — e.g. `daoris agent account "
                + $"order {owner} account-2`.");
        }

        // Refused rather than written: a typo in an order is an account the walk would pass without saying why.
        var existing = HarnessSettings.Profiles(at.Home, owner);
        if (HarnessSettings.OrderProblem(existing, named) is { } problem) throw new AgentWords(problem.Sentence(owner, existing));

        string[] first = [.. named.Select(account => account.Trim())];
        return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, Payload(first),
            _ =>
            {
                HarnessSettings.Change(at.Path, read => read.WithRotation(owner, first), agents: at.Agents);
                return Done();
            },
            _ => null,
            _ =>
            {
                at.Say($"{Prefix}: `{owner}`'s order is {Spelled(at.Order(owner))}.");
                at.Say("  Every account of Daoris's serves every workspace (D197): one by one, in order, begins at the first, and make the");
                at.Say("  most of them uses the order only to break a tie.");
                at.Say($"  Written to {at.Path} — {Written}");
                return 0;
            });
    }

    /// <summary>
    /// <c>profile ready &lt;agent&gt; &lt;profile&gt;|--own</c>: an account's cool-off ended early, the terminal's
    /// <i>Try now</i>; <c>--own</c> is the tool's own sign-in.
    /// </summary>
    private static AgentPress Ready(AccountVerbContext at)
    {
        var owner = at.Owner();
        var own = at.Args.Contains("--own");
        var account = at.Operand(3) is { } given ? at.Account(owner, given) : null;
        if (own && account is not null)
        {
            throw new AgentWords($"`--own` names no account — `daoris agent account ready {owner} --own` is the tool's own sign-in, and "
                + $"`daoris agent account ready {owner} {ShellWord.Of(account, ShellWord.Account)}` is that account.");
        }

        if (!own && account is null)
        {
            throw new AgentWords($"`agent account ready` needs <agent> <profile>|--own — e.g. `daoris agent account ready {owner} account-1`, "
                + "or `--own` for the tool's own sign-in.");
        }

        var now = at.World.Now();
        var who = account is null ? $"`{owner}`'s own sign-in" : $"the `{owner}` account `{account}`";
        var payload = new JsonObject { ["action"] = "ready", ["door"] = owner };
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

                var cooling = AccountCooling.Read(at.Home, now);
                var others = AgentAccountLines
                    .CoolingNow(cooling, owner);
                at.Say($"{Prefix}: {who} is not cooling, so nothing changed — "
                    + (others.Count > 0 ? $"cooling now: {string.Join(", ", others)}." : $"no account of `{owner}` is cooling."));
                return 0;
            });
    }

    /// <summary>
    /// <c>profile rename &lt;agent&gt; &lt;account&gt; &lt;name&gt;</c>: an account's name, the person's word, kept by its id in
    /// <c>accounts.json</c>, so every list, default, reading and record keeps it; its own id gives it none.
    /// </summary>
    private static AgentPress Rename(AccountVerbContext at)
    {
        var owner = at.Owner();
        var account = at.Account(owner, at.Bare(3, "<agent> <account> <name>"));
        var wanted = at.Operand(4) ?? throw new AgentWords(
            $"`agent account rename` needs <agent> <account> <name> — e.g. `daoris agent account rename {owner} "
            + $"{ShellWord.Of(account, ShellWord.Account)} work`; naming it `{account}` gives it none.");
        var accounts = HarnessSettings.Profiles(at.Home, owner);
        var names = AccountNames.Of(at.Home, owner);
        if (AccountNames.Problem(accounts, names, account, wanted) is { } problem)
        {
            throw new AgentWords(AccountNames.Sentence(owner, problem, account, wanted, accounts, names));
        }

        return new AgentPress(at.Verb, AgentRoutes.Harness, at.Isolated,
            new JsonObject { ["action"] = "profile-rename", ["door"] = owner, ["profile"] = account, ["name"] = wanted },
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

    /// <summary><see cref="AgentAccountLines.Says"/>, for <c>agent list</c> (PUSH1f2e4).</summary>
    internal static bool Says(ProviderRegistry set, string owner) => AgentAccountLines.Says(set, owner);

    /// <inheritdoc cref="AgentAccountLines.UseLines"/>
    internal static (List<(string Label, string Value)> Rows, List<string> Notes) UseLines(
        RotationScope scope, string product, bool says, Func<string, string> shown) =>
        AgentAccountLines.UseLines(scope, product, says, shown);

    /// <inheritdoc cref="AgentAccountLines.NextStartLines"/>
    internal static (string Row, string? Note) NextStartLines(RotationScope scope, bool says, bool anySaid, Func<string, string> shown) =>
        AgentAccountLines.NextStartLines(scope, says, anySaid, shown);

    /// <inheritdoc cref="AgentAccountLines.CoolingLine"/>
    internal static string CoolingLine(CoolingEntry entry, DateTimeOffset now, TimeZoneInfo zone) => AgentAccountLines.CoolingLine(entry, now, zone);

    /// <inheritdoc cref="AgentAccountLines.SaidLine"/>
    internal static string SaidLine(AccountSaid said, DateTimeOffset now, TimeZoneInfo zone, RotationUse? use) =>
        AgentAccountLines.SaidLine(said, now, zone, use);

    private static int? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}
