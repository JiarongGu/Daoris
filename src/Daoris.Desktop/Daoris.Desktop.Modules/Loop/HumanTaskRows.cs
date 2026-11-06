using System.Text.Json.Nodes;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// The rows of <i>What needs you</i> a person's task makes (NODEKIT1 HUMAN): one for each run's <c>human</c> node waiting on the person, keyed by
/// its run and node (<c>task:&lt;run&gt;/&lt;node&gt;</c>) as the page's obligations are, in the run's workspace, carrying the presses it waits for
/// and since when it has waited.
/// Read from the runtime's last look, never stored on the node, so a press kept clears it.
/// </summary>
internal static class HumanTaskRows
{
    public static IEnumerable<JsonObject> Of(IReadOnlyList<JsonObject> tasks)
    {
        foreach (var task in tasks)
        {
            var (run, node) = (Text(task, "run"), Text(task, "node"));
            var row = Row("task", $"{run}/{node}", Text(task, "since"), Text(task, "workspace") ?? "default");
            row["where"] = Text(task, "repository");
            row["task"] = new JsonObject { ["run"] = run, ["node"] = node, ["acts"] = task["acts"]?.DeepClone() ?? new JsonArray(), ["work"] = Text(task, "work") };
            yield return row;
        }
    }
}
