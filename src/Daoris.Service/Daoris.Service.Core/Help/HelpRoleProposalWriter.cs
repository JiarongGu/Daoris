using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>role</c> kind's writer: a working role or an appointed supervisor of the role catalogue added, changed,
/// removed or given back its seed (one role's, or every seeded role's at once), the change <c>daoris role</c> and Agents → Roles → Working roles make. Its door is
/// the change, its target the role's id, and its own <c>press</c> the record and choices as the driver's role door takes them, with the
/// revision Ask Daoris read. Whether the judge takes it is the driver's to say, before the person sees it.
/// </summary>
internal sealed class HelpRoleProposalWriter(HelpProposalFile file)
{
    /// <summary>The changes a role proposal makes, as the driver's role door reads them.</summary>
    public static readonly IReadOnlyList<string> Doors = ["set", "remove", "default", "default-all"];

    /// <summary>The <i>Restore all role defaults</i>: every seeded role given back its seed, added roles kept. It names no one role, so its id is <c>*</c>.</summary>
    private const string AllDoor = "default-all";

    private const string AllId = "*";

    private const string SupervisorPress = "supervisor";

    /// <summary>The staffing policy's presets, as the driver's role door fills them.</summary>
    private static readonly string[] Presets = ["first", "even", "weighted"];

    private static readonly System.Text.RegularExpressions.Regex Tag = new("^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$");

    /// <summary>
    /// Write one proposal to change the role catalogue: <paramref name="change"/> of <paramref name="id"/>; for a set, its names, the work
    /// it takes, its scope (<c>machine</c>, <c>workspace:&lt;id&gt;</c> or <c>repository:&lt;id&gt;</c>) and its choices, each
    /// <c>"agent [model|-] [effort|-]"</c>; a field a set leaves out keeps the role's own. Shape alone is checked here. A name is
    /// <paramref name="name"/> alone (one text as written), or with <paramref name="nameIn"/> a map of language tags to texts (the text
    /// kept as <c>en</c>), or the map alone; no language is required.
    /// </summary>
    public (string? Id, string Message) Propose(
        string change, string id, string? name, IReadOnlyDictionary<string, string>? nameIn, IReadOnlyList<string>? takes, string? scope, IReadOnlyList<string>? choices,
        long? revision, string why, string? session, DateTimeOffset at, string? policy = null, long? policyRevision = null,
        string? rulesPlace = null, string? rules = null, long? rulesRevision = null, HelpSupervisorFields? supervisor = null)
    {
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        if (supervisor is not null)
        {
            var working = takes?.Count > 0 || choices?.Count > 0 || Blank(policy) is not null || policyRevision is not null
                || rulesPlace is not null || rules is not null || rulesRevision is not null;
            if (HelpSupervisorFile.Refusal(supervisor, Blank(change), Blank(id), working) is { } unfit) return (null, $"{Capital(unfit)} Nothing was proposed.");
        }
        else if (Blank(id) == Supervisor || rulesPlace is not null || rules is not null || rulesRevision is not null)
        {
            return ProposeRules(change, id, name, nameIn, takes, scope, choices, revision, policy, policyRevision, rulesPlace, rules, rulesRevision, why, session, at);
        }

        var door = Blank(change);
        var policyText = Blank(policy);
        System.Text.Json.JsonElement? record = null;
        if (policyText is not null && policyText.StartsWith('{'))
        {
            try
            {
                record = System.Text.Json.JsonDocument.Parse(policyText).RootElement.Clone();
            }
            catch (System.Text.Json.JsonException)
            {
                return (null, "The policy record is not JSON. Nothing was proposed.");
            }
        }
        var named = Blank(id);
        var (kind, place) = Scope(Blank(scope));
        var entries = choices?.Select(Entry).ToList();
        var all = door == AllDoor;
        var names = (nameIn ?? new Dictionary<string, string>()).Where(each => Blank(each.Value) is not null).ToList();
        var badTag = names.FirstOrDefault(each => !Tag.IsMatch(each.Key.Trim())).Key;
        var refused = badTag is not null ? $"`{badTag}` is not a language tag (`en`, `zh-TW`, `fr`)."
            : door is null || !Doors.Contains(door) ? "a role's change is `set`, `remove`, `default` or `default-all`."
            : named is null ? "a role's change names the role's id."
            : all && named != AllId || !all && named == AllId ? $"`{AllId}` is the id of `{AllDoor}` alone, which names no one role, and `{AllDoor}` takes no other."
            : (all ? null : Word(named, "a role's id"))
              ?? (all && revision is not null ? $"`{AllDoor}` names no one role, so it has no revision to read."
                  : door != "set" && (Blank(name) ?? scope) is not null || door != "set" && names.Count > 0 || door != "set" && (takes?.Count > 0 || choices?.Count > 0 || policyText is not null)
                  ? $"`{door}` names the role alone: only `set` carries its names, work, scope, choices and policy."
                  : policyText is not null && record is null && policyText != "none" && !Presets.Contains(policyText) ? $"`{policyText}` is not a policy: a preset ({string.Join(", ", Presets)}), `none`, or the record as JSON."
                  : policyRevision is not null && policyText is null ? "a policy revision is read for a policy; name the policy."
                  : scope is not null && kind is null ? $"`{scope}` is not a scope: `machine`, `workspace:<id>` or `repository:<id>`."
                  : entries?.FirstOrDefault(entry => entry is null) is not null || entries?.Count > 4
                    ? "each choice is one quoted `agent [model|-] [effort|-]`, at most four."
                  : null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            Head(writer, door!, named!);
            writer.WriteStartObject("press");
            if (supervisor is not null) writer.WriteString("kind", SupervisorPress);
            if (names.Count == 0 && Blank(name) is { } alone) writer.WriteString("name", alone);
            else if (names.Count > 0)
            {
                writer.WriteStartObject("name");
                if (Blank(name) is { } main && !names.Any(each => each.Key.Trim().Equals("en", StringComparison.OrdinalIgnoreCase))) writer.WriteString("en", main);
                foreach (var (tag, text) in names) writer.WriteString(tag.Trim(), Blank(text));
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

            if (record is { } given)
            {
                writer.WritePropertyName("policy");
                given.WriteTo(writer);
            }
            else if (policyText == "none") writer.WriteNull("policy");
            else if (policyText is not null) writer.WriteString("policy", policyText);
            if (policyRevision is { } policyRead) writer.WriteNumber("policyRevision", policyRead);
            if (supervisor is not null) HelpSupervisorFile.Write(writer, supervisor);
            if (revision is { } read) writer.WriteNumber("revision", read);
            writer.WriteEndObject();
        }, why, session, at);
    }

    /// <summary>The reserved id whose proposal carries a place's rules, as the driver's role door reads it.</summary>
    private const string Supervisor = "supervisor";

    private static void Head(System.Text.Json.Utf8JsonWriter writer, string door, string target)
    {
        writer.WriteString("kind", "role");
        writer.WriteString("door", door);
        writer.WriteString("target", target);
        writer.WriteNull("workspace");
        writer.WriteNull("value");
        writer.WriteNull("sentence");
    }

    /// <summary>
    /// A <c>set</c> of <c>supervisor</c> carries a place's rules written whole, as JSON, with
    /// the revision of the place's rules read; nothing of a working role rides it. Shape alone is checked here; the rules' judge is the driver's.
    /// </summary>
    private (string? Id, string Message) ProposeRules(
        string change, string id, string? name, IReadOnlyDictionary<string, string>? nameIn, IReadOnlyList<string>? takes, string? scope, IReadOnlyList<string>? choices,
        long? revision, string? policy, long? policyRevision, string? place, string? rules, long? rulesRevision, string why, string? session, DateTimeOffset at)
    {
        System.Text.Json.JsonElement? list = null;
        if (Blank(rules) is { } text)
        {
            try
            {
                list = System.Text.Json.JsonDocument.Parse(text).RootElement.Clone();
            }
            catch (System.Text.Json.JsonException)
            {
                return (null, "The rules are not JSON. Nothing was proposed.");
            }
        }

        var where = Blank(place);
        var refused = Blank(id) != Supervisor ? $"a place's rules are the supervisor's: their id is `{Supervisor}`."
            : Blank(change) != "set" ? $"the supervisor's rules are a `set` of `{Supervisor}`."
            : Blank(name) is not null || nameIn is { Count: > 0 } || takes is { Count: > 0 } || scope is not null || choices is { Count: > 0 } || revision is not null
              || policy is not null || policyRevision is not null
                ? $"the supervisor is an appointed role: a proposal of it carries a place, its rules and the revision read alone."
            : where is null || !(where == "machine" || where.Split(':', 2) is [("workspace" or "repository"), { Length: > 0 }])
                ? $"`{where}` is not a place: `machine`, `workspace:<id>` or `repository:<id>`."
            : list is not { ValueKind: System.Text.Json.JsonValueKind.Array } ? "the rules are the place's whole list, a JSON array, `[]` for none."
            : rulesRevision is null ? "the revision of the place's rules you read is named, so a change made since is refused rather than overwritten."
            : null;
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            Head(writer, "set", Supervisor);
            writer.WriteStartObject("press");
            writer.WriteString("place", where);
            writer.WritePropertyName("rules");
            list!.Value.WriteTo(writer);
            writer.WriteNumber("rulesRevision", rulesRevision!.Value);
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
