using System.Text.Json.Nodes;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// The closed questions a design pass's comparison asks the person, as rows of <i>What needs you</i>: one per question not answered yet,
/// under the brainstorm and the question's key, with its two options (<c>yes</c>, <c>no</c>). Its <c>design</c> holds what it is, what
/// yes and no change and where to look; an answer is drafted as a decision for the person to keep, never written as their words.
/// </summary>
public static class DesignQuestionRows
{
    /// <summary>The kind of the row.</summary>
    public const string Kind = "design-question";

    /// <summary>One row for each unanswered question of each design pass.</summary>
    public static IEnumerable<JsonObject> Of(IEnumerable<JsonObject> brainstorms)
    {
        foreach (var brainstorm in brainstorms.Where(each => Text(each, "pass") == "design"))
        {
            // The keys answered already, as the brainstorm keeps them; a value of another shape answers none.
            var answered = (brainstorm["answered"] as JsonArray ?? [])
                .Select(key => key is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).ToHashSet(StringComparer.Ordinal);
            foreach (var question in Objects(brainstorm["comparison"]?["questions"]).Where(question => Text(question, "key") is { } key && !answered.Contains(key)))
            {
                var row = Row(Kind, $"{Text(brainstorm, "id")}:{Text(question, "key")}", Text(brainstorm, "updated"), Scope(brainstorm));
                row["brainstorm"] = Text(brainstorm, "id");
                row["design"] = question.DeepClone();
                row["options"] = new JsonArray("yes", "no");
                yield return row;
            }
        }
    }
}
