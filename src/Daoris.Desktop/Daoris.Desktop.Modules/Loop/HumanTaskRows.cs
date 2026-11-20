using System.Text.Json.Nodes;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// The rows of <i>What needs you</i> a run's steps make: one for each step waiting on a role the person holds (<c>HeldSteps</c>), keyed by
/// its run and node (<c>step:&lt;run&gt;/&lt;node&gt;</c>) as the page's obligations are, in the run's workspace, carrying what it waits for, the
/// role and its holder, the presses that settle it, the page where its act is taken and since when it has waited.
/// Read from the runtime's last look, never stored on the node, so a press kept clears it.
/// </summary>
internal static class HumanTaskRows
{
    /// <summary>The fields a step's row carries, as the slice pushes them (<see cref="AttentionStateSlice.Fields"/>).</summary>
    public static readonly IReadOnlyList<string> Fields = ["run", "node", "why", "role", "holder", "holderName", "presses", "words", "url", "opens", "work", "held", "epoch", "holds", "vacant", "givesUp"];

    /// <param name="groups">Sessions' placings, by which a row names the session its run's waits placed in Needs you: what its notification opens.</param>
    public static IEnumerable<JsonObject> Of(IReadOnlyList<JsonObject> steps, IReadOnlyList<JsonObject>? groups = null)
    {
        foreach (var step in steps)
        {
            var (run, node) = (Text(step, "run"), Text(step, "node"));
            var row = Row("step", $"{run}/{node}", Text(step, "since"), Text(step, "workspace") ?? "default");
            row["where"] = Text(step, "repository");
            if ((groups ?? []).FirstOrDefault(placed => Objects(placed["waits"]).Any(wait => Text(wait, "run") == run && Text(wait, "node") == node)) is { } placing)
            {
                row["session"] = Text(placing, "session");
            }
            var carried = new JsonObject();
            foreach (var field in Fields)
            {
                if (step[field] is { } value) carried[field] = value.DeepClone();
            }

            carried["presses"] ??= new JsonArray();
            row["step"] = carried;
            yield return row;
        }
    }
}
