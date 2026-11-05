using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A line's policy as <c>driver.json</c> keeps it, <c>linePolicies</c> and <c>workspaceLinePolicies</c>: how the line is integrated, read
/// and written by the settings' own parser, with the sentences a policy is refused in.
/// </summary>
public static class LinePolicyKey
{
    internal const string IntegrateShape =
        "`integrate` is an object: `request`, `commit`, `batch`, `order`, `gates`, `unconfirmed`, `conflicts`, `overlap` and `line`.";

    internal const string UnconfirmedShape = "`integrate.unconfirmed` is an object: `unreached` and `reached`, each `split`, `hold` or `land`.";

    internal const string BatchShape = "`integrate.batch` is how many members a candidate holds, 1 to 8.";

    internal const string LandWithoutWholeSet =
        "`unconfirmed: land` is refused where no whole-set verification runs on the line — here, a landed route naming a verification "
        + "pipeline whose gates you accepted. A failure that does not recur splits.";

    internal const string PolicyHoldsProcess =
        "a line's policy says how the line is integrated; who requests and where a conflict goes are the workflow's `integrate` step's.";

    private static string Listed(IReadOnlyList<string> choices) =>
        string.Join(", ", choices.Take(choices.Count - 1).Select(choice => $"`{choice}`")) + $" or `{choices[^1]}`";

    private static string ChoiceShape(string key, IReadOnlyList<string> choices) => $"`integrate.{key}` is {Listed(choices)}.";

    private static string ChoiceProblem(string key, IReadOnlyList<string> choices, string value) =>
        $"`integrate.{key}` is {Listed(choices)}, and `{value}` is none.";

    /// <summary>
    /// What is wrong with a line's policy, or null: each key one of its values, and <c>land</c> for an unconfirmed failure only where a
    /// whole-set verification runs on the line (<paramref name="wholeSet"/>). The unit's own process, <c>request</c> and <c>conflicts</c>,
    /// is the <c>integrate</c> node's, so a policy holds neither.
    /// </summary>
    public static string? PolicyProblem(LandingIntegrate policy, bool wholeSet)
    {
        if (policy.Request is not null || policy.Conflicts is not null) return PolicyHoldsProcess;
        var unknown = Unknown("commit", LandingIntegrate.Commits, policy.Commit)
            ?? (policy.Batch is < 1 or > LandingIntegrate.MaxBatch ? BatchShape : null)
            ?? Unknown("order", LandingIntegrate.Orders, policy.Order)
            ?? Unknown("gates", LandingIntegrate.GateChoices, policy.Gates)
            ?? Unknown("unconfirmed.unreached", LandingIntegrate.Unconfirmeds, policy.Unreached)
            ?? Unknown("unconfirmed.reached", LandingIntegrate.Unconfirmeds, policy.Reached)
            ?? Unknown("overlap", LandingIntegrate.Overlaps, policy.Overlap)
            ?? (policy.Line is { } line && !BranchName.IsValid(line)
                ? $"`integrate.line` is the line by name, and `{line}` is not a branch name git would take."
                : null);
        if (unknown is not null) return unknown;
        // The confirm run is evidence, not proof: `land` is the owner's choice only where a whole-set verification backs it.
        return !wholeSet && (policy.Unreached == LandingIntegrate.Land || policy.Reached == LandingIntegrate.Land) ? LandWithoutWholeSet : null;

        static string? Unknown(string key, IReadOnlyList<string> choices, string? value) =>
            value is null || choices.Contains(value, StringComparer.Ordinal) ? null : ChoiceProblem(key, choices, value);
    }

    /// <summary>
    /// A policy as the file or a press holds it, each key as written, or null with <paramref name="problem"/> where it does not read: no
    /// object, a key's value not text (<c>batch</c> not a whole number, <c>unconfirmed</c> not an object). A key written null is absent,
    /// and one this build does not know is passed over. Its values are judged by <see cref="PolicyProblem"/>.
    /// </summary>
    public static LandingIntegrate? IntegrateOf(JsonElement value, out string? problem)
    {
        problem = null;
        if (value.ValueKind != JsonValueKind.Object)
        {
            problem = IntegrateShape;
            return null;
        }

        static string? Choice(JsonElement holder, string key, string named, IReadOnlyList<string> choices, ref string? said)
        {
            if (said is not null || !holder.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
            if (field.ValueKind == JsonValueKind.String) return field.GetString();
            said = ChoiceShape(named, choices);
            return null;
        }

        string? said = null;
        var request = Choice(value, "request", "request", LandingIntegrate.Requests, ref said);
        var commit = Choice(value, "commit", "commit", LandingIntegrate.Commits, ref said);
        int? batch = null;
        if (said is null && value.TryGetProperty("batch", out var members) && members.ValueKind != JsonValueKind.Null)
        {
            if (members.ValueKind == JsonValueKind.Number && members.TryGetInt32(out var count)) batch = count;
            else said = BatchShape;
        }

        var order = Choice(value, "order", "order", LandingIntegrate.Orders, ref said);
        var gates = Choice(value, "gates", "gates", LandingIntegrate.GateChoices, ref said);
        string? unreached = null, reached = null;
        if (said is null && value.TryGetProperty("unconfirmed", out var unconfirmed) && unconfirmed.ValueKind != JsonValueKind.Null)
        {
            if (unconfirmed.ValueKind != JsonValueKind.Object) said = UnconfirmedShape;
            else
            {
                unreached = Choice(unconfirmed, "unreached", "unconfirmed.unreached", LandingIntegrate.Unconfirmeds, ref said);
                reached = Choice(unconfirmed, "reached", "unconfirmed.reached", LandingIntegrate.Unconfirmeds, ref said);
            }
        }

        var conflicts = Choice(value, "conflicts", "conflicts", LandingIntegrate.ConflictChoices, ref said);
        var overlap = Choice(value, "overlap", "overlap", LandingIntegrate.Overlaps, ref said);
        string? line = null;
        if (said is null && value.TryGetProperty("line", out var lineValue) && lineValue.ValueKind != JsonValueKind.Null)
        {
            if (lineValue.ValueKind == JsonValueKind.String) line = lineValue.GetString();
            else said = "`integrate.line` is the line by name.";
        }

        if (said is not null)
        {
            problem = said;
            return null;
        }

        return new LandingIntegrate(request, commit, batch, order, gates, unreached, reached, conflicts, overlap, line);
    }

    /// <summary>A map of names to line policies; one that does not read, or that <see cref="PolicyProblem"/> refuses, is not read.</summary>
    internal static IReadOnlyDictionary<string, LandingIntegrate> PolicyMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, LandingIntegrate>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (map.ContainsKey(property.Name) || IntegrateOf(property.Value, out _) is not { } read) continue;
                var policy = read with { Request = null, Conflicts = null };
                if (PolicyProblem(policy, wholeSet: true) is null) map[property.Name] = policy;
            }
        }

        return map;
    }

    internal static void WritePolicies(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, LandingIntegrate> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, policy) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            WriteValue(writer, policy);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, LandingIntegrate integrate)
    {
        writer.WriteStartObject();
        // Each written only when set, in the design's order: absent reads the Absent column (LandingIntegrate.Resolved).
        if (integrate.Request is not null) writer.WriteString("request", integrate.Request);
        if (integrate.Commit is not null) writer.WriteString("commit", integrate.Commit);
        if (integrate.Batch is { } batch) writer.WriteNumber("batch", batch);
        if (integrate.Order is not null) writer.WriteString("order", integrate.Order);
        if (integrate.Gates is not null) writer.WriteString("gates", integrate.Gates);
        if (integrate.Unreached is not null || integrate.Reached is not null)
        {
            writer.WriteStartObject("unconfirmed");
            if (integrate.Unreached is not null) writer.WriteString("unreached", integrate.Unreached);
            if (integrate.Reached is not null) writer.WriteString("reached", integrate.Reached);
            writer.WriteEndObject();
        }

        if (integrate.Conflicts is not null) writer.WriteString("conflicts", integrate.Conflicts);
        if (integrate.Overlap is not null) writer.WriteString("overlap", integrate.Overlap);
        if (integrate.Line is not null) writer.WriteString("line", integrate.Line);
        writer.WriteEndObject();
    }
}
