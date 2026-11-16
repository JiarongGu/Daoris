using System.Text.Json.Nodes;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// The rows of <i>What needs you</i> a person's task makes: one for each run's <c>human</c> node waiting on the person, keyed by
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

    /// <summary>
    /// One row for each run whose plugin step waits on an actor the person holds, keyed <c>actor:&lt;run&gt;/&lt;node&gt;</c>, carrying the
    /// actor, the act in its plugin's words and its page: the act is taken there, never by a press here.
    /// </summary>
    public static IEnumerable<JsonObject> Actors(IReadOnlyList<JsonObject> waits)
    {
        foreach (var wait in waits)
        {
            var (run, node) = (Text(wait, "run"), Text(wait, "node"));
            var row = Row("actor", $"{run}/{node}", Text(wait, "since"), Text(wait, "workspace") ?? "default");
            row["where"] = Text(wait, "repository");
            row["actor"] = new JsonObject
            {
                ["run"] = run, ["node"] = node, ["actor"] = Text(wait, "actor"), ["words"] = Text(wait, "words"), ["url"] = Text(wait, "url"), ["work"] = Text(wait, "work"),
            };
            yield return row;
        }
    }
}
