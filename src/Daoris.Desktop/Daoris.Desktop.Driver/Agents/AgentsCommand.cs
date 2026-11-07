using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris agent</c> (D194, the alone-path design §1.2): the agents' rules, trust and settings from a terminal, what an agent runs as,
/// and its pin taken back and its accounts (<see cref="AgentAccountsCommand"/>), and the list of this machine's agents, its bare word too
/// (<see cref="AgentListCommand"/>), and an agent's install and an API key's account, the long verbs no route takes
/// (<see cref="AgentLongCommand"/>), an agent's sign-in (<see cref="AgentLoginCommand"/>), and its pin and update
/// (<see cref="AgentPinCommand"/>): the whole family. Each change a route
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
[CommandGroup("agent", "list", "install", "update", "login", "key", "pin", "unpin", "which", "trust", "settings", "rules", "account",
    Nested = ["account: list add remove default order use ready rename join"])]
public static class AgentsCommand
{
    /// <summary>The verbs this program has, each the CLI hands on (the CLI's allow-list is held to it).</summary>
    public static IReadOnlyList<string> Verbs { get; } =
        ["list", "rules", "trust", "settings", "which", "unpin", "profile", "install", "key", "login", "update", "pin"];

    /// <summary>The <c>profile</c> verbs this program has, each the CLI hands on: every one (PUSH1f2e2, <c>use</c> PUSH1f2e3).</summary>
    public static IReadOnlyList<string> ProfileVerbs => AgentAccountsCommand.ProfileVerbs;

    public const string Usage =
        """
        usage: daoris agent list
               daoris agent rules [list] · allow|ask|deny|remove <rule> [--workspace <name> | --repository <name>]
               daoris agent rules default <id> on|off · proposals · accept|decline <id> [--note "…"]
               daoris agent trust <agent> <folder> [--account <account>] [--yes | --dry-run]
               daoris agent settings <agent> [--account <account>] [model <name>|unset] [effort <level>|unset [--for <model>]]
               daoris agent which <agent> [--workspace <name>] [--json]
               daoris agent unpin <agent> [--workspace <name>]
               daoris agent account [list] · add|remove <agent> <account>
               daoris agent account order <agent> <account>…|--clear · ready <agent> <account>|--own
               daoris agent account use <agent> [goal|order] [--keep <account>|--no-keep] [--switch smart|curve|off]
                     [--curve <window>=<elapsed>:<used>,…]… [--no-curve] · use <agent> --clear
               daoris agent account rename <agent> <account> <name>
               daoris agent install <agent>
               daoris agent key <agent> [--account <account>]     the key read from standard input, never an argument
               daoris agent login <agent> [--account <account>] [--device-code]
               daoris agent login <agent> --new [--name <name>] [--device-code]
               daoris agent update <agent> [--workspace <name>]
               daoris agent pin <agent> <version> [--workspace <name>]
        Each change takes --wait <minutes> (0 to 30, the default 30) or --no-wait: how long to wait on its Confirm in Daoris's window.
        """;

    internal const string Prefix = "daoris";

    /// <summary>The rules' edits, each a <c>RULE_ACTION</c>, and the answers to a proposal, each a <c>RULE_PROPOSAL</c>.</summary>
    private static readonly HashSet<string> RuleEdits = new(StringComparer.Ordinal) { "allow", "ask", "deny", "remove", "default" };

    private static readonly HashSet<string> Answers = new(StringComparer.Ordinal) { "accept", "decline" };

    /// <summary>The flags that take a value, as the CLI's agent family read them: a flag's value is no operand.</summary>
    internal static readonly HashSet<string> Valued = new(StringComparer.Ordinal)
    {
        "--workspace", "--account", "--for", "--keep", "--switch", "--curve", "--early", "--near", "--name",
    };

    /// <summary>
    /// Whether the words change a home, which holds its lease from the top: a rule's edit and a proposal's answer, a setting given a value,
    /// and a trust granted. A read, a question, a dry run and the usage hold none, and so do the long verbs (the alone-path design §1.4):
    /// <c>install</c> holds none at all, its installer writing the machine and not the home, and <c>key</c>, <c>login</c>, <c>pin</c> and a
    /// pinned <c>update</c> take it themselves to publish.
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
            "profile" => args.Count > 1 && AgentAccountsCommand.ProfileChanges(args),
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

    /// <summary>
    /// The verbs that only read, changing no home, so the verb table has no row for them: <c>list</c> (PUSH1f2e4), whose only write is the
    /// readings <c>reads.json</c> keeps of each account it asked, as a probe's are, and <c>which</c>.
    /// </summary>
    public static IReadOnlyList<string> Reads { get; } = ["list", "which"];

    /// <summary>The verb's run: a read said, a change pressed (its route where the instance answers, its operation alone), or its usage.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        var error = world.Instance.Error;
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            await world.Instance.Out.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        // None named is `list`, as `daoris agent` alone always was (PUSH1f2e4).
        var verb = args.Count > 0 ? args[0] : "list";
        if (!Verbs.Contains(verb, StringComparer.Ordinal))
        {
            await error.WriteLineAsync($"daoris agent: `{verb}` is not a verb of this program's.").ConfigureAwait(false);
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        try
        {
            // A read that asks each agent, said here: no press, no lease, and from a session's terminal too.
            if (verb == "list") return await AgentListCommand.RunAsync(world, ct).ConfigureAwait(false);

            // The long verbs (PUSH1f2e5, the alone-path design §1.4): no route takes them, and each holds no lease at its top.
            if (verb == "install") return await AgentLongCommand.InstallAsync(args, world, ct).ConfigureAwait(false);
            if (verb == "key") return await AgentLongCommand.KeyAsync(args, world, ct).ConfigureAwait(false);
            if (verb == "login") return await AgentLoginCommand.LoginAsync(args, world, ct).ConfigureAwait(false);
            if (verb == "pin") return await AgentPinCommand.PinAsync(args, world, ct).ConfigureAwait(false);
            if (verb == "update") return await AgentPinCommand.UpdateAsync(args, world, ct).ConfigureAwait(false);

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
                ? AgentRulesVerb.Proposals([.. args.Skip(1)], world, out exit)
                : AgentRulesVerb.Rules([.. args.Skip(1)], world, out exit),
            "trust" => AgentTrustVerb.Trust(args, world, out exit),
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

        var named = DriverConfig.Load(DriverConfig.ResolvePath(home)).Commands.GetValueOrDefault(name);
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
    /// (<see cref="ProviderRegistry.AgentNamed"/>, <c>codex</c>): the stubs name none (D46 §8), as the CLI's <c>TOOLCHAINS</c> named them.
    /// </summary>
    internal static (string Name, AccountAgent Toolchain, string? SettingsFile, HarnessRoster Roster) Agent(
        IReadOnlyList<string> args, string verb, string shape, AgentsWorld world, string path, int at = 1)
    {
        var set = world.Adapters(HarnessSettings.HomeOf(path));
        var roster = new HarnessRoster(set, path);
        AccountAgent? Of(string each) => set.AgentNamed(each);
        var named = Named(set);
        var name = Operand(args, at) ?? throw new AgentWords($"`agent {verb}` needs {shape} — e.g. `daoris agent {verb} claude-code`");
        if (!named.Contains(name, StringComparer.Ordinal) || Of(name) is not { } toolchain)
        {
            // Never a silent fallback: a machine that installed another agent than the person named is the same failure as a
            // repository that asked for one layout and received another (D23).
            throw new AgentWords($"unknown agent '{name}' — one of: {string.Join(", ", named)}. An agent is added deliberately, never guessed.");
        }

        // A door's accounts are its owner's (AGT7), and so is the file they keep their settings in.
        return (name, toolchain, set.Names.Contains(name, StringComparer.OrdinalIgnoreCase) ? roster.AccountAgentOf(name)?.Accounts.SettingsFile : toolchain.Accounts.SettingsFile, roster);
    }

    /// <summary>The agents a verb may name, in the roster's order: each door that names a real program, and each agent a door runs as.</summary>
    internal static IReadOnlyList<string> Named(AgentsWorld world, string path) => Named(world.Adapters(HarnessSettings.HomeOf(path)));

    /// <summary>
    /// The agents whose accounts this terminal's set knows (POOLSTRAY1, <see cref="ProviderRegistry.Agents"/>): what every read and write of the
    /// wiring here hands <see cref="HarnessSettings"/>, so a stray folder under the account homes is never read as an agent.
    /// </summary>
    internal static IReadOnlyList<string> Agents(AgentsWorld world, string path) => world.Adapters(HarnessSettings.HomeOf(path)).Agents;

    // The operands' rule (AGENTPROVIDER1 row b), read from the registry: a door, or the agent a door runs as, naming a binary.
    private static List<string> Named(ProviderRegistry set) => [.. set.Operands()];

    /// <summary>An account by its id or its name (ACCT2): its id, or what was given where it names none, for the verb to refuse.</summary>
    internal static string AccountNamed(string home, string owner, string given) =>
        AccountNames.Resolve(HarnessSettings.Profiles(home, owner), AccountNames.Of(home, owner), given) ?? given.Trim();

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
        var owner = roster.Adapters.OwnerOfAgent(name).Value;
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

        var given = FlagValue(args, "--account");
        // With none named, the order's first (POOL1b, the account-pool design §5.1), where the machine's default was.
        var account = (given is not null ? AccountNamed(home, owner, given) : null)
            ?? HarnessSettings.LoadSet(path, [owner]).FirstOf(owner)
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
        var payload = new JsonObject { ["door"] = name, ["profile"] = account };
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
