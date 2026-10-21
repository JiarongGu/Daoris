using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where <c>daoris-driver agent</c> acts (PUSH1f2d): the terminal it says its words on and finds the running Daoris from, and what a
/// verb reads the machine by: the folder a relative folder is named from, the person's own profile folder an agent's own home falls
/// back to, the agents this machine runs, and the moment an answer is settled at. A test hands its own of each.
/// </summary>
public sealed record AgentsWorld
{
    /// <summary>The terminal: its home (<c>DAORIS_HOME</c>, null where none is named), its variables, its streams, and where it finds the instance.</summary>
    public required InstanceWorld Instance { get; init; }

    /// <summary>The folder the terminal is in: a folder <c>trust</c> names by a relative path is this one's.</summary>
    public string Folder { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>The person's own profile folder: an agent's own configuration home where it names none of its own.</summary>
    public string UserHome { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The agents this machine runs, read from a home: the build's and those its plugins declare (D64), as the window's roster reads them.</summary>
    public Func<string, AdapterSet> Adapters { get; init; } = home =>
    {
        var built = AdapterSet.Built();
        return built.WithPlugins(PluginCatalog.Load(home, built.Names));
    };

    /// <summary>The moment the person's answer to a proposal is settled at.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;
}

/// <summary>
/// One agent change's press (PUSH1f2d, D194 points 4 and 6): the route the window's press sends and the payload the verb writes from its
/// words, the operation it calls alone, the press's own fields read from the route's answer, and the one function that says it from those
/// fields on both paths. <see cref="AgentsCommand.Plan"/> makes it; a test sends its payload through the route.
/// </summary>
/// <param name="Route">The route, or null where none takes the change and the verb acts alone even beside a running Daoris.</param>
/// <param name="Isolated">Whether the change is a named wiring file's home's, acted on alone, sending nothing (D194 point 2).</param>
/// <param name="Act">The operation, alone: it writes, and answers the press's own fields (or null where it has none).</param>
/// <param name="Fields">The press's own fields, read from the route's answer.</param>
/// <param name="Words">The verb's words from those fields, both paths', returning its exit code.</param>
public sealed record AgentPress(
    string Verb, InstanceRoute? Route, bool Isolated, JsonObject? Payload, Func<CancellationToken, Task<object?>> Act,
    Func<JsonElement, object?> Fields, Func<object?, int> Words);

/// <summary>A refusal the command makes before it acts, in its words, with today's exit: its grammar and its own checks.</summary>
public sealed class AgentWords(string message, int exit = 2) : Exception(message)
{
    public int Exit { get; } = exit;
}

/// <summary>Which home an agent verb writes (the alone-path design §1.2).</summary>
public enum AgentHome
{
    /// <summary>The home <c>DAORIS_HOME</c> names, whatever <c>DAORIS_HARNESS_CONFIG</c> names: the rules and their proposals.</summary>
    Daoris,

    /// <summary>The wiring file's home: the one <c>DAORIS_HARNESS_CONFIG</c> names where it is set, acted on alone, else <c>DAORIS_HOME</c>.</summary>
    Wiring,
}

/// <summary>
/// The routes <c>daoris-driver agent</c> sends while Daoris runs (PUSH1f2d; RPC1f, the terminal RPC design §9), each the window twin of the
/// change it makes, the verbs no route takes, each with why it acts alone (D171's PUSH1f note, kept by D194 point 5), and which home each
/// writes: the CLI's <c>AGENT_VERBS</c> rows for these verbs, now this program's. Held to the census by <c>PipeCensusTests</c>.
/// </summary>
public static class AgentRoutes
{
    private static InstanceRoute Person(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Person);

    /// <summary><c>agent rules allow|ask|deny|remove|default</c>: the rules place's change.</summary>
    public static InstanceRoute Rule { get; } = Person("RULE_ACTION");

    /// <summary><c>agent rules accept|decline &lt;id&gt;</c>: the person's answer to an agent's proposal.</summary>
    public static InstanceRoute Proposal { get; } = Person("RULE_PROPOSAL");

    /// <summary><c>agent settings &lt;agent&gt; model|effort …</c>: an account's own model and effort.</summary>
    public static InstanceRoute Settings { get; } = Person("SET_AGENT_SETTINGS");

    /// <summary><c>agent unpin</c> and <c>agent profile add|remove|default|rename|join</c>: the Agents place's own actions (PUSH1f2e2).</summary>
    public static InstanceRoute Harness { get; } = Person("HARNESS_ACTION");

    /// <summary><c>agent profile order|ready</c>: the Accounts place's order and <i>Try now</i> (PUSH1f2e2).</summary>
    public static InstanceRoute Account { get; } = Person("ACCOUNT_USE");

    /// <summary>
    /// Each verb that changes a home, in the verb's words: the route it sends while Daoris runs, or null with why it acts alone, and which
    /// home it writes.
    /// </summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason, AgentHome Home)> Verbs { get; } =
    [
        ("agent rules allow|ask|deny|remove|default", Rule, "the rules place's change", AgentHome.Daoris),
        ("agent rules accept|decline <id>", Proposal, "the person's answer to an agent's proposal, the only way a widening applies", AgentHome.Daoris),
        ("agent settings <agent> model|effort …", Settings, "an account's own model and effort, in the tool's own settings file", AgentHome.Wiring),
        ("agent trust <agent> <folder> --yes", null,
            "alone: the window's TRUST_FOLDER grants only a folder a hold names, and this names any folder", AgentHome.Wiring),
        ("agent profile add|remove|default|rename|join", Harness, "the Accounts place's `profile-*` actions", AgentHome.Wiring),
        ("agent unpin <agent>", Harness, "the machine's pin taken back", AgentHome.Wiring),
        ("agent profile order|ready", Account, "the Accounts place's order and *Try now*", AgentHome.Wiring),
        ("agent unpin <agent> --workspace <name>", null, "alone: HARNESS_ACTION's unpin names no workspace", AgentHome.Wiring),
    ];

    /// <summary>The home a verb of this program's writes: its row's, by its first word.</summary>
    public static AgentHome HomeOf(string verb) =>
        Verbs.First(row => row.Verb.Split(' ')[1] == verb).Home;
}

/// <summary>
/// <c>daoris-driver agent</c> (PUSH1f2d, D194, the alone-path design §1.2): the agents' rules, trust and settings from a terminal, the words
/// <c>daoris agent rules|trust|settings …</c> hands this program, what an agent runs as, and its pin taken back and its accounts
/// (PUSH1f2e1, PUSH1f2e2, <see cref="AgentAccountsCommand"/>); the rest of the family is the CLI's until the rest of PUSH1f2e. Each change a route
/// takes is its route through <see cref="InstanceDoor.PressAsync"/> where the running Daoris answers, confirmed in its window; else its
/// operation, on the terminal's word (D50), refused from a session's terminal (D194 point 5). The rules and their proposals are
/// <c>DAORIS_HOME</c>'s whatever <c>DAORIS_HARNESS_CONFIG</c> names, so they keep the door on that home; an account's settings and a
/// folder's trust are the wiring file's home's, acted on alone, sending nothing and holding nothing, where the variable names one. Its
/// words are one function per verb on both paths, today's text kept (<c>fixtures/agents-text.json</c>).
/// </summary>
/// <remarks>
/// Exit codes are the CLI's, case by case: 0 said or set · 1 a trust asked without <c>--yes</c>, a grant a re-read does not show, the
/// person's refusal in the window, its two minutes' end, or a session's terminal · 2 the usage, a refusal of the words, the operation or
/// the route.
/// </remarks>
public static class AgentsCommand
{
    /// <summary>The verbs this program has, each the CLI hands on (the CLI's allow-list is held to it).</summary>
    public static IReadOnlyList<string> Verbs { get; } = ["rules", "trust", "settings", "which", "unpin", "profile"];

    /// <summary>The <c>profile</c> verbs this program has, each the CLI hands on; <c>profile use</c> is still the CLI's (PUSH1f2e2).</summary>
    public static IReadOnlyList<string> ProfileVerbs => AgentAccountsCommand.ProfileVerbs;

    public const string Usage =
        """
        usage: daoris-driver agent rules [list] · allow|ask|deny|remove <rule> [--workspace <name> | --repository <name>]
               daoris-driver agent rules default <id> on|off · proposals · accept|decline <id> [--note "…"]
               daoris-driver agent trust <agent> <folder> [--profile <account>] [--yes | --dry-run]
               daoris-driver agent settings <agent> [--account <account>] [model <name>|unset] [effort <level>|unset [--for <model>]]
               daoris-driver agent which <agent> [--workspace <name>] [--json]
               daoris-driver agent unpin <agent> [--workspace <name>]
               daoris-driver agent profile [list] · add|remove <agent> <profile> · default <agent> <profile>|--clear [--workspace <name>]
               daoris-driver agent profile order <agent> <profile>…|--clear [--workspace <name>] · ready <agent> <profile>|--own
               daoris-driver agent profile rename <agent> <account> <name> · join <agent> <account> [<workspace>…] [--machine]
        `daoris agent rules|trust|settings|which|unpin …` and `daoris agent profile …` but `use` run these; the family's other verbs
        are still `daoris agent`'s own.
        """;

    private const string Prefix = "daoris";

    /// <summary>The rules' edits, each a <c>RULE_ACTION</c>, and the answers to a proposal, each a <c>RULE_PROPOSAL</c>.</summary>
    private static readonly HashSet<string> RuleEdits = new(StringComparer.Ordinal) { "allow", "ask", "deny", "remove", "default" };

    private static readonly HashSet<string> Answers = new(StringComparer.Ordinal) { "accept", "decline" };

    /// <summary>The flags that take a value, as the CLI's agent family read them: a flag's value is no operand.</summary>
    internal static readonly HashSet<string> Valued = new(StringComparer.Ordinal)
    {
        "--profile", "--workspace", "--account", "--for", "--keep", "--switch", "--curve", "--early", "--near", "--name", "--join",
    };

    /// <summary>
    /// Whether the words change a home, which holds its lease from the top: a rule's edit and a proposal's answer, a setting given a value,
    /// and a trust granted. A read, a question, a dry run and the usage hold none.
    /// </summary>
    public static bool Changes(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.Any(arg => arg is "--help" or "-h")) return false;
        return args[0] switch
        {
            "rules" => args.Count > 1 && (RuleEdits.Contains(args[1]) || Answers.Contains(args[1])),
            "settings" => Operands(args, Valued).Count > 2,
            "trust" => args.Contains("--yes") && !args.Contains("--dry-run"),
            "unpin" => true,
            "profile" => args.Count > 1 && AgentAccountsCommand.ProfileChanges(args[1]),
            _ => false,
        };
    }

    /// <summary>
    /// Whether the words act on a named wiring file's home alone (D194 point 2, the alone-path design §1.2): a verb that writes the wiring
    /// file's home, with <c>DAORIS_HARNESS_CONFIG</c> set. The rules never do: they are <c>DAORIS_HOME</c>'s.
    /// </summary>
    public static bool Isolated(IReadOnlyList<string> args, Func<string, string?> environment) =>
        args.Count > 0 && AgentRoutes.Verbs.Any(row => row.Verb.Split(' ')[1] == args[0] && row.Home == AgentHome.Wiring)
        && environment(HarnessSettings.PathVariable) is { Length: > 0 };

    /// <summary>The verbs that only read, changing no home, so the verb table has no row for them: <c>which</c>.</summary>
    public static IReadOnlyList<string> Reads { get; } = ["which"];

    /// <summary>The verb's run: a read said, a change pressed (its route where the instance answers, its operation alone), or its usage.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        var error = world.Instance.Error;
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            await world.Instance.Out.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        var verb = args.Count > 0 ? args[0] : "";
        if (!Verbs.Contains(verb, StringComparer.Ordinal))
        {
            await error.WriteLineAsync(verb.Length == 0
                ? "daoris-driver agent: name a verb of this program's."
                : $"daoris-driver agent: `{verb}` is not a verb of this program's.").ConfigureAwait(false);
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        try
        {
            // A person's grant from a session's terminal is refused before a word of it is said (D194 point 5): no route takes it.
            if (verb == "trust" && Changes(args) && world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            if (Plan(args, world, out var exit) is { } press) return await PressAsync(world, press, ct).ConfigureAwait(false);
            return exit;
        }
        catch (AgentWords refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return refused.Exit;
        }
        catch (DriverException refused)
        {
            // A refusal of the operation a read or a grant made here, one line with its code (D194 point 6).
            await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
            return 2;
        }
    }

    /// <summary>
    /// The press <paramref name="args"/> ask for, where they name a change: a rule's edit, a proposal's answer, a setting's change, or a
    /// trust granted. A read and a question are said here, answering null with their exit in <paramref name="exit"/>; a refusal of the
    /// words is <see cref="AgentWords"/>, nothing done. A test sends a press's payload through its route.
    /// </summary>
    public static AgentPress? Plan(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        return args[0] switch
        {
            "rules" => args.Count > 1 && args[1] is "proposals" or "accept" or "decline"
                ? Proposals([.. args.Skip(1)], world, out exit)
                : Rules([.. args.Skip(1)], world, out exit),
            "trust" => Trust(args, world, out exit),
            "which" => Which(args, world, out exit),
            "unpin" => AgentAccountsCommand.Unpin(args, world),
            "profile" => AgentAccountsCommand.Profile(args, world, out exit),
            _ => Settings(args, world, out exit),
        };
    }

    // ——— which

    /// <summary>
    /// <c>which &lt;agent&gt; [--workspace &lt;name&gt;] [--json]</c> (PUSH1f2e, the alone-path design §4): the program the agent runs as,
    /// resolved as every start resolves it (<see cref="HarnessProbe.CommandOf"/>, D57 rule 4): the command <c>driver.json</c> names for
    /// it, then its pin (the workspace's where it pins its own), then its binary on <c>PATH</c>. A read: it holds nothing and sends
    /// nothing. A pin with nothing installed at it answers <c>missing</c>, exit 0. With <c>--json</c> it is one object on the standard
    /// output, which the real-run tools read (<c>proof-kit.mjs</c>, <c>acp-trust-probe.mjs</c>).
    /// </summary>
    private static AgentPress? Which(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var (name, toolchain, _, _) = Agent(args, "which", "<agent>", world, path);
        var settings = HarnessSettings.Load(path);
        if (settings.Problem is { } problem) throw new AgentWords(problem);

        var named = DriverConfig.Load(DriverConfig.ResolvePath(world.Instance.Environment, home)).Commands.GetValueOrDefault(name);
        var run = HarnessProbe.CommandOf(name, toolchain, named, settings, home, FlagValue(args, "--workspace"));
        if (args.Contains("--json"))
        {
            Say(new JsonObject
            {
                ["agent"] = name,
                ["run"] = new JsonArray([.. run.Run.Select(word => (JsonNode?)word)]),
                ["managed"] = run.Managed,
                ["pinned"] = run.Pinned,
                ["missing"] = run.Missing,
            }.ToJsonString());
            return null;
        }

        var command = string.Join(' ', run.Run);
        if (run.Missing)
        {
            Say($"`{name}` is pinned to {run.Pinned}, which is not installed on this machine: there is nothing to run. "
                + $"`daoris agent pin {name} {run.Pinned}` installs it.");
        }
        else if (run.Managed is not null)
        {
            Say($"`{name}` runs {command}");
            Say($"  its pin, {run.Pinned}, managed by Daoris.");
        }
        else if (named is { Count: > 0 })
        {
            Say($"`{name}` runs {command}");
            Say($"  the command {DriverConfig.FileName} names for it.");
        }
        else
        {
            Say($"`{name}` runs {command}");
            Say("  found on PATH: no command is named for it and nothing pins it.");
        }

        return null;
    }

    // ——— rules

    private static string RulesHome(AgentsWorld world, HomeEntry entry) =>
        world.Instance.Home ?? throw new AgentWords($"{DaorisHome.Sentence} (wanted: {entry.New})");

    /// <summary><c>rules [list] | allow|ask|deny|remove &lt;rule&gt; | default &lt;id&gt; on|off</c>, the words after <c>rules</c>.</summary>
    private static AgentPress? Rules(IReadOnlyList<string> argv, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var verb = argv.Count > 0 ? argv[0] : "list";
        var home = RulesHome(world, HomeEntry.Permissions);
        var file = PermissionRules.Load(home);

        var workspace = FlagValue(argv, "--workspace");
        var repository = FlagValue(argv, "--repository");
        if (workspace is not null && repository is not null)
        {
            throw new AgentWords("a rule reaches one scope — name a workspace with `--workspace` or a repository with `--repository`, not both.");
        }

        var scope = workspace is not null ? RuleScope.Workspace : repository is not null ? RuleScope.Repository : RuleScope.Machine;
        var name = workspace ?? repository;
        var where = scope == RuleScope.Machine
            ? "every session on this machine"
            : $"{(scope == RuleScope.Workspace ? "workspace" : "repository")} `{name}`";

        string[] Needed(string what)
        {
            var bare = Operands(argv, new HashSet<string>(StringComparer.Ordinal) { "--workspace", "--repository" }).Skip(1).ToArray();
            return bare.Length > 0 ? bare : throw new AgentWords($"`daoris agent rules {verb}` needs a {what}.");
        }

        // Judged here before anything is sent, in the rules' own words, as the instance would judge it.
        static void Judged(Action judge)
        {
            try
            {
                judge();
            }
            catch (DriverException refused)
            {
                throw new AgentWords(refused.Message);
            }
        }

        JsonObject Payload(string action)
        {
            var payload = new JsonObject { ["action"] = action, ["scope"] = scope.ToString().ToLowerInvariant() };
            if (name is not null) payload["name"] = name;
            return payload;
        }

        AgentPress Press(JsonObject payload, Func<PermissionFile, PermissionFile> edit, Func<int> said) =>
            new($"rules {verb}", AgentRoutes.Rule, false, payload, _ =>
            {
                PermissionRules.Save(home, edit(PermissionRules.Load(home)));
                return Task.FromResult<object?>(null);
            }, _ => null, _ => said());

        switch (verb)
        {
            case "list":
                exit = RulesList(home, file, world);
                return null;

            case "allow" or "ask" or "deny":
            {
                var rule = Needed("<rule>")[0];
                var list = verb switch { "allow" => RuleList.Allow, "ask" => RuleList.Ask, _ => RuleList.Deny };
                Judged(() => PermissionRules.Add(PermissionFile.Empty, scope, name, list, rule));
                var payload = Payload("add");
                payload["list"] = verb;
                payload["rule"] = rule;
                return Press(payload, held => PermissionRules.Add(held, scope, name, list, rule), () =>
                {
                    Say($"{Prefix}: `{rule}` is now {(verb == "allow" ? "allowed" : verb == "ask" ? "asked for" : "denied")} for {where}.");
                    Say("  Sessions started from now on are handed it; one already running keeps what it began with.");
                    if (verb == "ask") Say("  An ask is a refusal for anything Daoris starts — nobody is there to answer it.");
                    return 0;
                });
            }

            case "remove":
            {
                var rule = Needed("<rule>")[0];
                Judged(() => PermissionRules.Remove(PermissionFile.Empty, scope, name, rule));
                var payload = Payload("remove");
                payload["rule"] = rule;
                return Press(payload, held => PermissionRules.Remove(held, scope, name, rule), () =>
                {
                    Say($"{Prefix}: `{rule}` is no longer one of the rules for {where}.");
                    return 0;
                });
            }

            case "default":
            {
                var operands = Needed("<id>");
                var id = operands[0];
                var state = operands.Length > 1 ? operands[1] : null;
                if (state is not ("on" or "off"))
                {
                    throw new AgentWords($"say whether the default `{id}` is on or off: `daoris agent rules default {id} on|off`.");
                }

                var on = state == "on";
                Judged(() => PermissionRules.SwitchDefault(PermissionFile.Empty, id, on));
                return new AgentPress($"rules {verb}", AgentRoutes.Rule, false, new JsonObject { ["action"] = "default", ["id"] = id, ["on"] = on }, _ =>
                {
                    PermissionRules.Save(home, PermissionRules.SwitchDefault(PermissionRules.Load(home), id, on));
                    return Task.FromResult<object?>(null);
                }, _ => null, _ =>
                {
                    Say($"{Prefix}: the default `{id}` is {state} on this machine.");
                    return 0;
                });
            }

            default:
                throw new AgentWords($"unknown rules verb '{verb}' — one of: list, allow, ask, deny, remove, default, proposals, accept, decline");
        }
    }

    /// <summary>Every default with whether it is on, then each scope's rules, as the file stands.</summary>
    private static int RulesList(string home, PermissionFile file, AgentsWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        Say($"What an agent Daoris starts may do on this machine — {PermissionRules.PathOf(home)}");
        Say("  Claude Code's own permission rules. Sessions on other agents are handed none of these.");
        if (file.Problem is not null) Say($"  ⚠ {file.Problem}");
        Say("");
        Say("Defaults");
        foreach (var shipped in PermissionRules.Defaults)
        {
            var on = !file.DefaultsOff.Contains(shipped.Id, StringComparer.Ordinal);
            var what = shipped.Hook is { } hook ? $"a hook on {hook}" : string.Join(", ", shipped.Rules);
            Say($"  {shipped.Id,-10} {(on ? "on " : "off")}  {shipped.List.ToString().ToLowerInvariant()}  {what}");
        }

        void Show(string title, RuleLists held)
        {
            if (held.IsEmpty) return;
            Say("");
            Say(title);
            foreach (var rule in held.Allow) Say($"  allow  {rule}");
            foreach (var rule in held.Ask) Say($"  ask    {rule}");
            foreach (var rule in held.Deny) Say($"  deny   {rule}");
        }

        Show("This machine", file.Machine);
        foreach (var (circle, held) in file.Workspaces) Show($"workspace `{circle}`", held);
        foreach (var (repository, held) in file.Repositories) Show($"repository `{repository}`", held);
        return 0;
    }

    /// <summary><c>rules proposals | accept &lt;id&gt; | decline &lt;id&gt; [--note "…"]</c>, the words after <c>rules</c>.</summary>
    private static AgentPress? Proposals(IReadOnlyList<string> argv, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var verb = argv[0];
        var home = RulesHome(world, HomeEntry.RuleProposals);
        if (verb == "proposals")
        {
            exit = ProposalsList(home, world);
            return null;
        }

        var note = FlagValue(argv, "--note");
        string? id = null;
        for (var at = 1; at < argv.Count && id is null; at++)
        {
            if (!argv[at].StartsWith("--", StringComparison.Ordinal) && argv[at - 1] != "--note") id = argv[at];
        }

        if (id is null) throw new AgentWords($"`daoris agent rules {verb}` needs the proposal's id — `daoris agent rules proposals` lists them.");

        var accept = verb == "accept";
        RuleProposal proposal;
        try
        {
            // None by that id, or one already history, is said before anything is sent, as the TypeScript said it.
            proposal = RuleProposals.Open(home, id);
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }

        var payload = new JsonObject { ["id"] = proposal.Id, ["accept"] = accept };
        if (note is not null) payload["note"] = note;
        return new AgentPress($"rules {verb}", AgentRoutes.Proposal, false, payload, _ =>
        {
            RuleProposals.Answer(home, id, accept, note, world.Now());
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            Say(accept
                ? $"{Prefix}: accepted #{proposal.Id} — {RuleProposals.Describe(proposal.Change)} is now in the rules."
                : $"{Prefix}: declined #{proposal.Id} — the rules are as they were.");
            if (accept) Say("  Sessions started from now on are handed it; one already running keeps what it began with.");
            return 0;
        });
    }

    /// <summary>Every proposal, newest first, with its state, its author and its reason, and how to answer those that wait.</summary>
    private static int ProposalsList(string home, AgentsWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var proposals = RuleProposals.Load(home);
        Say($"Agents' proposals to change these rules — {DaorisHome.Entry(home, HomeEntry.RuleProposals)}");
        Say("  A narrowing applies at the driver's next tick; a widening waits for your yes.");
        if (proposals.Count == 0)
        {
            Say("");
            Say("No agent has proposed a change.");
            return 0;
        }

        foreach (var proposal in proposals)
        {
            var state = proposal.State.ToString().ToLowerInvariant();
            Say("");
            Say($"#{proposal.Id}  {state,-9} {RuleProposals.Describe(proposal.Change)}");
            Say($"  from {RuleProposals.Author(proposal)}, {Moments.Write(proposal.Proposed)}");
            Say($"  why: {proposal.Why}");
            if (proposal.SettledBy is { } by) Say($"  {state} by {by}{(proposal.Note is { } note ? $": {note}" : "")}");
        }

        var open = proposals.Count(proposal => proposal.State is ProposalState.Proposed or ProposalState.Waiting);
        if (open > 0)
        {
            Say("");
            Say($"{open} {(open == 1 ? "waits" : "wait")} for an answer: `daoris agent rules accept <id>` or "
                + "`daoris agent rules decline <id> [--note \"…\"]`.");
        }

        return 0;
    }

    // ——— the wiring's verbs: trust and settings

    /// <summary>The wiring file the agent family reads: the one <c>DAORIS_HARNESS_CONFIG</c> names, else the home's.</summary>
    internal static string WiringPath(AgentsWorld world) =>
        world.Instance.Environment(HarnessSettings.PathVariable) is { Length: > 0 } named
            ? named
            : world.Instance.Home is { } home
                ? DaorisHome.Entry(home, HomeEntry.Harnesses)
                : throw new AgentWords($"{DaorisHome.Sentence} (wanted: {HomeEntry.Harnesses.New})");

    /// <summary>
    /// The agent named after the verb, refused rather than defaulted: its toolchain, the settings file its accounts keep, and the roster
    /// it is read from. An agent is a door this machine runs that names a real program, or an agent a door runs as with no door here
    /// (<see cref="AdapterSet.Holder"/>, <c>codex</c>): the stubs name none (D46 §8), as the CLI's <c>TOOLCHAINS</c> named them.
    /// </summary>
    internal static (string Name, HarnessToolchain Toolchain, string? SettingsFile, HarnessRoster Roster) Agent(
        IReadOnlyList<string> args, string verb, string shape, AgentsWorld world, string path, int at = 1)
    {
        var set = world.Adapters(HarnessSettings.HomeOf(path));
        var roster = new HarnessRoster(set, path);
        bool Door(string each) => set.Names.Contains(each, StringComparer.OrdinalIgnoreCase);
        HarnessToolchain? Of(string each) => Door(each) ? set.Resolve(each).Toolchain : set.Holder(each);
        var named = Named(set);
        var name = Operand(args, at) ?? throw new AgentWords($"`agent {verb}` needs {shape} — e.g. `daoris agent {verb} claude-code`");
        if (!named.Contains(name, StringComparer.Ordinal) || Of(name) is not { } toolchain)
        {
            // Never a silent fallback: a machine that installed another agent than the person named is the same failure as a
            // repository that asked for one layout and received another (D23).
            throw new AgentWords($"unknown agent '{name}' — one of: {string.Join(", ", named)}. An agent is added deliberately, never guessed.");
        }

        // A door's accounts are its owner's (AGT7), and so is the file they keep their settings in.
        return (name, toolchain, Door(name) ? roster.AccountToolchain(name)?.SettingsFile : toolchain.SettingsFile, roster);
    }

    /// <summary>The agents a verb may name, in the roster's order: each door that names a real program, and each agent a door runs as.</summary>
    internal static IReadOnlyList<string> Named(AgentsWorld world, string path) => Named(world.Adapters(HarnessSettings.HomeOf(path)));

    private static List<string> Named(AdapterSet set)
    {
        bool Door(string each) => set.Names.Contains(each, StringComparer.OrdinalIgnoreCase);
        HarnessToolchain? Of(string each) => Door(each) ? set.Resolve(each).Toolchain : set.Holder(each);
        return [.. set.Reserved.Where(each => Of(each) is { Binary.Count: > 0 })];
    }

    /// <summary>An account by its id or its name (ACCT2): its id, or what was given where it names none, for the verb to refuse.</summary>
    internal static string AccountNamed(string home, string owner, string given) =>
        AccountNames.Resolve(HarnessSettings.Profiles(home, owner), AccountNames.Of(home, owner), given) ?? given.Trim();

    /// <summary>
    /// <c>trust &lt;agent&gt; &lt;folder&gt; [--profile &lt;account&gt;] --yes</c> (D73): the agent's trust in a folder, asked, then written on
    /// the person's word. Without <c>--yes</c> the question is the command: it names the folder, the account's file and what trusting
    /// means, grants nothing, and exits 1. The account is the one named, else the machine's default, else the agent's own home.
    /// </summary>
    private static AgentPress? Trust(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var (name, toolchain, _, _) = Agent(args, "trust", "<agent>", world, path);
        if (toolchain.TrustFile is not { } trustFile)
        {
            throw new AgentWords($"`{name}` has no trust question Daoris knows of, so there is nothing to grant: its own permissions apply as they are.");
        }

        var given = Operand(args, 2) ?? throw new AgentWords("`agent trust` needs <agent> <folder> — e.g. `daoris agent trust claude-code`");
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(world.Folder, given)));
        if (!Directory.Exists(folder)) throw new AgentWords($"no folder at `{folder}` — trust is granted to a folder a session would run in.");

        var owner = toolchain.Owner(name);
        if (owner != name) Say($"{Prefix}: `{name}` runs as `{owner}`'s accounts — this is one of them.");
        var profile = (FlagValue(args, "--profile") is { } named ? AccountNamed(home, owner, named) : null)
            ?? (HarnessSettings.Load(path).Defaults.TryGetValue(owner, out var fallback) ? fallback : null);
        // The file the driver's hold reads (OWNSPACE1a): the account's, else the agent's own home, its own variable's where set.
        var own = toolchain.ProfileVariable is { Length: > 0 } variable ? world.Instance.Environment(variable) : null;
        var file = Path.Combine(
            profile is not null ? HarnessSettings.ProfileHome(home, owner, profile) : own is { Length: > 0 } ? own : world.UserHome, trustFile);

        Say($"{Prefix}: trusting `{folder}` for `{owner}`, {(profile is not null ? $"as the account `{profile}`" : "as its own account")} — in its own file:");
        Say($"  {file}");
        Say("  This is what the agent asks the first time it runs in a folder. Once it is granted, the");
        Say("  folder's own settings and `permissions.allow` apply there, and the agent stops asking.");
        Say("  Daoris writes that one flag and nothing else in the file.");

        if (args.Contains("--dry-run")) return null;
        if (!args.Contains("--yes"))
        {
            Say("  Not granted: this is the question. Run it again with --yes to grant it.");
            exit = 1;
            return null;
        }

        return new AgentPress("trust", null, false, null, _ => Task.FromResult<object?>(ClaudeTrust.Grant(file, folder)), _ => null, done =>
        {
            var grant = (TrustGrant)done!;
            if (!grant.Changed)
            {
                Say("  Already trusted, so nothing was written.");
            }
            else if (grant.Verified)
            {
                Say("  Granted.");
            }
            else
            {
                Say("  Written, but reading the file back does not show it — a running Claude Code may have saved");
                Say("  over it. Run this again once it has exited.");
                return 1;
            }

            return 0;
        });
    }

    /// <summary>
    /// <c>settings &lt;agent&gt; [--account &lt;account&gt;] [model &lt;v&gt;] [effort &lt;v&gt; [--for &lt;model&gt;]]</c> (AGT6, D98): an
    /// account's own model and effort, in the tool's own settings file under that account. Given nothing to change it says what the file
    /// says; <c>unset</c> clears a key. Never the tool's own configuration home: with no account anywhere it refuses and says how.
    /// </summary>
    private static AgentPress? Settings(IReadOnlyList<string> args, AgentsWorld world, out int exit)
    {
        exit = 0;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var (name, toolchain, known, roster) = Agent(args, "settings", "<agent>", world, path);
        var owner = toolchain.Owner(name);
        if (owner != name) Say($"{Prefix}: `{name}` runs as `{owner}`'s accounts — this is one of them.");
        var edit = SettingsEdit([.. Operands(args, Valued).Skip(2)], FlagValue(args, "--for"));

        if (known is not { } settingsFile)
        {
            var line = $"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, "
                + "so Daoris offers none — set its model with the tool itself.";
            if (edit is not null) throw new AgentWords(line);
            Say($"{Prefix}: {line}");
            return null;
        }

        var given = FlagValue(args, "--account") ?? FlagValue(args, "--profile");
        var account = (given is not null ? AccountNamed(home, owner, given) : null)
            ?? (HarnessSettings.Load(path).Defaults.TryGetValue(owner, out var fallback) ? fallback : null)
            ?? throw new AgentWords(
                $"`{owner}` has no Daoris account named here — Daoris never works in the tool's own configuration home. "
                + "Name one of Daoris's accounts with `--account <name>` (`daoris agent list` shows them), sign in to one with "
                + $"`daoris agent login {owner} --new`, or set the model with the tool itself.");
        // Refused rather than made: a setting is not how an account comes to exist (D66 §3).
        var accounts = HarnessSettings.Profiles(home, owner);
        if (!accounts.Contains(account, StringComparer.Ordinal))
        {
            throw new AgentWords($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
        }

        var where = Path.Combine(HarnessSettings.ProfileHome(home, owner, account), settingsFile);
        int Said(AgentSettingsRead held)
        {
            if (held.Problem is { } problem)
            {
                world.Instance.Error.WriteLine($"{Prefix}: {problem} — fix it, or change the setting with the tool itself.");
                return 2;
            }

            Say($"{Prefix}: `{owner}` account `{account}` — {(edit is not null ? "written to" : "read from")} the tool's own settings:");
            Say($"  {where}");
            const string unset = "the tool's own default";
            Say($"  model    {held.Model ?? unset}");
            Say($"  effort   {held.Effort ?? unset}");
            if (held.PerModel.Count > 0)
            {
                Say("  per model — the tool reads these first, for that model:");
                foreach (var entry in held.PerModel) Say($"    {entry.Model,-24} {entry.Effort}");
            }

            Say("  A session reads these when it starts. A repository's own .claude/settings.json and the");
            Say("  ANTHROPIC_MODEL variable take precedence over an account's model.");
            return 0;
        }

        if (edit is null)
        {
            exit = Said(AgentSettings.Read(where));
            return null;
        }

        // Every value is judged here before anything is sent, in the settings' own words, as the instance would judge it.
        try
        {
            if (edit.Value.Model?.Value is { } model) AgentSettings.JudgeModel(model);
            if (edit.Value.Effort?.Value is { } effort) AgentSettings.JudgeEffort(effort);
            foreach (var (each, value) in edit.Value.PerModel ?? new Dictionary<string, AgentSettingEdit>())
            {
                AgentSettings.JudgeModel(each);
                if (value.Value is { } level) AgentSettings.JudgeEffort(level);
            }
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }

        // While Daoris runs, the Settings screen's edit: a key sent as null cleared, one left out untouched. The answer is the file read
        // back, which this verb says as it says its own read.
        var payload = new JsonObject { ["harness"] = name, ["profile"] = account };
        if (edit.Value.Model is { } setModel) payload["model"] = setModel.Value;
        if (edit.Value.Effort is { } setEffort) payload["effort"] = setEffort.Value;
        if (edit.Value.PerModel is { } perModel)
        {
            payload["perModel"] = new JsonObject(perModel.Select(pair => KeyValuePair.Create(pair.Key, (JsonNode?)pair.Value.Value)));
        }

        return new AgentPress("settings", AgentRoutes.Settings, Isolated(args, world.Instance.Environment), payload,
            _ => Task.FromResult<object?>(AccountSettings.Write(roster, name, account, () => edit.Value).Read),
            answer => new AgentSettingsRead(
                Text(answer, "model"), Text(answer, "effort"),
                answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("perModel", out var models) && models.ValueKind == JsonValueKind.Array
                    ? [.. models.EnumerateArray()
                        .Where(each => Text(each, "model") is not null && Text(each, "effort") is not null)
                        .Select(each => new ModelEffort(Text(each, "model")!, Text(each, "effort")!))]
                    : [],
                Text(answer, "problem")),
            read => Said((AgentSettingsRead)read!));
    }

    /// <summary>The edit the words after <c>settings &lt;agent&gt;</c> ask for, pairs of a key and a value, or null for none.</summary>
    private static (AgentSettingEdit? Model, AgentSettingEdit? Effort, IReadOnlyDictionary<string, AgentSettingEdit>? PerModel)? SettingsEdit(
        IReadOnlyList<string> pairs, string? forModel)
    {
        if (pairs.Count == 0) return null;
        AgentSettingEdit? model = null;
        AgentSettingEdit? effort = null;
        Dictionary<string, AgentSettingEdit>? perModel = null;
        for (var at = 0; at < pairs.Count; at += 2)
        {
            var key = pairs[at];
            var value = at + 1 < pairs.Count ? pairs[at + 1] : null;
            if (key is not ("model" or "effort")) throw new AgentWords($"`{key}` is not a setting Daoris changes — the two are model and effort.");
            if (value is null)
            {
                throw new AgentWords($"`{key}` needs a value — e.g. `{key} {(key == "model" ? "opus" : "high")}`, or `{key} unset`.");
            }

            var set = new AgentSettingEdit(value == "unset" ? null : value);
            if (key == "model")
            {
                if (forModel is not null) throw new AgentWords("--for names the model an effort is for; a model has no model of its own.");
                model = set;
            }
            else if (forModel is not null)
            {
                perModel ??= new Dictionary<string, AgentSettingEdit>(StringComparer.Ordinal);
                perModel[forModel] = set;
            }
            else
            {
                effort = set;
            }
        }

        return (model, effort, perModel);
    }

    /// <summary>
    /// A press as the alone-path design §1.1 takes it: its route through the instance that answers, its words read from the answer's
    /// fields; else its operation here, refused from a session's terminal where it is the person's (D194 point 5), its refusal one line
    /// with its code (D194 point 6). A change no route takes, and one a named wiring file's home holds, act here and send nothing.
    /// </summary>
    private static async Task<int> PressAsync(AgentsWorld world, AgentPress press, CancellationToken ct)
    {
        var error = world.Instance.Error;
        async Task<int> Alone()
        {
            if ((press.Route is null || press.Route.Class == RouteClass.Person)
                && world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            object? done;
            try
            {
                done = await press.Act(ct).ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
                return 2;
            }
            catch (IOException held)
            {
                // The file's edit lock, held by another writer for the whole of its wait: nothing was written.
                await error.WriteLineAsync($"{Prefix}: {held.Message}").ConfigureAwait(false);
                return 2;
            }

            return press.Words(done);
        }

        if (press.Route is null || press.Isolated || press.Payload is null) return await Alone().ConfigureAwait(false);
        return await InstanceDoor.PressAsync(press.Route, press.Payload, Alone, answer => press.Words(press.Fields(answer)), world.Instance, ct)
            .ConfigureAwait(false);
    }

    /// <summary>The value after a flag, refusing a flag with nothing after it, as the CLI's <c>flagValue</c>.</summary>
    internal static string? FlagValue(IReadOnlyList<string> args, string flag)
    {
        var at = args.ToList().IndexOf(flag);
        if (at < 0) return null;
        var value = at + 1 < args.Count ? args[at + 1] : null;
        return value is { Length: > 0 } && !value.StartsWith("--", StringComparison.Ordinal)
            ? value
            : throw new AgentWords($"{flag} needs a name — e.g. `{flag} aurora`");
    }

    /// <summary>Every word that is neither a flag nor the value of one, as the CLI's <c>operands</c>.</summary>
    internal static List<string> Operands(IReadOnlyList<string> args, IReadOnlySet<string> valued)
    {
        var found = new List<string>();
        for (var at = 0; at < args.Count; at++)
        {
            if (valued.Contains(args[at])) at++;
            else if (!args[at].StartsWith("--", StringComparison.Ordinal)) found.Add(args[at]);
        }

        return found;
    }

    internal static string? Operand(IReadOnlyList<string> args, int index)
    {
        var found = Operands(args, Valued);
        return index < found.Count ? found[index] : null;
    }

    internal static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
