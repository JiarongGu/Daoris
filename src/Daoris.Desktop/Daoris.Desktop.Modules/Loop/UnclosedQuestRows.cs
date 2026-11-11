using System.Text.Json.Nodes;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// A quest its finished session left taken, the planner's typed <c>Unclosed</c> verdict: one row by the quest, waiting on the person's
/// done or a word to that session, and counted with the quests the person answers, as a policy's hold is.
/// </summary>
internal static class UnclosedQuestRows
{
    private const string Verdict = "Unclosed";

    /// <summary>Each such quest's row: since the verdict's time, else the quest's last move; the session that left it; the verdict whole.</summary>
    public static IEnumerable<JsonObject> Rows(AttentionFacts facts, IReadOnlyDictionary<string, JsonObject> quests)
    {
        foreach (var consideration in facts.Considered.Where(consideration => Text(consideration, "verdict") == Verdict))
        {
            var held = quests.GetValueOrDefault(Text(consideration, "quest") ?? "");
            var row = Row("unclosed", Text(consideration, "quest"), Text(consideration, "since") ?? Text(held, "updated"), held is null ? null : Scope(held));
            row["where"] = Text(consideration, "repository");
            row["session"] = Text(consideration["said"] as JsonObject, "session");
            row["sitting"] = consideration.DeepClone();
            yield return row;
        }
    }

    /// <summary>Each such quest by its own key, with its workspace where its record is in hand.</summary>
    public static IEnumerable<(string Key, JsonObject Value)> Counted(AttentionFacts facts, IReadOnlyDictionary<string, JsonObject> quests)
    {
        foreach (var quest in facts.Considered.Where(consideration => Text(consideration, "verdict") == Verdict).Select(each => Text(each, "quest")).Distinct())
        {
            var held = new JsonObject { ["kind"] = "unclosed" };
            if (quests.GetValueOrDefault(quest ?? "") is { } record) held["workspace"] = Scope(record);
            yield return ($"unclosed:{quest}", held);
        }
    }
}
