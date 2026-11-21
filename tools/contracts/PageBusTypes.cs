using System.Globalization;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.ContractsGen;

/// <summary>
/// The page's list of the bus's types, written from the one registry the driver declares them in (<see cref="BusTypes"/>), so the page
/// and its tests hold no second list: each type's kind, name and version, whether it is a fact, a request or a response, its emitter,
/// its obliged handler or answerer, its deadline in seconds and whether it is provisional, its retries, its give-up, and its payload's
/// fields with their shapes, in the registry's order. Written as a JSON array a line, so a check reads it with no TypeScript.
/// </summary>
public static class PageBusTypes
{
    /// <summary>The block appended to the service types' file.</summary>
    public static string Write()
    {
        var rows = BusTypes.All.Select(type => JsonSerializer.Serialize(new
        {
            kind = type.Kind,
            name = type.Name,
            version = type.Version,
            @class = type.Class,
            emitter = type.Emitter,
            obliged = type.Obliged,
            deadlineSeconds = (long)type.Deadline.Within.TotalSeconds,
            provisional = type.Deadline.Provisional,
            retries = type.Retries,
            giveUp = type.GiveUp,
            fields = type.Fields.ToDictionary(field => field.Name, field => field.Shape, StringComparer.Ordinal),
        }));

        var text = new StringBuilder();
        text.Append("\n/** A bus type as the driver's registry declares it once (D356): its kind (`name/version`), whether it is a fact, a request ");
        text.Append("or a response, who emits it, who must handle or answer it, its deadline in seconds and whether a measure set it yet, how many ");
        text.Append("times the bus tries again, what its give-up does, and its payload's fields with their shapes. */\n");
        text.Append("export type BusFieldShape = ").Append(string.Join(" | ", BusShapes.All.Select(Quoted))).Append(";\n");
        text.Append("export type BusGiveUp = ").Append(string.Join(" | ", BusGiveUps.All.Select(Quoted))).Append(";\n");
        text.Append("export type BusTypeRow = { readonly kind: string; readonly name: string; readonly version: number; ");
        text.Append("readonly class: ").Append(string.Join(" | ", BusClasses.All.Select(Quoted))).Append("; readonly emitter: string; ");
        text.Append("readonly obliged: string; readonly deadlineSeconds: number; readonly provisional: boolean; readonly retries: number; ");
        text.Append("readonly giveUp: BusGiveUp; readonly fields: { readonly [field: string]: BusFieldShape } };\n");
        text.Append("\n/** Every bus type, in the registry's order. */\n");
        // One type a line, with no trailing comma, so a check reads the list as JSON.
        text.Append("export const BUS_TYPES: readonly BusTypeRow[] = [\n");
        text.Append(string.Join(",\n", rows.Select(row => $"  {row}"))).Append("\n];\n");
        return text.ToString();
    }

    private static string Quoted(string word) => string.Create(CultureInfo.InvariantCulture, $"'{word}'");
}
