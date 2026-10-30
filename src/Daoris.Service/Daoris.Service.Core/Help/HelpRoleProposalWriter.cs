using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>role</c> kind's writer (FOUNDATION1y-i, the foundation design §2.7): a working role of the role catalogue added, changed,
/// removed or given back its seed, the change <c>daoris driver role</c> and Settings → AI features' Role catalogue make. Its door is
/// the change, its target the role's id, and its own <c>press</c> the record and choices as the driver's role door takes them, with the
/// revision Ask Daoris read. Whether the judge takes it is the driver's to say, before the person sees it.
/// </summary>
internal sealed class HelpRoleProposalWriter(HelpProposalFile file)
{
    /// <summary>The changes a role proposal makes, as the driver's role door reads them.</summary>
    public static readonly IReadOnlyList<string> Doors = ["set", "remove", "default"];

    /// <summary>
    /// Write one proposal to change the role catalogue: <paramref name="change"/> of <paramref name="id"/>; for a set, its names, the work
    /// it takes, its scope (<c>machine</c>, <c>workspace:&lt;id&gt;</c> or <c>repository:&lt;id&gt;</c>) and its choices, each
    /// <c>"agent [model|-] [effort|-]"</c>; a field a set leaves out keeps the role's own. Shape alone is checked here.
    /// </summary>
    public (string? Id, string Message) Propose(
        string change, string id, string? nameEn, string? nameZh, IReadOnlyList<string>? takes, string? scope, IReadOnlyList<string>? choices,
        long? revision, string why, string? session, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var door = Blank(change);
        var named = Blank(id);
        var (kind, place) = Scope(Blank(scope));
        var entries = choices?.Select(Entry).ToList();
        var refused = door is null || !Doors.Contains(door) ? "a role's change is `set`, `remove` or `default`."
            : named is null ? "a role's change names the role's id."
            : Word(named, "a role's id")
              ?? (door != "set" && (nameEn ?? nameZh ?? scope) is not null || door != "set" && (takes?.Count > 0 || choices?.Count > 0)
                  ? $"`{door}` names the role alone: only `set` carries its names, work, scope and choices."
                  : scope is not null && kind is null ? $"`{scope}` is not a scope: `machine`, `workspace:<id>` or `repository:<id>`."
                  : entries?.FirstOrDefault(entry => entry is null) is not null || entries?.Count > 4
                    ? "each choice is one quoted `agent [model|-] [effort|-]`, at most four."
                  : null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "role");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            writer.WriteStartObject("press");
            if (Blank(nameEn) is not null || Blank(nameZh) is not null)
            {
                writer.WriteStartObject("name");
                Nullable(writer, "en", Blank(nameEn));
                Nullable(writer, "zh", Blank(nameZh));
                writer.WriteEndObject();
            }

            if (takes is { Count: > 0 })
            {
                writer.WriteStartArray("takes");
                foreach (var work in takes.Select(Blank).OfType<string>()) writer.WriteStringValue(work);
                writer.WriteEndArray();
            }

            if (kind is not null)
            {
                writer.WriteStartObject("scope");
                writer.WriteString("kind", kind);
                Nullable(writer, "id", place);
                writer.WriteEndObject();
            }

            if (entries is { Count: > 0 })
            {
                writer.WriteStartArray("choices");
                foreach (var entry in entries.OfType<(string Agent, string? Model, string? Effort)>())
                {
                    writer.WriteStartObject();
                    writer.WriteString("agent", entry.Agent);
                    Nullable(writer, "model", entry.Model);
                    Nullable(writer, "effort", entry.Effort);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (revision is { } read) writer.WriteNumber("revision", read);
            writer.WriteEndObject();
        }, why, session, at);
    }

    private static (string? Kind, string? Id) Scope(string? scope)
    {
        if (scope is null) return (null, null);
        if (scope == "machine") return ("machine", null);
        var parts = scope.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0] is "workspace" or "repository" && parts[1].Length > 0 ? (parts[0], parts[1]) : (null, null);
    }

    private static (string Agent, string? Model, string? Effort)? Entry(string quoted)
    {
        var words = quoted.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is 0 or > 3) return null;
        string? Value(int at) => words.Length > at && words[at] != "-" ? words[at] : null;
        return (words[0], Value(1), Value(2));
    }
}
