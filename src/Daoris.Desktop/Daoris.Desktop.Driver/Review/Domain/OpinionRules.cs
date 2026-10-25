using System.Globalization;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

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
/// <para>One table, this suite's tests' <c>fixtures/opinion-rules.json</c>, cell for cell (<c>OpinionRulesTests</c>; its CLI twin,
/// <c>opinions.ts</c>, left with the driver family's TypeScript in PUSH1f2b): the reading and its precedence, every refusal in
/// the same words, each door's sentences and what each says after them of the gate, the edits, and which reviewers are the
/// working agent's own family.</para>
///
/// <para>Nothing here chooses a reviewer, starts a pass or holds a landing: the choice (XAGENT1b), the look and the landing gate
/// (XAGENT1f) read it, and each door says so after a rule's sentences (<see cref="Gate"/>, XAGENT1f4).</para>
/// </remarks>
public static class OpinionRules
{
    /// <summary>The occasion before a chain's work in a repository lands (design §2.1).</summary>
    public const string Landing = OpinionKey.Landing;

    /// <summary>The occasion before a chain's next step starts.</summary>
    public const string Steps = OpinionKey.Steps;

    /// <summary>The occasions a rule may name, in the order a rule keeps them — the CLI's <c>OPINION_OCCASIONS</c>.</summary>
    public static readonly IReadOnlyList<string> Occasions = OpinionKey.Occasions;

    /// <summary>One pass's bound where the rule names none (design §2.3): a starting point, not a measurement.</summary>
    public const int DefaultMinutes = OpinionKey.DefaultMinutes;

    /// <summary>
    /// What each door said after a rule's sentences until the gate read it (XAGENT1a–XAGENT1f), untrue since XAGENT1f. No door
    /// says it now (XAGENT1f4, <see cref="Gate"/>); it is kept so the tests that hold its absence name it, the workflow tables'
    /// among them.
    /// </summary>
    public const string DeclaredOnly = "Declared only: nothing reads it yet, so no reviewer is chosen and no landing waits for it.";

    /// <summary>
    /// Said by each door after the sentences of a rule that reads at <see cref="Landing"/> (XAGENT1f4, through <see cref="Gate"/>):
    /// what the landing's gate does with work that waits for another agent's reading since XAGENT1f, and the person's two
    /// terminal doors that answer it. The CLI's <c>OPINION_WAITING</c>, the page's <c>settings.opinion.says.waiting</c> and
    /// <c>setting_propose</c>'s copy, held to the shared table's <c>gate</c> rows: the doors' words change together.
    /// </summary>
    public const string Waiting = "Where work here waits for another agent's reading, it lands only once that reading is settled, "
        + "or once you go on without it, `daoris-driver opinion anyway <session>`, or say you looked yourself, "
        + "`daoris-driver opinion myself <session>`.";

    /// <summary>
    /// Said after the sentences of a rule that reads at <see cref="Steps"/> (XAGENT1f4): the look sits the chain's next step while
    /// the opinion on the step before is unsettled (XAGENT1f). The CLI's <c>OPINION_STEP_WAITING</c>.
    /// </summary>
    public const string StepWaiting = "Where a chain's next step here waits for another agent's reading of the step before it, it "
        + "starts only once that reading is settled.";

    /// <summary>
    /// Said after the sentences of a rule that lets a reviewer run what is declared safe (XAGENT1f4): nothing hands a reviewer the
    /// repository's declared safe commands yet (XAGENT1f3), so the rule's own sentence is a promise the gate does not keep yet.
    /// The CLI's <c>OPINION_SAFE_NOT_HANDED</c>.
    /// </summary>
    public const string SafeNotHanded = "What this repository declares safe is not handed to a reviewer yet.";

    /// <summary>What stands where nothing is set anywhere (design §2.6): today's behaviour, said as such.</summary>
    public const string NoneSet = "None: no other agent reads work here.";

    /// <summary>The refusal of an edit naming neither a repository nor a workspace, or both; Ask Daoris's door says it first.</summary>
    internal const string Scope = "a second-opinion rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its reviewers and how they read, say none, or clear.";
    private const string NoneWorkspace = "`none` is a repository's: a workspace with no second opinion sets none, and `--clear` takes its rule away.";
    private const string NoneHere = "No second opinion here, whatever its workspace says: no other agent reads work here before it lands.";

    /// <summary>
    /// A repository's entry read as a rule, with its first problem: <c>false</c> is <see cref="OpinionRule.None"/>; anything
    /// else must be an object naming at least one reviewer, then its occasions and its minutes judged (the settings' <see cref="OpinionKey.Read"/>).
    /// </summary>
    public static (OpinionRule? Rule, string? Problem) Read(JsonElement value) => OpinionKey.Read(value);

    /// <summary>A map of second-opinion rules as <c>driver.json</c> holds it (the settings' <see cref="OpinionKey.Map"/>).</summary>
    internal static IReadOnlyDictionary<string, OpinionRule> Map(JsonElement root, string name, bool allowNone) => OpinionKey.Map(root, name, allowNone);

    /// <summary>A map written as <c>driver.json</c> keeps it (the settings' <see cref="OpinionKey.WriteMap"/>).</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, OpinionRule> map) => OpinionKey.WriteMap(writer, name, map);

    /// <summary>One rule as kept (the settings' <see cref="OpinionKey.Write"/>).</summary>
    public static void Write(Utf8JsonWriter writer, OpinionRule rule) => OpinionKey.Write(writer, rule);

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
    /// What each door says after the rule's own sentences (XAGENT1f4): what the gate does with work that waits for another agent's
    /// reading, at landing (<see cref="Waiting"/>) and before a chain's next step (<see cref="StepWaiting"/>), as the rule's
    /// occasions name them, then that what is declared safe is not handed to a reviewer yet where the rule lets one run it
    /// (<see cref="SafeNotHanded"/>); nothing after <see cref="OpinionRule.None"/>, where nothing waits. The CLI's
    /// <c>opinionGate</c>, by the shared table's <c>gate</c> rows.
    /// </summary>
    public static IReadOnlyList<string> Gate(OpinionRule rule)
    {
        if (rule.IsNone) return [];
        var said = new List<string>();
        if (rule.On.Contains(Landing)) said.Add(Waiting);
        if (rule.On.Contains(Steps)) said.Add(StepWaiting);
        if (rule.Verify) said.Add(SafeNotHanded);
        return said;
    }

    /// <summary>
    /// Whether two adapters are one family (design §3.1): they run as the same agent's accounts (AGT7's owner), or both declare
    /// the same maker — read from <paramref name="adapters"/>, as the CLI reads its toolchain table and the plugins it is
    /// handed. A name the set does not carry is its own owner and declares no maker: its own family only by its own name. The
    /// judgement is <see cref="AgentFamily"/>'s, the one the reviewer's choice makes over the machine's agents.
    /// </summary>
    /// <param name="adapters">
    /// The machine's set, a plugin's harnesses among them, so a maker a plugin declares counts at a door as it counts in the
    /// choice (XAGENT1b2); null is the built-in set alone.
    /// </param>
    public static bool OneFamily(string a, string b, AdapterSet? adapters = null)
    {
        var set = adapters ?? AdapterSet.Built();
        return AgentFamily.Of(set, a).Same(AgentFamily.Of(set, b));
    }

    /// <summary>
    /// The reviewers of <paramref name="rule"/> that are the family of <paramref name="working"/>, the agent this machine's work
    /// runs on, judged over <paramref name="adapters"/> as <see cref="OneFamily"/> judges them.
    /// </summary>
    public static IReadOnlyList<string> SameAgent(OpinionRule rule, string working, AdapterSet? adapters = null)
    {
        var set = adapters ?? AdapterSet.Built();
        return [.. rule.Reviewers.Where(reviewer => OneFamily(working, reviewer, set))];
    }

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

            var judged = OpinionKey.Judge(new OpinionKey.Spelled(
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
        static bool? Flag(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

        OpinionSet? set = null;
        if (root.TryGetProperty("set", out var given) && given.ValueKind == JsonValueKind.Object)
        {
            set = new OpinionSet
            {
                // A list that is not one: no reviewer, and an occasion that is none, so each is refused in its words.
                Reviewers = given.TryGetProperty("reviewers", out var reviewers)
                    ? reviewers.ValueKind == JsonValueKind.Array ? OpinionKey.Texts(reviewers) : []
                    : null,
                On = given.TryGetProperty("on", out var on) ? on.ValueKind == JsonValueKind.Array ? OpinionKey.Texts(on) : [null] : null,
                Required = Flag(given, "required"),
                Verify = Flag(given, "verify"),
                Minutes = given.TryGetProperty("minutes", out var minutes) ? OpinionKey.Number(minutes) : null,
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
