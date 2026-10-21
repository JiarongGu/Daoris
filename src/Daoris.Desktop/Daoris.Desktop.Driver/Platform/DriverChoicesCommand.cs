using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    /// <summary>The planner's verdict on each quest, over a fresh snapshot of the service this machine's driver watches.</summary>
    public Func<DriverConfig, CancellationToken, Task<IReadOnlyList<Consideration>>> Verdicts { get; init; } =
        (_, _) => throw new DriverException("this command was handed no way to read them");

    /// <summary>The moment a standing answer is set, and a workflow's version saved.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// The folder the terminal is in (PUSH1f2b): where a file a verb names by a relative path is read and written, a cycle's, a
    /// workflow's to apply or import, and an export's.
    /// </summary>
    public string Folder { get; init; } = Directory.GetCurrentDirectory();
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
    public static InstanceRoute Intake { get; } = Person("SET_INTAKE");
    public static InstanceRoute Helper { get; } = Person("SET_HELPER");
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
        ("driver intake <adapter>|off", Intake, "the window's intake agent"),
        ("driver helper <adapter>|off", Helper, "Ask Daoris's agent"),
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
        ("driver timeout <minutes>", null, "alone: no route sets a session's timeout"),
        ("driver adapter <name>", null, "alone: no route sets the working agent; the window names it from the roster"),
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
        "brainstorms", "workflow", "keep",
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
               daoris-driver driver keep [set-aside <days>|whole|slim|default · downloads <days>|never|default · logs <days>|default]
        `daoris driver …` runs these.
        """;

    /// <summary>The marker a shell Daoris started carries (D194 point 5): its session's record id.</summary>
    public const string SessionVariable = IntakeRoom.SessionVariable;

    /// <summary>
    /// The refusal of a person's press acting in this process from a session's terminal (D194 point 5, the alone-path design §2): advice,
    /// since its absence grants nothing; an agent's honest attempt is not passed as the person's.
    /// </summary>
    public const string SessionsTerminal =
        "this change is the person's, and this terminal is a session's (`DAORIS_SESSION_ID`): make it in Daoris's window, through Ask "
        + "Daoris's proposal and its Apply, or from your own terminal; nothing was done.";

    private const string Prefix = "daoris";

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
            "workflow" => DriverWorkflowsCommand.Changes([.. args.Skip(1)]),
            "brainstorms" => args.Any(arg => arg is "--clear" or "--mode" or "--agents" || MinuteFlags.Any(each => each.Flag == arg)),
            // `keep` naming no kind says each rule, a read (KEEP1d).
            "keep" => args.Count > 1,
            _ => Verbs.Contains(verb, StringComparer.Ordinal),
        };
    }

    /// <summary>The brainstorm policy's minutes, each by its flag and its name in the rule.</summary>
    private static readonly (string Flag, string Name)[] MinuteFlags =
        [("--pass-minutes", "pass"), ("--synthesis-minutes", "synthesis"), ("--challenge-minutes", "challenge"), ("--request-minutes", "request")];

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
        if (verb == "workflow") return await WorkflowAsync(args, before, world, ct).ConfigureAwait(false);

        ChoicePress press;
        try
        {
            // `brainstorms` naming no change says the policy, a read that holds nothing and asks no confirmation (BRAINSTORM1b).
            if (verb == "brainstorms" && !Changes(args)) return BrainstormsSaid(args, before, world);
            if (verb == "keep" && !Changes(args)) return KeepsSaid(before, world);
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

    /// <summary>
    /// <c>workflow …</c> (PUSH1f2b): a read said, and a write acted on the terminal's word, since no route takes one (D171's PUSH1f note),
    /// refused from a session's terminal before anything is read (D194 point 5). <see cref="DriverWorkflowsCommand"/> is the verb.
    /// </summary>
    private static async Task<int> WorkflowAsync(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct)
    {
        var error = world.Instance.Error;
        if (DriverWorkflowsCommand.Changes([.. args.Skip(1)]) && world.Instance.Environment(SessionVariable) is { Length: > 0 })
        {
            await error.WriteLineAsync($"{Prefix}: {SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        try
        {
            return await DriverWorkflowsCommand.RunAsync(args, before, world, ct).ConfigureAwait(false);
        }
        catch (ChoiceWords refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return refused.Exit;
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            await error.WriteLineAsync($"{Prefix}: {unreadable.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
            return 2;
        }
    }

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

            case "retry":
                return Retry(args, before, world);

            case "timeout":
            {
                var value = Number(Named(args, verb));
                if (!IsWhole(value) || value < 1 || value > int.MaxValue)
                {
                    throw new ChoiceWords("`driver timeout` needs a whole number of minutes, at least 1 — e.g. `daoris driver timeout 120`.");
                }

                var minutes = (int)value;
                return Change(null, null, config => config with { TimeoutMinutes = minutes }, () =>
                {
                    Say($"daoris: a session on this machine may run {minutes} minutes before the driver ends it.");
                    Say("  One that runs out is recorded as failed, and its quest is carried on in the same tree at a");
                    Say("  later tick, until the strikes park it.");
                    Say($"  Written to {path} — the driver re-reads it every tick; a session already running keeps its own.");
                    return 0;
                });
            }

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

            case "adapter":
            {
                var adapter = Named(args, verb);
                // Not refused against a list: the driver errors naming what exists at its start (D23), the side that knows.
                return Change(null, null, config => config with { Adapter = adapter }, () =>
                {
                    Say($"daoris: sessions on this machine spawn via `{adapter}`.");
                    if (!Managed(adapter)) Say("  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.");
                    return 0;
                });
            }

            case "helper":
            {
                var agent = args.Skip(1).FirstOrDefault(token => !token.StartsWith("--", StringComparison.Ordinal))
                    ?? throw new ChoiceWords("`driver helper` needs <adapter>|off — e.g. `daoris driver helper claude-code-acp`.");
                var off = agent == "off";
                return Change(ChoiceRoutes.Helper, new JsonObject { ["adapter"] = off ? null : agent }, config => config.WithHelper(off ? null : agent), () =>
                {
                    Say(off
                        ? "daoris: Ask Daoris runs no agent here — it offers its starters, each a door to the screen that fixes it."
                        : $"daoris: Ask Daoris runs on `{agent}`. It reads, and proposes; every change is yours to apply (D89).");
                    if (!off && !Managed(agent)) Say("  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.");
                    Say($"  Written to {path}.");
                    return 0;
                });
            }

            case "intake":
            {
                var named = args.Skip(1).FirstOrDefault(token => !token.StartsWith("--", StringComparison.Ordinal))
                    ?? throw new ChoiceWords("`driver intake` needs <adapter>|off — e.g. `daoris driver intake claude-code-acp`, or `off` "
                        + "to answer asks by declarations only.");
                var off = named == "off";
                return Change(ChoiceRoutes.Intake, new JsonObject { ["adapter"] = off ? null : named }, config => config.WithIntake(off ? null : named), () =>
                {
                    if (off)
                    {
                        Say("daoris: asks on this machine are answered by declarations only — proposed, never published unasked.");
                        Say("  An intake already waiting on you still waits; answering its ask ends it.");
                    }
                    else
                    {
                        Say($"daoris: an ask the declarations do not settle opens an intake session via `{named}`.");
                        Say("  It reads the workspace's declarations, publishes the quests itself, and asks you where they do not");
                        Say("  settle it. 🔴 Every intake spends a real login on that harness's account.");
                        if (!Managed(named)) Say("  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.");
                    }

                    Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
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

            case "landing":
                return Landing(args, before, world);

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

            case "review":
                return Review(args, before, world);

            case "opinion":
                return Opinion(args, before, world);

            case "helpers":
                return Helpers(args, before, world);

            case "handover":
                return HandOver(args, before, world);

            case "commits":
                return Commits(args, before, world);

            // A quest's or an ask's cycle of work (FABRIC-GRAPH1c; the fabric events design §D7's `workCycles`): a whole graph for one kind,
            // for a repository or with `--workspace` for each repository there naming none of that kind, judged as `WithWorkCycle` judges
            // it. A branch's cycle is its landing rule's (`landing … --cycle`). No route sets it, so it acts alone.
            case "work-cycle":
            {
                var workspace = FlagValue(args, "--workspace");
                var clear = args.Contains("--clear");
                var words = Operands(args, ["--workspace"]).Skip(1).ToList();
                var owner = workspace;
                if (owner is null && words.Count > 0)
                {
                    owner = words[0];
                    words.RemoveAt(0);
                }

                var kind = words.ElementAtOrDefault(0);
                var file = words.ElementAtOrDefault(1);
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(kind) || clear == (file is not null))
                {
                    throw new ChoiceWords("`driver work-cycle` needs <repository>|--workspace <name>, then quest|ask, then <file>|--clear — e.g. "
                        + "`daoris driver work-cycle aurora-engine quest cycle.json`, a whole graph its work of that kind follows to its end.");
                }

                if (kind == WorkUnitKind.Branch)
                {
                    throw new ChoiceWords("a branch's cycle is its landing rule's `cycle`, so that a repository's landing and its end move together (D164); "
                        + "no kind has two homes. `daoris driver landing <repository> … --cycle <file>` sets it.");
                }

                if (!NodeGraphHomes.WorkCycleKinds.Contains(kind))
                {
                    throw new ChoiceWords($"`{kind}` is no kind of work with a cycle of its own here — "
                        + $"{string.Join(" or ", NodeGraphHomes.WorkCycleKinds.Select(each => $"`{each}`"))}.");
                }

                string? cycle = null;
                if (file is not null)
                {
                    cycle = CycleFile(world, file);
                    if (NodeGraphs.Read(cycle).Problems.FirstOrDefault() is { } first)
                    {
                        throw new ChoiceWords($"the cycle in `{file}` does not hold: {first.Words} Nothing was written.");
                    }
                }

                return Change(null, null,
                    config => workspace is not null ? config.WithWorkspaceWorkCycle(workspace, kind, cycle) : config.WithWorkCycle(owner, kind, cycle), () =>
                    {
                        var unit = kind == WorkUnitKind.Ask ? "an ask" : $"a {kind}";
                        var where = workspace is not null ? $"each repository of the workspace `{workspace}` that names none of its own" : $"`{owner}`";
                        Say(cycle is not null
                            ? $"daoris: {unit} in {where} follows its cycle: {CycleSaid(cycle, "")}."
                            : $"daoris: {unit} in {where} takes {(workspace is not null ? "none" : "its workspace's cycle")} again{(workspace is not null ? "" : ", else none")}.");
                        Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
                        return 0;
                    });
            }

            // The brainstorm policy (BRAINSTORM1b, D176 point 9): this machine's, or with `--workspace` for the questions about a workspace's
            // repositories. `--mode`, `--agents` and the minutes set what they name over the rule set there; `--clear`. No route sets it.
            case "brainstorms":
            {
                var (workspace, clear, set) = BrainstormsAsked(args);
                var edit = new BrainstormEdit { Workspace = workspace, Set = set, Clear = clear };
                var after = Judged(() => BrainstormRules.Apply(before, edit));
                return Change(null, null, config => BrainstormRules.Apply(config, edit), () =>
                {
                    if (workspace is null)
                    {
                        Say(after.Brainstorms is { } machine
                            ? $"daoris: brainstorms on this machine: {BrainstormRules.Listed(machine)}."
                            : $"daoris: this machine's brainstorms take the default again: {BrainstormRules.Listed(BrainstormRule.Default)}.");
                    }
                    else
                    {
                        var key = FindName(after.WorkspaceBrainstorms.Keys, workspace) ?? FindName(before.WorkspaceBrainstorms.Keys, workspace) ?? workspace;
                        Say(AtName(after.WorkspaceBrainstorms, key) is { } rule
                            ? $"daoris: brainstorms for questions about repositories in the workspace `{key}`: {BrainstormRules.Listed(rule)}."
                            : $"daoris: questions about repositories in the workspace `{key}` take this machine's brainstorms rule again.");
                    }

                    Say($"  Written to {path}.");
                    return 0;
                });
            }

            // The person's keep rules (KEEP1d, D195 point 2; the cleanup design §2.2): one kind's days, `whole` or `slim`, `never`, or its
            // default again. Judged here by the one judgement the window's SET_KEEP makes, so a value out of its bounds is refused before
            // the window is asked, in the line both paths say (`… (KEEP_OUT_OF_RANGE)`).
            case "keep":
            {
                var set = KeepAsked(args);
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
                return Change(ChoiceRoutes.Keep, payload, config => KeepRules.Apply(config, set), () =>
                {
                    Say($"daoris: {KeepRules.Said(set.Kind, after.Keep)}.");
                    Say($"  Written to {path} — the look's next pass keeps by it, so nothing restarts.");
                    return 0;
                });
            }

            default:
                throw new ChoiceWords($"`{verb}` is not a standing choice this program changes.");
        }
    }

    /// <summary><c>keep &lt;kind&gt; &lt;value&gt;</c>'s words read as the rule they set (KEEP1d); anything else is its usage.</summary>
    private static KeepSet KeepAsked(IReadOnlyList<string> args)
    {
        const string Usage = "`driver keep` needs a kind and its rule — `set-aside <days>|whole|slim|default`, `downloads <days>|never|default`, "
            + "`logs <days>|default` or `history <days>|never|default`; e.g. `daoris driver keep set-aside 30`.";
        if (args.Count != 3) throw new ChoiceWords(Usage);
        try
        {
            KeepRules.Kind(args[1]);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords($"{refused.Message} {Usage}");
        }

        try
        {
            return KeepRules.Asked(args[1], args[2]);
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
        foreach (var kind in KeepChoices.Settable)
        {
            var said = KeepRules.Said(kind.Id, choices.Keep);
            write.WriteLine(choices.Keep.IsDefault(kind.Id)
                ? $"  {kind.Id,-10} {said}  (the default — `daoris driver keep {kind.Id} …` changes it)"
                : $"  {kind.Id,-10} {said}  (set — `daoris driver keep {kind.Id} default` takes the default again)");
        }

        return 0;
    }

    /// <summary><c>list</c>: what this machine drives, as <c>daoris driver list</c> always said it.</summary>
    private static int List(DriverConfig choices, DriverChoicesWorld world)
    {
        var write = world.Instance.Out;
        void Say(string line) => write.WriteLine(line);
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

        Say(Written(choices, "timeoutMinutes")
            ? $"  timeout    {choices.TimeoutMinutes} minutes a session may run"
            : $"  timeout    {DriverConfig.Empty.TimeoutMinutes} minutes a session may run  (the default — `daoris driver timeout <minutes>` changes it)");
        Say(choices.CoolOffMinutes is { } cooloff
            ? $"  cooloff    {cooloff} minutes an account cools when its agent names no time for its limit"
            : $"  cooloff    {(int)AccountLimits.DefaultCoolOff.TotalMinutes} minutes an account cools when its agent names no time for its limit  (the default — "
              + "`daoris driver cooloff <minutes>` changes it)");
        Say($"  notify     {(choices.Notify ? "on" : "off")}  (a session parking, or ending without you asking{(choices.Notify ? "" : " — not said")})");
        Say(choices.HelperAdapter is { } helper
            ? $"  helper     {helper}  ({(choices.HelpMode == HelpModes.Full
                ? "Ask Daoris, full access: it acts on this machine itself, with no asking"
                : "Ask Daoris, restricted: it proposes, and you apply")} — `daoris-driver help mode` says and sets it)"
            : "  helper     off — Ask Daoris offers its starters; `daoris driver helper <adapter>` names an agent");
        Say(choices.IntakeAdapter is { } intake
            ? $"  intake     {intake}  (an ask the declarations do not settle opens a session — a login each)"
            : "  intake     off — asks are answered by declarations only; `daoris driver intake <adapter>` names a harness");

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

        static string Spelled(LandingRule rule) =>
            (rule.Form == LandingForm.Branch ? $"branch {rule.Pattern}" : rule.Form)
            + (rule.Key is { Length: > 0 } key ? $", key {key}" : "")
            + (rule.Plugin is { Length: > 0 } plugin ? $", plugin {plugin}" : "") + (rule.Tidy ? ", tidy" : "")
            + (rule.AutoAccept ? ", accept automatically" : "") + (rule.Squash ? ", squash" : "");
        foreach (var (repository, rule) in choices.Landings) Say($"  landing    {repository}  {Spelled(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceLandings) Say($"  landing    workspace {workspace}  {Spelled(rule)}  (for each repository there that sets none)");

        // FABRIC-GRAPH1d: what the file keeps that no unit reads is said, never skipped without a word.
        foreach (var stray in NodeGraphHomes.Strays(choices)) Say($"  unread     {stray.Words}");

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

        foreach (var (repository, rule) in choices.Reviews) Say($"  review     {repository}  {ReviewListed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceReviews) Say($"  review     workspace {workspace}  {ReviewListed(rule)}  (for each repository there that sets none)");
        if (choices.Reviews.Count == 0 && choices.WorkspaceReviews.Count == 0)
        {
            Say("  review     none set — work is offered to land once its quest is done; `daoris driver review <repository> "
                + "<environment> --kind local|deployed --procedure <path>` declares one");
        }

        foreach (var (repository, rule) in choices.Opinions) Say($"  opinion    {repository}  {OpinionListed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceOpinions) Say($"  opinion    workspace {workspace}  {OpinionListed(rule)}  (for each repository there that sets none)");
        if (choices.Opinions.Count == 0 && choices.WorkspaceOpinions.Count == 0)
        {
            Say("  opinion    none set — no other agent reads work before it lands; `daoris driver opinion <repository> "
                + "--reviewers <adapter>` declares one");
        }

        foreach (var (repository, rule) in choices.Helpers) Say($"  helpers    {repository}  {HelperRules.Listed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceHelpers) Say($"  helpers    workspace {workspace}  {HelperRules.Listed(rule)}  (for each repository there that sets none)");
        if (choices.Helpers.Count == 0 && choices.WorkspaceHelpers.Count == 0)
        {
            Say("  helpers    none set — each helper an agent asks for waits for your yes; `daoris driver helpers <repository> "
                + "--mode on|off` sets one");
        }

        foreach (var (repository, rule) in choices.HandOver) Say($"  hand-over  {repository}  {rule.Mode}");
        foreach (var (workspace, rule) in choices.WorkspaceHandOver) Say($"  hand-over  workspace {workspace}  {rule.Mode}  (for each repository there that sets none)");
        if (choices.HandOver.Count == 0 && choices.WorkspaceHandOver.Count == 0)
        {
            Say("  hand-over  none set — an agent's ask to hand its session over waits for your yes; `daoris driver handover "
                + "<repository> --mode on|off` sets one");
        }

        Say(choices.Brainstorms is { } brainstorms
            ? $"  brainstorms  {BrainstormRules.Listed(brainstorms)}"
            : $"  brainstorms  none set — {BrainstormRules.Listed(BrainstormRule.Default)}; `daoris driver brainstorms --agents <a,b>` sets one");
        foreach (var (workspace, rule) in choices.WorkspaceBrainstorms) Say($"  brainstorms  workspace {workspace}  {BrainstormRules.Listed(rule)}  (for its repositories' questions)");

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

    /// <summary>What *Try again* did, as the words read it: from the operation alone, or from the route's answer.</summary>
    private sealed record Retried(string Did, string? Session, int Mark, int Strikes);

    /// <summary>
    /// <c>retry</c> (RETRY1, SESSUX1b): with <c>--session</c> the person's stop released, with <c>--at</c> a mark the person gives, both
    /// alone; else the quest's *Try again*, judged from the planner's verdict (<see cref="StandingChoices.Retry"/>) on both paths.
    /// </summary>
    private static ChoicePress Retry(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var named = Named(args, "retry");
        var quest = named.StartsWith('#') ? named[1..] : named;

        int Released(string session)
        {
            Say($"daoris: quest `#{quest}` is released from your stop of session `{session}`.");
            Say("  The driver takes it up again at its next look: a taken quest is carried on in the tree that session");
            Say("  worked in, an open one is planned again. A later stop holds it again.");
            Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
            return 0;
        }

        int Marked(int mark, bool counted, int strikes)
        {
            Say($"daoris: quest `#{quest}` may be started again.");
            Say($"  Counting from {mark} failure(s){(counted ? ", as this machine's records count them" : "")} — what already");
            Say($"  happened is still in the records, and {(strikes == 0 ? "no" : strikes.ToString(CultureInfo.InvariantCulture))} more failure(s) will park it again.");
            Say($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
            return 0;
        }

        var sessionAt = IndexOf(args, "--session");
        if (sessionAt != -1)
        {
            var session = args.ElementAtOrDefault(sessionAt + 1)?.Trim();
            if (string.IsNullOrEmpty(session) || session.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ChoiceWords("`--session` needs the session your stop ended — the quest's *Sitting* names it: "
                    + "`daoris driver retry <quest> --session <id>`.");
            }

            if (args.Contains("--at"))
            {
                throw new ChoiceWords("a retry either releases your stop (`--session <id>`) or counts the strikes from a mark "
                    + "(`--at <n>`) — a stop is not a strike, so the two never go together.");
            }

            var key = FindName(before.Released.Keys, quest) ?? quest;
            return new("retry", null, null, _ =>
            {
                StandingChoices.Write(path, config => config.WithReleased(key, session));
                return Task.FromResult<object?>(null);
            }, _ => null, _ => Released(session));
        }

        var at = IndexOf(args, "--at");
        if (at != -1)
        {
            var mark = args.ElementAtOrDefault(at + 1) is { } given ? Number(given) : double.NaN;
            if (!IsWhole(mark) || mark < 0 || mark > int.MaxValue) throw new ChoiceWords("`driver retry --at` needs a whole number of failures to count from.");
            var count = (int)mark;
            return new("retry", null, null, _ =>
            {
                StandingChoices.Write(path, config => config.WithForgiven(quest, count));
                return Task.FromResult<object?>(null);
            }, _ => null, _ => Marked(count, false, before.Strikes));
        }

        // While Daoris runs, the quest's *Try again* (RETRY_QUEST): the instance judges from its planner's verdict and answers what it
        // did. Alone, the same operation judges from a fresh verdict over this machine's service.
        return new("retry", ChoiceRoutes.Retry, new JsonObject { ["quest"] = quest }, async ct =>
        {
            IReadOnlyList<Consideration> verdicts;
            try
            {
                verdicts = await world.Verdicts(DriverConfig.Load(path), ct).ConfigureAwait(false);
            }
            catch (Exception unread) when (unread is HttpRequestException or TaskCanceledException or DriverException or InvalidOperationException
                                               && unread is not DriverConfigUnreadableException && !ct.IsCancellationRequested)
            {
                var mark = before.ForgivenAt(quest);
                throw new ChoiceWords($"cannot count the failures of `#{quest}`: this machine's session records could not be read — "
                    + $"{unread.Message.TrimEnd('.')}.\n  Nothing was written. Give the count yourself: `daoris driver retry {quest} --at <n>`, with n the "
                    + $"failures its *Sitting* names{(mark > 0 ? $" plus {mark}, the mark its last retry left" : "")}.\n"
                    + "  The quest's page's *Try again* counts them for you.");
            }

            var decision = StandingChoices.Retry(quest, verdicts);
            var now = StandingChoices.Write(path, decision.Change);
            return new Retried(decision.Did, decision.Session, now.ForgivenAt(quest), now.Strikes);
        }, answer =>
        {
            var retried = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("retried", out var said) && said.ValueKind == JsonValueKind.Object
                ? said
                : default;
            var did = retried.ValueKind == JsonValueKind.Object ? Json.Text(retried, "did") : null;
            var session = retried.ValueKind == JsonValueKind.Object ? Json.Text(retried, "session") : null;
            var mark = 0;
            if (answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("forgiven", out var forgiven) && forgiven.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in forgiven.EnumerateObject())
                {
                    if (string.Equals(entry.Name, quest, StringComparison.OrdinalIgnoreCase) && entry.Value.ValueKind == JsonValueKind.Number)
                    {
                        mark = entry.Value.GetInt32();
                        break;
                    }
                }
            }

            var strikes = answer.ValueKind == JsonValueKind.Object && Json.Number(answer, "strikes") is { } counted ? (int)counted : before.Strikes;
            return new Retried(did ?? RetryDecision.Marked, session, mark, strikes);
        }, fields => fields is Retried { Did: RetryDecision.Released, Session: { } session } ? Released(session)
            : fields is Retried marked ? Marked(marked.Mark, true, marked.Strikes) : Marked(0, true, before.Strikes));
    }

    /// <summary>
    /// <c>landing</c> (WSR1, D87, D100, LAND2a, PROCESS1b): a rule replaced whole, or cleared; its plugin asked by the operation
    /// (<see cref="StandingChoices.Landing"/>). A cycle the rule holds is kept and said, judged under the rule as it now stands;
    /// <c>--cycle &lt;file&gt;</c> sets one, judged after this rule's landing as <see cref="DriverConfig.WithLandingCycle"/> judges it, and
    /// <c>--no-cycle</c> takes it (FABRIC-GRAPH1c): no route takes either, so the rule and its cycle are written alone (PUSH1f2b).
    /// </summary>
    private static ChoicePress Landing(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var clear = args.Contains("--clear");
        var words = Operands(args, ["--workspace", "--plugin", "--key", "--cycle"]).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var form = words.ElementAtOrDefault(0);
        var pattern = words.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(name) || (!clear && string.IsNullOrEmpty(form)))
        {
            throw new ChoiceWords("`driver landing` needs <repository>|--workspace <name>, then merge|branch <pattern>|--clear — "
                + "e.g. `daoris driver landing --workspace aurora branch \"feature/{quest}-{slug}\"`, and on a branch "
                + "`--plugin <id>` for an installed plugin that pushes it and opens the pull request, `--auto-accept` "
                + "for a quest's done to land it with no press, `--key <shape>` for the shape `{ticket}` is found by, and "
                + "`--squash` for the pull request's branch as one commit.");
        }

        var keyAt = IndexOf(args, "--key");
        var key = keyAt == -1 ? null : args.ElementAtOrDefault(keyAt + 1);
        if (keyAt != -1 && (key is null || key.StartsWith("--", StringComparison.Ordinal)))
        {
            throw new ChoiceWords($"`--key` needs the shape of a ticket's key — `--key <shape>`, e.g. `--key \"{LandingRules.DefaultKey}\"`.");
        }

        if (key is not null && args.Contains("--no-key"))
        {
            throw new ChoiceWords("a rule names its ticket's key or reads the default — say `--key <shape>` or `--no-key`, not both.");
        }

        var pluginAt = IndexOf(args, "--plugin");
        var plugin = pluginAt == -1 ? null : args.ElementAtOrDefault(pluginAt + 1);
        if (pluginAt != -1 && (string.IsNullOrEmpty(plugin) || plugin.StartsWith("--", StringComparison.Ordinal)))
        {
            throw new ChoiceWords("`--plugin` needs the id of an installed plugin — `--plugin <id>`; `daoris plugin list` shows what there is.");
        }

        var autoAccept = args.Contains("--auto-accept");
        if (autoAccept && args.Contains("--no-auto-accept"))
        {
            throw new ChoiceWords("a rule accepts automatically or waits for your Accept — say `--auto-accept` or `--no-auto-accept`, not both.");
        }

        var squash = args.Contains("--squash");
        if (squash && args.Contains("--no-squash"))
        {
            throw new ChoiceWords("a rule squashes the work to one commit or keeps the session's commits — say `--squash` or `--no-squash`, not both.");
        }

        var rule = clear ? null : new LandingRule(form!, pattern, args.Contains("--tidy"), plugin, autoAccept, key, squash);
        if (rule is not null && LandingRules.Problem(rule) is { } problem) throw new ChoiceWords(problem);

        // The work's cycle after the landing (FABRIC-GRAPH1c): set, taken, or with neither flag kept as the rule holds it.
        var cycleAt = IndexOf(args, "--cycle");
        var cycleFile = cycleAt == -1 ? null : args.ElementAtOrDefault(cycleAt + 1);
        if (cycleAt != -1 && (cycleFile is null || cycleFile.StartsWith("--", StringComparison.Ordinal)))
        {
            throw new ChoiceWords("`--cycle` needs the file holding the cycle's JSON — `--cycle <file>`, a graph fragment after the landing.");
        }

        var noCycle = args.Contains("--no-cycle");
        if (cycleFile is not null && noCycle) throw new ChoiceWords("a rule sets its cycle or takes it away — say `--cycle <file>` or `--no-cycle`, not both.");
        if (rule is null && (cycleFile is not null || noCycle))
        {
            throw new ChoiceWords("`--clear` takes the rule and its cycle with it — a cycle rides its rule (D164). Nothing was written.");
        }

        string? cycle = null;
        if (cycleFile is not null)
        {
            cycle = CycleFile(world, cycleFile);
            var source = workspace is not null ? LandingSource.Workspace : LandingSource.Repository;
            if (NodeGraphHomes.OfLanding(rule! with { Cycle = cycle }, source).Problems.FirstOrDefault() is { } first)
            {
                throw new ChoiceWords($"the cycle in `{cycleFile}` does not hold: {first.Words} Nothing was written.");
            }
        }

        var held = workspace is not null ? AtName(before.WorkspaceLandings, workspace) : AtName(before.Landings, name);
        var payload = Scoped(workspace, name);
        if (rule is not null)
        {
            // The rule is sent whole, its key and its squash named, as this verb replaces it whole: a route that kept the standing
            // rule's key or squash would leave what the terminal did not say.
            payload["form"] = rule.Form;
            if (rule.Pattern is not null) payload["pattern"] = rule.Pattern;
            payload["tidy"] = rule.Tidy;
            if (rule.Plugin is not null) payload["plugin"] = rule.Plugin;
            payload["autoAccept"] = rule.AutoAccept;
            payload["key"] = rule.Key;
            payload["squash"] = rule.Squash;
        }

        var home = DriverConfig.HomeOf(path);
        // SET_LANDING has no field for the cycle and keeps the one the rule holds, as this verb does with neither flag; a cycle set or taken
        // is this verb's alone (D171's PUSH1f note).
        var cycled = cycleFile is not null || noCycle;
        return new("landing", cycled ? null : ChoiceRoutes.Landing, cycled ? null : payload, _ =>
        {
            StandingChoices.Write(path, config =>
            {
                var landed = StandingChoices.Landing(config, workspace is null ? name : null, workspace, rule, PluginCatalog.Load(home, AdapterSet.Built().Names));
                if (!cycled) return landed;
                return workspace is not null ? landed.WithWorkspaceLandingCycle(workspace, cycle) : landed.WithLandingCycle(name, cycle);
            });
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var whose = workspace is not null ? $"repositories in the workspace `{workspace}`" : $"`{name}`";
            if (rule is null)
            {
                Say($"daoris: work in {whose} lands as {(workspace is not null ? "each one's own rule, else " : "its workspace's rule, else ")}the merge into its line.");
            }
            else if (rule.Form == LandingForm.Merge)
            {
                Say($"daoris: work in {whose} is merged into the line when you accept it, in the repository's own checkout.");
            }
            else if (rule.Plugin is not null)
            {
                Say($"daoris: work in {whose} is put on a branch named `{rule.Pattern}` when you accept it, and then");
                Say($"  plugin `{rule.Plugin}` pushes it and opens the pull request, over its wire. Daoris itself never pushes");
                Say("  (D87, D100): the plugin's own process does, signed in as its platform's tools are. A plugin that fails");
                Say("  leaves the branch standing, and the review says how to push it yourself.");
            }
            else
            {
                Say($"daoris: work in {whose} is put on a branch named `{rule.Pattern}` when you accept it —");
                Say("  from the session's branch, with nothing merged and no checkout touched. You push it and open");
                Say("  the pull request: Daoris never pushes (D87).");
            }

            // What names the branch's ticket, said where the pattern asks for one (PROCESS1b-i).
            if (rule is { Form: LandingForm.Branch } && (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal)) Say($"  {LandingRules.TicketSays(rule.Key)}");

            if (rule is { Tidy: true })
            {
                Say("  Once a press lands the work, its tree and its branch go — only where git proves the work is on a");
                Say("  branch of yours. Without --tidy the tree stays until you discard it.");
            }

            // The person's standing say-so for a push with no press, said as it is given (D145 point 5, design §6).
            if (rule is { AutoAccept: true }) Say($"  Accept automatically: {LandingRules.AutoAcceptSays(rule.Plugin)}");
            else if (rule is { Form: LandingForm.Branch } && args.Contains("--no-auto-accept"))
            {
                Say("  Done work there waits for your Accept — a repository's own rule replaces its workspace's whole, the switch with it.");
            }

            // The first commit Daoris itself makes (PROCESS1b-ii), said with the identity it is made under.
            if (rule is { Squash: true })
            {
                foreach (var line in LandingRules.SquashSays) Say($"  {line}");
            }
            else if (rule is { Form: LandingForm.Branch } && args.Contains("--no-squash"))
            {
                Say("  The branch holds the session's own commits — a repository's own rule replaces its workspace's whole, the squash with it.");
            }

            // The cycle: set, taken, or kept and judged under the rule as it now stands, since a form that changed under a cycle keeps it
            // and the driver says one that does not hold (FABRIC-GRAPH1's choice 3).
            if (cycle is not null)
            {
                Say($"  After the landing, its cycle: {CycleSaid(cycle)}.");
            }
            else if (noCycle)
            {
                Say("  Its cycle is cleared: the work ends when the line holds it.");
            }
            else if (rule is not null && held?.Cycle is { } kept)
            {
                var first = NodeGraphHomes.OfLanding(rule with { Cycle = kept }, LandingSource.Repository).Problems.FirstOrDefault();
                Say(first is null ? $"  Its cycle is kept: {CycleSaid(kept)}." : $"  Its cycle is kept as written, and does not hold under this rule: {first.Words}");
            }

            if (rule is not null && workspace is not null)
            {
                Say("  A repository with a rule of its own keeps it — `daoris driver landing <repository> --clear` hands it back.");
            }

            Say($"  Written to {path} — the review reads it at each press, so nothing restarts.");
            return 0;
        });
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

            if (AcrossRules.Problem(name, value ?? "") is { } problem) throw new ChoiceWords(problem);
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

    /// <summary>What a review's procedure look found, as the words read it.</summary>
    private sealed record Reviewed(IReadOnlyList<string> Lacking, bool Unchecked, string? Unread);

    /// <summary>
    /// <c>review</c> (REVIEWENV1a, D154 point 2): the person's words made one edit, judged by the rules' table, then, for an environment
    /// put, its procedure looked for in the checkouts the registry names (<see cref="StandingChoices.Review"/>) before anything is written.
    /// </summary>
    private static ChoicePress Review(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var words = Operands(args, ["--workspace", "--kind", "--procedure", "--address", "--run", "--drop"]).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var environment = words.ElementAtOrDefault(0);
        var clear = args.Contains("--clear");
        var drop = FlagValue(args, "--drop");
        if (args.Contains("--required") && args.Contains("--not-required"))
        {
            throw new ChoiceWords("a rule is required or not — say `--required` or `--not-required`, not both.");
        }

        bool? required = args.Contains("--required") ? true : args.Contains("--not-required") ? false : null;
        if (string.IsNullOrEmpty(name) || (environment is null && !clear && drop is null && required is null))
        {
            throw new ChoiceWords("`driver review` needs <repository>|--workspace <name>, then <environment> --kind local|deployed --procedure <path> "
                + "[--address <url>] [--run \"<command>\"] [--required|--not-required], or none, --drop <environment>, "
                + "--required|--not-required, or --clear — e.g. `daoris driver review storefront dev --kind local --procedure README.md "
                + "--address http://localhost:4200 --required`.");
        }

        var kind = FlagValue(args, "--kind");
        var procedure = FlagValue(args, "--procedure");
        var address = FlagValue(args, "--address");
        var run = FlagValue(args, "--run");
        // `none` is the word for a repository with no review environment, unless it is given an environment's parts.
        var none = environment == "none" && kind is null && procedure is null && address is null && run is null;
        var payload = Scoped(workspace, name);
        if (clear) payload["clear"] = true;
        if (drop is not null) payload["drop"] = drop;
        if (none)
        {
            payload["none"] = true;
        }
        else if (environment is not null)
        {
            var put = new JsonObject { ["name"] = environment };
            if (kind is not null) put["kind"] = kind;
            if (procedure is not null) put["procedure"] = procedure;
            if (address is not null) put["address"] = address;
            if (run is not null) put["run"] = run;
            payload["put"] = put;
        }

        if (required is not null) payload["required"] = required;
        var edit = ReviewRules.EditOf(JsonSerializer.SerializeToElement(payload));
        DriverConfig after;
        try
        {
            after = ReviewRules.Apply(before, edit);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused.Message);
        }

        var map = workspace is not null ? after.WorkspaceReviews : after.Reviews;
        var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceReviews : before.Reviews).Keys, name) ?? name;
        var whose = workspace is not null ? $"repositories in the workspace `{key}` that set none of their own" : $"`{key}`";

        void Said()
        {
            var rule = map.GetValueOrDefault(key);
            if (clear)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own review rule, else none."
                    : $"daoris: `{key}` takes its workspace's review rule again, else none.");
            }
            else if (none)
            {
                Say($"daoris: `{key}` declares no review environment.");
            }
            else if (drop is not null)
            {
                var first = rule is null || rule.IsNone ? null : rule.Environments[0].Name;
                var was = (workspace is not null ? before.WorkspaceReviews : before.Reviews).GetValueOrDefault(key);
                var wasDefault = was is { IsNone: false } && was.Environments[0].Name == drop;
                Say($"daoris: {whose} no longer declare{(workspace is not null ? "" : "s")} `{drop}`"
                    + $"{(wasDefault && first is not null ? $"; `{first}` is {(workspace is not null ? "their" : "its")} default now" : "")}.");
            }
            else if (environment is null)
            {
                Say($"daoris: {(workspace is not null ? $"the workspace `{key}`'s" : $"`{key}`'s")} review rule is {(required == true ? "" : "not ")}required now.");
            }
            else
            {
                var first = rule is null || rule.IsNone ? null : rule.Environments[0].Name;
                Say($"daoris: {whose} declare{(workspace is not null ? "" : "s")} the review environment `{environment}`"
                    + (first == environment ? $", {(workspace is not null ? "their" : "its")} default" : $"; `{first}` stays {(workspace is not null ? "their" : "its")} default") + ".");
            }

            if (rule is not null)
            {
                foreach (var sentence in ReviewRules.Says(rule).Concat(ReviewRules.Gate(rule))) Say($"  {sentence}");
            }

            if (rule is { IsNone: false } && workspace is not null)
            {
                Say("  A repository with a rule of its own keeps it — `daoris driver review <repository> --clear` hands it back.");
            }
        }

        if (edit.Put is not { Procedure: { } looked })
        {
            return new("review", ChoiceRoutes.Review, payload, _ =>
            {
                StandingChoices.Write(path, config => ReviewRules.Apply(config, edit));
                return Task.FromResult<object?>(null);
            }, _ => null, _ =>
            {
                Said();
                Say($"  Written to {path}.");
                return 0;
            });
        }

        IEnumerable<string> Found(Reviewed reviewed)
        {
            if (reviewed.Unread is { } unread)
            {
                yield return $"Not checked: the registry could not be read — {unread}; a set-up step sits until its tree holds `{looked}`.";
                yield break;
            }

            if (workspace is null)
            {
                yield return reviewed.Unchecked
                    ? $"Not checked: `{key}` has no checkout on this machine; a set-up step sits until its tree holds `{looked}`."
                    : $"`{looked}` is in `{key}`'s checkout here.";
                yield break;
            }

            if (reviewed.Unchecked) yield return $"Not checked: no repository of the workspace `{key}` has a checkout on this machine.";
            else if (reviewed.Lacking.Count == 0) yield return $"Every repository of the workspace `{key}` with a checkout here holds `{looked}`.";
            foreach (var each in reviewed.Lacking) yield return ReviewRules.SitsUntil(each, looked);
        }

        // An environment put: its procedure is looked for in each checkout the rule reaches, before anything is written: by this verb
        // alone, and by the instance's own route while Daoris runs, whose answer says what it found (`reviewed`).
        return new("review", ChoiceRoutes.Review, payload, async ct =>
        {
            var registry = await world.Registry(ct).ConfigureAwait(false);
            Reviewed reviewed;
            if (registry.Checkouts is not { } checkouts)
            {
                reviewed = new([], true, registry.Unread ?? "it answered nothing");
            }
            else
            {
                var look = StandingChoices.Review(checkouts, edit, looked);
                reviewed = new(look.Lacking, look.Unchecked, null);
            }

            StandingChoices.Write(path, config => ReviewRules.Apply(config, edit));
            return reviewed;
        }, answer =>
        {
            var said = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("reviewed", out var found) && found.ValueKind == JsonValueKind.Object
                ? found
                : default;
            IReadOnlyList<string> lacking = said.ValueKind == JsonValueKind.Object && said.TryGetProperty("lacking", out var listed) && listed.ValueKind == JsonValueKind.Array
                ? [.. listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : [];
            return new Reviewed(lacking, said.ValueKind == JsonValueKind.Object && Json.Flag(said, "unchecked"), null);
        }, fields =>
        {
            Said();
            foreach (var note in Found((Reviewed)fields!)) Say($"  {note}");
            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>
    /// <c>opinion</c> (XAGENT1a): the person's words made one edit, judged by the rules' table, and said in the driver's sentences, the
    /// working agent's own family named among the reviewers, then what the gate does with it (XAGENT1f4).
    /// </summary>
    private static ChoicePress Opinion(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--reviewers", "--on", "--minutes"];
        string[] switches = ["--required", "--not-required", "--verify", "--no-verify", "--recheck", "--no-recheck", "--clear"];
        const string usage = "`driver opinion` needs <repository>|--workspace <name>, then --reviewers <adapter,adapter> [--on landing,steps] "
            + "[--required|--not-required] [--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck], or none, or --clear — "
            + "e.g. `daoris driver opinion web-app --reviewers codex-acp --required`.";
        Flags(args, valued, switches, "opinion", usage);

        bool? Both(string on, string off, string why)
        {
            if (args.Contains(on) && args.Contains(off)) throw new ChoiceWords($"{why} — say `{on}` or `{off}`, not both.");
            return args.Contains(on) ? true : args.Contains(off) ? false : null;
        }

        var required = Both("--required", "--not-required", "a rule is required or not");
        var verify = Both("--verify", "--no-verify", "a reviewer may build and run or it may not");
        var recheck = Both("--recheck", "--no-recheck", "the answers are read again or they are not");
        var workspace = FlagValue(args, "--workspace");
        var words = Operands(args, valued).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var none = words.ElementAtOrDefault(0) == "none";
        var clear = args.Contains("--clear");
        JsonArray? Listed(string flag) => FlagValue(args, flag) is { } given ? [.. given.Split(',').Select(each => (JsonNode?)each.Trim())] : null;
        var reviewers = Listed("--reviewers");
        var on = Listed("--on");
        var minutes = FlagValue(args, "--minutes");
        var set = new JsonObject();
        if (reviewers is not null) set["reviewers"] = reviewers;
        if (on is not null) set["on"] = on;
        if (required is not null) set["required"] = required;
        if (verify is not null) set["verify"] = verify;
        // A number only as digits: anything else is refused in the rule's own words.
        if (minutes is not null) set["minutes"] = minutes.All(char.IsAsciiDigit) && minutes.Length > 0 ? JsonValue.Create(double.Parse(minutes, CultureInfo.InvariantCulture)) : minutes;
        if (recheck is not null) set["recheck"] = recheck;
        if (string.IsNullOrEmpty(name) || words.Count > (none ? 1 : 0) || (!none && !clear && set.Count == 0)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (set.Count > 0) payload["set"] = set;
        if (none) payload["none"] = true;
        if (clear) payload["clear"] = true;
        var edit = OpinionRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => OpinionRules.Apply(before, edit));
        return new("opinion", ChoiceRoutes.Opinion, payload, _ =>
        {
            StandingChoices.Write(path, config => OpinionRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceOpinions : after.Opinions;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceOpinions : before.Opinions).Keys, name) ?? name;
            var rule = map.GetValueOrDefault(key);
            if (clear)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own second-opinion rule, else none."
                    : $"daoris: `{key}` takes its workspace's second-opinion rule again, else none.");
            }
            else if (none)
            {
                Say($"daoris: `{key}` declares no second opinion.");
            }
            else if (rule is { IsNone: false })
            {
                var named = string.Join(", else ", rule.Reviewers.Select(each => $"`{each}`"));
                Say(workspace is not null
                    ? $"daoris: second opinions for repositories in the workspace `{key}` that set none of their own: {named}."
                    : $"daoris: second opinions for `{key}`: {named}.");
            }

            if (rule is not null)
            {
                // The working agent is the one this machine's work runs on: a reviewer of its family is no independent reading. A plugin's
                // agent is judged by what its plugin declares (XAGENT1b2), from the plugins beside the file, as the driver reads them.
                IReadOnlyList<string> same = rule.IsNone ? []
                    : OpinionRules.SameAgent(rule, before.Adapter, AdapterSet.Built().WithPlugins(PluginCatalog.Load(DriverConfig.HomeOf(path), AdapterSet.Built().Names)));
                foreach (var sentence in OpinionRules.Says(rule, [.. same]).Concat(OpinionRules.Gate(rule))) Say($"  {sentence}");
            }

            if (rule is { IsNone: false } && workspace is not null)
            {
                Say("  A repository with a rule of its own keeps it — `daoris driver opinion <repository> --clear` hands it back.");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary><c>helpers</c> (MIXAGENT2a-i): what a helper an agent asks for does here now.</summary>
    private static ChoicePress Helpers(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--mode", "--agents"];
        const string usage = "`driver helpers` needs <repository>|--workspace <name>, then --mode ask|on|off and/or --agents <adapter,adapter>, "
            + "or --clear — e.g. `daoris driver helpers web-app --mode on --agents codex-acp`.";
        Flags(args, valued, ["--clear"], "helpers", usage);
        var workspace = FlagValue(args, "--workspace");
        var name = workspace ?? Operands(args, valued).Skip(1).FirstOrDefault();
        var clear = args.Contains("--clear");
        var mode = FlagValue(args, "--mode");
        var agents = FlagValue(args, "--agents");
        var set = new JsonObject();
        if (mode is not null) set["mode"] = mode;
        if (agents is not null) set["agents"] = new JsonArray([.. agents.Split(',').Select(each => (JsonNode?)each.Trim())]);
        if (string.IsNullOrEmpty(name) || (!clear && set.Count == 0)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (set.Count > 0) payload["set"] = set;
        if (clear) payload["clear"] = true;
        var edit = HelperRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => HelperRules.Apply(before, edit));
        return new("helpers", ChoiceRoutes.Helpers, payload, _ =>
        {
            StandingChoices.Write(path, config => HelperRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceHelpers : after.Helpers;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceHelpers : before.Helpers).Keys, name) ?? name;
            if (map.GetValueOrDefault(key) is not { } rule)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own helpers rule, else each helper waits for your yes."
                    : $"daoris: `{key}` takes its workspace's helpers rule again, else each helper waits for your yes.");
            }
            else
            {
                Say(workspace is not null
                    ? $"daoris: helpers for repositories in the workspace `{key}` that set none of their own: {HelperRules.Listed(rule)}."
                    : $"daoris: helpers for `{key}`: {HelperRules.Listed(rule)}.");
                Say($"  {HelperRules.Says(rule.Mode)}");
            }

            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary><c>handover</c> (MIXAGENT1c): what an agent's ask to hand its session over does here now.</summary>
    private static ChoicePress HandOver(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        string[] valued = ["--workspace", "--mode"];
        const string usage = "`driver handover` needs <repository>|--workspace <name>, then --mode ask|on|off, or --clear — e.g. "
            + "`daoris driver handover web-app --mode on`.";
        Flags(args, valued, ["--clear"], "handover", usage);
        var workspace = FlagValue(args, "--workspace");
        var name = workspace ?? Operands(args, valued).Skip(1).FirstOrDefault();
        var clear = args.Contains("--clear");
        var mode = FlagValue(args, "--mode");
        if (string.IsNullOrEmpty(name) || (!clear && mode is null)) throw new ChoiceWords(usage);

        var payload = Scoped(workspace, name);
        if (mode is not null) payload["set"] = new JsonObject { ["mode"] = mode };
        if (clear) payload["clear"] = true;
        var edit = HandOverRules.EditOf(JsonSerializer.SerializeToElement(payload));
        var after = Judged(() => HandOverRules.Apply(before, edit));
        return new("handover", ChoiceRoutes.HandOver, payload, _ =>
        {
            StandingChoices.Write(path, config => HandOverRules.Apply(config, edit));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var map = workspace is not null ? after.WorkspaceHandOver : after.HandOver;
            var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceHandOver : before.HandOver).Keys, name) ?? name;
            if (map.GetValueOrDefault(key) is not { } rule)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own hand-over rule, else an agent's ask waits for your yes."
                    : $"daoris: `{key}` takes its workspace's hand-over rule again, else an agent's ask waits for your yes.");
            }
            else
            {
                Say(workspace is not null
                    ? $"daoris: hand-over for repositories in the workspace `{key}` that set none of their own: {rule.Mode}."
                    : $"daoris: hand-over for `{key}`: {rule.Mode}.");
                Say($"  {HandOverRules.Says(rule.Mode)}");
            }

            Say($"  Written to {path}.");
            return 0;
        });
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

    /// <summary>A rule's own judgement made before the press, as the command's own check: its refusal is the command's words.</summary>
    private static DriverConfig Judged(Func<DriverConfig> apply)
    {
        try
        {
            return apply();
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused.Message);
        }
    }

    /// <summary>Each word that is a flag must be one the verb takes, a valued flag's value skipped.</summary>
    private static void Flags(IReadOnlyList<string> args, string[] valued, string[] switches, string verb, string usage)
    {
        for (var at = 1; at < args.Count; at++)
        {
            var token = args[at];
            if (valued.Contains(token)) at++;
            else if (token.StartsWith("--", StringComparison.Ordinal) && !switches.Contains(token))
            {
                throw new ChoiceWords($"`{token}` is not a flag `driver {verb}` takes — {usage}");
            }
        }
    }

    /// <summary>A rule in a line of <c>list</c>: each environment with its kind, address, procedure and command, then whether it waits.</summary>
    private static string ReviewListed(ReviewRule rule)
    {
        if (rule.IsNone) return "none here, whatever its workspace says";
        var environments = rule.Environments.Select(each =>
        {
            var parts = new List<string> { each.Kind };
            if (each.Address is { } address) parts.Add(address);
            parts.Add($"by {each.Procedure}");
            if (each.Run is { } run) parts.Add($"runs `{run}`");
            return $"{each.Name} ({string.Join(", ", parts)})";
        });
        return $"{string.Join("; ", environments)}, {(rule.Required ? "required before landing" : "on a task's asking")}";
    }

    /// <summary>A rule in a line of <c>list</c>: its reviewers in order, when they read, what they may run, and the bound of a pass.</summary>
    private static string OpinionListed(OpinionRule rule)
    {
        if (rule.IsNone) return "none here, whatever its workspace says";
        var parts = new List<string>
        {
            string.Join(", else ", rule.Reviewers),
            rule.On.Contains("landing") ? (rule.On.Contains("steps") ? "before landing and each next step" : "before landing") : "before each next step",
        };
        if (rule.Required) parts.Add("required");
        if (rule.Verify) parts.Add("may build and run what is declared safe");
        parts.Add($"at most {rule.Bound} minutes a pass");
        if (!rule.Recheck) parts.Add("no recheck");
        return string.Join("; ", parts);
    }

    /// <summary>
    /// A cycle's size and end, as a door says it: `1 node and 1 wire, ending at `qa``; a fragment's end is the line where it names none,
    /// and a work cycle's (<paramref name="end"/> empty) none.
    /// </summary>
    private static string CycleSaid(string cycle, string end = NodeKinds.Line)
    {
        var parts = NodeGraphs.Parts(cycle, out _);
        var nodes = parts?.Nodes.Count ?? 0;
        var wires = parts?.Wires.Count ?? 0;
        return $"{nodes} node{(nodes == 1 ? "" : "s")} and {wires} wire{(wires == 1 ? "" : "s")}, ending at `{parts?.End ?? end}`";
    }

    /// <summary>
    /// A cycle's file, read for a door that sets it (FABRIC-GRAPH1c): its JSON as <c>driver.json</c> keeps it, compact, every value as
    /// written, or the refusal naming the file. A path is the terminal's, from the folder it was run in.
    /// </summary>
    private static string CycleFile(DriverChoicesWorld world, string file)
    {
        var path = Path.IsPathFullyQualified(file) ? file : Path.GetFullPath(Path.Combine(world.Folder, file));
        if (!File.Exists(path)) throw new ChoiceWords($"there is no file `{file}`. Nothing was written.");
        try
        {
            return NodeGraphHomes.Compact(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            throw new ChoiceWords($"`{file}` is not readable JSON. Nothing was written.");
        }
    }

    /// <summary>
    /// <c>brainstorms</c>' words read (BRAINSTORM1b): its workspace, whether it clears, and the set it names, each part only where it was
    /// said, as the screen and Ask Daoris send it. A minute given as anything but digits is handed on as written, to be refused in the
    /// rule's words; a repository named is refused here.
    /// </summary>
    private static (string? Workspace, bool Clear, BrainstormSet? Set) BrainstormsAsked(IReadOnlyList<string> args)
    {
        const string usage = "`driver brainstorms` takes [--workspace <name>], then --mode ask|off, --agents <adapter,adapter>, --pass-minutes, "
            + "--synthesis-minutes, --challenge-minutes and --request-minutes <n>, or --clear — e.g. "
            + "`daoris driver brainstorms --agents claude-code,codex-acp --pass-minutes 10`.";
        string[] valued = ["--workspace", "--mode", "--agents", .. MinuteFlags.Select(each => each.Flag)];
        for (var at = 1; at < args.Count; at++)
        {
            var token = args[at];
            if (valued.Contains(token)) at++;
            else if (token.StartsWith("--", StringComparison.Ordinal) && token != "--clear")
            {
                throw new ChoiceWords($"`{token}` is not a flag `driver brainstorms` takes — {usage}");
            }
            else if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ChoiceWords($"a brainstorms rule is set for this machine or for a workspace — never for one repository. {usage}");
            }
        }

        var minutes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (flag, name) in MinuteFlags)
        {
            if (FlagValue(args, flag) is { } value)
            {
                minutes[name] = value.All(char.IsAsciiDigit) ? double.Parse(value, CultureInfo.InvariantCulture) : value;
            }
        }

        var set = new BrainstormSet
        {
            Mode = FlagValue(args, "--mode"),
            Agents = FlagValue(args, "--agents")?.Split(',').Select(each => (string?)each.Trim()).ToList(),
            Minutes = minutes.Count > 0 ? minutes : null,
        };
        return (FlagValue(args, "--workspace"), args.Contains("--clear"), set.Names ? set : null);
    }

    /// <summary><c>brainstorms</c> naming no change: this machine's rule and each workspace's, said.</summary>
    private static int BrainstormsSaid(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        BrainstormsAsked(args);
        var said = world.Instance.Out;
        said.WriteLine($"daoris: brainstorms on this machine: {BrainstormRules.Listed(choices.Brainstorms ?? BrainstormRule.Default)}"
            + $"{(choices.Brainstorms is null ? " (the default)" : "")}.");
        foreach (var (name, rule) in choices.WorkspaceBrainstorms)
        {
            said.WriteLine($"  for questions about repositories in the workspace `{name}`: {BrainstormRules.Listed(rule)}.");
        }

        return 0;
    }

    /// <summary>Whether this machine's Daoris manages a toolchain for an agent's name: the CLI's <c>TOOLCHAINS</c>, the gate stubs aside.</summary>
    private static bool Managed(string name) =>
        AdapterSet.Built().Reserved.Contains(name, StringComparer.Ordinal) && name is not ("stub" or "acp-stub");

    /// <summary>Whether the file sets a key, read where it holds a whole number, as the CLI reads a choice set rather than defaulted.</summary>
    private static bool Written(DriverConfig choices, string key)
    {
        if (choices.Source is not { } source) return false;
        using var document = JsonDocument.Parse(source);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsInteger(number);
    }

    /// <summary>A moment as the CLI said it: ISO 8601 to the second, in UTC.</summary>
    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>The scope of a press: the workspace where one is named, else the repository.</summary>
    private static JsonObject Scoped(string? workspace, string name) =>
        workspace is not null ? new JsonObject { ["workspace"] = workspace } : new JsonObject { ["repository"] = name };

    /// <summary>The verb's name, the first operand after it: a flag's value is never the name (REV3).</summary>
    private static string Named(IReadOnlyList<string> args, string verb) =>
        Operands(args, ["--at", "--session"]).ElementAtOrDefault(1)
        ?? throw new ChoiceWords($"`driver {verb}` needs a name — e.g. `daoris driver {verb} aurora-engine`.");

    /// <summary>The positional operands, in order: every token that is neither a flag nor the value of one (the CLI's <c>operands</c>).</summary>
    private static List<string> Operands(IReadOnlyList<string> args, string[] valued)
    {
        var found = new List<string>();
        for (var at = 0; at < args.Count; at++)
        {
            var token = args[at];
            if (valued.Contains(token)) at++;
            else if (!token.StartsWith("--", StringComparison.Ordinal)) found.Add(token);
        }

        return found;
    }

    /// <summary>The value after a flag, refusing a flag with nothing after it (the CLI's <c>flagValue</c>).</summary>
    private static string? FlagValue(IReadOnlyList<string> args, string flag)
    {
        var at = IndexOf(args, flag);
        if (at == -1) return null;
        var value = args.ElementAtOrDefault(at + 1);
        if (string.IsNullOrEmpty(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ChoiceWords($"{flag} needs a name — e.g. `{flag} aurora`");
        }

        return value;
    }

    private static int IndexOf(IReadOnlyList<string> args, string word)
    {
        for (var at = 0; at < args.Count; at++)
        {
            if (args[at] == word) return at;
        }

        return -1;
    }

    /// <summary>A name in the list in any case, as it is spelled there, or null.</summary>
    private static string? FindName(IEnumerable<string> names, string name) =>
        names.FirstOrDefault(each => string.Equals(each, name, StringComparison.OrdinalIgnoreCase));

    private static T? AtName<T>(IReadOnlyDictionary<string, T> map, string name) where T : class =>
        FindName(map.Keys, name) is { } key ? map[key] : null;

    /// <summary>A word read as a number as the CLI read it (JavaScript's <c>Number</c>): blank is 0, anything else not a number is NaN.</summary>
    private static double Number(string word)
    {
        var text = word.Trim();
        if (text.Length == 0) return 0;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex))
        {
            return hex;
        }

        if (text is "Infinity" or "+Infinity") return double.PositiveInfinity;
        if (text == "-Infinity") return double.NegativeInfinity;
        return text.All(c => char.IsAsciiDigit(c) || c is '.' or 'e' or 'E' or '+' or '-')
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : double.NaN;
    }

    private static bool IsWhole(double value) => double.IsFinite(value) && Math.Floor(value) == value;
}
