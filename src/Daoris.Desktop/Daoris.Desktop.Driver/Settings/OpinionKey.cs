using System.Text.Json;
using static Daoris.Driver.Json;

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

    /// <summary>The bound of one pass: as written, else <see cref="OpinionKey.DefaultMinutes"/>.</summary>
    public int Bound => Minutes ?? OpinionKey.DefaultMinutes;
}

/// <summary>
/// The second-opinion rule as <c>driver.json</c> keeps it, <c>opinions</c> and <c>workspaceOpinions</c> (XAGENT1a, D155 point 3;
/// FOUNDATION1 mod-iii): the key's shape, its limits and the sentences that say what does not read, read and written by the
/// settings' own parser. What the rule means, its resolution, edit and words, stays with the second opinion (<see cref="OpinionRules"/>),
/// which answers through this for what it shares.
/// </summary>
public static class OpinionKey
{
    /// <summary>The occasion before a chain's work in a repository lands (design §2.1).</summary>
    public const string Landing = "landing";

    /// <summary>The occasion before a chain's next step starts.</summary>
    public const string Steps = "steps";

    /// <summary>The occasions a rule may name, in the order a rule keeps them — the CLI's <c>OPINION_OCCASIONS</c>.</summary>
    public static readonly IReadOnlyList<string> Occasions = [Landing, Steps];

    /// <summary>One pass's bound where the rule names none (design §2.3): a starting point, not a measurement.</summary>
    public const int DefaultMinutes = 20;

    private const int FewestMinutes = 5;
    private const int MostMinutes = 120;
    private const string RuleShape = "a second-opinion rule is its reviewers and when they read, or `false` for none here.";
    private const string NoReviewers = "a second-opinion rule names at least one reviewer: an adapter, in the order they are tried, such as `codex-acp`.";
    private const string ReviewerShape = "each reviewer is an adapter's name, such as `codex-acp` or `dsh`.";
    private const string OnShape = "`on` is `landing`, `steps` or both: when a reviewer reads work here by itself.";
    private const string MinutesShape = "`minutes` is a whole number from 5 to 120: how long one pass may take, 20 when absent.";
    /// <summary>A rule as an entry or a door spells it, before it is judged: <see cref="On"/> null is absent.</summary>
    internal sealed record Spelled(
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
    internal static (OpinionRule? Rule, string? Problem) Judge(Spelled spelled)
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

    internal static List<string?> Texts(JsonElement list) =>
        [.. list.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString() : null)];

    internal static double Number(JsonElement value) =>
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
}
