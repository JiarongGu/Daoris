namespace Daoris.Driver;

/// <summary>
/// The person's standing choices, machine-local by nature (D46 §6/§7): which repositories THIS machine
/// may drive, what is held, how wide the loop runs, and which adapter spawns. Read by <see cref="DriverConfigReader"/>, written by
/// <see cref="DriverConfigWriter"/>, loaded and edited onto the file as it stands by <see cref="DriverConfigDisk"/>.
/// </summary>
/// <param name="Drivable">Repositories the person opted in. Empty means the driver drives nothing.</param>
/// <param name="Holds">
/// Repositories the person holds: drivable, and nothing new starts there until they resume it, while what runs goes on.
/// A hold, never a pause: a pause is an ask's or a quest's, and stops what runs.
/// </param>
/// <param name="Cap">Concurrent sessions across all repositories.</param>
/// <param name="PollSeconds">How often the watch loop ticks.</param>
/// <param name="Commands">Per-adapter command configuration — what the stub runs.</param>
/// <param name="Trees">Repositories whose sessions open their OWN worktree instead of the registered root (D51). Empty is the default.</param>
/// <param name="Notify">
/// Whether this machine interrupts the person when a session parks or ends unasked. <b>On by default</b>: a driver that ran
/// silently would leave parked sessions sitting until somebody thinks to look.
/// </param>
public sealed record DriverConfig(
    IReadOnlyList<string> Drivable,
    IReadOnlyList<string> Holds,
    int Cap,
    int PollSeconds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Commands,
    IReadOnlyList<string> Trees,
    bool Notify = true,
    int Strikes = 3,
    IReadOnlyDictionary<string, int>? Forgiven = null)
{
    /// <summary>How many failed sessions park a quest. <c>0</c> never parks: for a person who wants the loop to keep trying.</summary>
    public int Strikes { get; init; } = Math.Max(0, Strikes);

    /// <summary>
    /// Quests the person has restarted, and the failure count they were restarted at. A mark rather
    /// than an erasure: the records still say what happened, and the next three failures park it again.
    /// </summary>
    public IReadOnlyDictionary<string, int> Forgiven { get; init; } =
        Forgiven ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>How long a session may be quiet before Daoris looks in, as the file sets it; null is the default (<see cref="QuietSetting"/>).</summary>
    public int? QuietMinutes { get; init; }

    /// <summary>Whether an update drains the look: set only by <see cref="InstallUpdate.Drained"/>, never read or written.</summary>
    public bool Draining { get; init; }

    /// <summary>The tests' one bound on a driven run, in minutes, so a hung stand-in fails fast; never read or written.</summary>
    internal int? TestBound { get; init; }

    /// <summary>Drives nothing, holds nothing — the safe shape silence takes.</summary>
    public static DriverConfig Empty { get; } = new(
        Drivable: [], Holds: [], Cap: 2,
        PollSeconds: 15,
        Commands: new Dictionary<string, IReadOnlyList<string>>(),
        Trees: []);

    /// <summary>The failure count this quest was last restarted at — zero when it never was.</summary>
    public int ForgivenAt(string questId) =>
        Forgiven.TryGetValue(questId, out var mark) ? mark : 0;

    /// <summary>Let this quest run again, counting from where it stands now.</summary>
    public DriverConfig WithForgiven(string questId, int at)
    {
        var next = new Dictionary<string, int>(Forgiven, StringComparer.OrdinalIgnoreCase)
        {
            [questId] = Math.Max(0, at),
        };
        return this with { Forgiven = next };
    }

    /// <summary>How many failures park a quest on this machine.</summary>
    public DriverConfig WithStrikes(int strikes) => this with { Strikes = Math.Max(0, strikes) };

    /// <summary>
    /// The quests the person released from their stop, each against the session they stopped: a later stop holds the quest again,
    /// since its session differs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Released { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The session whose stop of this quest the person released, or null when they released none.</summary>
    public string? ReleasedFor(string questId) => Released.TryGetValue(questId, out var session) ? session : null;

    /// <summary>Whether the person released this stop: the quest in any case, the session as its record spells it.</summary>
    public bool Releases(string questId, string session) => string.Equals(ReleasedFor(questId), session, StringComparison.Ordinal);

    /// <summary>Release this quest from the person's stop of this session: one release per quest, the later replacing the earlier.</summary>
    /// <exception cref="DriverException">A blank quest or session: a release names the stop it releases.</exception>
    public DriverConfig WithReleased(string questId, string session) => DriverConfigEdits.Released(this, questId, session);

    /// <summary>
    /// The transient holds the person released with <i>Try again</i>, each quest against the record's session and <b>the end it
    /// releases</b> (its <c>updated</c> as the press read it). Not keyed by session alone, as <see cref="Released"/> is: a released
    /// resume continues the same record, so the next exhaustion would pass too and the line would cycle with no strike.
    /// </summary>
    public IReadOnlyDictionary<string, TransientRelease> TransientReleased { get; init; } =
        new Dictionary<string, TransientRelease>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the person released this quest's transient hold of <paramref name="session"/> ended at <paramref name="ended"/>: both
    /// equal to the release's. An end that is unsaid passes nothing: the check fails closed.
    /// </summary>
    public bool ReleasesTransient(string questId, string session, DateTimeOffset? ended) =>
        ended is { } at
        && TransientReleased.TryGetValue(questId, out var release)
        && string.Equals(release.Session, session, StringComparison.Ordinal)
        && release.Ended == at;

    /// <summary>Release this quest's transient hold of this session's end (<i>Try again</i>): one per quest, the later replacing the earlier.</summary>
    /// <exception cref="DriverException">A blank quest or session: a release names the end it releases.</exception>
    public DriverConfig WithTransientReleased(string questId, string session, DateTimeOffset ended) =>
        DriverConfigEdits.TransientReleased(this, questId, session, ended);

    /// <summary>
    /// A role's releases, apart from the owner's, whose writers take no author: an older build finds no release of the steward's to take
    /// as the owner's. The planner reads an entry as a release only where this run's memory holds its operation at <c>released</c>
    /// (<see cref="IRoleStarts"/>); none outlives a restart.
    /// </summary>
    public IReadOnlyDictionary<string, RoleRelease> RoleReleases { get; init; } =
        new Dictionary<string, RoleRelease>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This quest's role release, or null where none is written.</summary>
    public RoleRelease? RoleReleaseFor(string questId) => RoleReleases.GetValueOrDefault(DriverConfigEdits.Quest(questId));

    /// <summary>A role's release of this quest written, one per quest, the later replacing the earlier.</summary>
    /// <exception cref="DriverException">A blank quest, session or act: a role's release names what it releases and the act that made it.</exception>
    public DriverConfig WithRoleReleased(string questId, RoleRelease release) => DriverConfigEdits.RoleReleased(this, questId, release);

    /// <summary>This quest's role release withdrawn (a cancel, a consumer's take, a restart's first look); unchanged where it has none.</summary>
    public DriverConfig WithoutRoleRelease(string questId) => DriverConfigEdits.WithoutRoleRelease(this, questId);

    /// <summary>
    /// The set-up quests admitted alone, each against the repository it was published to: a set-up pressed where its repository is not
    /// driven starts by its own id, while every other quest of that repository stays held as not driven (<c>SetupAdmission.Admits</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string> Admitted { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The repository this quest is admitted alone in, or null where it is admitted in none.</summary>
    public string? AdmittedIn(string questId) => Admitted.TryGetValue(DriverConfigEdits.Quest(questId), out var repository) ? repository : null;

    /// <summary>A set-up quest admitted alone in <paramref name="repository"/>, one per quest, the later replacing the earlier.</summary>
    /// <exception cref="DriverException">A blank quest or repository: an admission names the quest and where it starts.</exception>
    public DriverConfig WithAdmitted(string questId, string repository) => DriverConfigEdits.Admitted(this, questId, repository);

    /// <summary>
    /// This quest's releases, its restart mark and its admission dropped: once a quest is closed it is never planned again, so none says
    /// anything any more. A quest-keyed entry added to this file joins <see cref="DriverConfigEdits.WithoutQuest"/> and
    /// <c>HistoryHome</c>'s purge of quests no store holds, and one that starts work joins <c>InstallUpdate.Drained</c>.
    /// </summary>
    public DriverConfig WithoutQuest(string questId) => DriverConfigEdits.WithoutQuest(this, questId);

    /// <summary>
    /// The asks the person paused on this machine, each with when and the stops its pause made. The look holds each paused work's quests
    /// (<see cref="PausedWork"/>, <see cref="StartVerdict.Paused"/>), and <see cref="WorkPausing"/> alone writes it; every other writer
    /// keeps it as written.
    /// </summary>
    public IReadOnlyDictionary<string, WorkPause> PausedAsks { get; init; } =
        new Dictionary<string, WorkPause>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The quests the person paused on this machine, on their own: read and written as <see cref="PausedAsks"/>.</summary>
    public IReadOnlyDictionary<string, WorkPause> PausedQuests { get; init; } =
        new Dictionary<string, WorkPause>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This ask's pause, or null where it is not paused here: the id as a person writes it, matched in any case.</summary>
    public WorkPause? PausedAsk(string ask) => PausedAsks.GetValueOrDefault(DriverConfigEdits.PauseId(ask));

    /// <summary>This quest's own pause, or null: a quest in a paused ask's work is that ask's pause, not one of its own.</summary>
    public WorkPause? PausedQuest(string quest) => PausedQuests.GetValueOrDefault(DriverConfigEdits.PauseId(quest));

    /// <summary>Pause this ask, or with null resume it: one pause per ask, the later replacing the earlier under the spelling first written.</summary>
    /// <exception cref="DriverException">A blank id: a pause names what it pauses.</exception>
    public DriverConfig WithPausedAsk(string ask, WorkPause? pause) => this with { PausedAsks = DriverConfigEdits.WithPause(PausedAsks, ask, pause) };

    /// <summary>Pause this quest on its own, or with null resume it.</summary>
    /// <exception cref="DriverException">A blank id: a pause names what it pauses.</exception>
    public DriverConfig WithPausedQuest(string quest, WorkPause? pause) =>
        this with { PausedQuests = DriverConfigEdits.WithPause(PausedQuests, quest, pause) };

    /// <summary>The person's standing answer for every session in a repository, in their words with when it was set, handed beneath its quest.</summary>
    public IReadOnlyDictionary<string, StandingAnswer> Standing { get; init; } =
        new Dictionary<string, StandingAnswer>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The most characters a standing answer holds — the CLI's <c>STANDING_LIMIT</c>, a deliberate copy.</summary>
    public const int StandingLimit = 2_000;

    /// <summary>The refusal every door says for blank words or words past the bound.</summary>
    public const string StandingRefusal = "a standing answer is the person's words, at most 2,000 characters — or cleared.";

    /// <summary>This repository's standing answer on this machine, or null: the name matched in any case.</summary>
    public StandingAnswer? StandingFor(string repository) => Standing.GetValueOrDefault(repository.Trim());

    /// <summary>
    /// Keep a standing answer for this repository, set <paramref name="at"/>, or clear it with null: one per repository, the
    /// later replacing the earlier under the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">No repository, blank words, or words past <see cref="StandingLimit"/>.</exception>
    public DriverConfig WithStanding(string repository, string? says, DateTimeOffset at) => DriverConfigEdits.Standing(this, repository, says, at);

    /// <summary>
    /// The language a repository's sessions are asked to write to the person in, as a code of <see cref="SessionLanguages.Table"/>. It
    /// wins over the workspace's (<see cref="SessionLanguages.Resolve"/>); absent is the workspace's, then none, and none hands no line.
    /// </summary>
    public IReadOnlyDictionary<string, string> Languages { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's session language, for every repository in it that sets none, and for its intake.</summary>
    public IReadOnlyDictionary<string, string> WorkspaceLanguages { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set a repository's session language, or clear it with null to take its workspace's, else none.</summary>
    /// <exception cref="DriverException">No repository, or a code the table does not hold.</exception>
    public DriverConfig WithLanguage(string repository, string? code) =>
        this with { Languages = DriverConfigEdits.WithLanguageIn(Languages, repository, code, "repository") };

    /// <summary>Set a workspace's session language, or clear it with null.</summary>
    /// <exception cref="DriverException">No workspace, or a code the table does not hold.</exception>
    public DriverConfig WithWorkspaceLanguage(string workspace, string? code) =>
        this with { WorkspaceLanguages = DriverConfigEdits.WithLanguageIn(WorkspaceLanguages, workspace, code, "workspace") };

    /// <summary>
    /// Where each repository's work is reviewed before it is offered to land: a rule, or <see cref="ReviewRule.None"/> (the file's
    /// <c>false</c>), which says none whatever its workspace says. Resolved by <see cref="ReviewRules.Resolve"/>, edited by <see cref="ReviewRules.Apply"/>.
    /// </summary>
    public IReadOnlyDictionary<string, ReviewRule> Reviews { get; init; } =
        new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's review rule, for every repository in it that sets none of its own; never <see cref="ReviewRule.None"/>.</summary>
    public IReadOnlyDictionary<string, ReviewRule> WorkspaceReviews { get; init; } =
        new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which other agent reads each repository's work before it lands: a rule, or <see cref="OpinionRule.None"/> (the file's
    /// <c>false</c>), which says none whatever its workspace says. Resolved by <see cref="OpinionRules.Resolve"/>, edited by <see cref="OpinionRules.Apply"/>.
    /// </summary>
    public IReadOnlyDictionary<string, OpinionRule> Opinions { get; init; } =
        new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's second-opinion rule, for every repository in it that sets none of its own; never <see cref="OpinionRule.None"/>.</summary>
    public IReadOnlyDictionary<string, OpinionRule> WorkspaceOpinions { get; init; } =
        new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a helper an agent asks for does in each repository: a mode (<c>ask</c>, <c>on</c>, <c>off</c>) and the agents it may ask.
    /// Resolved by <see cref="HelperRules.Resolve"/>, edited by <see cref="HelperRules.Apply"/>; absent is <see cref="HelperRule.Default"/>.
    /// </summary>
    public IReadOnlyDictionary<string, HelperRule> Helpers { get; init; } =
        new Dictionary<string, HelperRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's helpers rule, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, HelperRule> WorkspaceHelpers { get; init; } =
        new Dictionary<string, HelperRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What an agent's ask to hand its session over to another agent does in each repository: a mode (<c>ask</c>, <c>on</c>, <c>off</c>).
    /// Resolved by <see cref="HandOverRules.Resolve"/>, edited by <see cref="HandOverRules.Apply"/>; absent is <c>ask</c>.
    /// </summary>
    public IReadOnlyDictionary<string, HandOverRule> HandOver { get; init; } =
        new Dictionary<string, HandOverRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's hand-over rule, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, HandOverRule> WorkspaceHandOver { get; init; } =
        new Dictionary<string, HandOverRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The person's brainstorm policy on this machine: who may start one, the agents it runs and its minutes. Resolved by
    /// <see cref="BrainstormRules.Resolve"/>, edited by <see cref="BrainstormRules.Apply"/>; null is the default.
    /// </summary>
    public BrainstormRule? Brainstorms { get; init; }

    /// <summary>A workspace's brainstorm policy, over the machine's for every question about a repository in it.</summary>
    public IReadOnlyDictionary<string, BrainstormRule> WorkspaceBrainstorms { get; init; } =
        new Dictionary<string, BrainstormRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How work is committed in each repository: a mode (<c>as-you-go</c>, <c>ask</c>, <c>at-end</c>) and an optional convention.
    /// Resolved by <see cref="CommitRules.Resolve"/>, edited by <see cref="CommitRules.Apply"/>; absent is <c>as-you-go</c>.
    /// </summary>
    public IReadOnlyDictionary<string, CommitRule> Commits { get; init; } =
        new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's commit rule, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, CommitRule> WorkspaceCommits { get; init; } =
        new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which workflow each repository's work follows: a default and a workflow for each of its workspace's kinds. Resolved by
    /// <see cref="WorkflowSelection.Resolve"/>, edited by <see cref="WorkflowSelection.Apply"/>; absent is its workspace's, then Current.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowChoice> Workflows { get; init; } =
        new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's choice of workflow, for every repository in it that names none, and the kinds it declares.</summary>
    public IReadOnlyDictionary<string, WorkflowChoice> WorkspaceWorkflows { get; init; } =
        new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The saved workflow a place follows where nothing above it names one: the home's default, level <see cref="WorkflowLevels.Home"/>
    /// of the choosing order, seeded once into a fresh home by <see cref="HomeSeed"/>. Null names none, and a place then follows Current.
    /// </summary>
    public string? DefaultWorkflow { get; init; }

    /// <summary>Set the home's default workflow, or clear it with null.</summary>
    /// <exception cref="DriverException">An id no saved workflow may take.</exception>
    public DriverConfig WithDefaultWorkflow(string? workflow) =>
        workflow is not null && (!WorkflowKey.IsId(workflow) || workflow == WorkflowKey.Current)
            ? throw new DriverException(WorkflowKey.WorkflowProblem(workflow))
            : this with { DefaultWorkflow = workflow };

    /// <summary>
    /// How each repository's line is integrated, by repository: the line's policy that was a landing rule's <c>integrate</c>, beside
    /// the unit's process, which is the workflow's. A repository's policy replaces its workspace's whole. Edited by <c>SET_LINE</c> alone.
    /// </summary>
    public IReadOnlyDictionary<string, LandingIntegrate> LinePolicies { get; init; } =
        new Dictionary<string, LandingIntegrate>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's line policy, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, LandingIntegrate> WorkspaceLinePolicies { get; init; } =
        new Dictionary<string, LandingIntegrate>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set a repository's line policy, or clear it with null.</summary>
    /// <exception cref="DriverException">A policy that holds a value it does not take.</exception>
    public DriverConfig WithLinePolicy(string repository, LandingIntegrate? policy) =>
        this with { LinePolicies = DriverConfigEdits.Policy(LinePolicies, repository, policy) };

    /// <summary>Set a workspace's line policy, or clear it with null.</summary>
    public DriverConfig WithWorkspaceLinePolicy(string workspace, LandingIntegrate? policy) =>
        this with { WorkspaceLinePolicies = DriverConfigEdits.Policy(WorkspaceLinePolicies, workspace, policy) };

    /// <summary>The line policy that reaches <paramref name="repository"/>: its own, else its workspace's, else null.</summary>
    public LandingIntegrate? LinePolicyOf(string repository, string? workspace) =>
        LinePolicies.TryGetValue(repository.Trim(), out var own) ? own
        : WorkspaceLinePolicies.TryGetValue(WorkspaceNames.Of(workspace), out var shared) ? shared : null;

    /// <summary>
    /// The integration declaration each repository's owner accepted on this machine: git's blob id of the <c>daoris.gates.json</c>
    /// accepted, or <c>none</c> where the line held none. Never bound to a chosen workflow, so a repository needs no verification
    /// pipeline to integrate; absent is nothing accepted, and nothing the declaration names runs. Read and written by <see cref="IntegrationGatesKey"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> IntegrationGates { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set the integration declaration accepted for a repository, by its blob, or clear it with null; the repository's existing spelling
    /// kept. The person's <i>Accept the gates</i> is the door that calls it.
    /// </summary>
    /// <exception cref="DriverException">No repository, or a value that is no blob id and not <c>none</c>.</exception>
    public DriverConfig WithIntegrationGates(string repository, string? blob) => DriverConfigEdits.IntegrationGates(this, repository, blob);

    /// <summary>
    /// Each repository's session access setup: full access, or the pauses it names, each resolved on its own (<see cref="AccessRules.Resolve"/>)
    /// and edited by <see cref="AccessRules.Apply"/>; absent everywhere is today's behaviour.
    /// </summary>
    public IReadOnlyDictionary<string, AccessSetting> Access { get; init; } =
        new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's access setup, for each pause a repository in it does not name.</summary>
    public IReadOnlyDictionary<string, AccessSetting> WorkspaceAccess { get; init; } =
        new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This machine's access setup, for each pause neither a repository nor its workspace names; null sets none.</summary>
    public AccessSetting? MachineAccess { get; init; }

    /// <summary>
    /// This machine's heavy gates for a repository whose own <c>daoris.gates.json</c> marks none, each list in the order its gates run
    /// (<see cref="Daoris.Driver.HeavyGates.Resolve"/>). Never an edit to the repository; edited by <see cref="HeavyMapping.Apply"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<HeavyGate>> HeavyGates { get; init; } =
        new Dictionary<string, IReadOnlyList<HeavyGate>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which checks each repository's sessions run under Current: <c>light</c> or <c>heavy-at-end</c>; absent is today's. Read by
    /// <see cref="Daoris.Driver.SessionChecks.Of"/>, edited by <see cref="Daoris.Driver.SessionChecks.Apply"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> SessionChecks { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The person's keep rules, each kind absent meaning its default. Read by the one effective policy (<see cref="KeepPolicy.Of"/>);
    /// edited only through <see cref="KeepRules.Apply"/>.
    /// </summary>
    public KeepChoices Keep { get; init; } = KeepChoices.None;

    /// <summary>
    /// Each actor's order as the person set it, by its id; an actor absent has no order of its own (<see cref="ActorChoices.Effective"/>).
    /// Written through <see cref="ActorChoices.With(DriverConfig, string, IReadOnlyList{ActorChoice}?)"/> alone.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors { get; init; } =
        new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal);

    /// <summary>Each stored actor that does not read, with why: its starts hold, never read as <i>as before</i>.</summary>
    public IReadOnlyDictionary<string, string> ActorErrors { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Ask Daoris's access mode: <see cref="HelpModeNames.Restricted"/>, the default, or <see cref="HelpModeNames.Full"/>. Read at each
    /// start of its conversation.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Restricted unless the file names full</b>: a value that is neither reads as restricted, so nothing but the person's own
    /// choice ever widens it. Written only while full, so an edit elsewhere never pins the default.
    /// </remarks>
    public string HelpMode { get; init; } = HelpModeNames.Restricted;

    /// <summary>Ask Daoris's access mode here, as the person chose it.</summary>
    /// <exception cref="DriverException">A mode that is neither restricted nor full.</exception>
    public DriverConfig WithHelpMode(string mode) =>
        this with
        {
            HelpMode = HelpModeNames.Named(mode)
                ?? throw new DriverException($"Ask Daoris's mode is `{HelpModeNames.Restricted}` or `{HelpModeNames.Full}`, not `{mode}`."),
        };

    /// <summary>
    /// How long an account cools, in minutes, when its agent's limit names no time this reads, or one it does not believe:
    /// <c>cooloff</c>, a whole number of at least 1. Null — absent, or anything else in the file — is the default,
    /// <see cref="CoolOffDefault.Value"/>.
    /// </summary>
    public int? CoolOffMinutes { get; init; }

    /// <summary>The cool-off a limit naming no time takes on this machine: <see cref="CoolOffMinutes"/>, or the hour.</summary>
    public TimeSpan CoolOff => CoolOffMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : CoolOffDefault.Value;

    /// <summary>The refusal both doors say for less than a minute.</summary>
    public const string CoolOffRefusal =
        "a cool-off is a whole number of minutes, at least 1 — a zero cool-off would start a spent account again at every look.";

    /// <summary>Set the cool-off a limit naming no time takes, or clear it with null to take the default.</summary>
    /// <exception cref="DriverException">Less than a minute: a zero cool-off is a spin.</exception>
    public DriverConfig WithCoolOff(int? minutes) =>
        minutes is < 1 ? throw new DriverException(CoolOffRefusal) : this with { CoolOffMinutes = minutes };

    /// <summary>The refusal both doors say for a cap under one.</summary>
    public const string CapRefusal =
        "the cap is a whole number of sessions, at least 1 — a cap of zero would start no session at all.";

    /// <summary>Set how many sessions Daoris starts at once on this machine.</summary>
    /// <exception cref="DriverException">Less than one: no session would ever start.</exception>
    public DriverConfig WithCap(int cap) =>
        cap < 1 ? throw new DriverException(CapRefusal) : this with { Cap = cap };

    /// <summary>
    /// The line a repository's work grows from and lands on, as the person set it, by repository. It wins over the workspace's and over
    /// the checkout's guess (<see cref="CanonicalLine"/>). Empty — the default — is the guess alone.
    /// </summary>
    public IReadOnlyDictionary<string, string> Lines { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's line, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, string> WorkspaceLines { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set a repository's line, or clear it with null to take the workspace's or the guess again.</summary>
    public DriverConfig WithLine(string repository, string? branch) => this with { Lines = DriverConfigEdits.Set(Lines, repository, branch) };

    /// <summary>Set a workspace's line, or clear it with null.</summary>
    public DriverConfig WithWorkspaceLine(string workspace, string? branch) =>
        this with { WorkspaceLines = DriverConfigEdits.Set(WorkspaceLines, workspace, branch) };

    /// <summary>
    /// Whether a repository's checkout may be read by agents outside it, as the person set it, by repository. It wins over the
    /// workspace's; absent takes the workspace's, then on (<see cref="AcrossRules.Reading"/>).
    /// </summary>
    public IReadOnlyDictionary<string, bool> ReadAcross { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's reading across, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, bool> WorkspaceReadAcross { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The declared relationships: the repositories each repository's sessions may also write into. One direction per entry; absent is
    /// none, which is the default.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> WriteAcross { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The repositories whose registered checkout Daoris may change the files of: the person's switch, by repository, off where
    /// absent. Only <c>true</c> is ever written.
    /// </summary>
    public IReadOnlyDictionary<string, bool> CheckoutWrites { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Turn the switch for a repository on, or off by taking its entry out.</summary>
    public DriverConfig WithCheckoutWrites(string repository, bool allow) =>
        this with { CheckoutWrites = DriverConfigEdits.SetFlag(CheckoutWrites, repository, allow ? true : null) };

    /// <summary>The read side of <see cref="WithCheckoutWrites"/>.</summary>
    public bool MayChangeCheckout(string repository) =>
        CheckoutWrites.TryGetValue(repository.Trim(), out var on) && on;

    /// <summary>Set whether a repository's checkout is read across, or clear it with null to take its workspace's.</summary>
    public DriverConfig WithReadAcross(string repository, bool? read) =>
        this with { ReadAcross = DriverConfigEdits.SetFlag(ReadAcross, repository, read) };

    /// <summary>Set a workspace's reading across, or clear it with null to take the default, on.</summary>
    public DriverConfig WithWorkspaceReadAcross(string workspace, bool? read) =>
        this with { WorkspaceReadAcross = DriverConfigEdits.SetFlag(WorkspaceReadAcross, workspace, read) };

    /// <summary>
    /// Declare that <paramref name="repository"/>'s sessions may write into <paramref name="to"/>, or take
    /// that back. A repository never names itself: its own tree is already its sessions'.
    /// </summary>
    public DriverConfig WithWriteAcross(string repository, string to, bool allow) => DriverConfigEdits.WriteAcross(this, repository, to, allow);

    /// <summary>The file's name, as a sentence names it: never a path to read.</summary>
    public static string FileName => Path.GetFileName(HomeEntry.DriverConfig.New);

    /// <summary>
    /// The conventional home, under the Daoris home (D63). A machine with no home is refused, naming what to set. The file is the home's
    /// <see cref="HomeEntry.DriverConfig"/>, asked here alone (CONFIGSEAM1); a run that wants other choices runs on a home of its own.
    /// </summary>
    public static string DefaultPath => DaorisHome.Require(HomeEntry.DriverConfig);

    /// <summary>
    /// The one resolution of the person's choices (D63, CONFIGSEAM1): the home's <c>driver.json</c>, and null on a machine that names no
    /// home. Every reader comes here, through this or a <c>ResolvePath</c>, and <c>ConfigSeamTests</c> scans for one that does not.
    /// </summary>
    public static string? FindPath(Func<string, string?> environment) => DaorisHome.File(environment, HomeEntry.DriverConfig);

    /// <summary>The same, from the process environment.</summary>
    public static string? FindPath() => FindPath(Environment.GetEnvironmentVariable);

    /// <summary>The home this environment's loop drives (<c>DAORIS_HOME</c>), null where none is named; found without reading or making anything.</summary>
    public static string? FindHome(Func<string, string?> environment) => DaorisHome.Resolve(environment);

    /// <summary>The same, from the process environment.</summary>
    public static string? FindHome() => FindHome(Environment.GetEnvironmentVariable);

    /// <summary>The file every door reads and writes, the home's; refused, naming what to set, with no home.</summary>
    public static string ResolvePath() => FindPath() ?? DefaultPath;

    /// <summary>The same file, for a door that holds its home rather than the path.</summary>
    public static string ResolvePath(string home) => DaorisHome.Entry(home, HomeEntry.DriverConfig);

    /// <summary>The Daoris home a config file lives in, at its new place (<c>data/driver.json</c>) or its old.</summary>
    public static string HomeOf(string configPath) => DaorisHome.HomeOf(configPath, HomeEntry.DriverConfig);

    /// <summary>
    /// The text this config was read from, or null for one built in memory: the writer keeps from it every key it does not own, in its
    /// place, and every value no edit changed, as written (<see cref="ToJson"/>).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>The file <see cref="Source"/> was read from, or null for a text parsed in memory: a save makes its edit onto that file as it stands.</summary>
    public string? SourceFile { get; init; }

    /// <summary>
    /// The staffing the file held where its fields were filled from the store (<see cref="RoleStaffingPort.Fill"/>), else null: a save
    /// of any other key keeps its actors as found, since they are written through the file step alone.
    /// </summary>
    internal FileStaffing? AsFile { get; init; }

    /// <summary>The choices in the file at <paramref name="path"/> (<see cref="DriverConfigDisk.Load"/>); a missing file is the empty config's, not an error.</summary>
    /// <exception cref="DriverConfigUnreadableException">The file is there and does not read: it names the file, and where the parser stopped.</exception>
    public static DriverConfig Load(string path) => DriverConfigDisk.Load(path);

    /// <summary>
    /// The arrays of <c>driver.json</c> that are sets: an edit of one is the names it added and took, made onto the file's array as it
    /// stands, so two writers each adding a repository keep both. Each is a key path, <c>*</c> any one name. Duplicated deliberately as
    /// the CLI's <c>DRIVER_SETS</c>; held to the tests' <c>fixtures/driver-config-keeps.json</c>'s <c>sets</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Sets = ["drivable", "holds", "trees", "writeAcross.*"];

    /// <summary>Write the choices back, the edit made onto the file as it stands and under its lock (<see cref="DriverConfigDisk.Save"/>).</summary>
    /// <exception cref="DriverConfigUnreadableException">The file now does not read, holds a key twice, or no longer holds an object this edit changes inside; nothing was written.</exception>
    /// <exception cref="IOException">Another writer held the file's lock for the whole of the wait, or took it before the rename; nothing was written.</exception>
    public void Save(string path) => Save(path, null);

    internal void Save(string path, KeepSeams? seams) => DriverConfigDisk.Save(this, path, seams);

    /// <summary>
    /// The file's text: the choices written over the text they were read from (<see cref="Source"/>), so every key this build does not
    /// own stays where it stands; read and written with no edit, the same bytes. A config built in memory is written whole.
    /// </summary>
    public string ToJson() =>
        Source is null
            ? DriverConfigWriter.Written(this)
            : JsonKeeping.Kept(Source, DriverConfigDisk.Understood(this), DriverConfigWriter.Written(this), Sets);

    /// <summary>This machine's answer to "may I drive that repository, now" — one flag flipped at a time.</summary>
    public DriverConfig WithDrivable(string repository, bool drivable) => this with
    {
        Drivable = DriverConfigEdits.Toggle(Drivable, repository, drivable),
    };

    public DriverConfig WithHold(string repository, bool held) => this with
    {
        Holds = DriverConfigEdits.Toggle(Holds, repository, held),
    };

    /// <summary>Whether this repository's sessions open their own worktree (D51).</summary>
    public DriverConfig WithTrees(string repository, bool ownTree) => this with
    {
        Trees = DriverConfigEdits.Toggle(Trees, repository, ownTree),
    };

    /// <summary>The read side of <see cref="WithTrees"/> — one comparison rule for both.</summary>
    public bool OpensOwnTree(string repository) =>
        Trees.Contains(repository, StringComparer.OrdinalIgnoreCase);

    /// <summary>The choices read from <c>driver.json</c>'s text.</summary>
    public static DriverConfig Parse(string json) => DriverConfigReader.Parse(json);

    /// <summary>Whether this machine says so when a session parks or ends unasked.</summary>
    public DriverConfig WithNotify(bool notify) => this with { Notify = notify };
}
