using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// What a <c>role</c> proposal carries of an appointed supervisor beside its names, scope and revision: the kinds of decision it decides
/// (an empty list is its scope's generic supervisor, null leaves the role's own), the acts it is granted, <c>&lt;kind&gt;.&lt;act&gt;</c> each,
/// its holder and its window in minutes. A field left null is not written, so the driver keeps the role's own.
/// </summary>
public sealed record HelpSupervisorFields(
    IReadOnlyList<string>? Decides, IReadOnlyList<string>? Acts, string? Holder, int? Window);

/// <summary>The shape of a supervisor's fields, and how they are written into a proposal's press as the record's own; the role door's judge decides what the record may be.</summary>
internal static partial class HelpSupervisorFile
{
    /// <summary>The seeded machine supervisor's id, whose own proposal is its rules by place.</summary>
    private const string RulesId = "supervisor";

    [GeneratedRegex("^[a-z][a-z-]*$")]
    private static partial Regex Word();

    [GeneratedRegex("^[a-z][a-z-]*\\.[a-z][a-z-]*$")]
    private static partial Regex Act();

    /// <summary>Why the proposal is not a supervisor's, or null: a <c>set</c>, a role id of its own, no working role's field, and every field of its shape.</summary>
    public static string? Refusal(HelpSupervisorFields fields, string? door, string? id, bool workingFields)
    {
        if (door != "set") return "a supervisor is proposed by a `set` of its record.";
        if (id == RulesId) return $"`{RulesId}` is the id of the machine's seeded supervisor, whose rules are proposed by place: a supervisor of kinds takes its own id.";
        if (workingFields) return "a supervisor's record carries its kinds, acts, holder and window beside its names, scope and revision alone.";
        if (fields.Decides?.FirstOrDefault(kind => !Word().IsMatch(kind)) is { } strange) return $"`{strange}` is not the name of a kind of decision: lowercase words.";
        if (fields.Acts?.FirstOrDefault(act => !Act().IsMatch(act)) is { } odd) return $"`{odd}` is not an act: each is `<kind>.<act>`.";
        if (fields.Holder is { } holder && !Word().IsMatch(holder)) return $"`{holder}` is not a holder: `rules` or `terminal`.";
        return fields.Window is < 1 ? "the window is whole minutes, one or more." : null;
    }

    /// <summary>Writes the supervisor's fields into the press, only those the proposal names.</summary>
    public static void Write(Utf8JsonWriter writer, HelpSupervisorFields fields)
    {
        if (fields.Decides is { } decides)
        {
            writer.WriteStartArray("decides");
            foreach (var kind in decides) writer.WriteStringValue(kind);
            writer.WriteEndArray();
        }

        if (fields.Acts is { } acts)
        {
            writer.WriteStartArray("authority");
            foreach (var act in acts) writer.WriteStringValue(act);
            writer.WriteEndArray();
        }

        if (fields.Holder is { } holder) writer.WriteString("holder", holder);
        if (fields.Window is { } window) writer.WriteNumber("window", window);
    }
}
