using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's standing choices, machine-local by nature (D46 §6/§7): which repositories THIS machine
/// may drive, what is held, how wide the loop runs, and which adapter spawns.
/// </summary>
/// <param name="Drivable">Repositories the person opted in. Empty means the driver drives nothing.</param>
/// <param name="Holds">Repositories the person paused — drivable, but not now.</param>
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
        else next[key] = LandingRules.Problem(rule) is { } problem ? throw new DriverException(problem) : Kept(rule);
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
    /// machine with no home and no override is refused, naming what to set, rather than written to.
    /// </summary>
    public static string DefaultPath => DaorisHome.Require("driver.json");

    /// <summary>The file every door reads and writes — the override, or the conventional home.</summary>
    public static string ResolvePath() =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath;

    /// <summary>
    /// The Daoris home a config file lives in — the directory holding <c>driver.json</c>, which every
    /// door derives the home from. Seven places wrote this expression out (REV3 CLEAN1).
    /// </summary>
    public static string HomeOf(string configPath) => Path.GetDirectoryName(Path.GetFullPath(configPath))!;

    /// <summary>A missing file is a machine that has opted nothing in — the empty config, not an error.</summary>
    public static DriverConfig Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;

    /// <summary>
    /// Write the choices back — atomically, beside-then-rename, like every write in this family: the
    /// watch loop re-reads this file every tick, and a torn read must never be what it finds.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        AtomicFile.WriteText(path, ToJson());
    }

    /// <summary>The file's shape, written by hand for the same AOT reason it is read by hand.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
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
            // Written only when set (WSR2): absent is the checkout's guess, and a file that never chose
            // a line should not start carrying an empty one.
            WriteMap(writer, "lines", Lines);
            WriteMap(writer, "workspaceLines", WorkspaceLines);
            // Written only when set (WSR1), for the same reason: absent is the merge it always was.
            WriteRules(writer, "landings", Landings);
            WriteRules(writer, "workspaceLandings", WorkspaceLandings);
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
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(key, value);
        writer.WriteEndObject();
    }

    private static void WriteFlags(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, bool> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteBoolean(key, value);
        writer.WriteEndObject();
    }

    /// <summary>A map of names to booleans; an entry of any other type is not read (D107).</summary>
    private static IReadOnlyDictionary<string, bool> FlagMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False && property.Name.Trim().Length > 0)
                {
                    map[property.Name.Trim()] = property.Value.GetBoolean();
                }
            }
        }

        return map;
    }

    /// <summary>
    /// The declared relationships (D107): each repository's list of other names, once each in any case, in
    /// the order first written. A list that is not one, an entry that is not a name, and the repository
    /// itself are not read; a repository left with none has no entry.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> TargetMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array || property.Name.Trim().Length == 0) continue;
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
            // Written only when named (D100): absent is the branch left for the person to push.
            if (rule.Plugin is not null) writer.WriteString("plugin", rule.Plugin);
            // Written only when on: absent is the tree staying, as it always has.
            if (rule.Tidy) writer.WriteBoolean("tidy", true);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// A map of names to landing rules; one that could not land work — an unknown form, a pattern that
    /// names no branch, a merge naming a plugin — is skipped. A plugin not installed here is still read:
    /// the press says so, and the file stays what the person wrote.
    /// </summary>
    private static IReadOnlyDictionary<string, LandingRule> RuleMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object) continue;
                var rule = new LandingRule(
                    String(property.Value, "form") ?? "", String(property.Value, "pattern"),
                    property.Value.TryGetProperty("tidy", out var tidy) && tidy.ValueKind == JsonValueKind.True,
                    String(property.Value, "plugin"));
                if (LandingRules.Problem(rule) is null) map[property.Name] = Kept(rule);
            }
        }

        return map;
    }

    /// <summary>A map of names to branch names; an entry that is not one git would take is skipped.</summary>
    private static IReadOnlyDictionary<string, string> BranchMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is { } branch && BranchName.IsValid(branch))
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
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new DriverConfig(
            Drivable: Strings(root, "drivable"),
            Holds: Strings(root, "holds"),
            Cap: Math.Max(1, Int(root, "cap") ?? Empty.Cap),
            Adapter: String(root, "adapter") ?? Empty.Adapter,
            TimeoutMinutes: Math.Max(1, Int(root, "timeoutMinutes") ?? Empty.TimeoutMinutes),
            PollSeconds: Math.Max(1, Int(root, "pollSeconds") ?? Empty.PollSeconds),
            Commands: CommandMap(root),
            Trees: Strings(root, "trees"),
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
            // 🔴 Absent means OFF — see IntakeAdapter for why this is the opposite of `notify`.
            IntakeAdapter = String(root, "intakeAdapter")?.Trim() is { Length: > 0 } intake ? intake : null,
            // Absent means OFF, as the intake's does (D89).
            HelperAdapter = String(root, "helperAdapter")?.Trim() is { Length: > 0 } helper ? helper : null,
            // Absent, or less than a minute, or not a whole number, is the default hour (TOOL4e): never a spin.
            CoolOffMinutes = root.TryGetProperty("cooloff", out var coolOff) && coolOff.ValueKind == JsonValueKind.Number
                && coolOff.TryGetInt32(out var minutes) && minutes >= 1 ? minutes : null,
            Released = ReleasedMap(root),
            Lines = BranchMap(root, "lines"),
            WorkspaceLines = BranchMap(root, "workspaceLines"),
            Landings = RuleMap(root, "landings"),
            WorkspaceLandings = RuleMap(root, "workspaceLandings"),
            ReadAcross = FlagMap(root, "readAcross"),
            WorkspaceReadAcross = FlagMap(root, "workspaceReadAcross"),
            WriteAcross = TargetMap(root, "writeAcross"),
        };
    }

    /// <summary>
    /// The releases (SESSUX1b): each quest against a session's id, read without the spaces around it. A blank session, one
    /// that is not text, a map that is not one, and a quest written again in another case are not read.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReleasedMap(JsonElement root)
    {
        var released = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("released", out var element) || element.ValueKind != JsonValueKind.Object) return released;

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
                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var mark) && mark > 0)
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

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    private static bool? Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}
