using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A second-opinion rule (XAGENT1a, D155 point 3, the second-agent design §2.3): the reviewers, adapters in the person's
/// order, and when they read work by themselves. With no reviewer it is <see cref="None"/>, a repository's <c>false</c>: no
/// second opinion here, whatever its workspace says.
/// </summary>
/// <param name="Reviewers">Adapters, in the order they are tried; named by the person, never guessed (D23).</param>
/// <param name="On">The occasions it reads at by itself, <c>landing</c> then <c>steps</c>; <c>landing</c> where the file names none.</param>
/// <param name="Required">Whether work waits for the person when no reviewer can read it (§8.4).</param>
/// <param name="Verify">Whether a reviewer may build and run what the repository declares safe, in its own copy (§5.3).</param>
/// <param name="Minutes">The wall-clock bound of one pass as written, 5 to 120; null where the file names none.</param>
/// <param name="Recheck">Whether the commits a session makes in answer are read once more (§6.5); off only by JSON <c>false</c>.</param>
public sealed record OpinionRule(
    IReadOnlyList<string> Reviewers, IReadOnlyList<string> On, bool Required = false, bool Verify = false, int? Minutes = null,
    bool Recheck = true)
{
    /// <summary>A repository's <c>false</c>: no second opinion, whatever its workspace says.</summary>
    public static OpinionRule None { get; } = new([], []);

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Reviewers.Count == 0;

    /// <summary>The bound of one pass: as written, else <see cref="OpinionRules.DefaultMinutes"/>.</summary>
    public int Bound => Minutes ?? OpinionRules.DefaultMinutes;
}

/// <summary>Where a repository's second-opinion rule was set, for the screen, the room and the terminal.</summary>
public static class OpinionSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
}

/// <summary>A rule as it resolves for a repository, and where it was set (<see cref="OpinionSource"/>).</summary>
public sealed record ResolvedOpinion(OpinionRule Rule, string Source);

/// <summary>
/// What a set changes, as the terminal's flags, the screen's form and the shared table spell it, before it is judged: each
/// field null where it is not named. A list that is not one is spelled as the reader spells it, so it is refused in the same
/// words; minutes that are not a number are <see cref="double.NaN"/>.
/// </summary>
public sealed record OpinionSet
{
    public IReadOnlyList<string?>? Reviewers { get; init; }
    public IReadOnlyList<string?>? On { get; init; }
    public bool? Required { get; init; }
    public bool? Verify { get; init; }
    public double? Minutes { get; init; }
    public bool? Recheck { get; init; }

    /// <summary>Whether it names anything: a set that names nothing is no change.</summary>
    public bool Names => Reviewers is not null || On is not null || Required is not null || Verify is not null || Minutes is not null
        || Recheck is not null;
}

/// <summary>
/// One change to a second-opinion rule, as every door makes it (design §2.5): for a <see cref="Repository"/> or a
/// <see cref="Workspace"/>, <see cref="Set"/> what it names over the rule set there, say a repository has <see cref="None"/>,
/// or <see cref="Clear"/>.
/// </summary>
public sealed record OpinionEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public OpinionSet? Set { get; init; }
    public bool None { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// The second-opinion rule, <c>opinions</c> and <c>workspaceOpinions</c> in <c>driver.json</c> (XAGENT1a, D155 point 3, the
/// second-agent design §2.2–§2.6): read, refused, resolved, said and edited — the one place every door asks.
/// </summary>
/// <remarks>
/// <para>A TWIN with the CLI's <c>opinions.ts</c>: both hold one table, this suite's tests' <c>fixtures/opinion-rules.json</c>,
/// cell for cell (<c>OpinionRulesTests</c> and <c>driverconfig.test.ts</c>): the reading and its precedence, every refusal in
/// the same words, each door's sentences, the edits, and which reviewers are the working agent's own family.</para>
///
/// <para>Declared only (design §15): nothing here chooses a reviewer, starts a pass or holds a landing; XAGENT1b–f read it.</para>
/// </remarks>
public static class OpinionRules
{
    /// <summary>The occasion before a chain's work in a repository lands (design §2.1).</summary>
    public const string Landing = "landing";

    /// <summary>The occasion before a chain's next step starts.</summary>
    public const string Steps = "steps";

    /// <summary>The occasions a rule may name, in the order a rule keeps them — the CLI's <c>OPINION_OCCASIONS</c>.</summary>
    public static readonly IReadOnlyList<string> Occasions = [Landing, Steps];

    /// <summary>One pass's bound where the rule names none (design §2.3): a starting point, not a measurement.</summary>
    public const int DefaultMinutes = 20;

    /// <summary>Said by each door after what the rule lets a reviewer do, until the choice and the gate read it (XAGENT1b, XAGENT1f).</summary>
    public const string DeclaredOnly = "Declared only: nothing reads it yet, so no reviewer is chosen and no landing waits for it.";

    /// <summary>What stands where nothing is set anywhere (design §2.6): today's behaviour, said as such.</summary>
    public const string NoneSet = "None: no other agent reads work here.";

    private const int FewestMinutes = 5;
    private const int MostMinutes = 120;
    private const string RuleShape = "a second-opinion rule is its reviewers and when they read, or `false` for none here.";
    private const string NoReviewers = "a second-opinion rule names at least one reviewer: an adapter, in the order they are tried, such as `codex-acp`.";
    private const string ReviewerShape = "each reviewer is an adapter's name, such as `codex-acp` or `dsh`.";
    private const string OnShape = "`on` is `landing`, `steps` or both: when a reviewer reads work here by itself.";
    private const string MinutesShape = "`minutes` is a whole number from 5 to 120: how long one pass may take, 20 when absent.";
    /// <summary>The refusal of an edit naming neither a repository nor a workspace, or both; Ask Daoris's door says it first.</summary>
    internal const string Scope = "a second-opinion rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its reviewers and how they read, say none, or clear.";
    private const string NoneWorkspace = "`none` is a repository's: a workspace with no second opinion sets none, and `--clear` takes its rule away.";
    private const string NoneHere = "No second opinion here, whatever its workspace says: no other agent reads work here before it lands.";

    /// <summary>A rule as an entry or a door spells it, before it is judged: <see cref="On"/> null is absent.</summary>
    private sealed record Spelled(
        IReadOnlyList<string?>? Reviewers, IReadOnlyList<string?>? On, bool Required, bool Verify, double? Minutes, bool Recheck);

    /// <summary>
    /// A repository's entry read as a rule, with its first problem: <c>false</c> is <see cref="OpinionRule.None"/>; anything
    /// else must be an object naming at least one reviewer, then its occasions and its minutes judged. Only JSON <c>true</c>
    /// requires or verifies, only JSON <c>false</c> turns the recheck off, and what a rule has no field for is not kept.
    /// </summary>
    public static (OpinionRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.False) return (OpinionRule.None, null);
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);

        return Judge(new Spelled(
            value.TryGetProperty("reviewers", out var reviewers) && reviewers.ValueKind == JsonValueKind.Array ? Texts(reviewers) : null,
            // Absent or null is `landing`; anything else that is not a list is spelled as a list holding no occasion.
            value.TryGetProperty("on", out var on) && on.ValueKind != JsonValueKind.Null
                ? on.ValueKind == JsonValueKind.Array ? Texts(on) : [null]
                : null,
            value.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True,
            value.TryGetProperty("verify", out var verify) && verify.ValueKind == JsonValueKind.True,
            value.TryGetProperty("minutes", out var minutes) && minutes.ValueKind != JsonValueKind.Null ? Number(minutes) : null,
            !(value.TryGetProperty("recheck", out var recheck) && recheck.ValueKind == JsonValueKind.False)));
    }

    /// <summary>The rule judged in order — reviewers, occasions, minutes — the first problem said, or the rule as kept.</summary>
    private static (OpinionRule? Rule, string? Problem) Judge(Spelled spelled)
    {
        if (spelled.Reviewers is not { Count: > 0 } named) return (null, NoReviewers);
        var reviewers = new List<string>();
        foreach (var each in named)
        {
            if (each is null || each.Trim().Length == 0) return (null, ReviewerShape);
            var reviewer = each.Trim();
            if (reviewers.Any(kept => string.Equals(kept, reviewer, StringComparison.OrdinalIgnoreCase)))
            {
                return (null, $"`{reviewer}` is named twice — each reviewer once, in the order they are tried.");
            }

            reviewers.Add(reviewer);
        }

        var occasions = new HashSet<string>(StringComparer.Ordinal);
        if (spelled.On is null)
        {
            occasions.Add(Landing);
        }
        else
        {
            if (spelled.On.Count == 0) return (null, OnShape);
            foreach (var each in spelled.On)
            {
                if (each is null) return (null, OnShape);
                if (!Occasions.Contains(each)) return (null, $"`{each}` is not an occasion — `landing`, `steps` or both.");
                if (!occasions.Add(each)) return (null, $"`{each}` is named twice in `on` — `landing`, `steps` or both.");
            }
        }

        int? minutes = null;
        if (spelled.Minutes is { } written)
        {
            if (double.IsNaN(written) || written != Math.Floor(written) || written < FewestMinutes || written > MostMinutes)
            {
                return (null, MinutesShape);
            }

            minutes = (int)written;
        }

        return (new OpinionRule(reviewers, [.. Occasions.Where(occasions.Contains)], spelled.Required, spelled.Verify, minutes, spelled.Recheck), null);
    }

    private static List<string?> Texts(JsonElement list) =>
        [.. list.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString() : null)];

    private static double Number(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : double.NaN;

    /// <summary>
    /// A map of second-opinion rules as <c>driver.json</c> holds it: each name trimmed, a blank one not read; a rule with a
    /// problem, and <c>true</c>, not read; <c>false</c> read only where <paramref name="allowNone"/> (a repository's, never a
    /// workspace's); a name written twice in any case read where first readable. A map that is not one is none.
    /// </summary>
    internal static IReadOnlyDictionary<string, OpinionRule> Map(JsonElement root, string name, bool allowNone)
    {
        var map = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || (property.Value.ValueKind == JsonValueKind.False && !allowNone)) continue;
            if (Read(property.Value) is not ({ } rule, null) || map.ContainsKey(named)) continue;
            map[named] = rule;
        }

        return map;
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set: <c>false</c> for none, else the rule as kept.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, OpinionRule> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, rule) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, rule);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// One rule as kept: <c>false</c> for none; else its occasions and reviewers, then <c>required</c> and <c>verify</c> only
    /// when on, <c>minutes</c> only as written, and <c>recheck</c> only when off.
    /// </summary>
    public static void Write(Utf8JsonWriter writer, OpinionRule rule)
    {
        if (rule.IsNone)
        {
            writer.WriteBooleanValue(false);
            return;
        }

        writer.WriteStartObject();
        writer.WriteStartArray("on");
        foreach (var occasion in rule.On) writer.WriteStringValue(occasion);
        writer.WriteEndArray();
        writer.WriteStartArray("reviewers");
        foreach (var reviewer in rule.Reviewers) writer.WriteStringValue(reviewer);
        writer.WriteEndArray();
        if (rule.Required) writer.WriteBoolean("required", true);
        if (rule.Verify) writer.WriteBoolean("verify", true);
        if (rule.Minutes is { } minutes) writer.WriteNumber("minutes", minutes);
        if (!rule.Recheck) writer.WriteBoolean("recheck", false);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The second-opinion rule standing for a repository: its own (a rule or none), else its workspace's (one in no workspace
    /// being in <c>default</c>), else null, which is nothing set anywhere — names matched in any case, as every door matches them.
    /// </summary>
    public static ResolvedOpinion? Resolve(DriverConfig config, string repository, string? workspace) =>
        config.Opinions.TryGetValue(repository.Trim(), out var own)
            ? new ResolvedOpinion(own, OpinionSource.Repository)
            : config.WorkspaceOpinions.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? new ResolvedOpinion(shared, OpinionSource.Workspace)
                : null;

    /// <summary>Names in backticks, in the order they are tried: <c>a</c>, <c>a, else b</c>, <c>a, else b, else c</c>.</summary>
    private static string Else(IEnumerable<string> names) => string.Join(", else ", names.Select(name => $"`{name}`"));

    /// <summary>Names in backticks, the last after <c>and</c>: <c>a</c>, <c>a and b</c>, <c>a, b and c</c>.</summary>
    private static string And(IReadOnlyList<string> names)
    {
        var ticked = names.Select(name => $"`{name}`").ToList();
        return ticked.Count <= 1 ? string.Concat(ticked) : $"{string.Join(", ", ticked.Take(ticked.Count - 1))} and {ticked[^1]}";
    }

    /// <summary>
    /// What each door says as a rule is set (design §2.5): when its reviewers read and where their findings go, what they may
    /// run, what happens when none can read, the bound of a pass, a recheck turned off, and which of them are the working
    /// agent's own family — the CLI's <c>opinionSays</c>, word for word.
    /// </summary>
    /// <param name="sameAgent">The reviewers of the rule that are the working agent's own family (<see cref="SameAgent"/>).</param>
    public static IReadOnlyList<string> Says(OpinionRule rule, IReadOnlyCollection<string> sameAgent)
    {
        if (rule.IsNone) return [NoneHere];
        // Several reviewers are set apart by commas: "`codex-acp`, else `dsh`, reads it".
        var reviewers = Else(rule.Reviewers) + (rule.Reviewers.Count > 1 ? "," : "");
        const string Copy = "in a copy of its own that nothing is taken back from; its findings go to the session that did the work, and to you.";
        var said = new List<string>();
        if (rule.On.Contains(Landing))
        {
            said.Add($"Before work here lands, {reviewers} reads it, {Copy}");
            if (rule.On.Contains(Steps)) said.Add("It reads each step's work here too, before the chain's next step starts.");
        }
        else
        {
            said.Add($"Before a chain's next step starts, {reviewers} reads the work of the step before it here, {Copy}");
        }

        if (rule.Verify) said.Add("It may build and run what this repository declares safe, in that copy.");
        said.Add(rule.Required
            ? "If no reviewer can read it, the work waits for you."
            : "If no reviewer can read it, you are told so, and nothing waits.");
        said.Add(string.Create(CultureInfo.InvariantCulture, $"One pass takes at most {rule.Bound} minutes."));
        if (!rule.Recheck) said.Add("Commits made in answer to its findings are not read again.");

        var same = rule.Reviewers.Where(reviewer => sameAgent.Contains(reviewer, StringComparer.OrdinalIgnoreCase)).ToList();
        if (same.Count > 0)
        {
            said.Add($"{And(same)} {(same.Count == 1 ? "is" : "are")} the same agent as the one that does the work here: a fresh "
                + "conversation of the same agent is not an independent reading, and each opinion says so.");
        }

        return said;
    }

    /// <summary>
    /// Whether two adapters are one family (design §3.1): they run as the same agent's accounts (AGT7's owner), or both declare
    /// the same maker — read from the adapters this build carries, as the CLI reads its toolchain table. A name this build
    /// does not carry is its own owner and declares no maker: its own family only by its own name. The judgement is
    /// <see cref="AgentFamily"/>'s, the one the reviewer's choice makes over the machine's agents, a plugin's among them
    /// (XAGENT1b); the doors read the built-in set alone, since that is the set their CLI twin declares.
    /// </summary>
    public static bool OneFamily(string a, string b)
    {
        var built = AdapterSet.Built();
        return AgentFamily.Of(built, a).Same(AgentFamily.Of(built, b));
    }

    /// <summary>The reviewers of <paramref name="rule"/> that are the family of <paramref name="working"/>, the agent this machine's work runs on.</summary>
    public static IReadOnlyList<string> SameAgent(OpinionRule rule, string working) =>
        [.. rule.Reviewers.Where(reviewer => OneFamily(working, reviewer))];

    /// <summary>
    /// The config with one change made, or the refusal thrown in the CLI's words, nothing changed — the CLI's
    /// <c>applyOpinionEdit</c>. A set changes what it names over the rule set there; a repository's rule starts afresh rather
    /// than from its workspace's, since it replaces that whole; the key keeps the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal, in the twin's words.</exception>
    public static DriverConfig Apply(DriverConfig config, OpinionEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if ((repository is null) == (workspace is null)) throw new DriverException(Scope);

        var set = edit.Set is { Names: true } named ? named : null;
        if (new[] { set is not null, edit.None, edit.Clear }.Count(change => change) != 1) throw new DriverException(OneAtATime);
        if (edit.None && workspace is not null) throw new DriverException(NoneWorkspace);

        var map = repository is not null ? config.Opinions : config.WorkspaceOpinions;
        var scope = (repository ?? workspace)!;
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, scope, StringComparison.OrdinalIgnoreCase)) ?? scope;
        var whose = repository is not null ? $"`{key}`" : $"the workspace `{key}`";
        var own = map.TryGetValue(key, out var standing) && !standing.IsNone ? standing : null;

        OpinionRule? next;
        if (edit.Clear)
        {
            next = null;
        }
        else if (edit.None)
        {
            next = OpinionRule.None;
        }
        else
        {
            if (own is null && set!.Reviewers is null)
            {
                throw new DriverException($"{whose} has no second-opinion rule of its own — name its reviewers first.");
            }

            var judged = Judge(new Spelled(
                set!.Reviewers ?? [.. own!.Reviewers],
                set.On ?? (own is null ? null : [.. own.On]),
                set.Required ?? own?.Required ?? false,
                set.Verify ?? own?.Verify ?? false,
                set.Minutes ?? own?.Minutes,
                set.Recheck ?? own?.Recheck ?? true));
            next = judged.Rule ?? throw new DriverException(judged.Problem!);
        }

        var changed = new Dictionary<string, OpinionRule>(map, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key);
        else changed[key] = next;
        return repository is not null ? config with { Opinions = changed } : config with { WorkspaceOpinions = changed };
    }

    /// <summary>
    /// An edit as the screen's route and the shared table spell it: <c>repository</c> or <c>workspace</c>, then <c>set</c>,
    /// <c>none</c> or <c>clear</c>. A field of another type is absent, <c>none</c> and <c>clear</c> only when true; in a set, a
    /// list that is not one and minutes that are not a number are kept to be refused in the twin's words.
    /// </summary>
    public static OpinionEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new OpinionEdit();
        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        static bool? Flag(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

        OpinionSet? set = null;
        if (root.TryGetProperty("set", out var given) && given.ValueKind == JsonValueKind.Object)
        {
            set = new OpinionSet
            {
                // A list that is not one: no reviewer, and an occasion that is none, so each is refused in its words.
                Reviewers = given.TryGetProperty("reviewers", out var reviewers)
                    ? reviewers.ValueKind == JsonValueKind.Array ? Texts(reviewers) : []
                    : null,
                On = given.TryGetProperty("on", out var on) ? on.ValueKind == JsonValueKind.Array ? Texts(on) : [null] : null,
                Required = Flag(given, "required"),
                Verify = Flag(given, "verify"),
                Minutes = given.TryGetProperty("minutes", out var minutes) ? Number(minutes) : null,
                Recheck = Flag(given, "recheck"),
            };
        }

        return new OpinionEdit
        {
            Repository = Text(root, "repository"),
            Workspace = Text(root, "workspace"),
            Set = set,
            None = root.TryGetProperty("none", out var none) && none.ValueKind == JsonValueKind.True,
            Clear = root.TryGetProperty("clear", out var clear) && clear.ValueKind == JsonValueKind.True,
        };
    }

    /// <summary>
    /// The room's cell for a repository (design §2.5–§2.6): its reviewers in order, when they read, what they may run, the
    /// bound of a pass, and where it was set; <c>no second opinion</c> where nothing is.
    /// </summary>
    public static string RoomCell(ResolvedOpinion? opinion)
    {
        if (opinion is null) return "no second opinion";
        var source = opinion.Source == OpinionSource.Repository ? "set for it" : "its workspace's rule";
        var rule = opinion.Rule;
        if (rule.IsNone) return $"no second opinion ({source})";

        var parts = new List<string>
        {
            Else(rule.Reviewers),
            rule.On.Contains(Landing)
                ? rule.On.Contains(Steps) ? "before landing and each next step" : "before landing"
                : "before each next step",
        };
        if (rule.Required) parts.Add("required");
        if (rule.Verify) parts.Add("may build and run what is declared safe");
        parts.Add(string.Create(CultureInfo.InvariantCulture, $"at most {rule.Bound} minutes a pass"));
        if (!rule.Recheck) parts.Add("no recheck");
        return $"{string.Join("; ", parts)} ({source})";
    }

    /// <summary>A rule as compact JSON, for a test to compare with the shared table's cells.</summary>
    public static string ToJson(OpinionRule rule)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, rule);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
