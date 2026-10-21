namespace Daoris.Driver;

/// <summary>
/// The workspace plan (WSSETUP6, D124 §4.1–§4.3): <i>Set up every repository in this workspace</i> writes a plan of single
/// set-ups, and the driver's tick works it, one at a time by default, the repositories other work touches first, pausing
/// after a pilot.
/// </summary>
/// <remarks>
/// <para><b>One quest per repository, never published at once</b> (D117 §9, D124 §4.1): under oldest-first and a cap of two,
/// twenty-nine set-ups published together would hold both slots for hours. So the press writes a plan, and each tick, while
/// fewer than <c>atOnce</c> of its set-ups are open and it is not paused, publishes the next to go as the single press
/// publishes it (<see cref="SetupPress.PublishAsync"/>), judged again at that moment.</para>
///
/// <para><b>Never the last slot</b>: <c>atOnce</c> is one by default and held to <c>cap − 1</c> at every tick, whatever the
/// file says, so other work always keeps a slot; at a cap of one it is one.</para>
///
/// <para><b>Nothing in the plan is a count</b> (D58): each repository's state is read from the registry (set up), the quests
/// (open, waiting for review, declined, deleted) and the strikes (parked) every time, so a set-up already open, from the
/// plan or from the single press, is never asked again. A repository refused at its turn is skipped with its reason and not
/// judged again until the person resumes the plan; a refusal of the machine's tools pauses the plan instead, since it
/// would refuse every repository alike.</para>
///
/// <para><b>The pilot</b>: the first <c>pilot</c> set-ups it publishes go alone. Nothing more is asked until all of them
/// have closed, and then the plan pauses itself and says so once, until the person resumes it with what they cost in
/// front of them (§7). A parked one has not closed: the person's attention is what it waits on.</para>
///
/// <para><b>The use cases, against their port</b>: the world comes through <see cref="IWorkspaceSetupWorld"/>, the plan's
/// file is <see cref="WorkspaceSetupFile"/>'s, and the rules are <see cref="WorkspaceSetupRules"/>', which its callers name.</para>
/// </remarks>
public static class WorkspaceSetup
{
    /// <summary>
    /// What a press on <paramref name="workspace"/> would write today: the repositories with a checkout here in §4.2's order,
    /// each with what touched it, where it stands, and what the press would refuse now; and why the press as a whole would
    /// not write. Reads, and writes nothing.
    /// </summary>
    public static async Task<WorkspaceSetupPreview> PreviewAsync(
        IWorkspaceSetupWorld world, DriverConfig config, SessionWire door, string home, string workspace, WorkspaceSetupOptions options,
        DateOnly day, CancellationToken ct = default)
    {
        var name = RemoteTarget.Workspace(workspace);
        var rows = await world.RegistrationsAsync(ct).ConfigureAwait(false);
        var members = rows.Where(row => Same(row.Workspace, name)).ToList();
        var spelled = members.FirstOrDefault()?.Workspace ?? name;
        var here = members.Where(row => !string.IsNullOrWhiteSpace(row.Root)).Select(row => row.Repository)
            .Order(StringComparer.OrdinalIgnoreCase).ToList();
        var away = members.Where(row => string.IsNullOrWhiteSpace(row.Root)).Select(row => row.Repository)
            .Order(StringComparer.OrdinalIgnoreCase).ToList();

        var refusals = new List<SetupRefusal>();
        if (here.Count == 0)
        {
            refusals.Add(new(WorkspaceSetupRefusals.NoRepository, members.Count == 0
                ? $"no repository on this machine's registry is in workspace `{name}`. Add them on Repositories, or with "
                  + $"`daoris import <folder> --workspace {Spelled(name)}`, then press again."
                : $"workspace `{spelled}` has no checkout here of any of its repositories ({Names(away)}): each is a teammate's "
                  + "registration, and its own machine's driver can set it up."));
        }

        if (WorkspaceSetupFile.Load(home, spelled).Plan is { Live: true } working)
        {
            refusals.Add(new(WorkspaceSetupRefusals.PlanWorking,
                $"a plan for workspace `{spelled}` is already working, made {Day(working.Created)}. "
                + $"`daoris-driver setup --workspace {Spelled(spelled)} --pause`, `--resume` or `--stop` steers it, and a stopped plan "
                + "may be replaced by a new press."));
        }

        var atOnce = options.AtOnce ?? WorkspaceSetupRules.DefaultAtOnce;
        var most = WorkspaceSetupRules.MostAtOnce(config.Cap);
        if (atOnce < 1 || atOnce > most)
        {
            refusals.Add(new(WorkspaceSetupRefusals.AtOnce, atOnce < 1
                ? "at least one set-up must be open at once, or the plan would never ask anything."
                : $"{atOnce} at once is more than this machine's cap of {config.Cap} leaves: at most {most}, so other work always "
                  + "keeps a slot. Raise the cap with `daoris driver cap <n>` first, or ask for fewer."));
        }

        var pilot = Math.Max(0, options.Pilot ?? WorkspaceSetupRules.DefaultPilot);
        var first = options.First ?? [];
        var skip = options.Skip ?? [];
        foreach (var named in first.Concat(skip).Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(named => !here.Contains(named, StringComparer.OrdinalIgnoreCase)))
        {
            refusals.Add(new(WorkspaceSetupRefusals.Named,
                $"`{named}` is not one of workspace `{spelled}`'s repositories with a checkout here, so it can neither go first "
                + "nor be left out."));
        }

        foreach (var both in first.Intersect(skip, StringComparer.OrdinalIgnoreCase))
        {
            refusals.Add(new(WorkspaceSetupRefusals.Named, $"`{both}` is named both to go first and to be left out: name it once."));
        }

        // The machine's own tools: refused here, they would refuse every repository the plan reached.
        var tools = SetupPressRules.ToolRefusals(await world.ToolsAsync(ct).ConfigureAwait(false));
        refusals.AddRange(tools);

        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var runs = await world.RunsAsync(ct).ConfigureAwait(false);
        var touches = SetupOrder.Count(here, rows, quests, runs, world.Touched);
        var order = SetupOrder.Order(here, touches, first, skip);
        var strikes = await world.StrikesAsync(ct).ConfigureAwait(false);
        var standings = WorkspaceSetupRules.Stand(new WorkspaceSetupPlan(spelled, order, atOnce, pilot), rows, quests, strikes, config);

        var listed = new List<WorkspaceSetupRow>();
        foreach (var standing in standings)
        {
            var row = new WorkspaceSetupRow(standing.Repository, touches.GetValueOrDefault(standing.Repository) ?? SetupTouches.None, standing);
            if (standing.State == SetupState.ToGo && tools.Count == 0)
            {
                var judged = await SetupPress.PlanAsync(world, config, door, standing.Repository, day, ct).ConfigureAwait(false);
                row = row with { Refusals = [.. judged.Refusals.Where(refusal => !SetupPressRules.IsToolRefusal(refusal))] };
            }

            listed.Add(row);
        }

        return new WorkspaceSetupPreview(spelled)
        {
            Rows = listed,
            Unticked = [.. here.Where(repository => skip.Contains(repository, StringComparer.OrdinalIgnoreCase))],
            NoCheckout = away,
            AtOnce = atOnce,
            Pilot = pilot,
            Cap = config.Cap,
            Adapter = config.Adapter,
            Refusals = refusals,
        };
    }

    /// <summary>
    /// Press: the doctrine tool's verbs into the workspace's scope, once (D124 §4.3), then the plan written. It publishes
    /// nothing: the tick does. A plan that could not be written takes back the rules it added, and keeps any the person had.
    /// </summary>
    public static Task<WorkspaceSetupOutcome> PressAsync(
        WorkspaceSetupPreview preview, IWorkspaceSetupWorld world, string home, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!preview.Pressable)
        {
            return Task.FromResult(new WorkspaceSetupOutcome(
                false, preview.Refusals.FirstOrDefault()?.Sentence ?? "there is no repository to set up.", null, []));
        }

        var file = PermissionRules.Load(home);
        var held = file.Workspaces
            .Where(pair => Same(pair.Key, preview.Workspace))
            .SelectMany(pair => pair.Value.Allow)
            .ToHashSet(StringComparer.Ordinal);
        var added = SetupPressRules.Rules.Where(rule => !held.Contains(rule)).ToList();
        PermissionRules.Save(home, added.Aggregate(file, (rules, rule) => PermissionRules.Add(rules, RuleScope.Workspace, preview.Workspace, RuleList.Allow, rule)));

        var plan = new WorkspaceSetupPlan(preview.Workspace, [.. preview.Rows.Select(row => row.Repository)], preview.AtOnce, preview.Pilot)
        {
            Created = now,
        };
        try
        {
            WorkspaceSetupFile.Save(home, plan);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            var after = PermissionRules.Load(home);
            PermissionRules.Save(home, added.Aggregate(after, (rules, rule) => PermissionRules.Remove(rules, RuleScope.Workspace, preview.Workspace, rule)));
            return Task.FromResult(new WorkspaceSetupOutcome(false, $"the plan could not be written under the home: {error.Message}", null, []));
        }

        world.Said(SetupLine.Planned(plan.Workspace, plan.Order.Count, plan.AtOnce, plan.Pilot));
        return Task.FromResult(new WorkspaceSetupOutcome(
            true,
            $"the plan for workspace `{plan.Workspace}` is written: {plan.Order.Count} repositories, {AtOnceWords(plan.AtOnce)}"
            + (plan.Pilot > 0 ? $", pausing once the first {plan.Pilot} have closed." : ", with no pilot.")
            + " The driver's loop asks the next at each look while fewer are open; with no loop running, nothing is asked until one runs.",
            plan,
            added));
    }

    /// <summary>
    /// One look's work on every plan under the home that works and is not paused: the next to go published while fewer than
    /// its <c>atOnce</c> are open, a refusal skipped and said, the pilot's pause taken once it has closed. What it did, in the
    /// lines a look's report carries, each classed as it is made (NOTIFY2h2): what moved, a pause and a skip included, which the
    /// workspace's set-up says where the person resumes it; a home with no plan to work reads nothing.
    /// </summary>
    /// <param name="look">The look's classing; none classes and keeps nothing.</param>
    public static async Task<IReadOnlyList<string>> TickAsync(
        IWorkspaceSetupWorld world, DriverConfig config, SessionWire door, string home, DateOnly day, DateTimeOffset now,
        CancellationToken ct = default, LookSaid? look = null)
    {
        var plans = WorkspaceSetupFile.All(home).Where(plan => plan is { Live: true, Paused: null }).ToList();
        if (plans.Count == 0) return [];

        var rows = await world.RegistrationsAsync(ct).ConfigureAwait(false);
        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var strikes = await world.StrikesAsync(ct).ConfigureAwait(false);
        var said = new LookLines(look ?? LookSaid.None, "setup");
        foreach (var plan in plans)
        {
            await WorkAsync(plan, WorkspaceSetupRules.Stand(plan, rows, quests, strikes, config)).ConfigureAwait(false);
        }

        return said.Said;

        async Task WorkAsync(WorkspaceSetupPlan plan, IReadOnlyList<SetupStanding> standings)
        {
            var workspace = plan.Workspace;
            var open = standings.Count(standing => standing.State == SetupState.Open);
            var asked = plan.Order.Count(plan.Published.ContainsKey);

            if (WorkspaceSetupRules.PilotHolds(plan, asked))
            {
                var pilot = plan.Order.Where(plan.Published.ContainsKey).Take(plan.Pilot).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var flown = standings.Where(standing => pilot.Contains(standing.Repository)).ToList();
                if (flown.All(standing => standing.State is not (SetupState.Open or SetupState.Parked)))
                {
                    var sentence = $"paused after the pilot: {string.Join(", ", flown.Select(standing => $"`{standing.Repository}` {WorkspaceSetupRules.Word(standing.State)}"))}. "
                        + $"Look at what each wrote and what it cost, then `daoris-driver setup --workspace {Spelled(workspace)} --resume` carries on.";
                    if (PauseItself(home, workspace, new SetupPause(SetupPausedBy.Pilot, sentence, now), current => !current.PilotResumed))
                    {
                        world.Said(SetupLine.Paused(workspace, SetupPausedBy.Pilot));
                        said.Progress($"setup  {workspace}: {sentence}");
                    }
                }

                return;
            }

            var most = Math.Min(Math.Max(1, plan.AtOnce), WorkspaceSetupRules.MostAtOnce(config.Cap));
            foreach (var next in standings.Where(standing => standing.State == SetupState.ToGo))
            {
                if (open >= most || WorkspaceSetupRules.PilotHolds(plan, asked)) return;

                var press = await SetupPress.PlanAsync(world, config, door, next.Repository, day, ct).ConfigureAwait(false);
                if (press.Pressable)
                {
                    var outcome = await SetupPress.PublishAsync(press, world, ct).ConfigureAwait(false);
                    if (outcome is { Published: true, QuestId: { Length: > 0 } quest })
                    {
                        WorkspaceSetupFile.Update(home, workspace, current => current with { Published = With(current.Published, next.Repository, quest) });
                        world.Said(SetupLine.Published(workspace, next.Repository, quest));
                        open++;
                        asked++;
                        said.Progress($"setup  {workspace}: asked `{next.Repository}` to set itself up, #{quest} ({open} of {most} at once).");
                        continue;
                    }

                    Skip(workspace, next.Repository, WorkspaceSetupRefusals.ServiceRefused, outcome.Message);
                    continue;
                }

                // The machine's tools refuse every repository alike: pause, and say why, rather than skip each in turn.
                if (press.Refusals.FirstOrDefault(SetupPressRules.IsToolRefusal) is { } tool)
                {
                    if (PauseItself(home, workspace, new SetupPause(SetupPausedBy.Tool, tool.Sentence, now), _ => true))
                    {
                        world.Said(SetupLine.Paused(workspace, SetupPausedBy.Tool));
                        said.Progress($"setup  {workspace}: paused: {tool.Sentence} `daoris-driver setup --workspace {Spelled(workspace)} --resume` carries on once it is.");
                    }

                    return;
                }

                // Opened since the facts were read, by another door: counted as open, and never asked a second time.
                if (press.Refusals.Any(refusal => refusal.Code == SetupRefusals.Open))
                {
                    open++;
                    continue;
                }

                var refused = press.Refusals.Count > 0
                    ? press.Refusals
                    : [new SetupRefusal(SetupRefusals.AlreadySetUp, "there is nothing to ask of it.")];
                Skip(workspace, next.Repository, refused[0].Code, string.Join(" ", refused.Select(refusal => refusal.Sentence)));
            }
        }

        void Skip(string workspace, string repository, string code, string sentence)
        {
            WorkspaceSetupFile.Update(home, workspace, current => current with { Skipped = With(current.Skipped, repository, new SetupSkip(code, sentence, now)) });
            world.Said(SetupLine.Skipped(workspace, repository, code));
            said.Progress($"setup  {workspace}: skipped `{repository}`: {sentence}");
        }
    }

    /// <summary>Where each repository of <paramref name="plan"/> stands now, read from the world.</summary>
    public static async Task<IReadOnlyList<SetupStanding>> StandAsync(
        IWorkspaceSetupWorld world, DriverConfig config, WorkspaceSetupPlan plan, CancellationToken ct = default) =>
        WorkspaceSetupRules.Stand(
            plan,
            await world.RegistrationsAsync(ct).ConfigureAwait(false),
            await world.QuestsAsync(ct).ConfigureAwait(false),
            await world.StrikesAsync(ct).ConfigureAwait(false),
            config);

    /// <summary>The person's pause: nothing more is asked until they resume it; what it already asked carries on.</summary>
    public static WorkspaceSetupSteer Pause(IWorkspaceSetupWorld world, string home, string workspace, DateTimeOffset now)
    {
        var steer = Steerable(home, workspace);
        if (steer.Plan is not { } plan) return steer;
        if (plan.Paused is { } held) return new(true, $"the plan for workspace `{plan.Workspace}` is already paused: {held.Said}", plan);

        var next = WorkspaceSetupFile.Update(home, plan.Workspace, current => current.Paused is null
            ? current with { Paused = new(SetupPausedBy.Person, "paused by you.", now) }
            : current);
        world.Said(SetupLine.Paused(plan.Workspace, SetupPausedBy.Person));
        return new(true,
            $"the plan for workspace `{plan.Workspace}` is paused: nothing more is asked until "
            + $"`daoris-driver setup --workspace {Spelled(plan.Workspace)} --resume`, and what it already asked carries on.",
            next);
    }

    /// <summary>
    /// The person's resume: a pause lifted, the pilot's for good, and every repository it skipped judged again in its turn,
    /// so a refusal they have since answered is asked.
    /// </summary>
    public static WorkspaceSetupSteer Resume(IWorkspaceSetupWorld world, string home, string workspace)
    {
        var steer = Steerable(home, workspace);
        if (steer.Plan is not { } plan) return steer;
        var held = plan.Paused;
        var skipped = plan.Skipped.Count;

        var next = WorkspaceSetupFile.Update(home, plan.Workspace, current => current with
        {
            Paused = null,
            PilotResumed = current.PilotResumed || current.Paused?.By == SetupPausedBy.Pilot,
            Skipped = new Dictionary<string, SetupSkip>(StringComparer.OrdinalIgnoreCase),
        });
        if (held is not null) world.Said(SetupLine.Resumed(plan.Workspace));

        var lifted = held is null
            ? $"the plan for workspace `{plan.Workspace}` was not paused"
            : $"the plan for workspace `{plan.Workspace}` carries on{(held.By == SetupPausedBy.Pilot ? " past its pilot" : "")}";
        return new(true, lifted + (skipped > 0 ? $"; the {skipped} it skipped are judged again in their turn." : "."), next);
    }

    /// <summary>The person's stop: the plan ends. What it published stays, and a quest nobody has started can be deleted (D95).</summary>
    public static WorkspaceSetupSteer Stop(IWorkspaceSetupWorld world, string home, string workspace, DateTimeOffset now)
    {
        var (plan, problem) = WorkspaceSetupFile.Load(home, workspace);
        if (plan is null) return new(false, problem ?? NoPlan(workspace), null);
        if (!plan.Live) return new(true, $"the plan for workspace `{plan.Workspace}` is already stopped.", plan);

        var next = WorkspaceSetupFile.Update(home, plan.Workspace, current => current.Live ? current with { Stopped = now } : current);
        world.Said(SetupLine.Stopped(plan.Workspace));
        return new(true,
            $"the plan for workspace `{plan.Workspace}` is stopped: nothing more is asked. What it published stays, and a quest "
            + "nobody has started can be deleted with `daoris-driver quest delete <id>`.",
            next);
    }

    /// <summary>The plan paused by a tick, once: only a live plan not already paused, where <paramref name="when"/> still holds.</summary>
    private static bool PauseItself(string home, string workspace, SetupPause pause, Func<WorkspaceSetupPlan, bool> when)
    {
        var paused = false;
        WorkspaceSetupFile.Update(home, workspace, plan =>
        {
            if (plan is not { Live: true, Paused: null } || !when(plan)) return plan;
            paused = true;
            return plan with { Paused = pause };
        });
        return paused;
    }

    /// <summary>A plan the person may steer, or the refusal: none, one that does not read, or one already stopped.</summary>
    private static WorkspaceSetupSteer Steerable(string home, string workspace)
    {
        var (plan, problem) = WorkspaceSetupFile.Load(home, workspace);
        if (plan is null) return new(false, problem ?? NoPlan(workspace), null);
        return plan.Live
            ? new(true, "", plan)
            : new(false, $"the plan for workspace `{plan.Workspace}` was stopped {Day(plan.Stopped!.Value)}: a new press makes a new plan.", null);
    }

    private static string NoPlan(string workspace) =>
        $"there is no plan for workspace `{RemoteTarget.Workspace(workspace)}`: `daoris-driver setup --workspace {Spelled(RemoteTarget.Workspace(workspace))}` makes one.";

    /// <summary>A workspace in a command a person pastes, spelled for any shell (ACCTQUOTE1b); the words around it name it as it is.</summary>
    internal static string Spelled(string workspace) => ShellWord.Of(workspace, ShellWord.Workspace);

    private static string AtOnceWords(int atOnce) => atOnce == 1 ? "one at a time" : $"{atOnce} at a time";

    private static string Day(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string Names(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, T> With<T>(IReadOnlyDictionary<string, T> held, string key, T value) =>
        new(held, StringComparer.OrdinalIgnoreCase) { [key] = value };
}
