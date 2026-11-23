using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// The standing choices from a terminal: <c>daoris settings</c>, <c>role</c>, and the place, workflow and retry words the tree routes
/// here. Each change is its route through <see cref="IChoiceTerminal.PressAsync"/> where the running Daoris answers, confirmed in its
/// window; else the choice's own operation, on the terminal's word, refused from a session's terminal. A change no
/// route takes acts alone even beside a running Daoris (<see cref="ChoiceRoutes"/>): <c>brainstorms</c>; the workflows' writes are routes (<see cref="DriverWorkflowsCommand"/>). Its words are one function per verb on
/// both paths (<c>fixtures/driver-choices-text.json</c>, <c>fixtures/driver-workflows-text.json</c>). The settings' own keys' verbs are
/// here; a rule a subdomain above owns has its verb beside the rule (<see cref="ChoiceVerb"/>), which Loop wires into the world
/// (<see cref="DriverChoiceVerbs"/>) and this command asks for, so it names no type of theirs, nor the instance's world and door.
/// </summary>
/// <remarks>
/// <para>Exit codes are the CLI's, case by case: 0 said or set · 1 the person's refusal in the window, its two minutes' end, or a
/// session's terminal · 2 the usage, a refusal of the words, the operation or the route.</para>
/// </remarks>
[CommandGroup("settings", "notify", "cooloff", "quiet", "drain", "keep", "cap", "manifest", "handover", "growth", "brainstorms", "terminal", "risk", "cast", "checks", "vacancy", Bare = true)]
[CommandGroup("role", "set", "remove", "default", "policy", "rules", "brief", Takes = "[<id>]")]
public static class DriverChoicesCommand
{
    /// <summary>The verbs this program has, each the CLI hands on (the CLI's allow-list is held to it).</summary>
    public static IReadOnlyList<string> Verbs { get; } =
    [
        "list", "drive", "undrive", "hold", "resume", "notify", "cap", "retry", "cooloff", "quiet", "drain", "manifest", "line", "across", "standing", "language", "review", "opinion", "helpers", "handover", "growth", "commits",
        "brainstorms", "workflow", "keep", "role", "terminal", "risk", "cast", "checks", "vacancy", MachineSettingsSaid.Every,
    ];

    public const string Usage =
        """
        usage: daoris place list
               daoris place drive|undrive|hold|resume <repository>
               daoris settings [notify|cap|cooloff|quiet|drain|manifest|terminal|keep|checks|vacancy]  (no verb says each setting; a verb with no value, that one)
               daoris settings notify on|off · cap <n> · cooloff <minutes> · quiet <minutes> · drain <minutes> · manifest files <n>|minutes <n>|copy <megabytes> · terminal ask|act
               daoris settings checks <n>|auto  (how many checks the machine runs at once; auto, the default, as many as its memory admits)
               daoris settings vacancy <hours>  (how long a step nobody's session answers stands before Daoris gives it up; 24 by default)
               daoris settings risk [<workspace>|machine] [--repository <name>] [low|medium|high] [--mark <marks>] [--unmark <marks>]
               daoris settings risk why <run> [<step>]  (what the risk policy said at each hold of a run, beside what happened)
               daoris settings risk propose <place> <act> none|role|owner|remove [--by <role>] [--mode full|restricted]  (Ask Daoris proposes a change to the policy; nothing changes)
               daoris settings risk propose <place> cast <role> <holder>|default  or  <place> access full|clear|<pause>=<value>…  (a cast or an access mode, proposed the same way)
               daoris settings risk apply <proposal> <digest>  (applies it on the owner's own turn naming both)
               daoris settings cast [<workspace>|machine] [--repository <name>] [<role> person|agent|agent:<name>|default] [--why "…"]
               daoris quest retry <quest> [--at <n> | --session <id>]
               daoris place line <repository>|--workspace <name> [<branch>|--clear] [--commit merge|rebase|squash] [--batch <1 to 8>]
                      [--order arrival|apart] [--gates declared|none] [--unreached|--reached split|hold|land] [--integrate-line <branch>];
                      the policy's keys merge into the place's own, `--no-policy` withdraws it; either alone leaves the line as it is
               daoris place across <repository>|--workspace <name> read on|off|--clear
               daoris place across <repository> write-to <other> [--clear]
               daoris place standing <repository> "…"|--clear
               daoris place language <repository>|--workspace <name> en|zh|--clear
               daoris place review <repository>|--workspace <name> <environment> --kind local|deployed --procedure <path>
                      [--address <url>] [--run "…"] [--required|--not-required]; none, --drop <environment>, or --clear
               daoris place opinion <repository>|--workspace <name> --agents <adapter,adapter> [--on landing,steps]
                      [--required|--not-required] [--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]; none, or --clear
               daoris place helpers <repository>|--workspace <name> [--mode ask|on|off] [--agents <a,b>] | --clear
               daoris settings handover <repository>|--workspace <name> --mode ask|on|off | --clear
               daoris settings growth <workspace> [--repository <name>] mode|asked|off  (no value says it: the person's alone, never an agent's)
               daoris place commits <repository>|--workspace <name> [--mode as-you-go|ask|at-end]
                      [--convention "…"|--no-convention] | --clear
               daoris settings brainstorms [--workspace <name>] [--mode ask|off] [--agents <a,b>] [--pass-minutes <n>]
                      [--synthesis-minutes <n>] [--challenge-minutes <n>] [--request-minutes <n>] | --clear
               daoris workflow list · show <id>[@<version>] · show --repository <name>|--workspace <name> [--kind <kind>]
               daoris workflow defaults [<default> [--to <file>]]
               daoris workflow new <id> --from <default>|<id>[@<version>]
                      [--with-opinion] [--with-look] [--name "…"]
               daoris workflow apply <id> <file> [--base <version>]
               daoris workflow export <id> [--to <file>] · import <file>
               daoris workflow use <id>|--clear --repository <name>|--workspace <name> [--kind <kind>|--landed]
                      (current only with --landed: no verification)
               daoris workflow kind <workspace> <kind> --label "…" [--paths <path,…>] | --drop
                      (a workflow's writes say their change, and with --plan save nothing)
               daoris settings keep [set-aside <days>|whole|slim|default · downloads <days>|never|default · logs <days>|default
                      · history <days>|never|default · floor <n>GB|<n>%|off|default]
               daoris role [<id>] · role set <id> [--name "…"] [--name-in <tag>="…"]… [--takes <work,…>]
                      [--scope machine|workspace:<id>|repository:<id>] ["<agent> [<model>|-] [<effort>|-]" … | off]
                      · role remove|default <id> · role suggested [--use]
        Each change takes --wait <minutes> (0 to 30, the default 30) or --no-wait: how long to wait on its Confirm in Daoris's window.
        """;

    private const string LineUsage =
        "`driver line` needs <repository>|--workspace <name>, then <branch>|--clear and/or the policy's keys (--commit merge|rebase|squash, "
        + "--batch <1 to 8>, --order arrival|apart, --gates declared|none, --unreached|--reached split|hold|land, --integrate-line <branch>), "
        + "or --no-policy — e.g. `daoris place line aurora-engine develop` or `daoris place line aurora-engine --gates declared`.";

    public const string SessionVariable = ChildMarks.SessionVariable;

    /// <summary>
    /// The refusal of a person's press acting in this process from a session's terminal: advice,
    /// since its absence grants nothing; an agent's honest attempt is not passed as the person's.
    /// </summary>
    public const string SessionsTerminal =
        "this change is the person's, and this terminal is a session's (`DAORIS_SESSION_ID`): make it in Daoris's window, through Ask "
        + "Daoris's proposal and its Apply, or from your own terminal; nothing was done.";

    internal const string Prefix = "daoris";

    private static readonly ConcurrentDictionary<string, Func<IReadOnlyList<string>, bool>> Readings = new(StringComparer.Ordinal);

    internal static void Answer(string verb, Func<IReadOnlyList<string>, bool> changes) => Readings[verb] = changes;

    /// <summary>
    /// Whether the words change the home: a verb this program has that is not <c>list</c>, and no plan or usage asked; <c>brainstorms</c>
    /// only where it sets or clears, and a workflow's only where it writes.
    /// </summary>
    public static bool Changes(IReadOnlyList<string> args)
    {
        if (args.Any(arg => arg is "--help" or "-h" or "--plan")) return false;
        var verb = args.Count > 0 ? args[0] : "list";
        return verb switch
        {
            "list" or MachineSettingsSaid.Every => false,
            _ when args.Count == 1 && MachineSettings.Of(verb) is not null => false,
            "workflow" => ChoiceGrammar.WorkflowChanges([.. args.Skip(1)]),
            "growth" => ChoiceGrammar.GrowthChanges(args),
            "risk" => PlaceRiskVerb.Changes(args),
            "brainstorms" => args.Any(arg => arg is "--clear" or "--mode" or "--agents" || ChoiceGrammar.BrainstormMinuteFlags.Contains(arg)),
            // `keep` naming no kind says each rule, a read.
            "keep" => args.Count > 1,
            // `role` naming one role, or none, says it, a read.
            "role" => args.Count > 1 && args[1] is "set" or "remove" or "default" or "policy" or "rules",
            _ when Readings.TryGetValue(verb, out var reading) => reading(args),
            _ => Verbs.Contains(verb, StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// The verb's run: a read said, a change pressed (its route where the instance answers, its operation alone), or its usage.
    /// </summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, DriverChoicesWorld world, CancellationToken ct)
    {
        var said = world.Instance.Out;
        var error = world.Instance.Error;
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            await said.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        var verb = args.Count > 0 ? args[0] : "list";
        if (!Verbs.Contains(verb, StringComparer.Ordinal))
        {
            await error.WriteLineAsync($"daoris: `{verb}` is not a verb of this program's.").ConfigureAwait(false);
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        DriverConfig before;
        try
        {
            before = DriverConfig.Load(world.ConfigPath);
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            await error.WriteLineAsync($"{Prefix}: {unreadable.Message}").ConfigureAwait(false);
            return 2;
        }

        if (verb == "list") return List(before, world);
        if (verb == MachineSettingsSaid.Every) return MachineSettingsSaid.All(before, world);
        if (Registered(world, verb)?.RunAsync(args, before, world, ct) is { } running) return await running.ConfigureAwait(false);

        ChoicePress press;
        try
        {
            if (!Changes(args) && Registered(world, verb)?.Said(args, before, world) is { } exit) return exit;
            if (!Changes(args) && MachineSettingsSaid.One(args, before, world) is { } one) return one;
            press = Plan(args, before, world);
        }
        catch (ChoiceWords refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return refused.Exit;
        }

        async Task<int> Alone()
        {
            if (world.Instance.Environment(SessionVariable) is { Length: > 0 })
            {
                await error.WriteLineAsync($"{Prefix}: {SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            object? fields;
            try
            {
                fields = await press.Act(ct).ConfigureAwait(false);
            }
            catch (ChoiceWords refused)
            {
                await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
                return refused.Exit;
            }
            catch (DriverException refused)
            {
                // One line on both paths: the route's refusal is `{verb}: {sentence} ({code})`, and so is the operation's.
                await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
                return 2;
            }

            return press.Words(fields);
        }

        if (press.Route is null) return await Alone().ConfigureAwait(false);
        return await world.Instance.PressAsync(press.Route, press.Payload, Alone, answer => press.Words(press.Fields(answer)), ct)
            .ConfigureAwait(false);
    }

    /// <summary>The verb of the world's that carries <paramref name="verb"/>, or null where none does.</summary>
    private static ChoiceVerb? Registered(DriverChoicesWorld world, string verb) => world.Verbs.FirstOrDefault(each => each.Word == verb);

    /// <summary>
    /// The press <paramref name="args"/> ask for, read from the words and judged against <paramref name="before"/>, the choices as this
    /// terminal read them; a refusal of the words is <see cref="ChoiceWords"/>, nothing done.
    /// </summary>
    public static ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = args.Count > 0 ? args[0] : "list";
        var path = world.ConfigPath;
        var write = world.Instance.Out;
        void Say(string line) => write.WriteLine(line);
        ChoicePress Change(InstanceRoute? route, JsonObject? payload, Func<DriverConfig, DriverConfig> change, Func<int> words) =>
            new(verb, route, payload, _ =>
            {
                StandingChoices.Write(path, change);
                return Task.FromResult<object?>(null);
            }, _ => null, _ => words());

        switch (verb)
        {
            case "drive":
            case "undrive":
            case "hold":
            case "resume":
            {
                var repository = Named(args, verb);
                var drivable = verb is "drive" or "undrive";
                var present = verb is "drive" or "hold";
                return Change(
                    drivable ? ChoiceRoutes.Drivable : ChoiceRoutes.Hold,
                    drivable ? new JsonObject { ["repository"] = repository, ["drivable"] = present } : new JsonObject { ["repository"] = repository, ["held"] = present },
                    config => drivable ? config.WithDrivable(repository, present) : config.WithHold(repository, present),
                    () =>
                    {
                        if (drivable)
                        {
                            Say(present ? $"daoris: this machine may drive `{repository}`." : $"daoris: this machine no longer drives `{repository}`.");
                            if (present)
                            {
                                Say("  Driving is additive (D46 §2): outside sessions and hand work in that repository are");
                                Say("  untouched, and a quest somebody else already took is never the driver's to start.");
                            }
                        }
                        else
                        {
                            Say(present ? $"daoris: `{repository}` is held — still drivable, not now." : $"daoris: `{repository}` is released; the driver may start it again.");
                        }

                        Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                        return 0;
                    });
            }

            case "notify":
            {
                var direction = args.Skip(1).FirstOrDefault(token => token is "on" or "off")
                    ?? throw new ChoiceWords("`driver notify` needs on|off — e.g. `daoris settings notify off`.");
                var on = direction == "on";
                return Change(ChoiceRoutes.Notify, new JsonObject { ["notify"] = on }, config => config.WithNotify(on), () =>
                {
                    Say(on ? "daoris: this machine says so when a session parks or ends without you asking." : "daoris: this machine will not interrupt you about sessions.");
                    if (!on)
                    {
                        Say("  The records still say everything they said — this is about being TOLD, not about");
                        Say("  what is recorded. A parked session waits for you either way.");
                    }

                    Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                    return 0;
                });
            }

            case "cap":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || value < 1 || value > int.MaxValue)
                {
                    throw new ChoiceWords("`driver cap` needs a whole number of concurrent sessions, at least 1 — e.g. `daoris settings cap 3`.");
                }

                var cap = (int)value;
                return Change(ChoiceRoutes.Cap, new JsonObject { ["cap"] = cap }, config => config.WithCap(cap), () =>
                {
                    Say($"daoris: this machine runs at most {cap} session(s) at once.");
                    return 0;
                });
            }

            case "cooloff":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || value < 1 || value > int.MaxValue)
                {
                    throw new ChoiceWords("`driver cooloff` needs a whole number of minutes, at least 1 — e.g. `daoris settings cooloff 60`; a zero "
                        + "cool-off would start a spent account again at every look.");
                }

                var minutes = (int)value;
                return Change(ChoiceRoutes.CoolOff, new JsonObject { ["minutes"] = minutes }, config => config.WithCoolOff(minutes), () =>
                {
                    Say($"daoris: an account whose agent names no time for its limit cools for {minutes} minutes on this machine.");
                    Say("  When the agent names when its limit resets, that time is the cool-off, whatever this says.");
                    Say($"  Written to {path} — read at the next limit, so nothing restarts.");
                    return 0;
                });
            }

            case "quiet":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || !QuietSetting.Takes((long)value))
                {
                    throw new ChoiceWords($"`driver quiet` needs a whole number of minutes from {QuietSetting.Least} to {QuietSetting.Most} — e.g. `daoris settings quiet 15`.");
                }

                var minutes = (int)value;
                return Change(ChoiceRoutes.Quiet, new JsonObject { ["minutes"] = minutes }, config => config.WithQuiet(minutes), () =>
                {
                    Say($"daoris: a session that says nothing and runs nothing for {minutes} minutes is looked in on; none is ever stopped for running long.");
                    Say($"  Written to {path} — read at the next look, so nothing restarts.");
                    return 0;
                });
            }

            case "drain":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || !DrainSetting.Takes((long)value)) throw new ChoiceWords(DrainSetting.Refusal);

                var minutes = (int)value;
                return Change(ChoiceRoutes.Drain, new JsonObject { ["minutes"] = minutes }, config => config.WithDrain(minutes), () =>
                {
                    Say($"daoris: a staged build holds new work for {minutes} minutes at most; then work starts again, and it installs once nothing runs.");
                    Say($"  Written to {path} — read at the update's next look, so nothing restarts.");
                    return 0;
                });
            }

            case "manifest":
            {
                var which = args.ElementAtOrDefault(1);
                var value = args.Count == 3 ? Number(args[2]) : double.NaN;
                var files = which == "files" && IsWhole(value) && ManifestSetting.TakesFiles((long)value) ? (int)value : (int?)null;
                var minutes = which == "minutes" && IsWhole(value) && ManifestSetting.TakesMinutes((long)value) ? (int)value : (int?)null;
                var copy = which == "copy" && IsWhole(value) && ManifestSetting.TakesCopy((long)value) ? (int)value : (int?)null;
                if (files is null && minutes is null && copy is null)
                {
                    throw new ChoiceWords(which switch
                    {
                        "files" => ManifestSetting.FilesRefusal,
                        "minutes" => ManifestSetting.MinutesRefusal,
                        "copy" => ManifestSetting.CopyRefusal,
                        _ => ManifestSetting.OneRefusal,
                    });
                }

                return Change(ChoiceRoutes.Manifest, new JsonObject { [which!] = files ?? minutes ?? copy }, config => config.WithManifest(files, minutes, copy), () =>
                {
                    Say(files is { } count
                        ? $"daoris: a folder's manifest read stops at {count} files; a folder past it is too large to follow, and still works in place."
                        : copy is { } megabytes
                        ? $"daoris: a folder of more than {megabytes} MB is not copied for a session working apart; its session works in the folder itself when it is free."
                        : $"daoris: a folder's manifest read stops after {minutes} minutes; a folder past it is too large to follow, and still works in place.");
                    Say($"  Written to {path} — read at the next manifest, so nothing restarts.");
                    return 0;
                });
            }

            case "line":
            {
                string[] valued = ["--workspace", .. LinePolicyFlags.Valued];
                Flags(args, valued, ["--clear", LinePolicyFlags.Withdraw], "place line", LineUsage);
                var workspace = FlagValue(args, "--workspace");
                var clear = args.Contains("--clear");
                var words = Operands(args, valued);
                var first = words.ElementAtOrDefault(1);
                var second = words.ElementAtOrDefault(2);
                var name = workspace ?? first;
                var branch = workspace is not null ? first : second;
                // The policy's keys (how the line is integrated) ride beside the branch, or alone, which leaves the line as it is.
                var policyNamed = LinePolicyFlags.Names(args);
                var branchNamed = clear || !string.IsNullOrEmpty(branch);
                if (string.IsNullOrEmpty(name) || (!branchNamed && !policyNamed)) throw new ChoiceWords(LineUsage);
                if (!clear && branchNamed && !BranchName.IsValid(branch!)) throw new ChoiceWords($"`{branch}` is not a branch name git would take.");
                var set = clear ? null : branch;
                var payload = Scoped(workspace, name);
                if (branchNamed) payload["branch"] = set;
                LandingIntegrate? policy = null, keys = null;
                if (policyNamed)
                {
                    // Only the keys named travel and are laid over the policy held when the change is made, never the whole as read now.
                    keys = LinePolicyFlags.Named(args);
                    var own = workspace is not null ? AtName(before.WorkspaceLinePolicies, workspace) : AtName(before.LinePolicies, name);
                    policy = keys is null ? null : LinePolicyKey.Overlay(own, keys);
                    // The judge the window's press meets, before a route is asked or a file written.
                    _ = Judged(() => workspace is not null ? before.WithWorkspaceLinePolicy(workspace, policy) : before.WithLinePolicy(name, policy));
                    payload["policy"] = keys is null ? null : LinePolicyKey.Wire(keys);
                    if (keys is not null) payload["merge"] = true;
                }

                return Change(ChoiceRoutes.Line, payload,
                    config =>
                    {
                        if (branchNamed) config = workspace is not null ? config.WithWorkspaceLine(workspace, set) : config.WithLine(name, set);
                        if (!policyNamed) return config;
                        var held = workspace is not null ? AtName(config.WorkspaceLinePolicies, workspace) : AtName(config.LinePolicies, name);
                        var next = keys is null ? null : LinePolicyKey.Overlay(held, keys);
                        return workspace is not null ? config.WithWorkspaceLinePolicy(workspace, next) : config.WithLinePolicy(name, next);
                    }, () =>
                    {
                        var whose = workspace is not null ? $"repositories in the workspace `{workspace}`" : $"`{name}`";
                        if (branchNamed)
                        {
                            Say(set is null
                                ? $"daoris: {whose} take{(workspace is not null ? "" : "s")} {(workspace is not null ? "their" : "its")} line from "
                                  + $"{(workspace is not null ? "their own setting or " : "its workspace, else ")}the checkout again."
                                : $"daoris: work in {whose} grows from `{set}` and lands on it.");
                            if (set is not null && workspace is not null)
                            {
                                Say("  A repository with a line of its own keeps it — `daoris place line <repository> --clear` hands it back.");
                            }

                            Say("  A session already running keeps the line it started from.");
                        }

                        if (policyNamed)
                        {
                            Say(policy is null
                                ? workspace is not null
                                    ? $"daoris: {whose} that set none of their own integrate their line by the defaults again."
                                    : $"daoris: {whose} takes its workspace's line policy, else the defaults, again."
                                : $"daoris: the line of {whose} is integrated with {LinePolicyFlags.Listed(policy)}; a key left out reads its default.");
                            if (policy is not null && workspace is not null)
                            {
                                Say("  A repository with a policy of its own keeps it — `daoris place line <repository> --no-policy` hands it back.");
                            }
                        }

                        Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                        return 0;
                    });
            }

            case "across":
                return Across(args, world);

            case "standing":
            {
                var clear = args.Contains("--clear");
                var words = Operands(args, []);
                var repository = words.ElementAtOrDefault(1);
                var says = string.Join(' ', words.Skip(2)).Trim();
                if (string.IsNullOrEmpty(repository) || (!clear && says.Length == 0))
                {
                    throw new ChoiceWords("`driver standing` needs <repository>, then the answer in your words, or --clear — e.g. "
                        + "`daoris place standing work-app \"dev writes allowed; test locally against dev; prod only on a yes\"`.");
                }

                if (!clear && says.Length > DriverConfig.StandingLimit)
                {
                    throw new ChoiceWords($"a standing answer is your words, at most 2,000 characters — these are {says.Length}.");
                }

                // The repository's spelling first written, when it has one in another case: one entry, never two.
                var key = FindName(before.Standing.Keys, repository) ?? repository;
                var now = world.Now();
                return Change(ChoiceRoutes.Standing, new JsonObject { ["repository"] = repository, ["says"] = clear ? null : says },
                    config => config.WithStanding(repository, clear ? null : says, now), () =>
                    {
                        if (clear)
                        {
                            Say($"daoris: `{key}` keeps no standing answer; its sessions are handed none.");
                        }
                        else
                        {
                            Say($"daoris: every session in `{key}` is handed your standing answer, beneath its quest:");
                            Say($"  > {says}");
                            Say("  A quest's own words, and yours on its ask, are newer and win where they differ. It is kept on this");
                            Say("  machine and never written into the repository.");
                        }

                        Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                        return 0;
                    });
            }

            case "language":
            {
                var workspace = FlagValue(args, "--workspace");
                var clear = args.Contains("--clear");
                var words = Operands(args, ["--workspace"]);
                var first = words.ElementAtOrDefault(1);
                var second = words.ElementAtOrDefault(2);
                var name = workspace ?? first;
                var spelled = workspace is not null ? first : second;
                if (string.IsNullOrEmpty(name) || (!clear && string.IsNullOrEmpty(spelled)))
                {
                    throw new ChoiceWords("`driver language` needs <repository>|--workspace <name>, then en|zh|--clear — "
                        + "e.g. `daoris place language aurora-engine zh`.");
                }

                var code = clear ? null : SessionLanguages.Code(spelled);
                if (!clear && code is null) throw new ChoiceWords(SessionLanguages.Refusal(spelled!));
                var map = workspace is not null ? before.WorkspaceLanguages : before.Languages;
                // The name's spelling first written, when it has one in another case: one entry, never two.
                var key = FindName(map.Keys, name) ?? name;
                var payload = Scoped(workspace, name);
                payload["language"] = code;
                return Change(ChoiceRoutes.Language, payload,
                    config => workspace is not null ? config.WithWorkspaceLanguage(workspace, code) : config.WithLanguage(name, code), () =>
                    {
                        if (code is null)
                        {
                            Say(workspace is not null
                                ? $"daoris: repositories in the workspace `{key}` keep their own session language, else none."
                                : $"daoris: `{key}` takes its workspace's session language again, else none.");
                        }
                        else
                        {
                            Say(workspace is not null
                                ? $"daoris: sessions in each repository in the workspace `{key}` that sets none of its own write to you in "
                                  + $"{SessionLanguages.NameOf(code)}: their questions, closing notes, decline reasons and last words."
                                : $"daoris: sessions in `{key}` write to you in {SessionLanguages.NameOf(code)}: their questions, closing notes, "
                                  + "decline reasons and last words.");
                            Say("  One line in each instruction asks it; code, commands and anything quoted stay as written. The window's own");
                            Say("  language is Settings → Appearance, and neither sets the other.");
                            if (workspace is not null)
                            {
                                Say("  A repository with a language of its own keeps it — `daoris place language <repository> --clear` hands it back.");
                            }
                        }

                        Say("  A session already running keeps what it was handed.");
                        Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                        return 0;
                    });
            }

            default:
                return Registered(world, verb)?.Plan(args, before, world)
                    ?? throw new ChoiceWords($"`{verb}` is not a standing choice this program changes.");
        }
    }

    private static int List(DriverConfig choices, DriverChoicesWorld world) => DriverChoicesList.List(choices, world);

    /// <summary><c>across</c>: whether a checkout is read by agents outside it, and the writes declared into another.</summary>
    private static ChoicePress Across(IReadOnlyList<string> args, DriverChoicesWorld world) => DriverChoicesAcross.Across(args, world);

    /// <summary>Whether the file sets a key, read where it holds a whole number, as the CLI reads a choice set rather than defaulted.</summary>
    private static bool Written(DriverConfig choices, string key)
    {
        if (choices.Source is not { } source) return false;
        using var document = JsonDocument.Parse(source);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsInteger(number);
    }
}
