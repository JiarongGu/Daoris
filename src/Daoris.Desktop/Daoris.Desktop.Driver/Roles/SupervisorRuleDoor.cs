using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A press of the supervisor's rules at one place: the place, its rules written whole, and the revision they were read at.</summary>
public sealed record SupervisorRulesPress(string Place, long Read, IReadOnlyList<SupervisorRule> Rules);

/// <summary>
/// The supervisor's rules through the role door (CONTEXTDRIVE1d; the context counters design §4.3, §4.4, §7.6): for the seeded
/// <c>supervisor</c>, <c>SET_ROLE</c>, <c>SET_ROLES</c>, <c>daoris role rules</c> and Ask Daoris's <c>role_propose</c> carry a place's
/// <c>rules</c> and <c>rulesRevision</c>, written to their own family and never a <c>roles</c> row. A rule grants nothing, so it is set
/// as a role's policy is, by the person's press; the supervisor's own record is set by a role's press like any role's.
/// </summary>
public static class SupervisorRuleDoor
{
    /// <summary>The seeded supervisor's id, whose press carries rules (<see cref="AppointedRoles.Supervisor"/>).</summary>
    public const string Id = AppointedRoles.Supervisor;

    /// <summary>The fields a press of the rules may carry; any other is a role's press.</summary>
    private static readonly string[] Fields = ["id", "change", "place", "rules", "rulesRevision"];

    /// <summary>Whether <paramref name="press"/> names the supervisor's rules: the supervisor's id with its <c>rules</c>, a <c>place</c> or a <c>rulesRevision</c>.</summary>
    public static bool Names(JsonElement press) =>
        press.ValueKind == JsonValueKind.Object && press.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString()?.Trim() == Id
        && (press.TryGetProperty("rules", out _) || press.TryGetProperty("place", out _) || press.TryGetProperty("rulesRevision", out _));

    /// <summary>
    /// A supervisor's press read: <c>{ id: "supervisor", place, rules: [{ id?, count, window?, at, do, upTo?, roles? }], rulesRevision }</c>, a
    /// rule's window its count's where it names none and its id given where it has none.
    /// </summary>
    /// <exception cref="RoleRefusedException"><c>mixed-shape</c>: the press carries a field of a role's press beside the rules.</exception>
    /// <exception cref="RuleRefusedException">The rules do not read as rules; the judge's code.</exception>
    public static SupervisorRulesPress Read(JsonElement press)
    {
        foreach (var property in press.EnumerateObject())
        {
            if (!Fields.Contains(property.Name, StringComparer.Ordinal)) throw Mixed($"`{property.Name}`");
        }

        if (Text(press, "change") is { } change && change != "set") throw Mixed($"`change: {change}`");
        var place = Text(press, "place") ?? throw Refused("place", "the supervisor's rules name their place: `machine`, `workspace:<id>`, `repository:<id>` or `line:<key>`.");
        if (!press.TryGetProperty("rulesRevision", out var revision) || !revision.TryGetInt64(out var read))
        {
            throw Refused("revision", $"the press names no `rulesRevision`: read {place}'s rules first, then write them whole.");
        }

        if (!press.TryGetProperty("rules", out var given) || given.ValueKind != JsonValueKind.Array)
        {
            throw Refused("count", "the press carries no `rules` list: a place's rules are written whole, an empty list for none.");
        }

        var rules = new List<SupervisorRule>();
        foreach (var each in given.EnumerateArray()) rules.Add(RuleOf(each, place, rules.Count));
        return new SupervisorRulesPress(place, read, rules);
    }

    /// <summary>
    /// The world a press is judged in: the catalogue's working roles, and each repository's workspace as its wider place, so a workspace's
    /// write is judged on its repositories' lines and a repository's on its workspace's rules, as the store keeps them in every process.
    /// </summary>
    public static SupervisorRuleWorld World(string home) => new(Roles(home), Wider: new RuleWiderPlaceStore(home).Read().Of);

    /// <summary>The judge's refusal of <paramref name="press"/> against the rules stored now, or null: what a door says before anything is sent.</summary>
    public static RuleRefusal? Judge(string home, SupervisorRulesPress press) =>
        SupervisorRules.Judge(press.Place, press.Read, press.Rules, new SupervisorRuleStore(home).All(), World(home));

    /// <summary>The rules as a press carries them, a rule's place and window left to the door.</summary>
    public static JsonArray Wire(IReadOnlyList<SupervisorRule> rules) =>
    [
        .. rules.Select(rule =>
        {
            var node = new JsonObject { ["id"] = rule.Id, ["count"] = rule.Count, ["window"] = rule.Window, ["at"] = rule.At, ["do"] = rule.Do };
            if (rule.UpTo is { } upTo) node["upTo"] = upTo;
            if (rule.Roles is { } roles) node["roles"] = new JsonArray([.. roles.Select(role => (JsonNode)JsonValue.Create(role))]);
            return (JsonNode)node;
        }),
    ];

    /// <summary>One rule in the terminal's words: <c>answers-alone at 2: hold</c>, with its minutes and its roles.</summary>
    public static string Said(SupervisorRule rule)
    {
        var said = new StringBuilder($"{rule.Count} at {rule.At}: {rule.Do}");
        if (rule.UpTo is { } upTo) said.Append($" up to {upTo} minutes");
        said.Append(rule.Roles is { } roles ? $" for {string.Join(", ", roles)}" : " for every role");
        return said.ToString();
    }

    private static IReadOnlyList<string> Roles(string home)
    {
        try
        {
            return [.. RoleCatalog.ReadHome(home).Slots.Where(slot => slot.Role is { Takes.Count: > 0 }).Select(slot => slot.Id)];
        }
        catch (DriverException)
        {
            return [.. RoleCatalog.Of([]).Slots.Where(slot => slot.Role is { Takes.Count: > 0 }).Select(slot => slot.Id)];
        }
    }

    private static SupervisorRule RuleOf(JsonElement each, string place, int index)
    {
        if (each.ValueKind != JsonValueKind.Object) throw Refused("count", $"rule {index + 1} is not a rule: `{{ count, at, do }}`.");
        var count = Text(each, "count") ?? throw Refused("count", $"rule {index + 1} names no count.");
        if (!each.TryGetProperty("at", out var at) || !at.TryGetInt32(out var threshold)) throw Refused("at", $"rule {index + 1} names no whole `at`.");
        var does = Text(each, "do") ?? throw Refused("do", $"rule {index + 1} names no action.");
        int? upTo = each.TryGetProperty("upTo", out var minutes) && minutes.ValueKind != JsonValueKind.Null
            ? minutes.TryGetInt32(out var number) ? number : throw Refused("upTo", $"rule {index + 1}'s `upTo` is not whole minutes.")
            : null;
        IReadOnlyList<string>? roles = null;
        if (each.TryGetProperty("roles", out var named) && named.ValueKind != JsonValueKind.Null)
        {
            roles = named.ValueKind == JsonValueKind.Array && named.EnumerateArray().All(role => role.ValueKind == JsonValueKind.String)
                ? [.. named.EnumerateArray().Select(role => role.GetString()!.Trim())]
                : throw Refused("roles", $"rule {index + 1}'s `roles` is not a list of role ids.");
        }

        var window = Text(each, "window") ?? SupervisorRules.Windows.GetValueOrDefault(count, "");
        return new SupervisorRule(Text(each, "id") ?? $"r-{index + 1}", count, window, threshold, does, upTo, place, roles);
    }

    private static RoleRefusedException Mixed(string field) => new(new RoleRefusal(
        "mixed-shape",
        $"a press of the supervisor's rules carries a place's `rules` and `rulesRevision` alone, and {field} is a role's press: set the supervisor's record by a press of its own."));

    private static RuleRefusedException Refused(string code, string said) => new(new RuleRefusal(code, said));

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;
}
