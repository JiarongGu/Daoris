using System.Text;
using Daoris.Driver;

namespace Daoris.ContractsGen;

/// <summary>
/// The page's list of the machine's settings, written from the one list the driver declares (<see cref="MachineSettings.All"/>), so the
/// page holds no second one: each setting's key, the route that sets it, the prefix of its words and the Settings card that draws it.
/// </summary>
public static class PageSettings
{
    /// <summary>The block appended to the window routes' file.</summary>
    public static string Write()
    {
        var text = new StringBuilder();
        text.Append("\n/** A machine setting as the driver declares it once: its one key, the route that sets it, its words' prefix, whether Ask Daoris ");
        text.Append("proposes it, and the Settings domain whose card draws its row, or null where another card does. */\n");
        text.Append("export type MachineSetting = { readonly key: string; readonly route: DriverRoute; readonly words: string; readonly door: boolean; ");
        text.Append("readonly card: string | null; readonly least: number | null; readonly most: number | null };\n");
        text.Append("\n/** Every machine setting, in the order the Driver domain draws its rows. */\n");
        text.Append("export const SETTINGS: readonly MachineSetting[] = [\n");
        foreach (var setting in MachineSettings.All)
        {
            var card = setting.Card is { } domain ? Quoted(domain) : "null";
            text.Append($"  {{ key: {Quoted(setting.Key)}, route: {Quoted(setting.Route)}, words: {Quoted(setting.Words)}, ");
            text.Append($"door: {(setting.Door ? "true" : "false")}, card: {card}, least: {Number(setting.Least)}, most: {Number(setting.Most)} }},\n");
        }

        text.Append("];\n");
        return text.ToString();
    }

    private static string Number(int? value) => value is { } number ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";

    private static string Quoted(string name) =>
        name.All(each => char.IsAsciiLetterOrDigit(each) || each is '_' or '.') ? $"'{name}'" : throw new InvalidOperationException($"`{name}` cannot be written as a setting's name.");
}
