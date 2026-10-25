using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What the steward's acts read and change (STEWARDROLE1b, D209 §3.1 rows 1 to 8): the driver's file, the registry, a session's record,
/// this instance's processes, the planner's verdicts and the moves of a parked session. The real one is the driver bridge's
/// (<see cref="DriverStewardWorld"/>); a test hands a stand-in, so no act starts a process, a window or an agent.
/// </summary>
public interface IStewardWorld
{
    /// <summary>The driver's file as it stands now.</summary>
    DriverConfig Config();

    /// <summary>An edit of the driver's file, and the loop nudged.</summary>
    void Change(Func<DriverConfig, DriverConfig> change);

    /// <summary>Whether the service's registry on this machine names <paramref name="repository"/>.</summary>
    Task<bool> RegisteredAsync(string repository, CancellationToken ct);

    /// <summary>The workspace the registry places <paramref name="repository"/> in, or null.</summary>
    Task<string?> WorkspaceOfAsync(string repository, CancellationToken ct);

    /// <summary>The session's record on this machine, or null where none has the id.</summary>
    Task<PriorSession?> SessionAsync(string id, CancellationToken ct);

    /// <summary>A quest's status as the service answers it, or null where it holds none.</summary>
    Task<string?> QuestStatusAsync(string quest, CancellationToken ct);

    /// <summary>Whether this instance runs the session's process.</summary>
    bool RunsHere(string id);

    /// <summary>Whether a process of the session is alive on this machine, here or in another Daoris process.</summary>
    bool AliveOnThisMachine(string id);

    /// <summary>Whether <paramref name="tree"/> is a tree this home opened (D51).</summary>
    bool OwnTree(string? tree);

    /// <summary>The live session stopped by this instance's own stop, its note handed in at the call (§5.1a): never a request file.</summary>
    bool StopHere(string id, Noted note);

    /// <summary>A parked session stopped by the steward, moved by the ledger only from <c>awaiting-person</c>.</summary>
    Task<string> StopParkedAsync(string id, CancellationToken ct);

    /// <summary>A parked session finished by the steward, moved by the ledger only from <c>awaiting-person</c>.</summary>
    Task<string> FinishAsync(string id, CancellationToken ct);

    /// <summary>The planner's verdicts as <c>RETRY_QUEST</c> reads them: the last look's, else a fresh plan.</summary>
    Task<IReadOnlyList<Consideration>> VerdictsAsync(CancellationToken ct);
}

/// <summary>The steward's acts over the driver bridge's own loop and service (STEWARDROLE1b).</summary>
public sealed class DriverStewardWorld(DriverContext context) : IStewardWorld
{
    private DriverLoop Loop => context.Loop;

    private ServiceClient Service => Loop.Service ?? throw DriverArguments.NotReady();

    public DriverConfig Config() => DriverConfig.Load(Loop.ConfigPath);

    public void Change(Func<DriverConfig, DriverConfig> change) => context.Change(change);

    public async Task<bool> RegisteredAsync(string repository, CancellationToken ct) =>
        (await Service.RegistryAsync(ct).ConfigureAwait(false)).Any(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));

    public async Task<string?> WorkspaceOfAsync(string repository, CancellationToken ct) =>
        (await Service.RegistryAsync(ct).ConfigureAwait(false))
        .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;

    public Task<PriorSession?> SessionAsync(string id, CancellationToken ct) => Service.RecordAsync(id, ct);

    public async Task<string?> QuestStatusAsync(string quest, CancellationToken ct) =>
        (await Service.FindQuestAsync(quest, ct).ConfigureAwait(false))?.Status;

    public bool RunsHere(string id) => Loop.Processes.RunsHere(id);

    public bool AliveOnThisMachine(string id) => Loop.Processes.AliveOnThisMachine(id);

    public bool OwnTree(string? tree) => tree is { Length: > 0 } && new SessionTrees(Loop.Home).Holds(tree);

    public bool StopHere(string id, Noted note)
    {
        var stopped = Loop.Processes.Stop(id, note: note);
        Loop.Nudge();
        return stopped;
    }

    public async Task<string> StopParkedAsync(string id, CancellationToken ct)
    {
        var said = await SessionMoves.StopParkedByStewardAsync(session => Loop.Processes.Stop(session), Service, id, ct).ConfigureAwait(false);
        Loop.Nudge();
        return said;
    }

    public async Task<string> FinishAsync(string id, CancellationToken ct)
    {
        var said = await SessionMoves.FinishByStewardAsync(Service, id, ct).ConfigureAwait(false);
        Loop.Nudge();
        return said;
    }

    public async Task<IReadOnlyList<Consideration>> VerdictsAsync(CancellationToken ct)
    {
        var config = Config();
        return Loop.Look.Latest
               ?? await SessionGroups.VerdictsAsync(Service, config, context.Door(config), lastLook: null, ct).ConfigureAwait(false);
    }
}

/// <summary>The steward's acts by the names <c>ROLE_ACT</c>'s <c>act</c> carries (D209 §3.1).</summary>
public static class StewardActNames
{
    public const string Drive = "drive";
    public const string Undrive = "undrive";
    public const string Trees = "trees";
    public const string Hold = "hold";
    public const string Resume = "resume";
    public const string Stop = "stop";
    public const string Finish = "finish";
    public const string Retry = "retry";
}

/// <summary>The stages of the steward's pending operations (D209 §5.1a): a Try again's release, which the planner's start takes.</summary>
public static class StewardStages
{
    public const string Released = "released";
}

/// <summary>Why a steward's act is refused by its conditions (D209 §3.4 step 6), each said by its code.</summary>
public static class StewardWhy
{
    public const string Unregistered = "unregistered";
    public const string LandingPushes = "landing-pushes";
    public const string NoTree = "no-tree";
    public const string NotOwn = "not-own";
    public const string RunningHere = "running-here";
    public const string RunningElsewhere = "running-elsewhere";
    public const string NotQuestSession = "not-quest-session";
    public const string WordsWaiting = "words-waiting";
    public const string NotParked = "not-parked";
    public const string Ended = "ended";
}

/// <summary>
/// The steward's acts of D209 §3.1 rows 1 to 8 (STEWARDROLE1b), each one route's exact form, its conditions judged under the gates of its
/// target and its dependencies immediately before its handler, and its handler's effect recorded as the steward's.
/// </summary>
public static class StewardActRows
{
    /// <summary>Every row, in §3.1's order.</summary>
    public static IReadOnlyList<IRoleAct> All { get; } =
    [
        new Setting(StewardActNames.Drive, "drivable", true, pushes: true, noTree: true),
        new Setting(StewardActNames.Undrive, "drivable", false, pushes: false, noTree: false),
        new Setting(StewardActNames.Trees, "ownTree", true, pushes: true, noTree: false),
        new Setting(StewardActNames.Hold, "held", true, pushes: false, noTree: false),
        new Setting(StewardActNames.Resume, "held", false, pushes: false, noTree: false),
        new StopAct(),
        new FinishAct(),
        new RetryAct(),
    ];

    /// <summary>
    /// Whether <paramref name="form"/> is exactly <paramref name="fields"/>: every name read as D201's <c>IsForm</c> reads it (ignoring case,
    /// each once), one the form names, its value the one allowed, and every name there.
    /// </summary>
    internal static bool Exact(JsonElement form, IReadOnlyDictionary<string, Func<JsonElement, bool>> fields)
    {
        if (form.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in form.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !fields.TryGetValue(property.Name, out var allowed) || !allowed(property.Value)) return false;
        }

        return seen.Count == fields.Count;
    }

    internal static bool Named(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Trim().Length == text.Length;

    /// <summary>A field's text, its name read without case.</summary>
    internal static string Text(JsonElement form, string name) =>
        form.EnumerateObject().First(each => string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase)).Value.GetString()!;

    /// <summary>Whether the effective landing of <paramref name="repository"/> is a plugin that pushes (§3.1 rows 1 and 3).</summary>
    internal static async Task<bool> PushesAsync(IStewardWorld world, DriverConfig config, string repository, CancellationToken ct) =>
        LandingRules.Effective(config, repository, await world.WorkspaceOfAsync(repository, ct).ConfigureAwait(false)).Rule
            is { Form: LandingForm.Branch, Plugin: not null };

    /// <summary>
    /// Whether moving <paramref name="record"/> to <paramref name="state"/> would make a pushed landing due (§3.1 rows 6 and 7): its quest
    /// <c>Done</c>, the effective rule accepting by itself through a plugin that pushes, and the record in a tree this home opened.
    /// </summary>
    internal static async Task<bool> ReleasesPushAsync(IStewardWorld world, PriorSession record, string state, CancellationToken ct)
    {
        if (record.Quest is not { } quest || record.Repository is not { } repository) return false;
        var status = await world.QuestStatusAsync(quest, ct).ConfigureAwait(false);
        if (status != "Done") return false;
        var landing = LandingRules.Effective(world.Config(), repository, await world.WorkspaceOfAsync(repository, ct).ConfigureAwait(false));
        return landing.Rule.Plugin is not null && AutoLandingRules.Concluded(landing, status, world.OwnTree(record.Tree), state) == AutoConcluded.Due;
    }

    /// <summary>A quest's own session (§3.1): driven, with a quest, never a conversation's, an intake's, a teammate's, a helper's or a partner's.</summary>
    internal static bool QuestSession(PriorSession record) =>
        record is { Kind: not "chat", Quest: not null, Ask: null, Teammate: false, Serves: null, Piece: null };

    private static IStewardWorld WorldOf(RoleActRun run) => run.World ?? throw new InvalidOperationException("no world was handed to the act.");

    /// <summary>Rows 1 to 5: a repository's standing choice, <c>{ repository, &lt;field&gt;: &lt;value&gt; }</c>.</summary>
    private sealed class Setting(string name, string field, bool value, bool pushes, bool noTree) : IRoleAct
    {
        public string Name => name;

        public bool IsForm(JsonElement form) => Exact(form, new Dictionary<string, Func<JsonElement, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["repository"] = Named,
            [field] = each => each.ValueKind == (value ? JsonValueKind.True : JsonValueKind.False),
        });

        public string Target(JsonElement form) => Text(form, "repository");

        public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct)
        {
            var world = WorldOf(run);
            var repository = Target(run.Form);
            if (!await world.RegisteredAsync(repository, ct).ConfigureAwait(false)) return StewardWhy.Unregistered;
            var config = world.Config();
            if (pushes && await PushesAsync(world, config, repository, ct).ConfigureAwait(false)) return StewardWhy.LandingPushes;
            // Row 1: trees are opt-in, so a repository whose sessions open none works in the owner's checkout (D51): the owner's to drive.
            if (noTree && !config.OpensOwnTree(repository)) return StewardWhy.NoTree;
            // Row 5: only a hold the steward made this run that nothing has touched since (D201's rule, its memory the role's).
            if (name == StewardActNames.Resume
                && (!run.Judge.StewardHolds(repository) || !config.Holds.Contains(repository, StringComparer.OrdinalIgnoreCase)))
            {
                return StewardWhy.NotOwn;
            }

            return null;
        }

        public Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
        {
            var repository = Target(run.Form);
            WorldOf(run).Change(config => name switch
            {
                StewardActNames.Drive or StewardActNames.Undrive => config.WithDrivable(repository, value),
                StewardActNames.Trees => config.WithTrees(repository, true),
                _ => config.WithHold(repository, value),
            });
            run.Judge.StewardDid(run.Id, name, repository: repository);
            return Task.FromResult<object?>(new { Act = name, Repository = repository });
        }
    }

    /// <summary>Row 6: D201's stop form, <c>{ id, said: true }</c>, run in the instance, never through the request folder.</summary>
    private sealed class StopAct : IRoleAct
    {
        public string Name => StewardActNames.Stop;

        public string TargetKind => OwnerKinds.Session;

        public bool IsForm(JsonElement form) => StandingConfirmation.IsForm(StandingPresses.Stop, form);

        public string Target(JsonElement form) => Text(form, "id");

        public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct)
        {
            var world = WorldOf(run);
            var id = Target(run.Form);
            if (await world.SessionAsync(id, ct).ConfigureAwait(false) is not { } record || !QuestSession(record)) return StewardWhy.NotQuestSession;
            if (!record.Parked && !record.Running) return StewardWhy.Ended;
            // The driver's own stop reaches only this instance's processes; one another Daoris process runs is refused with nothing written.
            if (!record.Parked && !world.RunsHere(id)) return StewardWhy.RunningElsewhere;
            if (await ReleasesPushAsync(world, record, "stopped", ct).ConfigureAwait(false)) return StewardWhy.LandingPushes;
            return null;
        }

        public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
        {
            var world = WorldOf(run);
            var id = Target(run.Form);
            var record = await world.SessionAsync(id, ct).ConfigureAwait(false);
            string said;
            if (record is { Parked: true })
            {
                said = await world.StopParkedAsync(id, ct).ConfigureAwait(false);
            }
            else
            {
                if (!world.StopHere(id, Observation.StoppedBySteward)) return new { Act = Name, Session = id, Stopped = false };
                said = "stopped";
            }

            run.Judge.StewardDid(run.Id, Name, record?.Repository, record?.Quest, id);
            return new { Act = Name, Session = id, Stopped = true, Said = said };
        }
    }

    /// <summary>Row 7: D201's finish form, <c>{ id, state: "completed", said: true }</c>, moved only from <c>awaiting-person</c>.</summary>
    private sealed class FinishAct : IRoleAct
    {
        public string Name => StewardActNames.Finish;

        public string TargetKind => OwnerKinds.Session;

        public bool IsForm(JsonElement form) => StandingConfirmation.IsForm(StandingPresses.Finish, form);

        public string Target(JsonElement form) => Text(form, "id");

        public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct)
        {
            var world = WorldOf(run);
            var id = Target(run.Form);
            if (await world.SessionAsync(id, ct).ConfigureAwait(false) is not { } record || !QuestSession(record)) return StewardWhy.NotQuestSession;
            if (world.RunsHere(id) || world.AliveOnThisMachine(id)) return StewardWhy.RunningHere;
            if (!record.Parked) return StewardWhy.NotParked;
            if (record.Waiting.Count > 0) return StewardWhy.WordsWaiting;
            if (await ReleasesPushAsync(world, record, "completed", ct).ConfigureAwait(false)) return StewardWhy.LandingPushes;
            return null;
        }

        public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
        {
            var world = WorldOf(run);
            var id = Target(run.Form);
            var record = await world.SessionAsync(id, ct).ConfigureAwait(false);
            // It neither accepts nor closes its quest (finding 14): the record alone moves, the quest stays as it is for the owner.
            var said = await world.FinishAsync(id, ct).ConfigureAwait(false);
            run.Judge.StewardDid(run.Id, Name, record?.Repository, record?.Quest, id);
            return new { Act = Name, Session = id, Said = said };
        }
    }

    /// <summary>
    /// Row 8: <c>{ quest }</c>, releasing only the steward's own stop of this run, written in <c>roleReleases</c> and a pending operation
    /// the planner's start takes (§5.1a).
    /// </summary>
    private sealed class RetryAct : IRoleAct
    {
        public string Name => StewardActNames.Retry;

        public string TargetKind => OwnerKinds.Quest;

        public bool IsForm(JsonElement form) =>
            Exact(form, new Dictionary<string, Func<JsonElement, bool>>(StringComparer.OrdinalIgnoreCase) { ["quest"] = Named });

        public string Target(JsonElement form) => QuestOf(form);

        private static string QuestOf(JsonElement form) => Text(form, "quest").TrimStart('#');

        public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct) =>
            await StopOfAsync(run, ct).ConfigureAwait(false) is null ? StewardWhy.NotOwn : null;

        // The session of the steward's own stop the verdict holds the quest by, where Try again would release it (`Released`), else null.
        private static async Task<string?> StopOfAsync(RoleActRun run, CancellationToken ct)
        {
            var quest = QuestOf(run.Form);
            if (run.Judge.StewardStopOf(quest) is not { } own) return null;
            var verdicts = await WorldOf(run).VerdictsAsync(ct).ConfigureAwait(false);
            RetryDecision decision;
            try
            {
                decision = QuestRetry.Decide(quest, verdicts);
            }
            catch (ChoiceRefusal)
            {
                return null;
            }

            var held = verdicts.FirstOrDefault(each => string.Equals(each.Quest.Id, quest, StringComparison.OrdinalIgnoreCase))?.HeldBy;
            return decision.Did == RetryDecision.Released && held is { StewardStopped: true } && string.Equals(held.Session, own, StringComparison.Ordinal)
                ? own
                : null;
        }

        public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
        {
            var quest = Target(run.Form);
            var session = run.Judge.StewardStopOf(quest)!;
            run.Judge.Pend(run.Id, Name, [StewardStages.Released], binds: false, new { Quest = quest, Session = session });
            WorldOf(run).Change(config => config.WithRoleReleased(quest, new RoleRelease(RoleReleaseKinds.Stop, session, Roles.Steward, run.Id)));
            run.Judge.StewardDid(run.Id, Name, quest: quest, session: session);
            await Task.CompletedTask.ConfigureAwait(false);
            return new { Act = Name, Quest = quest, Session = session };
        }
    }
}

/// <summary>
/// The run's memory of the steward's acts as the driver's starts read it (STEWARDROLE1b, D209 §3.1a, §5.1a): the judge's own, a role's
/// release withdrawn from the driver's file when its operation is taken or ended.
/// </summary>
public sealed class RoleStarts(StandingConfirmation judge, string configPath) : IRoleStarts
{
    public bool Caused(string repository, string quest) => judge.StewardCaused(repository, quest);

    public bool Released(string quest, RoleRelease release) =>
        release.By == Roles.Steward && judge.StewardReleased(quest, release.Act);

    public bool Take(string quest, RoleRelease release)
    {
        var taken = release.By == Roles.Steward && judge.TakeRelease(quest, release.Act) is not null;
        Withdraw(quest);
        return taken;
    }

    public void HeldForOwner(string quest)
    {
        judge.HeldForOwner(quest);
        Withdraw(quest);
    }

    private void Withdraw(string quest)
    {
        var config = DriverConfig.Load(configPath);
        if (config.RoleReleaseFor(quest) is not null) config.WithoutRoleRelease(quest).Save(configPath);
    }

    /// <summary>
    /// A revoke's or a start's sweep of the driver's file (§5.1a): every role's release whose operation this run's memory no longer holds at
    /// <c>released</c> withdrawn, the owner's own releases kept whole. Answers how many it withdrew, for the log.
    /// </summary>
    public static int Sweep(StandingConfirmation judge, string configPath)
    {
        var config = DriverConfig.Load(configPath);
        var gone = config.RoleReleases.Where(pair => !(pair.Value.By == Roles.Steward && judge.StewardReleased(pair.Key, pair.Value.Act)))
            .Select(pair => pair.Key).ToList();
        if (gone.Count == 0) return 0;
        foreach (var quest in gone) config = config.WithoutRoleRelease(quest);
        config.Save(configPath);
        return gone.Count;
    }
}
