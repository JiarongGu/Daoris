using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a manifest read from a repository's line registers as (WSSETUP5, D124 §3.3): the body <c>connect</c> would
/// send, or why <c>connect</c> would send none.
/// </summary>
/// <param name="Body">The body, in <c>connect</c>'s order of fields; null where a file was refused.</param>
/// <param name="Declares">Whether the manifest's domain says anything (<c>connect</c>'s <c>isDeclared</c>).</param>
/// <param name="ManifestProblem">Why <c>daoris.json</c> does not read, or a field in it that is refused; null when it reads.</param>
public sealed record ComposedRegistration(JsonObject? Body, bool Declares, string? ManifestProblem)
{
    /// <summary>Why <c>daoris.lanes.json</c> is refused whole, one line each, as <c>connect</c> says them; none when it reads.</summary>
    public IReadOnlyList<string> LanesProblems { get; init; } = [];
}

/// <summary>
/// A registration composed from the two files a repository's line holds (WSSETUP5, D124 §3.3): exactly what
/// <c>connect</c> would send from that manifest, so the registry follows the line with no <c>connect</c> run.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A TWIN of the CLI's <c>connect.ts</c></b> (<c>.claude/knowledge/twins.md</c>): its <c>registration()</c>
/// and what <c>connect</c> runs first, <c>readManifest</c>, <c>isDeclared</c> and <c>readLanes</c> (<c>lanes.ts</c>).
/// They share no code; <c>LineRegistrationTests</c> here and <c>connect-twin.test.ts</c> there hold the same table, row
/// for row. The body: the packs; the domain as written with <c>uses</c> by its rule, and only when it names something;
/// <c>join</c> and <c>shareKnowledge</c> as explicit booleans; the lanes' words as a list, <c>[]</c> for none; and the
/// root, to a service on this machine only. Adoption is said by sending no <c>adopted: false</c> (D70), and no workspace
/// is sent, since silence preserves the wiring (D48).</para>
///
/// <para><b>What is read is what the registration sends.</b> A manifest is refused as <c>connect</c> refuses it where
/// that bears on the body: not JSON, not an object, no <c>source</c>, knowledge without a join. A shape <c>connect</c>
/// would pass on and the service would refuse (a domain that is not an object, a summary that is not text, a list
/// that is not of text) is refused here first, naming the field. The layout's fields (<c>harness</c>,
/// <c>target</c>, <c>rooms</c>, <c>documents</c>, <c>switchedOff</c>) are neither sent nor judged: the set-up press
/// reads the layout, and that repository's own <c>check</c> holds the rest.</para>
/// </remarks>
public static partial class LineRegistration
{
    /// <summary>The manifest, at the repository's root.</summary>
    public const string ManifestFile = "daoris.json";

    /// <summary>The lanes (D115 §2.1), beside it.</summary>
    public const string LanesFile = "daoris.lanes.json";

    /// <summary>
    /// What <c>connect</c> would send for <paramref name="repository"/> from these texts, or why it would not.
    /// </summary>
    /// <param name="lanes">The lanes file's text; null where the line holds none.</param>
    /// <param name="root">The checkout's root, sent only to <paramref name="localService"/>.</param>
    public static ComposedRegistration Compose(string repository, string manifest, string? lanes, string? root, bool localService)
    {
        if (Parse(manifest) is not { } parsed)
        {
            return new(null, false, $"{ManifestFile} is not valid JSON");
        }

        if (parsed is not JsonObject read)
        {
            return new(null, false, $"{ManifestFile} is not a JSON object");
        }

        if (!Truthy(read["source"])) return new(null, false, $"{ManifestFile} has no 'source'");

        // `"remote": null` is what absence means: local, silently. Knowledge feeds only from a joined repository.
        var remote = read["remote"];
        var join = remote is not null && Truthy(remote is JsonObject named ? named["join"] : null);
        var knowledge = remote is not null && Truthy(remote is JsonObject told ? told["knowledge"] : null);
        if (knowledge && !join)
        {
            return new(null, false, $"{ManifestFile} declares remote.knowledge without remote.join — knowledge feeds only from a joined repository");
        }

        // `{ ...DEFAULTS, ...parsed }`: no packs is none.
        var packs = read.ContainsKey("packs") ? read["packs"] : new JsonArray();
        if (packs is not null && !IsTextList(packs)) return new(null, false, Refused("packs", "a list of pack names"));

        var domain = read["domain"];
        JsonObject? declared = null;
        if (Truthy(domain))
        {
            if (domain is not JsonObject written) return new(null, false, Refused("domain", "an object"));
            if (written["summary"] is { } summary && summary.GetValueKind() != JsonValueKind.String)
            {
                return new(null, false, Refused("domain.summary", "text"));
            }

            foreach (var list in (string[])["owns", "accepts"])
            {
                if (written[list] is { } given && !IsTextList(given)) return new(null, false, Refused($"domain.{list}", "a list of text"));
            }

            declared = Declared(written, repository);
        }

        var composed = new JsonObject
        {
            ["repository"] = repository,
            ["packs"] = packs?.DeepClone(),
            ["domain"] = declared,
            ["join"] = join,
            ["shareKnowledge"] = knowledge,
        };

        // `connect` refuses a declaration that says nothing before it reads the lanes.
        var declares = Declares(declared);

        var lanesProblems = new List<string>();
        var words = new JsonArray();
        if (lanes is not null)
        {
            if (Parse(lanes) is not { } file)
            {
                lanesProblems.Add($"{LanesFile} is not valid JSON");
            }
            else
            {
                lanesProblems.AddRange(LanesProblemsOf(file));
                if (lanesProblems.Count == 0) words = LaneWords(file is JsonObject lanesObject ? lanesObject["lanes"] : null);
            }
        }

        composed["lanes"] = words;
        if (localService && root is not null) composed["root"] = root;
        return lanesProblems.Count > 0
            ? new(null, declares, null) { LanesProblems = lanesProblems }
            : new(composed, declares, null);
    }

    /// <summary>
    /// What a repository says it uses (D91), by the rule the CLI's <c>usesOf</c> and the service's <c>Declared.Uses</c>
    /// hold: each name trimmed, a blank dropped, a repeat in any case dropped with the first spelling kept, and the
    /// repository's own name dropped. Anything but a list is none, and anything in it but text is skipped.
    /// </summary>
    public static IReadOnlyList<string> UsesOf(JsonNode? uses, string repository)
    {
        var kept = new List<string>();
        if (uses is not JsonArray names) return kept;
        foreach (var raw in names)
        {
            if (raw?.GetValueKind() != JsonValueKind.String) continue;
            var name = raw.GetValue<string>().Trim();
            if (name.Length == 0 || string.Equals(name, repository, StringComparison.OrdinalIgnoreCase)) continue;
            if (kept.Any(other => string.Equals(other, name, StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(name);
        }

        return kept;
    }

    /// <summary>
    /// Whether a domain says enough for a sibling to know what is worth asking (<c>connect</c>'s <c>isDeclared</c>): a
    /// summary with words, an area owned, or a kind of work accepted. A list it leaves out is none.
    /// </summary>
    public static bool Declares(JsonObject? domain) =>
        domain is not null
        && ((domain["summary"] is { } summary && summary.GetValueKind() == JsonValueKind.String && summary.GetValue<string>().Trim().Length > 0)
            || domain["owns"] is JsonArray { Count: > 0 }
            || domain["accepts"] is JsonArray { Count: > 0 });

    /// <summary>
    /// Why a parsed lanes file cannot be read, one line each, in the CLI's <c>lanesProblems</c>' sentences: a malformed
    /// or repeated id, a lane that owns nothing, two stewards. No <c>lanes</c> and an empty list are both none.
    /// </summary>
    public static IReadOnlyList<string> LanesProblemsOf(JsonNode? parsed)
    {
        if (parsed is not JsonObject file) return ["the file is not a JSON object"];
        // Absent is none; a `null` is a field that is not a list, as the CLI reads it.
        if (!file.TryGetPropertyValue("lanes", out var lanes)) return [];
        if (lanes is not JsonArray list) return ["'lanes' is not a list"];

        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stewards = new List<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i] as JsonObject;
            var id = entry?["id"] is { } named && named.GetValueKind() == JsonValueKind.String ? named.GetValue<string>() : null;
            var name = id is not null ? $"lane '{id}'" : $"lane {i + 1}";
            if (id is null || !LaneId().IsMatch(id)) problems.Add($"{name}: its id must be lower-case letters, digits and dashes, starting with a letter");
            else if (!seen.Add(id)) problems.Add($"{name}: the id is used twice");

            var paths = entry?["paths"] as JsonArray ?? [];
            if (!paths.Any(path => path?.GetValueKind() == JsonValueKind.String && path.GetValue<string>() is { Length: > 0 } text && !text.StartsWith('!')))
            {
                problems.Add($"{name}: it has no paths");
            }

            if (entry?["steward"]?.GetValueKind() == JsonValueKind.True) stewards.Add(id ?? $"lane {i + 1}");
        }

        if (stewards.Count > 1) problems.Add($"two stewards ({string.Join(", ", stewards)}): at most one lane keeps the records");
        return problems;
    }

    /// <summary>
    /// The words of each lane (the CLI's <c>laneWords</c>, the service's <c>Declared.Lanes</c>): trimmed; an id outside
    /// the alphabet dropped; a repeated id dropped, the first kept; the steward's mark kept by the first lane that
    /// carries it; a missing title or summary empty. Absent is none.
    /// </summary>
    public static JsonArray LaneWords(JsonNode? lanes)
    {
        var kept = new List<(string Id, string Title, string Summary, bool Steward)>();
        if (lanes is JsonArray list)
        {
            foreach (var lane in list)
            {
                if (lane is not JsonObject entry) continue;
                var id = Text(entry["id"]);
                if (!LaneId().IsMatch(id) || kept.Any(other => other.Id == id)) continue;
                var steward = entry["steward"]?.GetValueKind() == JsonValueKind.True && !kept.Any(other => other.Steward);
                kept.Add((id, Text(entry["title"]), Text(entry["summary"]), steward));
            }
        }

        return [.. kept.Select(lane => (JsonNode)new JsonObject
        {
            ["id"] = lane.Id, ["title"] = lane.Title, ["summary"] = lane.Summary, ["steward"] = lane.Steward,
        })];
    }

    /// <summary>The declaration as it goes on the wire: as written, <c>uses</c> by its rule and only when it names something.</summary>
    private static JsonObject Declared(JsonObject domain, string repository)
    {
        var declared = new JsonObject();
        foreach (var (name, value) in domain)
        {
            if (name == "uses") continue;
            declared[name] = value?.DeepClone();
        }

        var uses = UsesOf(domain["uses"], repository);
        if (uses.Count > 0) declared["uses"] = new JsonArray([.. uses.Select(use => (JsonNode)JsonValue.Create(use))]);
        return declared;
    }

    /// <summary>A text, or null for one that is not JSON. A byte-order mark is read past, as the CLI reads one.</summary>
    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text.TrimStart('﻿'));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>JavaScript's truthiness, which <c>connect</c> reads these fields by: absent, null, false, 0 and "" are not.</summary>
    private static bool Truthy(JsonNode? node) => node?.GetValueKind() switch
    {
        null or JsonValueKind.Null or JsonValueKind.False or JsonValueKind.Undefined => false,
        JsonValueKind.String => node.GetValue<string>().Length > 0,
        JsonValueKind.Number => node.GetValue<double>() != 0,
        _ => true,
    };

    private static bool IsTextList(JsonNode node) =>
        node is JsonArray list && list.All(item => item?.GetValueKind() == JsonValueKind.String);

    private static string Text(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>().Trim() : "";

    private static string Refused(string field, string what) =>
        $"`{field}` in {ManifestFile} is not {what}, so the service would refuse the registration";

    /// <summary>How a quest addresses a lane (<c>repository:lane</c>): the CLI's <c>LANE_ID</c>.</summary>
    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex LaneId();
}
