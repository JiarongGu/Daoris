using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.ContractsGen;

/// <summary>
/// The page's list of the node kinds, written from the kit the driver registers (<see cref="NodeKit"/>) so the chart, the step picker
/// and the run titles hold no second list: each built-in's id, the picker's group, how the chart draws it, the step a run draws it as, how it
/// is reached and the outcomes it routes on, in the kit's order. Written as a JSON array, so the names check reads it with no TypeScript.
/// </summary>
public static class PageNodeKinds
{
    /// <summary>The block appended to the service types' file, which the presentational components may read as they may its types.</summary>
    public static string Write()
    {
        var groups = NodeKit.Built.Order.ToDictionary(part => part.Name, part => part.Phase.ToString().ToLowerInvariant(), StringComparer.Ordinal);
        var rows = NodeKit.Built.Units.Select(unit =>
        {
            var kind = unit.Row.Kind;
            var drawn = unit.Part(new NodeDeclaration(kind, kind, 1, unit.Row.Reach)).Step;
            return JsonSerializer.Serialize(new
            {
                kind,
                group = groups[kind],
                look = unit.Look.ToString().ToLowerInvariant(),
                reach = unit.Row.Reach,
                drawn,
                outcomes = unit.Outcomes,
            });
        });

        var text = new StringBuilder();
        text.Append("\n/** A built-in node kind as the driver's kit declares it once: its id, the picker's group, the variant the chart draws, how it is ");
        text.Append("reached, the step a run draws it as, and the outcomes it routes on (`scripts/kind-words.mjs`). */\n");
        text.Append("export type NodeKindRow = { readonly kind: string; readonly group: string; readonly look: string; readonly reach: string; ");
        text.Append("readonly drawn: string; readonly outcomes: readonly string[] };\n");
        text.Append("\n/** Every built-in node kind, in the picker's groups and their order. */\n");
        // One kind a line, with no trailing comma, so the names check reads the list as JSON.
        text.Append("export const NODE_KINDS: readonly NodeKindRow[] = [\n");
        text.Append(string.Join(",\n", rows.Select(row => $"  {row}"))).Append("\n];\n");
        return text.ToString();
    }
}
