using System.Text.Json.Nodes;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// The questions an ask's delivery plan still asks the person, as rows of <i>What needs you</i>: one per question, under the question's key, so
/// the bell counts it once and the ask's page answers it. The service judges what is open (answered ones are gone from the ask), so a row is
/// only what the ask carries; its <c>question</c> holds what it is, what yes does, what it blocks and the default.
/// </summary>
public static class PlanQuestionRows
{
    /// <summary>The kind of the row.</summary>
    public const string Kind = "plan-question";

    /// <summary>One row for each open question of each ask that is not closed.</summary>
    public static IEnumerable<JsonObject> Of(IEnumerable<JsonObject> asks)
    {
        foreach (var ask in asks.Where(ask => Text(ask, "state") != "Closed"))
        {
            foreach (var question in Objects(ask["planQuestions"]).Where(question => Text(question, "key") is not null))
            {
                var row = Row(Kind, $"{Text(ask, "id")}:{Text(question, "key")}", Text(ask, "updated"), Scope(ask));
                row["ask"] = Text(ask, "id");
                row["question"] = question.DeepClone();
                yield return row;
            }
        }
    }
}
