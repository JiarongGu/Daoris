using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>The checkouts the service's registry names, or why it could not be read: what a review's procedure is looked for in.</summary>
public sealed record ChoicesRegistry(IReadOnlyList<(string Repository, string? Workspace, string? Root)>? Checkouts, string? Unread);

/// <summary>
/// Where <c>daoris-driver driver</c> acts (PUSH1f2a): the terminal it says its words on and finds the running Daoris from, the
/// <c>driver.json</c> it reads and writes, and the facts a choice judges by when it acts alone: the registry's checkouts a review's
/// procedure is looked for in, and the planner's verdicts *Try again* judges from. A test hands its own of each.
/// </summary>
public sealed record DriverChoicesWorld
{
    /// <summary>The terminal: its home, its variables (read here, never the process's), its streams, and where it finds the instance.</summary>
    public required InstanceWorld Instance { get; init; }

    /// <summary>The <c>driver.json</c> the choices are read from and written to.</summary>
    public required string ConfigPath { get; init; }

    /// <summary>The registry's checkouts, or why they could not be read.</summary>
    public Func<CancellationToken, Task<ChoicesRegistry>> Registry { get; init; } =
        _ => Task.FromResult(new ChoicesRegistry(null, "this command was handed no way to read it"));

    /// <summary>The moment a standing answer is set, and a workflow's version saved.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// The folder the terminal is in (PUSH1f2b): where a file a verb names by a relative path is read and written, a cycle's, a
    /// workflow's to apply or import, and an export's.
    /// </summary>
    public string Folder { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// The size of the Daoris folder's volume in bytes (KEEP1f), which a floor's size is judged against; the system's read of the home's
    /// folder by default, a test's stand-in otherwise. Null where it does not read.
    /// </summary>
    public Func<long?>? FloorVolume { get; init; }

    /// <summary>
    /// The verbs the subdomains above the settings own (<see cref="ChoiceVerb"/>), wired by Loop (<c>DriverChoiceVerbs</c>); a verb's word
    /// no verb here carries is no verb of the world's.
    /// </summary>
    public IReadOnlyList<ChoiceVerb> Verbs { get; init; } = [];
}

/// <summary>
/// One standing choice's press (PUSH1f2a, D194 points 4 and 6): the route the window's press sends and the payload the verb writes from
/// its words, the operation it calls alone, the press's own fields read from the route's answer, and the one function that says it
/// from those fields on both paths. <see cref="DriverChoicesCommand.Plan"/> makes it; a test sends its payload through the route.
/// </summary>
/// <param name="Route">The route, or null where none takes the change and the verb acts alone even beside a running Daoris.</param>
/// <param name="Act">The operation, alone: it writes, and answers the press's own fields (or null where it has none).</param>
/// <param name="Fields">The press's own fields, read from the route's answer.</param>
/// <param name="Words">The verb's words from those fields, both paths', returning its exit code.</param>
public sealed record ChoicePress(
    string Verb, InstanceRoute? Route, JsonObject? Payload, Func<CancellationToken, Task<object?>> Act, Func<JsonElement, object?> Fields,
    Func<object?, int> Words);

/// <summary>A refusal the command makes before it acts, in its words, with today's exit: its grammar and its own checks.</summary>
public sealed class ChoiceWords(string message, int exit = 2) : Exception(message)
{
    public int Exit { get; } = exit;
}

/// <summary>
/// The routes <c>daoris-driver driver</c> sends while Daoris runs (PUSH1f2a; RPC1f, the terminal RPC design §9), each the window twin of
/// the choice it edits, and the verbs no route takes, each with why it acts alone (D171's PUSH1f note, kept by D194 point 5): the CLI's
/// <c>DRIVER_VERBS</c>, now this program's. Every one is the person's (D171's RPC1a note). Held to the census by <c>PipeCensusTests</c>.
/// </summary>
public static class ChoiceRoutes
{
    private static InstanceRoute Person(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Person);

    public static InstanceRoute Drivable { get; } = Person("SET_DRIVABLE");
    public static InstanceRoute Hold { get; } = Person("SET_HOLD");
    public static InstanceRoute Trees { get; } = Person("SET_TREES");
    public static InstanceRoute Notify { get; } = Person("SET_NOTIFY");
    public static InstanceRoute Strikes { get; } = Person("SET_STRIKES");
    public static InstanceRoute CoolOff { get; } = Person("SET_COOLOFF");
    public static InstanceRoute Cap { get; } = Person("SET_CAP");
    public static InstanceRoute Retry { get; } = Person("RETRY_QUEST");
    public static InstanceRoute Line { get; } = Person("SET_LINE");
    public static InstanceRoute Language { get; } = Person("SET_LANGUAGE");
    public static InstanceRoute Landing { get; } = Person("SET_LANDING");
    public static InstanceRoute ReadAcross { get; } = Person("SET_READ_ACROSS");
    public static InstanceRoute WriteAcross { get; } = Person("SET_WRITE_ACROSS");
    public static InstanceRoute Standing { get; } = Person("SET_STANDING");
    public static InstanceRoute Review { get; } = Person("SET_REVIEW");
    public static InstanceRoute Opinion { get; } = Person("SET_OPINION");
    public static InstanceRoute Helpers { get; } = Person("SET_HELPERS");
    public static InstanceRoute HandOver { get; } = Person("SET_HAND_OVER");
    public static InstanceRoute Commits { get; } = Person("SET_COMMITS");
    public static InstanceRoute Keep { get; } = Person("SET_KEEP");
    public static InstanceRoute Actor { get; } = Person("SET_ACTOR");

    /// <summary>Each verb that changes the home, in the verb's words: the route it sends while Daoris runs, or null with why it acts alone.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("driver drive|undrive <repo>", Drivable, "the window's drive switch"),
        ("driver hold|resume <repo>", Hold, "the window's hold"),
        ("driver trees <repo> on|off", Trees, "the window's own-tree switch"),
        ("driver notify on|off", Notify, "the window's notify switch"),
        ("driver strikes <n>", Strikes, "the window's strike limit"),
        ("driver cooloff <minutes>", CoolOff, "the window's cool-off"),
        ("driver cap <n>", Cap, "the window's session cap"),
        ("driver intake <adapter>|off", Actor, "Intake's order, one choice at account default or off (D205 §6.4)"),
        ("driver helper <adapter>|off", Actor, "Ask Daoris's order, one choice at account default or off (D205 §6.4)"),
        ("driver retry <quest>", Retry, "the quest's *Try again*, which judges from the planner's verdict whether to mark or release it"),
        ("driver line", Line, "the repository's or the workspace's line"),
        ("driver language", Language, "the session language"),
        ("driver landing", Landing, "the landing rule, sent with its key and its squash named so the rule is replaced whole, as the verb replaces it"),
        ("driver across … read", ReadAcross, "reading across"),
        ("driver across … write-to", WriteAcross, "writing across"),
        ("driver standing", Standing, "the standing answer"),
        ("driver review", Review, "the review rule; its procedure is looked for by the instance in the registry's checkouts"),
        ("driver opinion", Opinion, "the second-opinion rule"),
        ("driver helpers", Helpers, "the helpers rule"),
        ("driver handover", HandOver, "the hand-over rule"),
        ("driver commits", Commits, "the commit rule"),
        ("driver keep <kind> …", Keep, "the Disk space card's rule of that kind"),
        ("driver actor <actor> …", Actor, "an actor's order (D205 §9.4)"),
        ("driver adapter <name>", Actor, "Build's order, one choice at account default (D205 §6.4)"),
        ("driver timeout <minutes>", null, "alone: no route sets a session's timeout"),
        ("driver retry <quest> --at <n>", null, "alone: RETRY_QUEST marks at the failures its verdict counts, never at a number the person gives"),
        ("driver retry <quest> --session <id>", null, "alone: RETRY_QUEST releases the stop its verdict names, never a session the person names"),
        ("driver landing … --cycle <file>|--no-cycle", null, "alone: SET_LANDING has no field for the cycle, and keeps the one the rule holds"),
        ("driver work-cycle", null, "alone: no route sets a quest's or an ask's cycle of work"),
        ("driver brainstorms", null, "alone: no route sets the brainstorm policy"),
        ("driver workflow new|edit|apply|import|use|kind", null,
            "alone: no route writes a named workflow or chooses one; the window's WORKFLOW_LANDED reads the tip from its own registry"),
    ];
}

/// <summary>
/// <c>daoris-driver driver</c> (PUSH1f2a, PUSH1f2b, D194, the alone-path design §1.2): the driver family from a terminal, the words
/// <c>daoris driver …</c> hands this program, every one of them since PUSH1f2b. Each change is its route through
/// <see cref="InstanceDoor.PressAsync"/> where the running Daoris answers, confirmed in its window; else the choice's own operation, on
/// the terminal's word (D50), refused from a session's terminal (D194 point 5); and alone, holding nothing and sending nothing, on a
/// file <c>DAORIS_DRIVER_CONFIG</c> names. A change no route takes acts alone even beside a running Daoris (<see cref="ChoiceRoutes"/>):
/// a landing's cycle, <c>work-cycle</c>, <c>brainstorms</c> and the workflows' writes (<see cref="DriverWorkflowsCommand"/>). Its words
/// are one function per verb on both paths, today's text kept (<c>fixtures/driver-choices-text.json</c>,
/// <c>fixtures/driver-workflows-text.json</c>).
/// </summary>
/// <remarks>
/// <para>Exit codes are the CLI's, case by case: 0 said or set · 1 the person's refusal in the window, its two minutes' end, or a
/// session's terminal · 2 the usage, a refusal of the words, the operation or the route.</para>
/// </remarks>
public static class DriverChoicesCommand
{
    /// <summary>The verbs this program has, each the CLI hands on (the CLI's allow-list is held to it).</summary>
    public static IReadOnlyList<string> Verbs { get; } =
    [
        "list", "drive", "undrive", "hold", "resume", "trees", "notify", "cap", "strikes", "retry", "timeout", "cooloff", "adapter", "helper",
        "intake", "line", "landing", "across", "standing", "language", "review", "opinion", "helpers", "handover", "commits", "work-cycle",
        "brainstorms", "workflow", "keep", "actor",
    ];

    public const string Usage =
        """
        usage: daoris-driver driver [list]
               daoris-driver driver drive|undrive|hold|resume <repository>
               daoris-driver driver trees <repository> on|off · notify on|off
               daoris-driver driver cap <n> · strikes <n> · timeout <minutes> · cooloff <minutes> · adapter <name>
               daoris-driver driver intake <adapter>|off · helper <adapter>|off
               daoris-driver driver retry <quest> [--at <n> | --session <id>]
               daoris-driver driver line <repository>|--workspace <name> <branch>|--clear
               daoris-driver driver landing <repository>|--workspace <name> merge|branch <pattern>|--clear
                      [--tidy] [--plugin <id>] [--auto-accept|--no-auto-accept] [--key <shape>|--no-key] [--squash|--no-squash]
                      [--cycle <file>|--no-cycle]
               daoris-driver driver work-cycle <repository>|--workspace <name> quest|ask <file>|--clear
               daoris-driver driver across <repository>|--workspace <name> read on|off|--clear
               daoris-driver driver across <repository> write-to <other> [--clear]
               daoris-driver driver standing <repository> "…"|--clear
               daoris-driver driver language <repository>|--workspace <name> en|zh|--clear
               daoris-driver driver review <repository>|--workspace <name> <environment> --kind local|deployed --procedure <path>
                      [--address <url>] [--run "…"] [--required|--not-required]; none, --drop <environment>, or --clear
               daoris-driver driver opinion <repository>|--workspace <name> --reviewers <adapter,adapter> [--on landing,steps]
                      [--required|--not-required] [--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]; none, or --clear
               daoris-driver driver helpers <repository>|--workspace <name> [--mode ask|on|off] [--agents <a,b>] | --clear
               daoris-driver driver handover <repository>|--workspace <name> --mode ask|on|off | --clear
               daoris-driver driver commits <repository>|--workspace <name> [--mode as-you-go|ask|at-end]
                      [--convention "…"|--no-convention] | --clear
               daoris-driver driver brainstorms [--workspace <name>] [--mode ask|off] [--agents <a,b>] [--pass-minutes <n>]
                      [--synthesis-minutes <n>] [--challenge-minutes <n>] [--request-minutes <n>] | --clear
               daoris-driver driver workflow list · show <id>[@<version>] · show --repository <name>|--workspace <name> [--kind <kind>]
               daoris-driver driver workflow new <id> --from <preset>|current:<repository>|<id>[@<version>]
                      [--with-opinion] [--with-look] [--name "…"]
               daoris-driver driver workflow edit <id> [--base <version>] <change>… · apply <id> <file>|--as-graph [--base <version>]
               daoris-driver driver workflow export <id> [--to <file>] · import <file>
               daoris-driver driver workflow use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>|--landed]
               daoris-driver driver workflow kind <workspace> <kind> --label "…" [--paths <path,…>] | --drop
                      (a workflow's writes say their change, and with --plan save nothing)
               daoris-driver driver keep [set-aside <days>|whole|slim|default · downloads <days>|never|default · logs <days>|default
                      · history <days>|never|default · floor <n>GB|<n>%|off|default]
               daoris-driver driver actor [<actor>] · actor <actor> "<agent> [<model>|-] [<effort>|-]" … · actor <actor> --clear
                      · actor intake|ask-daoris off
        `daoris driver …` runs these.
        Each change takes --wait <minutes> (0 to 30, the default 30) or --no-wait: how long to wait on its Confirm in Daoris's window.
        """;

    /// <summary>The marker a shell Daoris started carries (D194 point 5): its session's record id.</summary>
    public const string SessionVariable = ChildMarks.SessionVariable;

    /// <summary>
    /// The refusal of a person's press acting in this process from a session's terminal (D194 point 5, the alone-path design §2): advice,
    /// since its absence grants nothing; an agent's honest attempt is not passed as the person's.
    /// </summary>
    public const string SessionsTerminal =
        "this change is the person's, and this terminal is a session's (`DAORIS_SESSION_ID`): make it in Daoris's window, through Ask "
        + "Daoris's proposal and its Apply, or from your own terminal; nothing was done.";

    /// <summary>
    /// What the retired <c>timeout</c> verb, Ask Daoris's <c>timeout</c> door and the service's <c>settings_timeout</c> answer (SESSWATCH1a,
    /// D200, the session-watch design §3): sessions are no longer limited by time. The service's twin says the same words.
    /// </summary>
    public const string TimeoutRetired =
        "sessions are no longer limited by time: a working session runs as long as it works, and Daoris looks in on one that has gone "
        + "quiet and brings it to you; nothing was changed.";

    internal const string Prefix = "daoris";

    /// <summary>
    /// Whether the words change the home: a verb this program has that is not <c>list</c>, and no plan or usage asked; <c>brainstorms</c>
    /// only where it sets or clears, and a workflow's only where it writes (PUSH1f2b).
    /// </summary>
    public static bool Changes(IReadOnlyList<string> args)
    {
        if (args.Any(arg => arg is "--help" or "-h" or "--plan")) return false;
        var verb = args.Count > 0 ? args[0] : "list";
        return verb switch
        {
            "list" => false,
            "workflow" => ChoiceGrammar.WorkflowChanges([.. args.Skip(1)]),
            "brainstorms" => args.Any(arg => arg is "--clear" or "--mode" or "--agents" || ChoiceGrammar.BrainstormMinuteFlags.Contains(arg)),
            // `keep` naming no kind says each rule, a read (KEEP1d).
            "keep" => args.Count > 1,
            // `actor` naming one actor, or none, says its order, a read (MODELROLE1c).
            "actor" => args.Count > 2,
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
            await error.WriteLineAsync($"daoris-driver driver: `{verb}` is not a verb of this program's.").ConfigureAwait(false);
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
        if (Registered(world, verb)?.RunAsync(args, before, world, ct) is { } running) return await running.ConfigureAwait(false);

        ChoicePress press;
        try
        {
            if (verb == "keep" && !Changes(args)) return KeepsSaid(before, world);
            if (!Changes(args) && Registered(world, verb)?.Said(args, before, world) is { } exit) return exit;
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
                // One line on both paths (D194 point 6): the route's refusal is `{verb}: {sentence} ({code})`, and so is the operation's.
                await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
                return 2;
            }

            return press.Words(fields);
        }

        // A file DAORIS_DRIVER_CONFIG names is no instance's (D194 point 2): written alone, and nothing sent.
        if (press.Route is null || DriverConfig.Overridden(world.Instance.Environment)) return await Alone().ConfigureAwait(false);
        return await InstanceDoor.PressAsync(press.Route, press.Payload, Alone, answer => press.Words(press.Fields(answer)), world.Instance, ct)
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

            case "trees":
            {
                var repository = Named(args, verb);
                var direction = args.Skip(1).FirstOrDefault(token => token is "on" or "off")
                    ?? throw new ChoiceWords("`driver trees` needs on|off — e.g. `daoris driver trees aurora-engine on`.");
                var on = direction == "on";
                return Change(ChoiceRoutes.Trees, new JsonObject { ["repository"] = repository, ["ownTree"] = on },
                    config => config.WithTrees(repository, on), () =>
                    {
                        Say(on ? $"daoris: sessions in `{repository}` open their own worktree (D51)." : $"daoris: sessions in `{repository}` run in its registered root again.");
                        if (on)
                        {
                            // TREEDEPS1: npm's dependencies are copied in where the tree's lockfile matches the checkout's.
                            Say("  A fresh tree holds nothing git does not track but the npm dependencies copied");
                            Say("  from your checkout where the tree's lockfile matches — no build outputs, no other");
                            Say("  installs — so a session pays the rest of that repository's own setup cost per tree.");
                            Say("  In exchange, your uncommitted work in the root no longer holds the driver.");
                        }

                        Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                        return 0;
                    });
            }

            case "notify":
            {
                var direction = args.Skip(1).FirstOrDefault(token => token is "on" or "off")
                    ?? throw new ChoiceWords("`driver notify` needs on|off — e.g. `daoris driver notify off`.");
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
                    throw new ChoiceWords("`driver cap` needs a whole number of concurrent sessions, at least 1 — e.g. `daoris driver cap 3`.");
                }

                var cap = (int)value;
                return Change(ChoiceRoutes.Cap, new JsonObject { ["cap"] = cap }, config => config.WithCap(cap), () =>
                {
                    Say($"daoris: this machine runs at most {cap} session(s) at once.");
                    return 0;
                });
            }

            case "strikes":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || value < 0 || value > int.MaxValue)
                {
                    throw new ChoiceWords("`driver strikes` needs a whole number of failed sessions, 0 or more — "
                        + "e.g. `daoris driver strikes 3`, or `0` to never park a quest.");
                }

                var strikes = (int)value;
                return Change(ChoiceRoutes.Strikes, new JsonObject { ["strikes"] = strikes }, config => config.WithStrikes(strikes), () =>
                {
                    Say(strikes == 0
                        ? "daoris: this machine never parks a quest — it keeps trying, as it did before this setting."
                        : $"daoris: a quest is parked after {strikes} failed session(s), and waits for you.");
                    if (strikes == 0)
                    {
                        Say("  🔴 A quest that fails for a reason no retry can fix will be started again every tick,");
                        Say("     and every start spends a real login. That is the behaviour this setting exists for.");
                    }
                    else
                    {
                        Say("  A failure is a session that ended without landing anything. A stand-down, a decline,");
                        Say("  a stop and a held repository are not failures and never count toward this.");
                    }

                    Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                    return 0;
                });
            }

            // SESSWATCH1a (D200, the session-watch design §3): retired, changing nothing, and saying why.
            case "timeout":
                throw new ChoiceWords(TimeoutRetired, exit: 1);

            case "cooloff":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || value < 1 || value > int.MaxValue)
                {
                    throw new ChoiceWords("`driver cooloff` needs a whole number of minutes, at least 1 — e.g. `daoris driver cooloff 60`; a zero "
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

            case "line":
            {
                var workspace = FlagValue(args, "--workspace");
                var clear = args.Contains("--clear");
                var words = Operands(args, ["--workspace"]);
                var first = words.ElementAtOrDefault(1);
                var second = words.ElementAtOrDefault(2);
                var name = workspace ?? first;
                var branch = workspace is not null ? first : second;
                if (string.IsNullOrEmpty(name) || (!clear && string.IsNullOrEmpty(branch)))
                {
                    throw new ChoiceWords("`driver line` needs <repository>|--workspace <name>, then <branch>|--clear — "
                        + "e.g. `daoris driver line aurora-engine develop`.");
                }

                if (!clear && !BranchName.IsValid(branch!)) throw new ChoiceWords($"`{branch}` is not a branch name git would take.");
                var set = clear ? null : branch;
                var payload = Scoped(workspace, name);
                payload["branch"] = set;
                return Change(ChoiceRoutes.Line, payload,
                    config => workspace is not null ? config.WithWorkspaceLine(workspace, set) : config.WithLine(name, set), () =>
                    {
                        var whose = workspace is not null ? $"repositories in the workspace `{workspace}`" : $"`{name}`";
                        Say(set is null
                            ? $"daoris: {whose} take{(workspace is not null ? "" : "s")} {(workspace is not null ? "their" : "its")} line from "
                              + $"{(workspace is not null ? "their own setting or " : "its workspace, else ")}the checkout again."
                            : $"daoris: work in {whose} grows from `{set}` and lands on it.");
                        if (set is not null && workspace is not null)
                        {
                            Say("  A repository with a line of its own keeps it — `daoris driver line <repository> --clear` hands it back.");
                        }

                        Say("  A session already running keeps the line it started from.");
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
                        + "`daoris driver standing work-app \"dev writes allowed; test locally against dev; prod only on a yes\"`.");
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
                        + "e.g. `daoris driver language aurora-engine zh`.");
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
                                Say("  A repository with a language of its own keeps it — `daoris driver language <repository> --clear` hands it back.");
                            }
                        }

                        Say("  A session already running keeps what it was handed.");
                        Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                        return 0;
                    });
            }

            case "commits":
                return Commits(args, before, world);

            // The person's keep rules (KEEP1d, D195 point 2; the cleanup design §2.2): one kind's days, `whole` or `slim`, `never`, or its
            // default again. Judged here by the one judgement the window's SET_KEEP makes, so a value out of its bounds is refused before
            // the window is asked, in the line both paths say (`… (KEEP_OUT_OF_RANGE)`).
            case "keep":
            {
                var set = KeepAsked(args, world);
                DriverConfig after;
                try
                {
                    after = KeepRules.Apply(before, set);
                }
                catch (DriverException refused)
                {
                    throw new ChoiceWords(refused is ChoiceRefusal ? $"{refused.Message} ({ChoiceCodes.Of(refused)})" : refused.Message);
                }

                var payload = new JsonObject { ["kind"] = set.Kind };
                if (set.Days is { } days) payload["days"] = days;
                if (set.Never) payload["never"] = true;
                if (set.Whole is { } whole) payload["whole"] = whole;
                if (set.Default) payload["default"] = true;
                // KEEP1f: the floor's size, share or `off`; the window judges a size against its own read of the volume.
                if (set.Gb is { } gb) payload["gb"] = gb;
                if (set.Percent is { } percent) payload["percent"] = percent;
                if (set.Off) payload["off"] = true;
                return Change(ChoiceRoutes.Keep, payload, config => KeepRules.Apply(config, set), () =>
                {
                    Say($"daoris: {KeepRules.Said(set.Kind, after.Keep)}.");
                    Say($"  Written to {path} — the look's next pass keeps by it, so nothing restarts.");
                    return 0;
                });
            }

            default:
                return Registered(world, verb)?.Plan(args, before, world)
                    ?? throw new ChoiceWords($"`{verb}` is not a standing choice this program changes.");
        }
    }

    /// <summary><c>keep &lt;kind&gt; &lt;value&gt;</c>'s words read as the rule they set (KEEP1d); anything else is its usage.</summary>
    private static KeepSet KeepAsked(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        const string Usage = "`driver keep` needs a kind and its rule — `set-aside <days>|whole|slim|default`, `downloads <days>|never|default`, "
            + "`logs <days>|default`, `history <days>|never|default` or `floor <n>GB|<n>%|off|default`; e.g. `daoris driver keep set-aside 30`.";
        if (args.Count != 3) throw new ChoiceWords(Usage);
        try
        {
            if (!KeepRules.IsFloor(args[1])) KeepRules.Kind(args[1]);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords($"{refused.Message} {Usage}");
        }

        try
        {
            var set = KeepRules.Asked(args[1], args[2]);
            if (!KeepRules.IsFloor(set.Kind) || set.Gb is null) return set;
            // KEEP1f: a size is at most half the Daoris folder's volume, read here so the refusal comes before the window is asked.
            var volume = world.FloorVolume is { } read
                ? read()
                : world.Instance.Home is { } home ? SpaceVolumes.Read(DaorisHome.FolderOf(home).Folder)?.Total : null;
            return set with { Volume = volume };
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused.Message);
        }
    }

    /// <summary><c>keep</c>: each kind's rule the person sets, and whether it is the default (KEEP1d).</summary>
    private static int KeepsSaid(DriverConfig choices, DriverChoicesWorld world)
    {
        var write = world.Instance.Out;
        write.WriteLine($"daoris: {world.ConfigPath}");
        foreach (var kind in KeepRules.Settable)
        {
            var said = KeepRules.Said(kind.Id, choices.Keep);
            write.WriteLine(choices.Keep.IsDefault(kind.Id)
                ? $"  {kind.Id,-10} {said}  (the default — `daoris driver keep {kind.Id} …` changes it)"
                : $"  {kind.Id,-10} {said}  (set — `daoris driver keep {kind.Id} default` takes the default again)");
        }

        // KEEP1f: the free-space floor, set beside the kinds' rules.
        var floor = KeepRules.Said(KeepRules.Floor, choices.Keep);
        write.WriteLine(choices.Keep.IsDefault(KeepRules.Floor)
            ? $"  {KeepRules.Floor,-10} {floor}  (the default — `daoris driver keep floor …` changes it)"
            : $"  {KeepRules.Floor,-10} {floor}  (set — `daoris driver keep floor default` takes the default again)");
        return 0;
    }

    /// <summary><c>list</c>: what this machine drives, as <c>daoris driver list</c> always said it.</summary>
    private static int List(DriverConfig choices, DriverChoicesWorld world)
    {
        var write = world.Instance.Out;
        void Say(string line) => write.WriteLine(line);
        void Section(string word) => Registered(world, word)?.Listed(choices, world);
        Say($"daoris: {world.ConfigPath}");
        Say($"  adapter    {choices.Adapter}");
        Say($"  cap        {choices.Cap} concurrent session(s)");
        Say($"  strikes    {(choices.Strikes == 0
            ? "off — a quest is never parked, and it keeps trying at the cost of a login each time"
            : $"{choices.Strikes} failed session(s) park a quest")}");
        foreach (var (quest, mark) in choices.Forgiven) Say($"  retried    #{quest}  (counting from {mark} failure(s))");
        foreach (var (quest, session) in choices.Released) Say($"  released   #{quest}  (your stop of session {session})");

        // PAUSE1a (D132 §2.5): each pause this machine keeps, said as paused, never held, with the door that resumes it.
        foreach (var (scope, pauses) in new[] { ("ask", choices.PausedAsks), ("quest", choices.PausedQuests) })
        {
            foreach (var (id, pause) in pauses)
            {
                var when = pause.At is { } at ? $"since {Moment(at)}" : "when is not recorded";
                var stops = pause.Stopped.Keys.OrderBy(quest => quest, StringComparer.Ordinal).Select(quest => $"#{quest}'s session {pause.Stopped[quest]}").ToList();
                var resume = scope == "ask" ? $"daoris-driver ask --resume {id}" : $"daoris-driver quest resume {id}";
                Say($"  paused     {scope} #{id}  ({when}{(stops.Count > 0 ? $"; its pause stopped {string.Join(", ", stops)}" : "")}) — resume: {resume}");
            }
        }

        // SESSWATCH1a: a value an older Daoris wrote is said once, as kept for it; nothing reads it for a bound.
        if (choices.KeptTimeoutMinutes is { } kept) Say($"  timeout    {kept}  (no longer limits a session; kept for an older Daoris)");
        Say(choices.CoolOffMinutes is { } cooloff
            ? $"  cooloff    {cooloff} minutes an account cools when its agent names no time for its limit"
            : $"  cooloff    {(int)CoolOffDefault.Value.TotalMinutes} minutes an account cools when its agent names no time for its limit  (the default — "
              + "`daoris driver cooloff <minutes>` changes it)");
        Say($"  notify     {(choices.Notify ? "on" : "off")}  (a session parking, or ending without you asking{(choices.Notify ? "" : " — not said")})");
        Section("helper");
        Section("intake");

        if (choices.Drivable.Count == 0)
        {
            Say("  drivable   nothing — this machine drives no repository, which is the default (D46 §2).");
            Say("             `daoris driver drive <repository>` opts one in.");
        }

        foreach (var repository in choices.Drivable)
        {
            var held = FindName(choices.Holds, repository) is not null;
            var trees = FindName(choices.Trees, repository) is not null;
            Say($"  drivable   {repository}{(held ? "  (held by you — `daoris driver resume` releases it)" : "")}{(trees ? "  (sessions open their own tree — D51)" : "")}");
        }

        foreach (var repository in choices.Trees.Where(each => FindName(choices.Drivable, each) is null))
        {
            Say($"  trees      {repository}  (sessions there open their own tree when anything spawns one)");
        }

        foreach (var (repository, branch) in choices.Lines) Say($"  line       {repository}  {branch}");
        foreach (var (workspace, branch) in choices.WorkspaceLines) Say($"  line       workspace {workspace}  {branch}  (for each repository there that sets none)");

        Section("landing");

        foreach (var (repository, read) in choices.ReadAcross) Say($"  across     {repository}  read {(read ? "on" : "off")}");
        foreach (var (workspace, read) in choices.WorkspaceReadAcross) Say($"  across     workspace {workspace}  read {(read ? "on" : "off")}  (for each repository there that sets none)");
        foreach (var (repository, targets) in choices.WriteAcross) Say($"  across     {repository}  writes into {string.Join(", ", targets)}  (declared by you)");

        foreach (var (repository, answer) in choices.Standing)
        {
            Say($"  standing   {repository}  \"{answer.Says}\"{(answer.At is { } at ? $"  (set {Moment(at)})" : "")}");
        }

        foreach (var (repository, code) in choices.Languages) Say($"  language   {repository}  {code} ({SessionLanguages.NameOf(code)})  (set for it)");
        foreach (var (workspace, code) in choices.WorkspaceLanguages) Say($"  language   workspace {workspace}  {code} ({SessionLanguages.NameOf(code)})  (for each repository there that sets none)");
        if (choices.Languages.Count == 0 && choices.WorkspaceLanguages.Count == 0)
        {
            Say("  language   none set — sessions are asked for no language; `daoris driver language <repository> en|zh` sets one");
        }

        Section("review");

        Section("opinion");

        Section("helpers");

        Section("handover");

        Section("brainstorms");

        foreach (var (repository, rule) in choices.Commits) Say($"  commits    {repository}  {CommitRules.Listed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceCommits) Say($"  commits    workspace {workspace}  {CommitRules.Listed(rule)}  (for each repository there that sets none)");
        if (choices.Commits.Count == 0 && choices.WorkspaceCommits.Count == 0)
        {
            Say("  commits    none set — work is committed as it goes; `daoris driver commits <repository> "
                + "--mode ask|at-end` sets one");
        }

        // A hold on something not opted in is inert, and saying so is the point: it reads as protection and is not.
        foreach (var held in choices.Holds.Where(each => FindName(choices.Drivable, each) is null))
        {
            Say($"  held       {held}  (not drivable anyway — the hold changes nothing)");
        }

        return 0;
    }

    /// <summary><c>across</c> (READ1, D107): whether a checkout is read by agents outside it, and the writes declared into another.</summary>
    private static ChoicePress Across(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var clear = args.Contains("--clear");
        var words = Operands(args, ["--workspace"]).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var what = words.ElementAtOrDefault(0);
        var value = words.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(name))
        {
            throw new ChoiceWords("`driver across` needs <repository>|--workspace <name>, then read on|off|--clear or write-to <other> "
                + "[--clear] — e.g. `daoris driver across engine read off`.");
        }

        if (what == "read")
        {
            if (!clear && value is not ("on" or "off"))
            {
                throw new ChoiceWords("`driver across … read` needs on|off|--clear — e.g. `daoris driver across engine read off`.");
            }

            bool? set = clear ? null : value == "on";
            var payload = Scoped(workspace, name);
            payload["read"] = set;
            return new("across", ChoiceRoutes.ReadAcross, payload, _ =>
            {
                StandingChoices.Write(path, config => workspace is not null ? config.WithWorkspaceReadAcross(workspace, set) : config.WithReadAcross(name, set));
                return Task.FromResult<object?>(null);
            }, _ => null, _ =>
            {
                var whose = workspace is not null ? $"each checkout in the workspace `{workspace}` that sets none of its own is" : $"`{name}`'s checkout is";
                if (set is null)
                {
                    Say(workspace is not null
                        ? $"daoris: checkouts in the workspace `{workspace}` are read across again, unless one says otherwise."
                        : $"daoris: `{name}` takes its workspace's reading again, else on.");
                }
                else if (set == true)
                {
                    Say($"daoris: {whose} read by sessions in its workspace's other repositories and by Ask Daoris —");
                    Say("  its files and `git status` and the branch list, never a write.");
                }
                else
                {
                    Say($"daoris: {whose} read by no agent outside it: no session in another repository, and not Ask Daoris.");
                }

                if (set is not null && workspace is not null)
                {
                    Say("  A repository with a setting of its own keeps it — `daoris driver across <repository> read --clear` hands it back.");
                }

                Say("  A session already running keeps what it started with.");
                Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                return 0;
            });
        }

        if (what == "write-to")
        {
            if (workspace is not null)
            {
                throw new ChoiceWords("a relationship is declared from one repository — `daoris driver across <repository> write-to <other>`.");
            }

            if (AcrossDeclarations.Problem(name, value ?? "") is { } problem) throw new ChoiceWords(problem);
            return new("across", ChoiceRoutes.WriteAcross, new JsonObject { ["repository"] = name, ["to"] = value, ["allow"] = !clear }, _ =>
            {
                StandingChoices.Write(path, config => config.WithWriteAcross(name, value!, !clear));
                return Task.FromResult<object?>(null);
            }, _ => null, _ =>
            {
                if (clear)
                {
                    Say($"daoris: sessions in `{name}` no longer write into `{value}`; a change needed there is a quest again.");
                }
                else
                {
                    Say($"daoris: sessions in `{name}` may also write into `{value}` — its files, and a commit there —");
                    Say("  where both are in one workspace with a checkout here. This is your standing say-so for writing");
                    Say($"  across; it has one direction, so `{value}` writes nothing into `{name}` unless you declare that too.");
                    Say($"  `daoris driver across {name} write-to {value} --clear` takes it back.");
                }

                Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                return 0;
            });
        }

        throw new ChoiceWords($"`driver across` sets read or write-to, not `{what ?? ""}` — e.g. `daoris driver across engine read off`, "
            + "`daoris driver across plugins write-to engine`.");
    }

    /// <summary><c>commits</c> (PROCESS1a): how work is committed here now.</summary>
    private static ChoicePress Commits(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--mode", "--convention"];
        const string usage = "`driver commits` needs <repository>|--workspace <name>, then --mode as-you-go|ask|at-end and/or "
            + "--convention \"<your words>\" (one paragraph, at most 400 characters) or --no-convention, or --clear — "
            + "e.g. `daoris driver commits web-app --mode ask --convention \"Imperative subject line.\"`.";
        Flags(args, valued, ["--clear", "--no-convention"], "commits", usage);
        var workspace = FlagValue(args, "--workspace");
        var name = workspace ?? Operands(args, valued).Skip(1).FirstOrDefault();
        var clear = args.Contains("--clear");
        var mode = FlagValue(args, "--mode");
        // `--no-convention` is the empty convention, which takes the standing one away (a flag cannot carry an empty value).
        if (args.Contains("--no-convention") && args.Contains("--convention")) throw new ChoiceWords(usage);
        var convention = args.Contains("--no-convention") ? "" : FlagValue(args, "--convention");
        var set = new JsonObject();
        if (mode is not null) set["mode"] = mode;
        if (convention is not null) set["convention"] = convention;
        if (string.IsNullOrEmpty(name) || (!clear && set.Count == 0)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (set.Count > 0) payload["set"] = set;
        if (clear) payload["clear"] = true;
        var edit = CommitRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => CommitRules.Apply(before, edit));
        return new("commits", ChoiceRoutes.Commits, payload, _ =>
        {
            StandingChoices.Write(path, config => CommitRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceCommits : after.Commits;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceCommits : before.Commits).Keys, name) ?? name;
            if (map.GetValueOrDefault(key) is not { } rule)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own commit rule, else work is committed as it goes."
                    : $"daoris: `{key}` takes its workspace's commit rule again, else work is committed as it goes.");
            }
            else
            {
                Say(workspace is not null
                    ? $"daoris: commits for repositories in the workspace `{key}` that set none of their own: {CommitRules.Listed(rule)}"
                    : $"daoris: commits for `{key}`: {CommitRules.Listed(rule)}");
                Say($"  {CommitRules.Says(rule.Mode)}");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>Whether the file sets a key, read where it holds a whole number, as the CLI reads a choice set rather than defaulted.</summary>
    private static bool Written(DriverConfig choices, string key)
    {
        if (choices.Source is not { } source) return false;
        using var document = JsonDocument.Parse(source);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsInteger(number);
    }
}
