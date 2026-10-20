using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The rows and records <i>What needs you</i>'s judge is tested on (PUSH1d4), shared by <see cref="AttentionStateSliceTests"/> and
/// <see cref="AccountAttentionSliceTests"/>: a record made from a basis with fields changed, the judge's rows, and two rows compared
/// key order apart.
/// </summary>
internal static class AttentionTestRows
{
    public static JsonObject With(string basis, string over)
    {
        var made = JsonNode.Parse(basis)!.AsObject();
        foreach (var (field, value) in JsonNode.Parse(over)!.AsObject()) made[field] = value?.DeepClone();
        return made;
    }

    public static JsonObject With(JsonObject basis, string over) => With(basis.ToJsonString(), over);

    public static JsonObject Session(string over = "{}") => With(
        """{"id":"s1a2b3c4","repository":"engine","adapter":"claude-code","state":"working","kind":"driven","created":"2026-09-21T09:00:00Z","updated":"2026-09-21T10:00:00Z"}""",
        over);

    public static JsonObject Registration(string repository, string over = "{}") => With(
        $$"""{"repository":"{{repository}}","adopted":true,"registered":true,"owns":[],"accepts":[],"packs":[],"entries":1}""", over);

    public static IReadOnlyList<JsonObject> Rows(AttentionFacts facts) => NeedsAPerson.Rows(facts);

    /// <summary>Each row as the band keys it (`attentionKey`), in no order: the order is the page's.</summary>
    public static string[] Keys(IEnumerable<JsonObject> rows) =>
        [.. rows.Select(row => $"{row["kind"]!.GetValue<string>()}:{row["id"]!.GetValue<string>()}").Order(StringComparer.Ordinal)];

    public static void Is(string expected, JsonObject row) => Assert.Equal(Normal(JsonNode.Parse(expected)!), Normal(row));

    /// <summary>Key order apart, as the bridge and the pump each write an object.</summary>
    public static string Normal(JsonNode node) => node switch
    {
        JsonObject each => "{" + string.Join(",", each.OrderBy(field => field.Key, StringComparer.Ordinal).Select(field => $"{field.Key}:{(field.Value is null ? "null" : Normal(field.Value))}")) + "}",
        JsonArray items => "[" + string.Join(",", items.Select(item => item is null ? "null" : Normal(item))) + "]",
        _ => node.ToJsonString(),
    };
}
