namespace Daoris.Driver;

/// <summary>Where one repository of a workspace plan stands, read from the facts every time (WSSETUP6, D124 §4.1).</summary>
public enum SetupState
{
    /// <summary>Registered from its line: adopted, and declaring what it owns.</summary>
    SetUp,

    /// <summary>Its set-up closed done, and its work is not on the line yet: the person's review, then <i>Bring up to date</i>.</summary>
    Waiting,

    /// <summary>Its set-up quest is open or taken: a session sets it up, or will.</summary>
    Open,

    /// <summary>Its set-up quest is open or taken and its failed sessions parked it (DRV6).</summary>
    Parked,

    /// <summary>Nothing asked yet: the plan publishes it in its turn.</summary>
    ToGo,

    /// <summary>The press refused it when its turn came, and the plan said why.</summary>
    Skipped,

    /// <summary>Its set-up closed declined: never asked again by the plan.</summary>
    Declined,

    /// <summary>The set-up the plan published is gone, deleted by the person (D95): never asked again by the plan.</summary>
    Deleted,
}

/// <summary>One repository's standing in a plan: its state, the set-up quest that says so, and the sentence a person reads.</summary>
public sealed record SetupStanding(string Repository, SetupState State, string? Quest, string Said);

/// <summary>Who paused a plan (D124 §4.1): the person, the pilot's close, or a doctrine tool this machine cannot run.</summary>
public static class SetupPausedBy
{
    public const string Person = "person";

    public const string Pilot = "pilot";

    public const string Tool = "tool";
}

/// <summary>A plan's pause: who paused it, the sentence that says why, and when.</summary>
public sealed record SetupPause(string By, string Said, DateTimeOffset At);

/// <summary>A repository the press refused when its turn came (D124 §4.1): the refusal's code, its sentences, and when.</summary>
public sealed record SetupSkip(string Code, string Said, DateTimeOffset At);

/// <summary>
/// A workspace's set-up plan, <c>&lt;home&gt;/setup/&lt;workspace&gt;.json</c> (WSSETUP6, D124 §4.1): the person's choices and
/// the quests it published. <b>Nothing in it is a count</b> (D58): progress is read from the quests and the registry every time.
/// </summary>
/// <param name="Order">The repositories, in §4.2's order, less the ones the person unticked.</param>
/// <param name="AtOnce">How many of its set-ups may be open at once, as the person chose it; a tick holds it to the cap's <c>cap − 1</c>.</param>
/// <param name="Pilot">How many go first: once they have all closed, the plan pauses itself until the person resumes it.</param>
public sealed record WorkspaceSetupPlan(string Workspace, IReadOnlyList<string> Order, int AtOnce, int Pilot)
{
    /// <summary>When the press wrote it.</summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>Why it publishes nothing for now, or null while it works.</summary>
    public SetupPause? Paused { get; init; }

    /// <summary>Whether the person resumed it after the pilot's pause, so the pilot never pauses it again.</summary>
    public bool PilotResumed { get; init; }

    /// <summary>When the person stopped it, or null: a stopped plan publishes nothing, and a new press may replace it.</summary>
    public DateTimeOffset? Stopped { get; init; }

    /// <summary>Each repository's set-up quest, by the repository's name, as the plan published it.</summary>
    public IReadOnlyDictionary<string, string> Published { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each repository the press refused at its turn, with why, by the repository's name.</summary>
    public IReadOnlyDictionary<string, SetupSkip> Skipped { get; init; } =
        new Dictionary<string, SetupSkip>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the plan still works: made, and not stopped.</summary>
    public bool Live => Stopped is null;
}

/// <summary>What other work touched a repository, from the facts this machine keeps (D124 §4.2).</summary>
/// <param name="Asked">Quests addressed to it by anyone but itself: another repository, an ask, the person. A set-up is not one.</param>
/// <param name="Read">Tool calls of other repositories' sessions that touched a file in its checkout, each call once (D76).</param>
/// <param name="Sessions">Sessions this machine ran in it.</param>
public sealed record SetupTouches(int Asked, int Read, int Sessions)
{
    public static SetupTouches None { get; } = new(0, 0, 0);
}

/// <summary>A session record this machine keeps, and the repository it ran in.</summary>
public sealed record SessionRun(string Session, string Repository);

/// <summary>A file one tool call touched, as the conversation record names it (D76): the call's id and the path.</summary>
public sealed record ToolTouch(string Call, string Path);

/// <summary>
/// A workspace plan's line in the machine log (WSSETUP6, D94): its event and its fields, names, words and counts only,
/// never a sentence or a path. The catalogue is here, so every writer writes the same lines.
/// </summary>
public sealed record SetupLine(string Event, IReadOnlyList<(string Key, object? Value)> Data)
{
    /// <summary>The press wrote a plan.</summary>
    public static SetupLine Planned(string workspace, int repositories, int atOnce, int pilot) =>
        new("setup.planned", [("workspace", workspace), ("repositories", repositories), ("atOnce", atOnce), ("pilot", pilot)]);

    /// <summary>A tick published a repository's set-up.</summary>
    public static SetupLine Published(string workspace, string repository, string quest) =>
        new("setup.published", [("workspace", workspace), ("repository", repository), ("quest", quest)]);

    /// <summary>A tick skipped a repository the press refused, by the refusal's code.</summary>
    public static SetupLine Skipped(string workspace, string repository, string refusal) =>
        new("setup.skipped", [("workspace", workspace), ("repository", repository), ("refusal", refusal)]);

    /// <summary>The plan paused, by <see cref="SetupPausedBy"/>'s word.</summary>
    public static SetupLine Paused(string workspace, string by) => new("setup.paused", [("workspace", workspace), ("by", by)]);

    /// <summary>The person resumed it.</summary>
    public static SetupLine Resumed(string workspace) => new("setup.resumed", [("workspace", workspace)]);

    /// <summary>The person stopped it.</summary>
    public static SetupLine Stopped(string workspace) => new("setup.stopped", [("workspace", workspace)]);
}

/// <summary>What a workspace plan reads beyond the single press's facts (WSSETUP6), and where its lines go.</summary>
/// <remarks>An interface so the plan is held without a service, git or a spawn; <see cref="WorkspaceSetupWorld"/> is the real one.</remarks>
public interface IWorkspaceSetupWorld : ISetupWorld
{
    /// <summary>Every registry row, with the declaration it holds: <i>set up</i> is read from here.</summary>
    Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct);

    /// <summary>How many sessions failed on each quest, by quest id, as the snapshot derives them (DRV6).</summary>
    Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct);

    /// <summary>Every session record this machine keeps, with the repository it ran in: what §4.2 counts.</summary>
    Task<IReadOnlyList<SessionRun>> RunsAsync(CancellationToken ct);

    /// <summary>The files a session's tool calls touched, from its conversation record (D76); none where it has none.</summary>
    IReadOnlyList<ToolTouch> Touched(string session);

    /// <summary>A plan's line, for the machine log.</summary>
    void Said(SetupLine line);
}

/// <summary>The person's choices for a workspace press (D124 §4.5): each null or empty is the default.</summary>
public sealed record WorkspaceSetupOptions(
    int? AtOnce = null, int? Pilot = null, IReadOnlyList<string>? First = null, IReadOnlyList<string>? Skip = null);

/// <summary>One repository of a press's list: what touched it, where it stands, and the press's refusals now, where it is still to go.</summary>
public sealed record WorkspaceSetupRow(string Repository, SetupTouches Touches, SetupStanding Standing)
{
    public IReadOnlyList<SetupRefusal> Refusals { get; init; } = [];
}

/// <summary>What a workspace press would write, or why it would not: plan, never apply.</summary>
public sealed record WorkspaceSetupPreview(string Workspace)
{
    /// <summary>The repositories with a checkout here, in the order the plan would work them.</summary>
    public IReadOnlyList<WorkspaceSetupRow> Rows { get; init; } = [];

    /// <summary>The repositories the person unticked (<c>--skip</c>).</summary>
    public IReadOnlyList<string> Unticked { get; init; } = [];

    /// <summary>The workspace's rows with no checkout here: their own machine's driver sets them up.</summary>
    public IReadOnlyList<string> NoCheckout { get; init; } = [];

    public int AtOnce { get; init; } = WorkspaceSetup.DefaultAtOnce;

    public int Pilot { get; init; } = WorkspaceSetup.DefaultPilot;

    /// <summary>The driver's cap when this was read, which bounds <see cref="AtOnce"/>.</summary>
    public int Cap { get; init; }

    /// <summary>The agent this machine's driven sessions start on, which would carry each set-up.</summary>
    public string? Adapter { get; init; }

    /// <summary>Why the press as a whole would not write a plan; none is a press that writes.</summary>
    public IReadOnlyList<SetupRefusal> Refusals { get; init; } = [];

    public bool Pressable => Refusals.Count == 0 && Rows.Count > 0;
}

/// <summary>What a workspace press came to: the plan written, and the rules it added to the workspace's scope.</summary>
public sealed record WorkspaceSetupOutcome(bool Written, string Message, WorkspaceSetupPlan? Plan, IReadOnlyList<string> Added);

/// <summary>What a pause, a resume or a stop came to.</summary>
public sealed record WorkspaceSetupSteer(bool Ok, string Message, WorkspaceSetupPlan? Plan);

/// <summary>Why a workspace press as a whole would not write a plan (D124 §4.1, §4.5), beside the tools' own refusals.</summary>
public static class WorkspaceSetupRefusals
{
    /// <summary>No repository of the workspace has a checkout here.</summary>
    public const string NoRepository = "no-repository";

    /// <summary>A plan for the workspace already works: never two, so never two set-ups for one repository.</summary>
    public const string PlanWorking = "plan-working";

    /// <summary>More at once than the cap's <c>cap − 1</c>, or fewer than one.</summary>
    public const string AtOnce = "at-once";

    /// <summary>A repository named to go first or to be left out that is not one of the workspace's here, or named both ways.</summary>
    public const string Named = "named";

    /// <summary>The word a skip carries when the service refused the ask, beside the press's own codes (<see cref="SetupRefusals"/>).</summary>
    public const string ServiceRefused = "service-refused";
}

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
/// </remarks>
public static class WorkspaceSetup
{
    public const int DefaultAtOnce = 1;

    public const int DefaultPilot = 2;

    /// <summary>The states in the order the head line says them (§4.4).</summary>
    private static readonly SetupState[] Spoken =
    [
        SetupState.SetUp, SetupState.Waiting, SetupState.Open, SetupState.Parked, SetupState.ToGo, SetupState.Skipped,
        SetupState.Declined, SetupState.Deleted,
    ];

    /// <summary>The most set-ups open at once at a cap: never the last slot, so other work keeps one; one at a cap of one.</summary>
    public static int MostAtOnce(int cap) => Math.Max(1, cap - 1);

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

        var atOnce = options.AtOnce ?? DefaultAtOnce;
        var most = MostAtOnce(config.Cap);
        if (atOnce < 1 || atOnce > most)
        {
            refusals.Add(new(WorkspaceSetupRefusals.AtOnce, atOnce < 1
                ? "at least one set-up must be open at once, or the plan would never ask anything."
                : $"{atOnce} at once is more than this machine's cap of {config.Cap} leaves: at most {most}, so other work always "
                  + "keeps a slot. Raise the cap with `daoris driver cap <n>` first, or ask for fewer."));
        }

        var pilot = Math.Max(0, options.Pilot ?? DefaultPilot);
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
        var tools = SetupPress.ToolRefusals(await world.ToolsAsync(ct).ConfigureAwait(false));
        refusals.AddRange(tools);

        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var runs = await world.RunsAsync(ct).ConfigureAwait(false);
        var touches = SetupOrder.Count(here, rows, quests, runs, world.Touched);
        var order = SetupOrder.Order(here, touches, first, skip);
        var strikes = await world.StrikesAsync(ct).ConfigureAwait(false);
        var standings = Stand(new WorkspaceSetupPlan(spelled, order, atOnce, pilot), rows, quests, strikes, config);

        var listed = new List<WorkspaceSetupRow>();
        foreach (var standing in standings)
        {
            var row = new WorkspaceSetupRow(standing.Repository, touches.GetValueOrDefault(standing.Repository) ?? SetupTouches.None, standing);
            if (standing.State == SetupState.ToGo && tools.Count == 0)
            {
                var judged = await SetupPress.PlanAsync(world, config, door, standing.Repository, day, ct).ConfigureAwait(false);
                row = row with { Refusals = [.. judged.Refusals.Where(refusal => !SetupPress.IsToolRefusal(refusal))] };
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
        var added = SetupPress.Rules.Where(rule => !held.Contains(rule)).ToList();
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
    /// lines a look's report carries; a home with no plan to work reads nothing.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TickAsync(
        IWorkspaceSetupWorld world, DriverConfig config, SessionWire door, string home, DateOnly day, DateTimeOffset now,
        CancellationToken ct = default)
    {
        var plans = WorkspaceSetupFile.All(home).Where(plan => plan is { Live: true, Paused: null }).ToList();
        if (plans.Count == 0) return [];

        var rows = await world.RegistrationsAsync(ct).ConfigureAwait(false);
        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var strikes = await world.StrikesAsync(ct).ConfigureAwait(false);
        var said = new List<string>();
        foreach (var plan in plans)
        {
            await WorkAsync(plan, Stand(plan, rows, quests, strikes, config)).ConfigureAwait(false);
        }

        return said;

        async Task WorkAsync(WorkspaceSetupPlan plan, IReadOnlyList<SetupStanding> standings)
        {
            var workspace = plan.Workspace;
            var open = standings.Count(standing => standing.State == SetupState.Open);
            var asked = plan.Order.Count(plan.Published.ContainsKey);

            if (PilotHolds(plan, asked))
            {
                var pilot = plan.Order.Where(plan.Published.ContainsKey).Take(plan.Pilot).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var flown = standings.Where(standing => pilot.Contains(standing.Repository)).ToList();
                if (flown.All(standing => standing.State is not (SetupState.Open or SetupState.Parked)))
                {
                    var sentence = $"paused after the pilot: {string.Join(", ", flown.Select(standing => $"`{standing.Repository}` {Word(standing.State)}"))}. "
                        + $"Look at what each wrote and what it cost, then `daoris-driver setup --workspace {Spelled(workspace)} --resume` carries on.";
                    if (PauseItself(home, workspace, new SetupPause(SetupPausedBy.Pilot, sentence, now), current => !current.PilotResumed))
                    {
                        world.Said(SetupLine.Paused(workspace, SetupPausedBy.Pilot));
                        said.Add($"setup  {workspace}: {sentence}");
                    }
                }

                return;
            }

            var most = Math.Min(Math.Max(1, plan.AtOnce), MostAtOnce(config.Cap));
            foreach (var next in standings.Where(standing => standing.State == SetupState.ToGo))
            {
                if (open >= most || PilotHolds(plan, asked)) return;

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
                        said.Add($"setup  {workspace}: asked `{next.Repository}` to set itself up, #{quest} ({open} of {most} at once).");
                        continue;
                    }

                    Skip(workspace, next.Repository, WorkspaceSetupRefusals.ServiceRefused, outcome.Message);
                    continue;
                }

                // The machine's tools refuse every repository alike: pause, and say why, rather than skip each in turn.
                if (press.Refusals.FirstOrDefault(SetupPress.IsToolRefusal) is { } tool)
                {
                    if (PauseItself(home, workspace, new SetupPause(SetupPausedBy.Tool, tool.Sentence, now), _ => true))
                    {
                        world.Said(SetupLine.Paused(workspace, SetupPausedBy.Tool));
                        said.Add($"setup  {workspace}: paused: {tool.Sentence} `daoris-driver setup --workspace {Spelled(workspace)} --resume` carries on once it is.");
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
            said.Add($"setup  {workspace}: skipped `{repository}`: {sentence}");
        }
    }

    /// <summary>
    /// Where each repository of <paramref name="plan"/> stands, in its order, read from the facts (D124 §4.1): set up from the
    /// registry; open, parked, waiting for review, declined or deleted from its set-up quests and their strikes; skipped from
    /// the plan's own record; else to go. A set-up open or taken, the plan's or the single press's, is read first, so no
    /// repository is ever asked twice.
    /// </summary>
    public static IReadOnlyList<SetupStanding> Stand(
        WorkspaceSetupPlan plan, IReadOnlyList<RegistrationRow> rows, IReadOnlyList<QuestView> quests,
        IReadOnlyDictionary<string, int> strikes, DriverConfig config)
    {
        var standings = new List<SetupStanding>();
        foreach (var repository in plan.Order)
        {
            standings.Add(One(repository));
        }

        return standings;

        SetupStanding One(string repository)
        {
            if (rows.FirstOrDefault(row => Same(row.Repository, repository)) is { Registered: true } row)
            {
                return new(repository, SetupState.SetUp, null, $"set up: {row.Summary?.Trim() ?? "adopted, and declaring what it owns"}");
            }

            var setups = quests.Where(quest => Same(quest.To, repository) && SetupQuests.IsSetup(quest.Title)).ToList();
            if (setups.LastOrDefault(quest => quest.Status is "Open" or "Taken") is { } active)
            {
                var failed = (strikes.TryGetValue(active.Id, out var count) ? count : 0) - config.ForgivenAt(active.Id);
                // Parked as the planner parks a quest (DRV6): its failed sessions, less where the person last restarted it.
                return config.Strikes > 0 && failed >= config.Strikes
                    ? new(repository, SetupState.Parked, active.Id,
                        $"its set-up #{active.Id} is parked: {failed} session(s) failed on it. `daoris driver retry {active.Id}` "
                        + "starts it again once you know why.")
                    : new(repository, SetupState.Open, active.Id, $"its set-up #{active.Id} is {active.Status.ToLowerInvariant()}.");
            }

            var mine = plan.Published.TryGetValue(repository, out var published)
                ? setups.FirstOrDefault(quest => Same(quest.Id, published))
                : null;
            switch ((mine ?? setups.LastOrDefault())?.Status)
            {
                case "Done":
                    var done = mine ?? setups.Last();
                    return new(repository, SetupState.Waiting, done.Id,
                        $"its set-up #{done.Id} is done and waits for your review; once it is merged, *Bring up to date* registers it.");
                case "Declined":
                    var declined = mine ?? setups.Last();
                    return new(repository, SetupState.Declined, declined.Id,
                        $"its set-up #{declined.Id} was declined{(declined.Note is { Length: > 0 } note ? $": {note.Trim()}" : ".")} "
                        + $"The plan does not ask it again; `daoris-driver setup {repository}` asks it alone.");
            }

            if (published is not null)
            {
                return new(repository, SetupState.Deleted, published,
                    $"its set-up #{published} was deleted. The plan does not ask it again; `daoris-driver setup {repository}` asks it alone.");
            }

            return plan.Skipped.TryGetValue(repository, out var skipped)
                ? new(repository, SetupState.Skipped, null, skipped.Said)
                : new(repository, SetupState.ToGo, null, "to go.");
        }
    }

    /// <summary>Where each repository of <paramref name="plan"/> stands now, read from the world.</summary>
    public static async Task<IReadOnlyList<SetupStanding>> StandAsync(
        IWorkspaceSetupWorld world, DriverConfig config, WorkspaceSetupPlan plan, CancellationToken ct = default) =>
        Stand(
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

    /// <summary>The head line (D124 §4.4): how many stand in each state, in the design's words, then whether it is paused or stopped.</summary>
    public static string Summary(WorkspaceSetupPlan plan, IReadOnlyList<SetupStanding> standings)
    {
        var counted = Spoken
            .Select(state => (State: state, Count: standings.Count(standing => standing.State == state)))
            .Where(each => each.Count > 0)
            .Select(each => $"{each.Count} {Word(each.State)}")
            .ToList();
        var tail = !plan.Live ? " — stopped"
            : plan.Paused?.By switch
            {
                SetupPausedBy.Pilot => " — paused after the pilot",
                SetupPausedBy.Person => " — paused by you",
                SetupPausedBy.Tool => " — paused: the doctrine tool cannot run here",
                null => "",
                _ => " — paused",
            };
        return $"Setting up — {(counted.Count == 0 ? "nothing to set up" : string.Join(" · ", counted))}{tail}";
    }

    /// <summary>A state in the words a person reads (§4.4).</summary>
    public static string Word(SetupState state) => state switch
    {
        SetupState.SetUp => "set up",
        SetupState.Waiting => "waiting for your review",
        SetupState.Open => "setting up",
        SetupState.Parked => "parked",
        SetupState.ToGo => "to go",
        SetupState.Skipped => "skipped",
        SetupState.Declined => "declined",
        _ => "deleted",
    };

    private static bool PilotHolds(WorkspaceSetupPlan plan, int asked) => plan.Pilot > 0 && !plan.PilotResumed && asked >= plan.Pilot;

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
