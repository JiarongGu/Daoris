using System.Text.Json;
using System.Text.RegularExpressions;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// One review environment (REVIEWENV1a, D154 point 1, the review-environment design §1.1): where a repository's work runs for
/// the person to look at before it lands.
/// </summary>
/// <param name="Name">Lower-case letters, digits and dashes, at most 32, never production.</param>
/// <param name="Kind"><c>local</c> or <c>deployed</c>.</param>
/// <param name="Procedure">A repository-relative path to the document or skill that says how work reaches it; never read here.</param>
/// <param name="Address">Where the app is used: a local one's, required; a deployed one's, optional.</param>
/// <param name="Run">A local one's command for work needing a process of its own (§2.3), the person's standing say-so.</param>
public sealed record ReviewEnvironment(string Name, string Kind, string Procedure, string? Address = null, string? Run = null);

/// <summary>
/// A review rule (design §1.3): its environments, the first the default, and whether work waits for the person's look. With
/// no environment it is <see cref="None"/>, a repository's <c>false</c>: none here, whatever its workspace says.
/// </summary>
public sealed record ReviewRule(IReadOnlyList<ReviewEnvironment> Environments, bool Required = false)
{
    /// <summary>A repository's <c>false</c>: no review environment, whatever its workspace says.</summary>
    public static ReviewRule None { get; } = new([], false);

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Environments.Count == 0;
}

/// <summary>An environment as an entry or a door spells it, before it is judged: a field that is not text is null.</summary>
public sealed record ReviewSpelled(string? Name, string? Kind, string? Procedure, string? Address = null, string? Run = null);

/// <summary>
/// The review rule as <c>driver.json</c> keeps it, <c>reviews</c> and <c>workspaceReviews</c> (REVIEWENV1a, D154 point 2;
/// FOUNDATION1 mod-iii): the key's shape, its limits and the sentences that say what does not read, read and written by the settings'
/// own parser. What the rule means, its resolution, edit and words, stays with the review rule (<see cref="ReviewRules"/>), which
/// answers through this for what it shares.
/// </summary>
public static class ReviewKey
{
    /// <summary>The kinds a review environment may be — the CLI's <c>REVIEW_KINDS</c>.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["local", "deployed"];

    /// <summary>
    /// The words a name reads as production by, whole or as a word between its dashes — a deliberate copy of the place words
    /// the service's <c>GoAheads.cs</c> reads <c>production</c> by, and of the CLI's <c>PRODUCTION_WORDS</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Production = ["production", "prod", "prd", "live"];

    private const string RuleShape = "a review rule is its environments and whether it is required, or `false` for none here.";
    private const string NoEnvironments = "a review rule names at least one environment; the first is the default.";
    private const string EnvironmentShape = "each environment is an object: its `name`, `kind` and `procedure`, and a local one's `address`.";
    private const string NameRule = "lower-case letters, digits and dashes, at most 32 characters, such as `local` or `dev`.";
    private const string KindMissing = "an environment's `kind` is `local` or `deployed`.";
    private const string ProcedureMissing = "an environment names its `procedure`: the path, in the repository, of the document or skill that "
        + "says how work reaches it, such as `README.md`.";
    private const string AddressMissing = "a local environment needs its `address`: where the app normally runs, such as `http://localhost:4200`, "
        + "the only place a set-up step may show the work.";
    private const string RunDeployed = "only a local environment carries `run`: a deployed one is reached by its procedure, under your go-aheads.";
    private const string RunShape = "`run` is one command on one line: the one that starts work needing a process of its own.";
    private static readonly Regex AddressShape = new(
        @"^https?://(?:\[[0-9A-Fa-f:.]+\]|[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*)(?::([0-9]{1,5}))?/?\z",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A repository's entry read as a rule, with its first problem: <c>false</c> is <see cref="ReviewRule.None"/>; anything
    /// else must be an object naming at least one environment, each judged in order. A field that is not text is absent;
    /// only JSON <c>true</c> is required; what an environment has no field for is not kept.
    /// </summary>
    public static (ReviewRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.False) return (ReviewRule.None, null);
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        if (!value.TryGetProperty("environments", out var environments) || environments.ValueKind != JsonValueKind.Array)
        {
            return (null, NoEnvironments);
        }

        var spelled = new List<ReviewSpelled>();
        foreach (var each in environments.EnumerateArray())
        {
            // An entry that is not an object is said at its place, after the environments before it are judged.
            if (each.ValueKind != JsonValueKind.Object)
            {
                return (null, (spelled.Count > 0 ? Judge(spelled, false).Problem : null) ?? EnvironmentShape);
            }

            spelled.Add(new ReviewSpelled(Text(each, "name"), Text(each, "kind"), Text(each, "procedure"), Text(each, "address"), Text(each, "run")));
        }

        return Judge(spelled, value.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True);
    }

    /// <summary>The environments judged in order, the first problem said, or the rule as kept.</summary>
    internal static (ReviewRule? Rule, string? Problem) Judge(IReadOnlyList<ReviewSpelled> environments, bool required)
    {
        if (environments.Count == 0) return (null, NoEnvironments);
        var kept = new List<ReviewEnvironment>();
        foreach (var environment in environments)
        {
            if (Problem(environment, kept) is { } problem) return (null, problem);
            kept.Add(Kept(environment));
        }

        return (new ReviewRule(kept, required), null);
    }

    /// <summary>The first problem of one environment, judged after those before it in its rule — the CLI's <c>environmentProblem</c>.</summary>
    private static string? Problem(ReviewSpelled environment, IReadOnlyList<ReviewEnvironment> earlier)
    {
        var (name, kind, procedure, address, run) = environment;
        if (NameProblem(name) is { } unnamed) return unnamed;
        if (earlier.Any(each => each.Name == name)) return $"`{name}` is named twice in one rule — each environment has a name of its own.";

        if (kind is null) return KindMissing;
        if (!Kinds.Contains(kind)) return $"`{kind}` is not a kind of review environment — `local` or `deployed`.";

        if (procedure is null || procedure.Trim().Length == 0) return ProcedureMissing;
        if (!IsProcedurePath(procedure))
        {
            return $"`{procedure}` is not a path in the repository — a procedure is named from its root with forward slashes, and "
                + "no `..`, root or drive, such as `docs/deploying-to-dev.md`.";
        }

        if (address is null && kind == "local") return AddressMissing;
        if (address is not null && !IsAddress(address))
        {
            return $"`{address}` is not an address — an absolute `http` or `https` origin, such as `http://localhost:4200`.";
        }

        if (run is not null && kind != "local") return RunDeployed;
        if (run is not null && (run.Trim().Length == 0 || run.Contains('\r') || run.Contains('\n'))) return RunShape;
        return null;
    }

    /// <summary>
    /// Why <paramref name="name"/> cannot name a review environment, or null when it can: its shape, <c>none</c>, and production.
    /// The rule's own judgement of a name, which the terminal's review choice reads too (REVIEWENV1j), so a choice is refused in
    /// the words a rule is.
    /// </summary>
    public static string? NameProblem(string? name)
    {
        if (name is null || name.Trim().Length == 0) return $"an environment is named: {NameRule}";
        if (!SafeNames.IsKebabId(name) || name.Length > 32) return $"`{name}` is not an environment's name — {NameRule}";
        if (name == "none") return "`none` says a repository has no review environment, so no environment is named it.";
        return ReadsAsProduction(name)
            ? $"`{name}` reads as production, and production is never a review environment — name where work is looked at "
              + "before it lands, such as `local` or `dev`."
            : null;
    }

    /// <summary>Whether a name reads as production: the whole of it, or a word between its dashes, is one of <see cref="Production"/>.</summary>
    public static bool ReadsAsProduction(string name) => name.Split('-').Any(word => Production.Contains(word));

    /// <summary>
    /// A path a procedure may be (design §1.1, as KNOWUSE1c names a router): from the repository's root, with forward slashes,
    /// no space around it, and no <c>..</c>, <c>.</c>, empty part, root, drive or control character.
    /// </summary>
    internal static bool IsProcedurePath(string path) => path == path.Trim() && SafeNames.IsRepositoryPath(path);

    /// <summary>An absolute <c>http</c> or <c>https</c> origin: a host or a bracketed IPv6 address, a port from 1 to 65535, nothing after but one <c>/</c>.</summary>
    private static bool IsAddress(string address)
    {
        var match = AddressShape.Match(address);
        if (!match.Success) return false;
        return !match.Groups[1].Success || int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) is >= 1 and <= 65_535;
    }

    /// <summary>An environment as kept: an address without its last slash, and absent fields left out.</summary>
    private static ReviewEnvironment Kept(ReviewSpelled environment) => new(
        environment.Name!, environment.Kind!, environment.Procedure!,
        environment.Address is { } address && address.EndsWith('/') ? address[..^1] : environment.Address,
        environment.Run);

    /// <summary>
    /// A map of review rules as <c>driver.json</c> holds it: each name trimmed, a blank one not read; a rule with a problem,
    /// and <c>true</c>, not read; <c>false</c> read only where <paramref name="allowNone"/> (a repository's, never a
    /// workspace's); a name written twice in any case read where first readable. A map that is not one is none.
    /// </summary>
    internal static IReadOnlyDictionary<string, ReviewRule> Map(JsonElement root, string name, bool allowNone)
    {
        var map = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase);
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
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, ReviewRule> map)
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

    /// <summary>One rule as kept: <c>false</c> for none; else <c>required</c> only when on, then the environments in order.</summary>
    public static void Write(Utf8JsonWriter writer, ReviewRule rule)
    {
        if (rule.IsNone)
        {
            writer.WriteBooleanValue(false);
            return;
        }

        writer.WriteStartObject();
        if (rule.Required) writer.WriteBoolean("required", true);
        writer.WriteStartArray("environments");
        foreach (var environment in rule.Environments)
        {
            writer.WriteStartObject();
            writer.WriteString("name", environment.Name);
            writer.WriteString("kind", environment.Kind);
            writer.WriteString("procedure", environment.Procedure);
            if (environment.Address is { } address) writer.WriteString("address", address);
            if (environment.Run is { } run) writer.WriteString("run", run);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
