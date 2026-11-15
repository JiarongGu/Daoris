using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris notices --show &lt;subject&gt;</c> says of a memory wait: the rows the window's Memory section shows for it, in the
/// words its catalogue gives them, and where the person decides it. The terminal only reads: an approval, a decline, a keep and a drop
/// are the person's press in the window.
/// </summary>
public static class MemoryWaitShown
{
    /// <summary>The lines for a memory wait, in the section's order.</summary>
    /// <param name="subject">The session the wait is by, or the operation of a sweep's end.</param>
    /// <param name="counts">The row's <c>ends</c>, <c>replacements</c> and <c>staged</c>.</param>
    /// <param name="ops">A session's rows as the service answers them (<c>{ ops: [...] }</c>); null for a sweep's end.</param>
    /// <param name="end">A sweep's end as its row holds it, which has no session; null for a session's.</param>
    public static TreesSaid Said(string subject, JsonNode? counts, JsonNode? ops, JsonObject? end)
    {
        var lines = new List<string>
        {
            $"memory {subject} waits for your word: {Count(counts, "ends", "end", "ends")}, "
            + $"{Count(counts, "replacements", "replacement", "replacements")}, {Number(counts, "staged")} staged",
        };

        var staged = 0;
        if (end is not null)
        {
            Row(lines, Text(end, "op") ?? subject, "end", Text(end, "repository"), Text(end, "target"), Revision(end), "awaiting", Text(end, "reason"),
                null, null, null);
        }

        foreach (var node in ops?["ops"] as JsonArray ?? [])
        {
            if (node is not JsonObject row) continue;
            var change = row["change"] as JsonObject;
            var by = row["by"] as JsonObject;
            var state = Text(row, "state") ?? "";
            if (state == "staged") staged++;
            Row(lines, Text(row, "id") ?? "", Text(change, "kind") ?? "", Text(row, "repository"), Text(change, "target"), Revision(change), state,
                Text(change, "text"), Text(by, "session"), Text(by, "door"), Text(row, "models"),
                Text(change, "relation"), Text(row, "carried"), Text(row, "answer"));
        }

        if (staged > 0) lines.Add($"  {staged} staged {(staged == 1 ? "change waits" : "changes wait")} on your Keep or Drop, in the window");
        return new(lines, [], 0);
    }

    /// <summary>The refusal for a subject on which no memory change waits.</summary>
    public static TreesSaid None(string subject) =>
        new([], [$"notices: no memory change waits for your word on `{subject}`; `daoris notices` lists what does."], 1);

    private static void Row(
        List<string> lines, string id, string kind, string? repository, string? target, string? revision, string state, string? text,
        string? by, string? door, string? models, string? relation = null, string? quest = null, string? answer = null)
    {
        var place = $"{repository}:{target}";
        var what = kind switch
        {
            "write" when relation is "disputes" or "amends" => $"{(relation == "disputes" ? "Disputes" : "Amends")} {place}",
            "write" => $"Writes to {repository}'s memory",
            "end" => $"End of {place} at revision {revision ?? "?"}",
            "supersede" => $"Replaces entries of {repository}'s memory",
            "propose" => $"Proposed to {repository} on quest #{quest ?? "?"}: {relation} {place}",
            _ => kind,
        };
        lines.Add($"  {id} · {what} · {Standing(kind, state, answer)}");
        if (text is { Length: > 0 }) lines.Add($"    {text}");
        if (kind is "end" or "supersede")
        {
            lines.Add($"    proposed by {by ?? "Daoris's recheck"} · {door ?? "?"} · {models ?? "model unknown"}");
            if (state == "awaiting") lines.Add("    approve or decline it in the window's What needs you");
        }
    }

    private static string Standing(string kind, string state, string? answer)
    {
        if (kind == "propose")
        {
            return state switch
            {
                "awaiting" => "open on the owner's quest",
                "declined" => $"declined: {answer}",
                "answered" => $"answered, applying nothing itself: {answer}",
                "stale" => "stale: the entry moved before the owner decided",
                "dropped" => "dropped: its quest is gone",
                _ => state,
            };
        }

        return state switch
        {
            "staged" => "kept with this session's work, applied when it is accepted",
            "ready" => "applies next",
            "awaiting" => "awaits your approval",
            "applying" => "applying",
            "applied" => "applied",
            "dropped" => "dropped",
            "declined" => "declined",
            "stale" => "stale: the entry moved first",
            "held" => "held: whether it ran is not known",
            "answered" => "answered",
            _ => state,
        };
    }

    private static string Count(JsonNode? counts, string name, string one, string many) =>
        $"{Number(counts, name)} {(Number(counts, name) == "1" ? one : many)}";

    private static string Number(JsonNode? counts, string name) =>
        counts?[name] is JsonValue value && value.TryGetValue<int>(out var n) ? n.ToString(CultureInfo.InvariantCulture) : "0";

    private static string? Text(JsonNode? node, string name) => node?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Revision(JsonNode? node) =>
        node?["revision"] is JsonValue value && value.TryGetValue<int>(out var n) ? n.ToString(CultureInfo.InvariantCulture) : null;
}
