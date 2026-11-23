using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An edit of the policy made onto <c>driver.json</c>'s <c>risk</c> as a JSON object, every other key kept as written: a row with the same act and
/// mode is replaced (or taken away), any other row stands, and a place left with no rows is taken away. Pure, so a proposal reads the policy it
/// would make from the same edit that is later written.
/// </summary>
public static class PolicyRows
{
    public static void Apply(JsonObject root, IReadOnlyList<PolicyEdit> edits)
    {
        foreach (var edit in edits)
        {
            var risk = root[RiskPolicy.Key] as JsonObject ?? new JsonObject();
            var rows = risk[edit.Place] as JsonArray ?? [];
            var kept = new JsonArray();
            foreach (var row in rows)
            {
                if (row is JsonObject existing && Same(existing, edit)) continue;
                kept.Add(row?.DeepClone());
            }

            if (edit.Class is { } riskClass)
            {
                var written = new JsonObject { ["kind"] = edit.Act, ["class"] = riskClass };
                if (edit.By is { } by) written["by"] = by;
                if (edit.Mode is { } mode) written["mode"] = mode;
                kept.Add(written);
            }

            risk.Remove(edit.Place);
            if (kept.Count > 0) risk[edit.Place] = kept;
            root.Remove(RiskPolicy.Key);
            if (risk.Count > 0) root[RiskPolicy.Key] = risk;
        }
    }

    private static bool Same(JsonObject row, PolicyEdit edit) =>
        string.Equals(Text(row, "kind"), edit.Act, StringComparison.Ordinal) && string.Equals(Text(row, "mode"), edit.Mode, StringComparison.Ordinal);

    private static string? Text(JsonObject row, string name) => row[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
