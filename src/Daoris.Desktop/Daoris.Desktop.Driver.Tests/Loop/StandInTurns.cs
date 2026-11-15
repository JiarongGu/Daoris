using System.Text.Json.Nodes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The stand-in service's turns of a record, kept as the ledger keeps them at the one move out of an ended state: the ended turn sealed with
/// how the record ended, then the wake's turn, Daoris's where every word waiting is Daoris's own and names no step, the person's where any is
/// theirs. A record keeps none until it first goes on, so its first turn is written then.
/// </summary>
internal static class StandInTurns
{
    public static void WentOn(JsonObject session)
    {
        var id = session["id"]!.GetValue<string>();
        var turns = session["turns"] as JsonArray ?? new JsonArray(Turn(1, "start", session["created"]!.GetValue<string>(), id));
        var codes = (session["noteParts"] as JsonArray ?? []).Select(part => part?["code"]?.GetValue<string>()).OfType<string>();
        turns[^1]!["end"] = new JsonObject
        {
            ["state"] = session["state"]!.GetValue<string>(),
            ["limit"] = session["limit"]?.GetValue<bool>() == true,
            ["interrupted"] = session["interrupted"]?.GetValue<bool>() == true,
            ["codes"] = new JsonArray([.. codes.Select(code => (JsonNode)JsonValue.Create(code))]),
        };
        var said = session["said"]!.AsArray();
        var cause = said.Any(word => word!["from"] is null && word["helper"] is null) ? "person"
            : said.All(word => word!["from"]?.GetValue<string>() == "daoris" && !Step(word["key"]?.GetValue<string>())) ? "daoris"
            : "step";
        var at = said.Select(word => word!["at"]?.GetValue<string>()).Append(session["created"]!.GetValue<string>()).OfType<string>().OrderBy(DateTimeOffset.Parse).Last();
        turns.Add(Turn(turns.Count + 1, cause, at, id));
        session["turns"] = turns;
    }

    /// <summary>Whether a word's key names a workflow step: a check's, or a run's <c>&lt;unit&gt;/&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;</c>.</summary>
    private static bool Step(string? key) => key is not null && (key.StartsWith("check/", StringComparison.Ordinal) || key.Split('/').Length >= 4);

    private static JsonObject Turn(int number, string cause, string at, string part) =>
        new() { ["number"] = number, ["cause"] = cause, ["at"] = at, ["part"] = part };
}
