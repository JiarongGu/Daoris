using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's standing choices, machine-local by nature (D46 §6/§7): which repositories THIS machine
/// may drive, what is held, how wide the loop runs, and which adapter spawns.
/// </summary>
/// <param name="Drivable">Repositories the person opted in. Empty means the driver drives nothing.</param>
/// <param name="Holds">
/// Repositories the person holds: drivable, and nothing new starts there until they resume it, while what runs goes on
/// (D46 §3). A hold, never a pause: a pause is an ask's or a quest's, and stops what runs (D132, design §8.2).
/// </param>
/// <param name="Cap">Concurrent sessions across all repositories.</param>
/// <param name="Adapter">Which adapter spawns sessions. The supported harness by default (D23).</param>
/// <param name="TimeoutMinutes">How long a session may run before the driver concludes it failed.</param>
/// <param name="PollSeconds">How often the watch loop ticks.</param>
/// <param name="Commands">Per-adapter command configuration — what the stub runs.</param>
/// <param name="Trees">
/// Repositories whose sessions open their OWN worktree instead of the registered root (D51). Empty —
/// the default — is today's behaviour byte for byte: an additive feature, like the profiles (D49 §4).
/// </param>
/// <param name="Notify">
/// Whether this machine interrupts the person when a session parks or ends unasked (SURF5b).
/// <b>On by default</b>: the whole point of a driver is that nobody has to watch it, and a driver
/// that ran silently by default would be one whose parked sessions sit until somebody thinks to look.
/// Off in one click, and machine-local like every other choice in this file.
/// </param>
public sealed record DriverConfig(
    IReadOnlyList<string> Drivable,
    IReadOnlyList<string> Holds,
    int Cap,
    string Adapter,
    int TimeoutMinutes,
    int PollSeconds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Commands,
    IReadOnlyList<string> Trees,
    bool Notify = true,
    int Strikes = 3,
    IReadOnlyDictionary<string, int>? Forgiven = null)
{
    /// <summary>
    /// How many failed sessions park a quest (DRV6). <c>0</c> never parks — today's behaviour, for a
    /// person who wants the loop to keep trying.
    /// </summary>
    public int Strikes { get; init; } = Math.Max(0, Strikes);

    /// <summary>
    /// Quests the person has restarted, and the failure count they were restarted at. A mark rather
    /// than an erasure: the records still say what happened, and the next three failures park it
    /// again. Machine-local, like every other choice in this file.
    /// </summary>
    public IReadOnlyDictionary<string, int> Forgiven { get; init; } =
        Forgiven ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Drives nothing, holds nothing — the safe shape silence takes.</summary>
    public static DriverConfig Empty { get; } = new(
        Drivable: [], Holds: [], Cap: 2, Adapter: "claude-code",
        TimeoutMinutes: 30, PollSeconds: 15,
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
    /// The quests the person released from their stop (SESSUX1b, D126 §3.4), each against the session they stopped: a
    /// person's stop holds its quest on this machine until its session is named here. A later stop holds it again, since
    /// its session differs. Absent is none, written only when set. The CLI's <c>driverconfig.ts</c> reads it the same way
    /// (<c>ReleasedTests</c>, held row for row by its <c>driverconfig.test.ts</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string> Released { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The session whose stop of this quest the person released, or null when they released none.</summary>
    public string? ReleasedFor(string questId) => Released.TryGetValue(questId, out var session) ? session : null;

    /// <summary>Whether the person released this stop: the quest in any case, the session as its record spells it.</summary>
    public bool Releases(string questId, string session) => string.Equals(ReleasedFor(questId), session, StringComparison.Ordinal);

    /// <summary>Release this quest from the person's stop of this session: one release per quest, the later replacing the earlier.</summary>
    /// <exception cref="DriverException">A blank quest or session: a release names the stop it releases.</exception>
    public DriverConfig WithReleased(string questId, string session)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(session))
        {
            throw new DriverException("a release names the quest and the session whose stop it releases.");
        }

        var quest = questId.Trim().TrimStart('#');
        var next = new Dictionary<string, string>(Released, StringComparer.OrdinalIgnoreCase);
        // The quest's spelling first written, when it has one in another case: one entry, never two.
        var key = next.Keys.FirstOrDefault(k => string.Equals(k, quest, StringComparison.OrdinalIgnoreCase)) ?? quest;
        next[key] = session.Trim();
        return this with { Released = next };
    }

    /// <summary>
    /// This quest's release and its restart mark dropped (PAUSE1d, D132 §3.4 step 7): once a quest is closed it is never
    /// planned again (D46 §3), so neither says anything any more. Unchanged where it has neither.
    /// </summary>
    public DriverConfig WithoutQuest(string questId)
    {
        var quest = questId.Trim().TrimStart('#');
        bool Same(string key) => string.Equals(key, quest, StringComparison.OrdinalIgnoreCase);
        if (!Released.Keys.Any(Same) && !Forgiven.Keys.Any(Same)) return this;
        return this with
        {
            Released = Released.Where(pair => !Same(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Forgiven = Forgiven.Where(pair => !Same(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// The asks the person paused on this machine (PAUSE1a, D132 point 5, design §2.5), each with when and the stops its pause
    /// made. Absent is none, written only when set. The look holds each paused work's quests (<see cref="PausedWork"/>,
    /// <see cref="StartVerdict.Paused"/>), and <see cref="WorkPausing"/> alone writes it (PAUSE1b). The CLI's
    /// <c>driverconfig.ts</c> reads it the same way (<c>PausedWorkTests</c>, held row for row by its
    /// <c>driverconfig.test.ts</c>), and keeps it as written.
    /// </summary>
    public IReadOnlyDictionary<string, WorkPause> PausedAsks { get; init; } =
        new Dictionary<string, WorkPause>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The quests the person paused on this machine, on their own (design §2.5): read and written as <see cref="PausedAsks"/>.</summary>
    public IReadOnlyDictionary<string, WorkPause> PausedQuests { get; init; } =
        new Dictionary<string, WorkPause>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This ask's pause, or null where it is not paused here: the id as a person writes it, matched in any case.</summary>
    public WorkPause? PausedAsk(string ask) => PausedAsks.GetValueOrDefault(Id(ask));

    /// <summary>This quest's own pause, or null: a quest in a paused ask's work is that ask's pause, not one of its own.</summary>
    public WorkPause? PausedQuest(string quest) => PausedQuests.GetValueOrDefault(Id(quest));

    /// <summary>Pause this ask, or with null resume it: one pause per ask, the later replacing the earlier under the spelling first written.</summary>
    /// <exception cref="DriverException">A blank id: a pause names what it pauses.</exception>
    public DriverConfig WithPausedAsk(string ask, WorkPause? pause) => this with { PausedAsks = WithPause(PausedAsks, ask, pause) };

    /// <summary>Pause this quest on its own, or with null resume it.</summary>
    /// <exception cref="DriverException">A blank id: a pause names what it pauses.</exception>
    public DriverConfig WithPausedQuest(string quest, WorkPause? pause) => this with { PausedQuests = WithPause(PausedQuests, quest, pause) };

    private static string Id(string id) => id.Trim().TrimStart('#').Trim();

    private static IReadOnlyDictionary<string, WorkPause> WithPause(
        IReadOnlyDictionary<string, WorkPause> map, string id, WorkPause? pause)
    {
        var named = Id(id ?? "");
        if (named.Length == 0) throw new DriverException("a pause names the ask or the quest it pauses.");

        var next = new Dictionary<string, WorkPause>(map, StringComparer.OrdinalIgnoreCase);
        // The id's spelling first written, when it has one in another case: one entry, never two.
        var key = next.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;
        if (pause is null) next.Remove(key);
        else next[key] = pause;
        return next;
    }

    /// <summary>
    /// What the person has told this machine holds for every session in a repository (KNOWUSE1b, D135 §3): a standing answer
    /// by repository, in their words, with when it was set, handed to every session there beneath its quest. Absent is none,
    /// written only when set. The CLI's <c>driverconfig.ts</c> reads it the same way (<c>StandingTests</c>, held row for row
    /// by its <c>driverconfig.test.ts</c>).
    /// </summary>
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
    public DriverConfig WithStanding(string repository, string? says, DateTimeOffset at)
    {
        var named = repository?.Trim() ?? "";
        if (named.Length == 0) throw new DriverException("a standing answer names the repository it holds for.");
        var words = says?.Trim();
        if (says is not null && (words!.Length == 0 || words.Length > StandingLimit)) throw new DriverException(StandingRefusal);

        var next = new Dictionary<string, StandingAnswer>(Standing, StringComparer.OrdinalIgnoreCase);
        // The repository's spelling first written, when it has one in another case: one entry, never two.
        var key = next.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;
        if (words is null) next.Remove(key);
        else next[key] = new StandingAnswer(words, ToTheSecond(at));
        return this with { Standing = next };
    }

    /// <summary>
    /// The language a repository's sessions are asked to write to the person in (LANG1c, D142 point 7), by repository, as a
    /// code of <see cref="SessionLanguages.Table"/>. It wins over the workspace's (<see cref="SessionLanguages.Resolve"/>);
    /// absent is the workspace's, then none, and none hands no line. The CLI's <c>driverconfig.ts</c> reads it the same way
    /// (<c>SessionLanguageTests</c>, held row for row by its <c>driverconfig.test.ts</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string> Languages { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's session language, for every repository in it that sets none, and for its intake.</summary>
    public IReadOnlyDictionary<string, string> WorkspaceLanguages { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set a repository's session language, or clear it with null to take its workspace's, else none.</summary>
    /// <exception cref="DriverException">No repository, or a code the table does not hold.</exception>
    public DriverConfig WithLanguage(string repository, string? code) =>
        this with { Languages = WithLanguageIn(Languages, repository, code, "repository") };

    /// <summary>Set a workspace's session language, or clear it with null.</summary>
    /// <exception cref="DriverException">No workspace, or a code the table does not hold.</exception>
    public DriverConfig WithWorkspaceLanguage(string workspace, string? code) =>
        this with { WorkspaceLanguages = WithLanguageIn(WorkspaceLanguages, workspace, code, "workspace") };

    private static IReadOnlyDictionary<string, string> WithLanguageIn(
        IReadOnlyDictionary<string, string> map, string name, string? code, string what)
    {
        var named = name?.Trim() ?? "";
        if (named.Length == 0) throw new DriverException($"a session language is set for a {what} — name it.");
        var language = code is null ? null : SessionLanguages.Code(code) ?? throw new DriverException(SessionLanguages.Refusal(code));

        var next = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        // The name's spelling first written, when it has one in another case: one entry, never two.
        var key = next.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;
        if (language is null) next.Remove(key);
        else next[key] = language;
        return next;
    }

    /// <summary>
    /// Where each repository's work is reviewed before it is offered to land (REVIEWENV1a, D154 point 2, the review-environment
    /// design §1.2–§1.3), by repository: a rule, or <see cref="ReviewRule.None"/> (the file's <c>false</c>), which says it has
    /// none whatever its workspace says. A repository's rule replaces its workspace's whole (<see cref="ReviewRules.Resolve"/>);
    /// absent is the workspace's, then none, which is today's behaviour. Edited only through <see cref="ReviewRules.Apply"/>.
    /// The CLI's <c>driverconfig.ts</c> reads it the same way, by one shared table (<c>ReviewRulesTests</c>).
    /// </summary>
    public IReadOnlyDictionary<string, ReviewRule> Reviews { get; init; } =
        new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's review rule, for every repository in it that sets none of its own; never <see cref="ReviewRule.None"/>.</summary>
    public IReadOnlyDictionary<string, ReviewRule> WorkspaceReviews { get; init; } =
        new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which other agent reads each repository's work before it lands (XAGENT1a, D155 point 3, the second-agent design
    /// §2.2–§2.3), by repository: a rule, or <see cref="OpinionRule.None"/> (the file's <c>false</c>), which says there is no
    /// second opinion whatever its workspace says. A repository's rule replaces its workspace's whole
    /// (<see cref="OpinionRules.Resolve"/>); absent is the workspace's, then none, which is today's behaviour. Edited only
    /// through <see cref="OpinionRules.Apply"/>. The CLI's <c>driverconfig.ts</c> reads it the same way, by one shared table
    /// (<c>OpinionRulesTests</c>).
    /// </summary>
    public IReadOnlyDictionary<string, OpinionRule> Opinions { get; init; } =
        new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's second-opinion rule, for every repository in it that sets none of its own; never <see cref="OpinionRule.None"/>.</summary>
    public IReadOnlyDictionary<string, OpinionRule> WorkspaceOpinions { get; init; } =
        new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a helper an agent asks for does in each repository (MIXAGENT2a-i, D163 point 5; the mixed agents design §5): a mode
    /// (<c>ask</c>, <c>on</c>, <c>off</c>) and the agents it may ask. A repository's rule replaces its workspace's whole
    /// (<see cref="HelperRules.Resolve"/>); absent is the default, <c>ask</c>. Edited only through <see cref="HelperRules.Apply"/>;
    /// the CLI's <c>driverconfig.ts</c> reads it the same way, by one shared table (<c>HelperRulesTests</c>).
    /// </summary>
    public IReadOnlyDictionary<string, HelperRule> Helpers { get; init; } =
        new Dictionary<string, HelperRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's helpers rule, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, HelperRule> WorkspaceHelpers { get; init; } =
        new Dictionary<string, HelperRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The person's brainstorm policy on this machine (BRAINSTORM1b, D176 point 9): who may start one, the agents it runs and its
    /// minutes. A workspace's rule replaces it whole for a question about a repository in that workspace
    /// (<see cref="BrainstormRules.Resolve"/>); null is the default. Edited only through <see cref="BrainstormRules.Apply"/>; the
    /// CLI's <c>driverconfig.ts</c> reads it the same way, by one shared table (<c>BrainstormRulesTests</c>).
    /// </summary>
    public BrainstormRule? Brainstorms { get; init; }

    /// <summary>A workspace's brainstorm policy, over the machine's for every question about a repository in it.</summary>
    public IReadOnlyDictionary<string, BrainstormRule> WorkspaceBrainstorms { get; init; } =
        new Dictionary<string, BrainstormRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How work is committed in each repository (PROCESS1a, D164's amendment point 1): a mode (<c>as-you-go</c>, <c>ask</c>,
    /// <c>at-end</c>) and an optional convention. A repository's rule replaces its workspace's whole (<see cref="CommitRules.Resolve"/>);
    /// absent is <c>as-you-go</c>. Edited only through <see cref="CommitRules.Apply"/>; the CLI's <c>driverconfig.ts</c> reads it
    /// the same way, by one shared table (<c>CommitRulesTests</c>).
    /// </summary>
    public IReadOnlyDictionary<string, CommitRule> Commits { get; init; } =
        new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's commit rule, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, CommitRule> WorkspaceCommits { get; init; } =
        new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which workflow each repository's work follows (WORKFLOW1e, D157 point 10, the workflow design §2.5, §4.1), by repository: a
    /// default and a workflow for each of its workspace's kinds. A repository that names any replaces its workspace's whole
    /// (<see cref="WorkflowSelection.Resolve"/>); absent is its workspace's, then Current, which is today's behaviour. Edited only
    /// through <see cref="WorkflowSelection.Apply"/>. The CLI's <c>driverconfig.ts</c> reads it the same way, by one shared table
    /// (<c>WorkflowSelectionTests</c>).
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowChoice> Workflows { get; init; } =
        new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's choice of workflow, for every repository in it that names none, and the kinds it declares.</summary>
    public IReadOnlyDictionary<string, WorkflowChoice> WorkspaceWorkflows { get; init; } =
        new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Each repository's session access setup (AUTONOMY1a, D184 points 1-3), by repository: full access, or the pauses it names. Each
    /// pause resolves on its own, the repository's over its workspace's over this machine's (<see cref="AccessRules.Resolve"/>);
    /// absent everywhere is today's behaviour. Edited only through <see cref="AccessRules.Apply"/>; the CLI's <c>driverconfig.ts</c>
    /// reads and keeps it the same way.
    /// </summary>
    public IReadOnlyDictionary<string, AccessSetting> Access { get; init; } =
        new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's access setup, for each pause a repository in it does not name.</summary>
    public IReadOnlyDictionary<string, AccessSetting> WorkspaceAccess { get; init; } =
        new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This machine's access setup, for each pause neither a repository nor its workspace names; null sets none.</summary>
    public AccessSetting? MachineAccess { get; init; }

    /// <summary>One change to an access setup, through <see cref="AccessRules.Apply"/>.</summary>
    /// <exception cref="DriverException">The edit's refusal, nothing changed.</exception>
    public DriverConfig WithAccess(AccessEdit edit) => AccessRules.Apply(this, edit);

    /// <summary>
    /// This machine's heavy gates for a repository whose own <c>daoris.gates.json</c> marks none (HEAVYRUN1b2a, D180's HEAVYRUN1b2a
    /// note), by repository, each list in the order its gates run; what <c>run-heavy</c> runs for it and its sessions are told of
    /// (<see cref="Daoris.Driver.HeavyGates.Resolve"/>). Never an edit to the repository. Edited only through <see cref="HeavyMapping.Apply"/>; the
    /// CLI's <c>driverconfig.ts</c> reads and keeps it the same way.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<HeavyGate>> HeavyGates { get; init; } =
        new Dictionary<string, IReadOnlyList<HeavyGate>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A repository's heavy gates set in order, or taken away with none, through <see cref="HeavyMapping.Apply"/>.</summary>
    /// <exception cref="DriverException">The edit's refusal, nothing changed.</exception>
    public DriverConfig WithHeavyGates(string repository, IReadOnlyList<HeavyGate>? gates) => HeavyMapping.Apply(this, repository, gates);

    /// <summary>A moment as the file keeps it: in UTC, to the second.</summary>
    private static DateTimeOffset ToTheSecond(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, TimeSpan.Zero);
    }

    /// <summary>
    /// The harness an ask's INTAKE session runs on (D65 §1b) — or null, and no intake runs.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Absent means OFF</b>, the reading `notify` does not take and for the opposite reason.
    /// An intake spends a real login on every ask a person makes; a machine whose file predates this
    /// field has been answering asks by declarations only (INT4a), and silently starting to spend
    /// accounts on them after an upgrade is not a default anyone chose.</para>
    ///
    /// <para><b>Named, never "the same as the adapter"</b>: which harness answers asks and which does
    /// the work are two choices, and the second changing must not quietly move the first.</para>
    /// </remarks>
    public string? IntakeAdapter { get; init; }

    /// <summary>Which harness answers asks here, or null for none — the declarations tier alone.</summary>
    public DriverConfig WithIntake(string? adapter) =>
        this with { IntakeAdapter = string.IsNullOrWhiteSpace(adapter) ? null : adapter.Trim() };

    /// <summary>
    /// The harness Ask Daoris runs on (HELP1, D89), or null for none: it offers only its starters then.
    /// Off until named, as the intake is, and named apart from it: answering asks and helping a person
    /// are two jobs, and changing one must not quietly move the other.
    /// </summary>
    public string? HelperAdapter { get; init; }

    /// <summary>
    /// Ask Daoris's access mode (HELPMODE1a1, D183 point 1): <see cref="HelpModes.Restricted"/>, the default, or
    /// <see cref="HelpModes.Full"/>. Read at each start of its conversation. The CLI's <c>driverconfig.ts</c> reads it the same way.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Restricted unless the file names full</b>: a value that is neither reads as restricted, so nothing but the person's own
    /// choice ever widens it (D183 point 2). Written only while full, so an edit elsewhere never pins the default.
    /// </remarks>
    public string HelpMode { get; init; } = HelpModes.Restricted;

    /// <summary>Ask Daoris's access mode here, as the person chose it (HELPMODE1a1).</summary>
    /// <exception cref="DriverException">A mode that is neither restricted nor full.</exception>
    public DriverConfig WithHelpMode(string mode) =>
        this with
        {
            HelpMode = HelpModes.Named(mode)
                ?? throw new DriverException($"Ask Daoris's mode is `{HelpModes.Restricted}` or `{HelpModes.Full}`, not `{mode}`."),
        };

    /// <summary>
    /// How long an account cools, in minutes, when its agent's limit names no time this reads, or one it does not believe
    /// (TOOL4e, D125 §2.2): <c>cooloff</c>, a whole number of at least 1. Null — absent, or anything else in the file —
    /// is the default, <see cref="AccountLimits.DefaultCoolOff"/>. The CLI's <c>driverconfig.ts</c> reads it the same way
    /// (<c>CoolOffTests</c>, held row for row by its <c>driverconfig.test.ts</c>).
    /// </summary>
    public int? CoolOffMinutes { get; init; }

    /// <summary>The cool-off a limit naming no time takes on this machine: <see cref="CoolOffMinutes"/>, or the hour.</summary>
    public TimeSpan CoolOff => CoolOffMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : AccountLimits.DefaultCoolOff;

    /// <summary>The refusal both doors say for less than a minute.</summary>
    public const string CoolOffRefusal =
        "a cool-off is a whole number of minutes, at least 1 — a zero cool-off would start a spent account again at every look.";

    /// <summary>Set the cool-off a limit naming no time takes, or clear it with null to take the default.</summary>
    /// <exception cref="DriverException">Less than a minute: a zero cool-off is a spin (D125 §6).</exception>
    public DriverConfig WithCoolOff(int? minutes) =>
        minutes is < 1 ? throw new DriverException(CoolOffRefusal) : this with { CoolOffMinutes = minutes };

    /// <summary>Which harness Ask Daoris runs on here, or null for none.</summary>
    public DriverConfig WithHelper(string? adapter) =>
        this with { HelperAdapter = string.IsNullOrWhiteSpace(adapter) ? null : adapter.Trim() };

    /// <summary>
    /// The line a repository's work grows from and lands on, as the person set it (WSR2), by
    /// repository. It wins over the workspace's and over the checkout's guess (<see cref="CanonicalLine"/>).
    /// Empty — the default — is the guess alone, today's behaviour.
    /// </summary>
    public IReadOnlyDictionary<string, string> Lines { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's line, for every repository in it that sets none of its own (WSR2).</summary>
    public IReadOnlyDictionary<string, string> WorkspaceLines { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set a repository's line, or clear it with null to take the workspace's or the guess again.</summary>
    public DriverConfig WithLine(string repository, string? branch) => this with { Lines = Set(Lines, repository, branch) };

    /// <summary>Set a workspace's line, or clear it with null.</summary>
    public DriverConfig WithWorkspaceLine(string workspace, string? branch) =>
        this with { WorkspaceLines = Set(WorkspaceLines, workspace, branch) };

    /// <summary>
    /// How work lands, as the person set it (WSR1, D87), by repository. It wins over the workspace's;
    /// empty — the default — is the merge door as it always was (<see cref="LandingRules.Choose"/>).
    /// </summary>
    public IReadOnlyDictionary<string, LandingRule> Landings { get; init; } =
        new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's landing rule, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, LandingRule> WorkspaceLandings { get; init; } =
        new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set a repository's landing rule, or clear it with null to take its workspace's or the merge.</summary>
    public DriverConfig WithLanding(string repository, LandingRule? rule) =>
        this with { Landings = Set(Landings, repository, rule) };

    /// <summary>Set a workspace's landing rule, or clear it with null.</summary>
    public DriverConfig WithWorkspaceLanding(string workspace, LandingRule? rule) =>
        this with { WorkspaceLandings = Set(WorkspaceLandings, workspace, rule) };

    /// <summary>
    /// Set the cycle on a repository's own landing rule (FABRIC-GRAPH1, §C2), or clear it with null to take the default, today's. The
    /// cycle rides its rule (D164's amendment), so a repository with no rule of its own is refused, and so is a cycle whose graph does
    /// not hold under its rule, naming the first problem.
    /// </summary>
    public DriverConfig WithLandingCycle(string repository, string? cycle) =>
        this with { Landings = SetCycle(Landings, repository, cycle, "repository") };

    /// <summary>Set the cycle on a workspace's landing rule, or clear it with null.</summary>
    public DriverConfig WithWorkspaceLandingCycle(string workspace, string? cycle) =>
        this with { WorkspaceLandings = SetCycle(WorkspaceLandings, workspace, cycle, "workspace") };

    private static IReadOnlyDictionary<string, LandingRule> SetCycle(IReadOnlyDictionary<string, LandingRule> map, string key, string? cycle, string what)
    {
        if (!map.TryGetValue(key.Trim(), out var rule))
        {
            throw new DriverException($"`{key.Trim()}` has no landing rule of its own, and a cycle rides its {what}'s rule (D164): set the rule first, then its cycle.");
        }

        var next = new Dictionary<string, LandingRule>(map, StringComparer.OrdinalIgnoreCase);
        if (cycle is null)
        {
            next[key.Trim()] = rule with { Cycle = null };
            return next;
        }

        var kept = rule with { Cycle = CompactOrRefuse(cycle) };
        if (NodeGraphHomes.OfLanding(kept, what).Problems is [var first, ..]) throw new DriverException(first.Words);
        next[key.Trim()] = kept;
        return next;
    }

    private static string CompactOrRefuse(string json)
    {
        try
        {
            return NodeGraphHomes.Compact(json);
        }
        catch (JsonException error)
        {
            throw new DriverException($"the cycle is not JSON: {error.Message}");
        }
    }

    /// <summary>
    /// The cycles of the kinds of work that do not land (FABRIC-GRAPH1; §D7's <c>workCycles</c>): by repository, then by unit kind
    /// (<see cref="NodeGraphHomes.WorkCycleKinds"/>), each a whole graph's JSON text, compact. Absent is none, today's.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> WorkCycles { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's work cycles (§D7's <c>workspaceWorkCycles</c>), by kind, for each repository in it naming none of that kind.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> WorkspaceWorkCycles { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set a repository's cycle for a kind of work, or clear it with null to take its workspace's. A kind with no work cycle is refused,
    /// a branch's included (its cycle is its landing rule's, §D7), and so is a graph that does not hold, naming the first problem.
    /// </summary>
    public DriverConfig WithWorkCycle(string repository, string kind, string? graph) =>
        this with { WorkCycles = SetWorkCycle(WorkCycles, repository, kind, graph) };

    /// <summary>Set a workspace's cycle for a kind of work, or clear it with null.</summary>
    public DriverConfig WithWorkspaceWorkCycle(string workspace, string kind, string? graph) =>
        this with { WorkspaceWorkCycles = SetWorkCycle(WorkspaceWorkCycles, workspace, kind, graph) };

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SetWorkCycle(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> map, string key, string kind, string? graph)
    {
        if (!NodeGraphHomes.WorkCycleKinds.Contains(kind))
        {
            throw new DriverException(kind == WorkUnitKind.Branch
                ? "a branch's cycle is its landing rule's `cycle`, so that a repository's landing and its end move together (D164); no kind has two homes."
                : $"`{kind}` is no kind of work with a cycle of its own here — {string.Join(" or ", NodeGraphHomes.WorkCycleKinds.Select(each => $"`{each}`"))}.");
        }

        var kinds = new Dictionary<string, string>(map.GetValueOrDefault(key.Trim()) ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        if (graph is null) kinds.Remove(kind);
        else
        {
            var kept = CompactOrRefuse(graph);
            if (NodeGraphs.Read(kept).Problems is [var first, ..]) throw new DriverException(first.Words);
            kinds[kind] = kept;
        }

        var next = new Dictionary<string, IReadOnlyDictionary<string, string>>(map, StringComparer.OrdinalIgnoreCase);
        var spelled = next.Keys.FirstOrDefault(name => string.Equals(name, key.Trim(), StringComparison.OrdinalIgnoreCase)) ?? key.Trim();
        next.Remove(spelled);
        if (kinds.Count > 0) next[spelled] = kinds;
        return next;
    }

    /// <summary>
    /// The work cycles under <paramref name="name"/>: each repository's or workspace's kinds of work, every kind kept as written, compact, null
    /// being none (the review's finding 2: a kind with no work cycle, or a cycle that is not an object, was dropped by an edit beside it).
    /// Only <see cref="NodeGraphHomes.WorkCycleKinds"/> govern; whether a graph holds is said where it is read (<see cref="NodeGraphHomes.Work"/>),
    /// and a kind no unit reads by <see cref="NodeGraphHomes.Strays"/>. An owner naming no kind is not read, and the first owner written in
    /// any case governs (finding 3). The CLI's <c>cycles.ts</c> reads it the same way.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> WorkCycleMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;
        foreach (var owner in element.EnumerateObject())
        {
            if (owner.Value.ValueKind != JsonValueKind.Object || map.ContainsKey(owner.Name)) continue;
            var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kind in owner.Value.EnumerateObject())
            {
                if (kind.Value.ValueKind != JsonValueKind.Null) kinds.TryAdd(kind.Name, NodeGraphHomes.Compact(kind.Value));
            }

            if (kinds.Count > 0) map[owner.Name] = kinds;
        }

        return map;
    }

    private static void WriteWorkCycles(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (owner, kinds) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(owner);
            foreach (var (kind, cycle) in kinds.OrderBy(pair => pair.Key, StringComparer.Ordinal)) WriteCycle(writer, kind, cycle);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>A cycle the config keeps as JSON text, written as JSON in the file's own layout.</summary>
    private static void WriteCycle(Utf8JsonWriter writer, string name, string cycle)
    {
        using var document = JsonDocument.Parse(cycle);
        writer.WritePropertyName(name);
        document.RootElement.WriteTo(writer);
    }

    /// <summary>
    /// Whether a repository's checkout may be read by agents outside it (READ1, D107), as the person set
    /// it, by repository. It wins over the workspace's; absent takes the workspace's, then on
    /// (<see cref="AcrossRules.Reading"/>).
    /// </summary>
    public IReadOnlyDictionary<string, bool> ReadAcross { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A workspace's reading across, for every repository in it that sets none of its own.</summary>
    public IReadOnlyDictionary<string, bool> WorkspaceReadAcross { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The declared relationships (D107): the repositories each repository's sessions may also write into.
    /// One direction per entry; absent is none, which is the default.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> WriteAcross { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set whether a repository's checkout is read across, or clear it with null to take its workspace's.</summary>
    public DriverConfig WithReadAcross(string repository, bool? read) =>
        this with { ReadAcross = SetFlag(ReadAcross, repository, read) };

    /// <summary>Set a workspace's reading across, or clear it with null to take the default, on.</summary>
    public DriverConfig WithWorkspaceReadAcross(string workspace, bool? read) =>
        this with { WorkspaceReadAcross = SetFlag(WorkspaceReadAcross, workspace, read) };

    /// <summary>
    /// Declare that <paramref name="repository"/>'s sessions may write into <paramref name="to"/>, or take
    /// that back. A repository never names itself: its own tree is already its sessions'.
    /// </summary>
    public DriverConfig WithWriteAcross(string repository, string to, bool allow)
    {
        if (AcrossRules.Problem(repository, to) is { } problem) throw new DriverException(problem);

        bool Same(string name) => string.Equals(name, to.Trim(), StringComparison.OrdinalIgnoreCase);
        var next = new Dictionary<string, IReadOnlyList<string>>(WriteAcross, StringComparer.OrdinalIgnoreCase);
        var held = next.GetValueOrDefault(repository.Trim()) ?? [];
        IReadOnlyList<string> kept = allow
            ? (held.Any(Same) ? held : [.. held, to.Trim()])
            : [.. held.Where(name => !Same(name))];

        // The repository's existing spelling, when it has one in another case: one entry, never two.
        var key = next.Keys.FirstOrDefault(k => string.Equals(k, repository.Trim(), StringComparison.OrdinalIgnoreCase)) ?? repository.Trim();
        next.Remove(key);
        if (kept.Count > 0) next[key] = kept;
        return this with { WriteAcross = next };
    }

    private static IReadOnlyDictionary<string, bool> SetFlag(IReadOnlyDictionary<string, bool> map, string key, bool? value)
    {
        var next = new Dictionary<string, bool>(map, StringComparer.OrdinalIgnoreCase);
        if (value is { } set) next[key.Trim()] = set;
        else next.Remove(key.Trim());
        return next;
    }

    private static IReadOnlyDictionary<string, LandingRule> Set(
        IReadOnlyDictionary<string, LandingRule> map, string key, LandingRule? rule)
    {
        var next = new Dictionary<string, LandingRule>(map, StringComparer.OrdinalIgnoreCase);
        if (rule is null) next.Remove(key);
        else
        {
            // FABRIC-GRAPH1: a door with no field for the cycle (Setup's landing row, Ask Daoris, the terminal) keeps the one the rule
            // has; only WithLandingCycle sets or clears it. A cycle the new form no longer reaches is said where it is read, never dropped.
            if (rule.Cycle is null && map.TryGetValue(key, out var held) && held.Cycle is not null) rule = rule with { Cycle = held.Cycle };
            next[key] = LandingRules.Problem(rule) is { } problem ? throw new DriverException(problem) : Kept(rule);
        }

        return next;
    }

    /// <summary>A rule as it is kept: a merge carries no pattern, whatever it was handed.</summary>
    private static LandingRule Kept(LandingRule rule) => rule.Form == LandingForm.Merge ? rule with { Pattern = null } : rule;

    private static IReadOnlyDictionary<string, string> Set(IReadOnlyDictionary<string, string> map, string key, string? branch)
    {
        var next = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        if (branch is null) next.Remove(key);
        else next[key] = BranchName.IsValid(branch)
            ? branch
            : throw new DriverException($"`{branch}` is not a branch name git would take.");
        return next;
    }

    public const string PathVariable = "DAORIS_DRIVER_CONFIG";

    /// <summary>
    /// The conventional home, beside the store the driver watches — under the Daoris home (D63). A
    /// machine with no home and no override is refused, naming what to set, rather than written to. The file is the home's
    /// <see cref="HomeEntry.DriverConfig"/>, asked here alone (CONFIGSEAM1): a reader that joined it to its home itself read another
    /// file than the loop's wherever the override named one, and so landed without the override's landing and review rules.
    /// </summary>
    public static string DefaultPath => DaorisHome.Require(HomeEntry.DriverConfig);

    /// <summary>
    /// The one resolution of the person's choices (D63, CONFIGSEAM1): the override where set, the home's <c>driver.json</c>
    /// otherwise, and null on a machine that names neither. Every reader comes here, through this or a <c>ResolvePath</c>, and
    /// <c>ConfigSeamTests</c> scans for one that does not.
    /// </summary>
    public static string? FindPath(Func<string, string?> environment) =>
        environment(PathVariable) ?? DaorisHome.File(environment, HomeEntry.DriverConfig);

    /// <summary>The same, from the process environment: for a reader with no home in hand that reads nothing on a machine with none.</summary>
    public static string? FindPath() => FindPath(Environment.GetEnvironmentVariable);

    /// <summary>The file every door reads and writes — the override, or the conventional home; refused, naming what to set, with neither.</summary>
    public static string ResolvePath() => FindPath() ?? DefaultPath;

    /// <summary>
    /// The same file, for a door that holds its home rather than the path — the trees and every landing, a plugin's page, a chat
    /// going on (CONFIGSEAM1): the one resolution where it is a file in this home, as every home is the folder its config sits in
    /// (<see cref="HomeOf"/>), and this home's <c>driver.json</c> otherwise. Never another home's file: an override naming one
    /// elsewhere is another machine's, or another test's.
    /// </summary>
    public static string ResolvePath(string home) => ResolvePath(Environment.GetEnvironmentVariable, home);

    /// <summary>The same, over an environment a test hands in.</summary>
    public static string ResolvePath(Func<string, string?> environment, string home) =>
        FindPath(environment) is { Length: > 0 } found && SameFolder(HomeOf(found), home) ? found : DaorisHome.Entry(home, HomeEntry.DriverConfig);

    private static bool SameFolder(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// The Daoris home a config file lives in — the directory holding <c>driver.json</c>, which every
    /// door derives the home from. Seven places wrote this expression out (REV3 CLEAN1).
    /// </summary>
    public static string HomeOf(string configPath) => Path.GetDirectoryName(Path.GetFullPath(configPath))!;

    /// <summary>
    /// The text this config was read from, or null for one built in memory (FABRIC-GRAPH1's review, finding 1): the writer keeps from it
    /// every key it does not own, in its place, and every value no edit changed, as written (<see cref="ToJson"/>).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// The file <see cref="Source"/> was read from (<see cref="Load"/>), whole, or null for a text parsed in memory: <see cref="Save"/> makes
    /// an edit onto that file as it stands, and writes over <see cref="Source"/> anywhere else (FABRIC-GRAPH1's re-check, finding 1).
    /// </summary>
    public string? SourceFile { get; init; }

    /// <summary>
    /// A missing file is a machine that has opted nothing in — the empty config's choices, not an error. They carry the path and <c>{}</c>
    /// as their text (FABRIC-GRAPH1's second re-check, finding 2), so their <see cref="Save"/> is an edit like any other: read without a
    /// text, they were written whole over a file another writer made before the save, a file that did not read or held a key twice
    /// included.
    /// </summary>
    /// <exception cref="DriverConfigUnreadableException">
    /// The file is there and does not read (CONFIGREAD1): it could not be opened, is not JSON, or is not the choices' shape.
    /// The sentence names the file, and the line and byte where the parser stopped; every door says it as it says a refusal.
    /// </exception>
    public static DriverConfig Load(string path)
    {
        if (!File.Exists(path)) return Empty with { Source = "{}", SourceFile = Path.GetFullPath(path) };

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new DriverConfigUnreadableException(path, $"{path} could not be read ({error.Message.TrimEnd('.')}).");
        }

        try
        {
            return Parse(text) with { SourceFile = Path.GetFullPath(path) };
        }
        catch (JsonException error) when (error.LineNumber is { } line && error.BytePositionInLine is { } position)
        {
            // The parser counts both from zero and repeats them at the end of its message; a person counts from one.
            var reason = error.Message;
            var counts = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);
            if (counts >= 0) reason = reason[..counts];
            throw new DriverConfigUnreadableException(
                path,
                $"{path} is not readable JSON at line {line + 1}, byte {position + 1} ({reason.TrimEnd('.', ' ')}). "
                + DriverConfigUnreadableException.Remedy,
                line + 1,
                position + 1);
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException)
        {
            // JSON that parsed and is not the choices' shape: Parse's own words, or the runtime's where a reader below it
            // met a value of a kind it does not take.
            throw new DriverConfigUnreadableException(
                path, $"{path} does not read: {error.Message.TrimEnd('.', ' ')}. {DriverConfigUnreadableException.Remedy}");
        }
    }

    /// <summary>
    /// The arrays of <c>driver.json</c> that are sets (CONFIGMERGE1, D173's CONFIG1 note): an edit of one is the names it added and took,
    /// compared without case, made onto the file's array as it stands, so two writers each adding a repository keep both
    /// (<see cref="JsonKeeping.Kept"/>). Each is a key path, <c>*</c> any one name; every other array is a value, written whole
    /// (<c>commands</c>' lists are ordered, <c>heavyGates</c>' read whole or not at all, a cycle's its own). Duplicated deliberately as
    /// the CLI's <c>DRIVER_SETS</c>; both are held to <c>test/fixtures/driver-config-keeps.json</c>'s <c>sets</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Sets = ["drivable", "holds", "trees", "writeAcross.*"];

    /// <summary>
    /// Write the choices back — atomically, beside-then-rename, like every write in this family: the
    /// watch loop re-reads this file every tick, and a torn read must never be what it finds.
    /// </summary>
    /// <remarks>
    /// 🔴 The edit (what changed between <see cref="Source"/> as read and these choices) is made onto the file read again here, under the
    /// same keep rules (FABRIC-GRAPH1's re-check, finding 1): written over <see cref="Source"/>, a change another writer made in between was
    /// lost, and a file that had since stopped reading was overwritten. A file that does not read now, or holds a key twice in one object,
    /// is refused and left as it is; one that is gone is written from nothing. The edit touches only the key paths it changed (the second
    /// re-check, finding 1, <see cref="JsonKeeping"/>): an object taken out since is made again with only the edit's keys, and one that is
    /// no longer an object refuses it. A config built in memory is written whole, and one parsed from a text (<see cref="Parse"/>) or read
    /// from another file over that text (<see cref="ToJson"/>). The CLI's <c>writeDriverChoices</c> does the same.
    /// </remarks>
    /// <exception cref="DriverConfigUnreadableException">
    /// The file there now does not read, holds a key twice, or holds no object where this edit changes something inside one; nothing was
    /// written.
    /// </exception>
    public void Save(string path)
    {
        var file = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string text;
        try
        {
            text = Source is not null && string.Equals(SourceFile, file, same) ? JsonKeeping.Kept(Current(path), Understood(), Written(), Sets) : ToJson();
        }
        catch (JsonEditRefusedException refused)
        {
            // The second re-check's finding 1: another writer made a value this edit changes something inside no longer an object.
            throw new DriverConfigUnreadableException(path, $"{path} has changed under this edit: {refused.Message}, so this edit was not written. "
                + DriverConfigUnreadableException.Remedy);
        }

        AtomicFile.WriteText(path, text);
    }

    /// <summary>The file at <paramref name="path"/> as it stands, for an edit to be written over; <c>{}</c> where it is gone.</summary>
    private static string Current(string path)
    {
        DriverConfig now;
        try
        {
            now = Load(path);
        }
        catch (DriverConfigUnreadableException error)
        {
            throw new DriverConfigUnreadableException(
                error.Path, $"{error.Message} This edit was not written: a file that does not read is never overwritten.", error.Line, error.Byte);
        }

        if (now.Source is not { } text) return "{}\n";
        if (JsonKeeping.Twice(text) is { } key)
        {
            throw new DriverConfigUnreadableException(path, $"{path} holds the key `{key}` twice in one object, so an edit cannot tell which it "
                + $"changes; this edit was not written. {DriverConfigUnreadableException.Remedy}");
        }

        return text;
    }

    /// <summary>The choices as their reader understood <see cref="Source"/>, written: what an edit is measured from.</summary>
    private string Understood() => (Parse(Source!) with { Source = null }).Written();

    /// <summary>
    /// The file's text: the choices written over the text they were read from (<see cref="Source"/>), so every key this build does not own
    /// stays where it stands and every value no edit changed stays as written (<see cref="JsonKeeping"/>, FABRIC-GRAPH1's review, finding 1);
    /// read and written with no edit, the same bytes. A config built in memory is written whole. <see cref="Save"/> writes the edit over the
    /// file as it stands instead.
    /// </summary>
    public string ToJson() => Source is null ? Written() : JsonKeeping.Kept(Source, Understood(), Written(), Sets);

    /// <summary>The choices' own shape, written by hand for the same AOT reason it is read by hand.</summary>
    private string Written()
    {
        return JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("drivable");
            foreach (var name in Drivable) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteStartArray("holds");
            foreach (var name in Holds) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteStartArray("trees");
            foreach (var name in Trees) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteNumber("cap", Cap);
            writer.WriteString("adapter", Adapter);
            // Written only when named: absent IS off, and the CLI twin writes it the same way.
            if (IntakeAdapter is not null) writer.WriteString("intakeAdapter", IntakeAdapter);
            if (HelperAdapter is not null) writer.WriteString("helperAdapter", HelperAdapter);
            // Written only while full (HELPMODE1a1): absent is restricted, and the CLI twin writes it the same way.
            if (HelpMode == HelpModes.Full) writer.WriteString("helpMode", HelpMode);
            writer.WriteBoolean("notify", Notify);
            writer.WriteNumber("timeoutMinutes", TimeoutMinutes);
            writer.WriteNumber("pollSeconds", PollSeconds);
            writer.WriteNumber("strikes", Strikes);
            // Written only when set (TOOL4e): absent is the default, and an edit elsewhere must not pin today's default.
            if (CoolOffMinutes is { } coolOff) writer.WriteNumber("cooloff", coolOff);
            writer.WriteStartObject("forgiven");
            foreach (var (quest, mark) in Forgiven.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                writer.WriteNumber(quest, mark);
            }

            writer.WriteEndObject();
            // Written only when set (SESSUX1b), as the CLI writes it: absent is no release.
            WriteMap(writer, "released", Released);
            // Written only when set (PAUSE1a), as the CLI keeps them: absent is no pause.
            WritePauses(writer, "pausedAsks", PausedAsks);
            WritePauses(writer, "pausedQuests", PausedQuests);
            // Written only when set (KNOWUSE1b), as the CLI writes it: absent is no standing answer.
            WriteStanding(writer, Standing);
            // Written only when set (WSR2): absent is the checkout's guess, and a file that never chose
            // a line should not start carrying an empty one.
            WriteMap(writer, "lines", Lines);
            WriteMap(writer, "workspaceLines", WorkspaceLines);
            // Written only when set (LANG1c), as the CLI writes them: absent is no language, and no line handed.
            WriteMap(writer, "languages", Languages);
            WriteMap(writer, "workspaceLanguages", WorkspaceLanguages);
            // Written only when set (WSR1), for the same reason: absent is the merge it always was.
            WriteRules(writer, "landings", Landings);
            WriteRules(writer, "workspaceLandings", WorkspaceLandings);
            // Written only when set (FABRIC-GRAPH1), as the CLI writes them: absent is no cycle for a quest or an ask, today's.
            WriteWorkCycles(writer, "workCycles", WorkCycles);
            WriteWorkCycles(writer, "workspaceWorkCycles", WorkspaceWorkCycles);
            // Written only when set (REVIEWENV1a), as the CLI writes them: absent is no review environment, today's behaviour.
            ReviewRules.WriteMap(writer, "reviews", Reviews);
            ReviewRules.WriteMap(writer, "workspaceReviews", WorkspaceReviews);
            // Written only when set (XAGENT1a), as the CLI writes them: absent is no second opinion, today's behaviour.
            OpinionRules.WriteMap(writer, "opinions", Opinions);
            OpinionRules.WriteMap(writer, "workspaceOpinions", WorkspaceOpinions);
            // Written only when set (MIXAGENT2a-i), as the CLI writes them: absent is the default, `ask`.
            HelperRules.WriteMap(writer, "helpers", Helpers);
            HelperRules.WriteMap(writer, "workspaceHelpers", WorkspaceHelpers);
            // Written only when set (BRAINSTORM1b), as the CLI writes them: absent is D176 point 9's proposed defaults.
            BrainstormRules.WriteMachine(writer, Brainstorms);
            BrainstormRules.WriteMap(writer, "workspaceBrainstorms", WorkspaceBrainstorms);
            CommitRules.WriteMap(writer, "commits", Commits);
            CommitRules.WriteMap(writer, "workspaceCommits", WorkspaceCommits);
            // Written only when set (WORKFLOW1e), as the CLI writes them: absent is Current, today's behaviour.
            WorkflowSelection.WriteMap(writer, "workflows", Workflows, workspace: false);
            WorkflowSelection.WriteMap(writer, "workspaceWorkflows", WorkspaceWorkflows, workspace: true);
            // Written only when set (AUTONOMY1a), as the CLI writes them: absent is today's behaviour, and nothing loosens by itself.
            AccessRules.WriteMap(writer, "access", Access);
            AccessRules.WriteMap(writer, "workspaceAccess", WorkspaceAccess);
            AccessRules.WriteMachine(writer, MachineAccess);
            // Written only when set (HEAVYRUN1b2a), as the CLI writes it: absent is the repository's own heavy gates alone.
            HeavyMapping.WriteMap(writer, HeavyGates);
            // Written only when set (D107), for the same reason: absent is reading on and no relationship.
            WriteFlags(writer, "readAcross", ReadAcross);
            WriteFlags(writer, "workspaceReadAcross", WorkspaceReadAcross);
            if (WriteAcross.Count > 0)
            {
                writer.WriteStartObject("writeAcross");
                foreach (var (repository, targets) in WriteAcross.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartArray(repository);
                    foreach (var target in targets) writer.WriteStringValue(target);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteStartObject("commands");
            foreach (var (name, command) in Commands.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                writer.WriteStartArray(name);
                foreach (var part in command) writer.WriteStringValue(part);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(key, value);
        writer.WriteEndObject();
    }

    /// <summary>The design's shape (§2.5): each id an object, its time in UTC to the second where known, and its stops always, none as <c>{}</c>.</summary>
    private static void WritePauses(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, WorkPause> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (id, pause) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(id);
            if (pause.At is { } at) writer.WriteString("at", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            writer.WriteStartObject("stopped");
            foreach (var (quest, session) in pause.Stopped.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(quest, session);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// The pauses (PAUSE1a): each id an object, read where first written in any case. Its <c>at</c> is read only as ISO 8601
    /// writes a moment, and a time that does not read leaves the pause standing with its time unknown: the pause is the
    /// person's, and the time only says when. Its <c>stopped</c> is read as <c>released</c> is.
    /// </summary>
    private static IReadOnlyDictionary<string, WorkPause> PauseMap(JsonElement root, string name)
    {
        var pauses = new Dictionary<string, WorkPause>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return pauses;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object || property.Name.Length == 0 || pauses.ContainsKey(property.Name)) continue;
            pauses[property.Name] = new WorkPause(Moment(Json.NonEmpty(property.Value,"at")), SessionMap(property.Value, "stopped"));
        }

        return pauses;
    }

    /// <summary>The standing answers (KNOWUSE1b): each repository an object, its words, and its time in UTC to the second where known.</summary>
    private static void WriteStanding(Utf8JsonWriter writer, IReadOnlyDictionary<string, StandingAnswer> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject("standing");
        foreach (var (repository, answer) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(repository);
            writer.WriteString("says", answer.Says);
            if (answer.At is { } at) writer.WriteString("at", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// The standing answers (KNOWUSE1b): each repository an object whose <c>says</c> is text, read without the spaces around it,
    /// and read where first written in any case. Its <c>at</c> is read only as ISO 8601 writes a moment, and a time that does not
    /// read leaves the answer standing with its time unknown. Blank words, an entry that is not an object, and a map that is not
    /// one are none.
    /// </summary>
    private static IReadOnlyDictionary<string, StandingAnswer> StandingMap(JsonElement root, string name)
    {
        var standing = new Dictionary<string, StandingAnswer>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return standing;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object || property.Name.Trim().Length == 0 || standing.ContainsKey(property.Name)) continue;
            if (!property.Value.TryGetProperty("says", out var says) || says.ValueKind != JsonValueKind.String
                || says.GetString()?.Trim() is not { Length: > 0 } words)
            {
                continue;
            }

            standing[property.Name] = new StandingAnswer(words, Moment(Json.NonEmpty(property.Value,"at")));
        }

        return standing;
    }

    /// <summary>
    /// A moment as ISO 8601 writes it, and only so: to the second, with a fraction or not, in UTC or at an offset. The same
    /// forms <see cref="AccountCooling"/> reads (TOOL4d), and the CLI's <c>cooling.ts</c> (<c>isoMoment</c>): a lenient parse
    /// would read <i>Oct 2</i> as this year's.
    /// </summary>
    private static DateTimeOffset? Moment(string? text) => Moments.ReadStrict(text);

    private static void WriteFlags(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, bool> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteBoolean(key, value);
        writer.WriteEndObject();
    }

    /// <summary>
    /// A map of names to booleans; an entry of any other type is not read (D107). A name written twice in any case is read where first
    /// readable, as the CLI reads it (CASEMAP1, D173's CONFIG1 note: the last value was read here, each spelling there).
    /// </summary>
    private static IReadOnlyDictionary<string, bool> FlagMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False && property.Name.Trim() is { Length: > 0 } named
                    && !map.ContainsKey(named))
                {
                    map[named] = property.Value.GetBoolean();
                }
            }
        }

        return map;
    }

    /// <summary>
    /// The declared relationships (D107): each repository's list of other names, once each in any case, in
    /// the order first written. A list that is not one, an entry that is not a name, and the repository
    /// itself are not read; a repository left with none has no entry. A repository written twice in any case
    /// is read where first readable, as the CLI reads it (CASEMAP1: the last list was read here).
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> TargetMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array || property.Name.Trim().Length == 0 || map.ContainsKey(property.Name.Trim())) continue;
            var targets = new List<string>();
            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString()?.Trim() is { Length: > 0 } target
                    && AcrossRules.Problem(property.Name, target) is null
                    && !targets.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    targets.Add(target);
                }
            }

            if (targets.Count > 0) map[property.Name.Trim()] = targets;
        }

        return map;
    }

    private static void WriteRules(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, LandingRule> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, rule) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(key);
            writer.WriteString("form", rule.Form);
            if (rule.Pattern is not null) writer.WriteString("pattern", rule.Pattern);
            // Written only when named, after the pattern it serves (PROCESS1b-i): absent is the default key. The CLI writes it here too.
            if (rule.Key is not null) writer.WriteString("key", rule.Key);
            // Written only when named (D100): absent is the branch left for the person to push.
            if (rule.Plugin is not null) writer.WriteString("plugin", rule.Plugin);
            // Written only when on: absent is the tree staying, as it always has.
            if (rule.Tidy) writer.WriteBoolean("tidy", true);
            // Written only when on (LAND2a): absent is the person's press, as it always was. The CLI writes it here too.
            if (rule.AutoAccept) writer.WriteBoolean("autoAccept", true);
            // Written only when on (PROCESS1b-ii): absent is the branch at the session's own commits. The CLI writes it here too.
            if (rule.Squash) writer.WriteBoolean("squash", true);
            // Written only when set (FABRIC-GRAPH1), last, as written: absent is the default cycle, today's. The CLI writes it here too.
            if (rule.Cycle is not null) WriteCycle(writer, "cycle", rule.Cycle);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// A map of names to landing rules; one that could not land work — an unknown form, a pattern that
    /// names no branch, a merge naming a plugin or accepting automatically — is skipped. A ticket's key it refuses is ignored, the
    /// rest of the rule standing. A plugin not installed here is still read: the press says so, and the file stays what the
    /// person wrote. An empty key is none.
    /// </summary>
    private static IReadOnlyDictionary<string, LandingRule> RuleMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object) continue;
                // Only JSON `true` switches the tidy or the automatic acceptance on (LAND2a): `"true"` or 1 is not the
                // person's say-so for a push with no press.
                var rule = new LandingRule(
                    Json.NonEmpty(property.Value,"form") ?? "", Json.NonEmpty(property.Value,"pattern"),
                    property.Value.TryGetProperty("tidy", out var tidy) && tidy.ValueKind == JsonValueKind.True,
                    Json.NonEmpty(property.Value,"plugin"),
                    property.Value.TryGetProperty("autoAccept", out var auto) && auto.ValueKind == JsonValueKind.True,
                    Json.NonEmpty(property.Value,"key"),
                    // Only JSON `true` squashes (PROCESS1b-ii), as only it accepts automatically.
                    property.Value.TryGetProperty("squash", out var squash) && squash.ValueKind == JsonValueKind.True);
                // A key refused here is ignored and the rest of the rule stands (PROCESS1b-i): a key another reader took never
                // drops the rule, which would land the repository's work by its workspace's rule or a merge unasked.
                if (rule.Key is not null && LandingRules.Problem(rule) is not null) rule = rule with { Key = null };
                // FABRIC-GRAPH1: the cycle is kept as written whether or not it is a graph that holds, said where it is read, so a
                // mistyped end never changes how the work lands; only null is none (the review's finding 2: `[]` was dropped).
                if (property.Value.TryGetProperty("cycle", out var cycle) && cycle.ValueKind != JsonValueKind.Null) rule = rule with { Cycle = NodeGraphHomes.Compact(cycle) };
                // The first rule written in any case governs, as the CLI reads it, and a later spelling is said (the review's finding 3:
                // the driver kept the last and the terminal the first, so the two chose different graphs).
                if (LandingRules.Problem(rule) is null && !map.ContainsKey(property.Name)) map[property.Name] = Kept(rule);
            }
        }

        return map;
    }

    /// <summary>
    /// The session languages (LANG1c): each name a code of the table, read in any case without the spaces around it. A code
    /// the table does not hold, a value that is not text, a blank name and a map that is not one are not read; a name written
    /// twice in any case is read where first written.
    /// </summary>
    private static IReadOnlyDictionary<string, string> LanguageMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && SessionLanguages.Code(property.Value.GetString()) is { } code
                && property.Name.Trim() is { Length: > 0 } named && !map.ContainsKey(named))
            {
                map[named] = code;
            }
        }

        return map;
    }

    /// <summary>
    /// A map of names to branch names; an entry that is not one git would take is skipped. A name written twice in any case is read
    /// where first readable, as the CLI reads it (CASEMAP1: the first spelling was kept here with the last value).
    /// </summary>
    private static IReadOnlyDictionary<string, string> BranchMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is { } branch && BranchName.IsValid(branch) && !map.ContainsKey(property.Name))
                {
                    map[property.Name] = branch;
                }
            }
        }

        return map;
    }

    /// <summary>This machine's answer to "may I drive that repository, now" — one flag flipped at a time.</summary>
    public DriverConfig WithDrivable(string repository, bool drivable) => this with
    {
        Drivable = Toggle(Drivable, repository, drivable),
    };

    public DriverConfig WithHold(string repository, bool held) => this with
    {
        Holds = Toggle(Holds, repository, held),
    };

    /// <summary>Whether this repository's sessions open their own worktree (D51).</summary>
    public DriverConfig WithTrees(string repository, bool ownTree) => this with
    {
        Trees = Toggle(Trees, repository, ownTree),
    };

    /// <summary>The read side of <see cref="WithTrees"/> — one comparison rule for both.</summary>
    public bool OpensOwnTree(string repository) =>
        Trees.Contains(repository, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Toggle(IReadOnlyList<string> names, string repository, bool present)
    {
        var kept = names.Where(n => !string.Equals(n, repository, StringComparison.OrdinalIgnoreCase));
        return present ? [.. kept, repository] : [.. kept];
    }

    // Read by hand for the same reason the registration store writes by hand: nothing here may
    // quietly stop working under AOT, and the shape is small enough to be explicit about.
    public static DriverConfig Parse(string json)
    {
        // A key written twice in one object reads as its last value in its first place, as the CLI's JSON.parse reads it (the re-check's
        // finding 3); the text read stays as written, and no edit is written over it (Save).
        using var document = JsonDocument.Parse(JsonKeeping.Once(json));
        var root = document.RootElement;
        // Each field below asks the root for a property, which throws a runtime exception naming no file for any other kind
        // (CONFIGREAD1): said here, so the reader can say it with its file.
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("it holds no JSON object");

        return new DriverConfig(
            Drivable: Json.Strings(root,"drivable"),
            Holds: Json.Strings(root,"holds"),
            Cap: Math.Max(1, Int(root, "cap") ?? Empty.Cap),
            Adapter: Json.NonEmpty(root,"adapter") ?? Empty.Adapter,
            TimeoutMinutes: Math.Max(1, Int(root, "timeoutMinutes") ?? Empty.TimeoutMinutes),
            PollSeconds: Math.Max(1, Int(root, "pollSeconds") ?? Empty.PollSeconds),
            Commands: CommandMap(root),
            Trees: Json.Strings(root,"trees"),
            // Absent means ON (SURF5b): every machine that already has a driver.json predates this
            // field, and reading silence as "off" would ship the feature switched off everywhere it
            // matters most — a machine that has been driving for a while.
            Notify: Bool(root, "notify") ?? Empty.Notify,
            // 🔴 Absent means the DEFAULT, not off — the opposite reading from `notify` and for the
            // opposite reason. A machine that predates this field is exactly the one that has been
            // driving unattended longest, and reading silence as "never park" would leave it doing
            // the thing this field exists to stop.
            Strikes: Int(root, "strikes") ?? Empty.Strikes,
            Forgiven: ForgivenMap(root))
        {
            // The text read, which the writer keeps every key it does not own from (FABRIC-GRAPH1's review, finding 1).
            Source = json,
            // 🔴 Absent means OFF — see IntakeAdapter for why this is the opposite of `notify`.
            IntakeAdapter = Json.NonEmpty(root,"intakeAdapter")?.Trim() is { Length: > 0 } intake ? intake : null,
            // Absent means OFF, as the intake's does (D89).
            HelperAdapter = Json.NonEmpty(root,"helperAdapter")?.Trim() is { Length: > 0 } helper ? helper : null,
            // Restricted unless the file names full (HELPMODE1a1): never widened by a value it does not know.
            HelpMode = HelpModes.Named(Json.NonEmpty(root, "helpMode")) == HelpModes.Full ? HelpModes.Full : HelpModes.Restricted,
            // Absent, or less than a minute, or not a whole number, is the default hour (TOOL4e): never a spin.
            CoolOffMinutes = root.TryGetProperty("cooloff", out var coolOff) && coolOff.ValueKind == JsonValueKind.Number
                && coolOff.TryGetInt32(out var minutes) && minutes >= 1 ? minutes : null,
            Released = SessionMap(root, "released"),
            PausedAsks = PauseMap(root, "pausedAsks"),
            PausedQuests = PauseMap(root, "pausedQuests"),
            Standing = StandingMap(root, "standing"),
            Lines = BranchMap(root, "lines"),
            WorkspaceLines = BranchMap(root, "workspaceLines"),
            Languages = LanguageMap(root, "languages"),
            WorkspaceLanguages = LanguageMap(root, "workspaceLanguages"),
            Landings = RuleMap(root, "landings"),
            WorkspaceLandings = RuleMap(root, "workspaceLandings"),
            WorkCycles = WorkCycleMap(root, "workCycles"),
            WorkspaceWorkCycles = WorkCycleMap(root, "workspaceWorkCycles"),
            // A rule with a problem is not read, and `false` only for a repository (REVIEWENV1a).
            Reviews = ReviewRules.Map(root, "reviews", allowNone: true),
            WorkspaceReviews = ReviewRules.Map(root, "workspaceReviews", allowNone: false),
            // A rule with a problem is not read, and `false` only for a repository (XAGENT1a).
            Opinions = OpinionRules.Map(root, "opinions", allowNone: true),
            WorkspaceOpinions = OpinionRules.Map(root, "workspaceOpinions", allowNone: false),
            Helpers = HelperRules.Map(root, "helpers"),
            WorkspaceHelpers = HelperRules.Map(root, "workspaceHelpers"),
            Brainstorms = BrainstormRules.Machine(root),
            WorkspaceBrainstorms = BrainstormRules.Map(root, "workspaceBrainstorms"),
            Commits =CommitRules.Map(root, "commits"),
            WorkspaceCommits = CommitRules.Map(root, "workspaceCommits"),
            // An entry with a problem, or naming nothing, is not read (WORKFLOW1e).
            Workflows = WorkflowSelection.Map(root, "workflows", workspace: false),
            WorkspaceWorkflows = WorkflowSelection.Map(root, "workspaceWorkflows", workspace: true),
            // Read as a stored shape (AUTONOMY1a): a pause this build does not know passed over, a value it does not know read as today's.
            Access = AccessRules.Map(root, "access"),
            WorkspaceAccess = AccessRules.Map(root, "workspaceAccess"),
            MachineAccess = AccessRules.Machine(root),
            // Read as a stored shape (HEAVYRUN1b2a): a list with a gate that does not read is not read, whole.
            HeavyGates = HeavyMapping.Map(root),
            ReadAcross = FlagMap(root, "readAcross"),
            WorkspaceReadAcross = FlagMap(root, "workspaceReadAcross"),
            WriteAcross = TargetMap(root, "writeAcross"),
        };
    }

    /// <summary>
    /// The releases (SESSUX1b), and a pause's stops (PAUSE1a): each quest against a session's id, read without the spaces
    /// around it. A blank session, one that is not text, a map that is not one, and a quest written again in another case
    /// are not read.
    /// </summary>
    private static IReadOnlyDictionary<string, string> SessionMap(JsonElement container, string name)
    {
        var released = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!container.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return released;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()?.Trim() is { Length: > 0 } session
                && property.Name.Length > 0 && !released.ContainsKey(property.Name))
            {
                released[property.Name] = session;
            }
        }

        return released;
    }

    private static IReadOnlyDictionary<string, int> ForgivenMap(JsonElement root)
    {
        var forgiven = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("forgiven", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                // A quest written twice in any case is read where first readable, as the CLI reads it (CASEMAP1).
                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var mark) && mark > 0 && !forgiven.ContainsKey(property.Name))
                {
                    forgiven[property.Name] = mark;
                }
            }
        }

        return forgiven;
    }

    /// <summary>Whether this machine says so when a session parks or ends unasked (SURF5b).</summary>
    public DriverConfig WithNotify(bool notify) => this with { Notify = notify };

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CommandMap(JsonElement root)
    {
        var commands = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("commands", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var items = new List<string>();
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
                        {
                            items.Add(text);
                        }
                    }
                }

                commands[property.Name] = items;
            }
        }

        return commands;
    }

    private static bool? Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    // A number that is not a whole one is refused as it always was, now in words that name the field (CONFIGREAD1): the
    // runtime's own said only that an item was in an invalid format.
    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt32(out var number) ? number : throw new JsonException($"`{name}` is not a whole number")
            : null;
}

/// <summary>
/// One pause of an ask's work or a quest's, as <c>driver.json</c> keeps it (PAUSE1a, design §2.5). Machine-local, like
/// everything in that file: a pause is this machine's (D132 point 10).
/// </summary>
/// <param name="At">When the person paused it, or null where the file does not say in a form this reads.</param>
/// <param name="Stopped">Each quest the pause stopped, against the session it stopped: what <i>Resume</i> writes into <see cref="DriverConfig.Released"/> (design §2.4).</param>
public sealed record WorkPause(DateTimeOffset? At, IReadOnlyDictionary<string, string> Stopped);
