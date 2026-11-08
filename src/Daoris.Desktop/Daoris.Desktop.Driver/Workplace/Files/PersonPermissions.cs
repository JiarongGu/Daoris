using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The person's <c>permissions</c> written into their layer: a pattern added to a list or taken out, an allow with who gave it. The
/// same file and the same refusal as the rules' writes, so a file that does not read is never written over and no allow is made
/// while it does not read.
/// </summary>
internal static class PersonPermissions
{
    internal static readonly IReadOnlyList<string> Lists = ["deny", "ask", "allow"];

    /// <summary>
    /// <paramref name="rule"/> added to <paramref name="list"/> of the machine's scope (<paramref name="repository"/> null) or the
    /// repository's; an allow is written with <paramref name="by"/>. False where the list already held it.
    /// </summary>
    /// <exception cref="DriverException">No such list, a pattern naming none of Daoris's acts, or a file that stands and does not read.</exception>
    internal static bool Add(string home, string? repository, string list, string rule, string? by, string? decision = null)
    {
        Check(list, rule);
        return PersonFiles.EditScope(home, repository, scope =>
        {
            var held = Held(scope, list);
            if (held.Any(each => Rule(each) == rule)) return 0;
            if (list != "allow" || by is not { Length: > 0 }) held.Add(rule);
            else if (decision is { Length: > 0 }) held.Add(new JsonObject { ["rule"] = rule, ["by"] = by, ["decision"] = decision });
            else held.Add(new JsonObject { ["rule"] = rule, ["by"] = by });
            return 1;
        }) > 0;
    }

    /// <summary>Each entry of the list naming <paramref name="rule"/> taken out; false where none did.</summary>
    /// <exception cref="DriverException">No such list, or a file that stands and does not read.</exception>
    internal static bool Remove(string home, string? repository, string list, string rule)
    {
        Check(list, rule);
        return PersonFiles.EditScope(home, repository, scope =>
        {
            var held = Held(scope, list);
            var named = held.Where(each => Rule(each) == rule).ToList();
            foreach (var each in named) held.Remove(each);
            return named.Count;
        }) > 0;
    }

    private static void Check(string list, string rule)
    {
        if (!Lists.Contains(list)) throw new DriverException($"a permission goes in `deny`, `ask` or `allow`, not `{list}`.");
        if (!FileActs.Names(rule)) throw new DriverException($"`{rule}` names none of run(…), copy(…) or share(…).");
    }

    private static JsonArray Held(JsonObject scope, string list)
    {
        if (scope["permissions"] is not JsonObject permissions) scope["permissions"] = permissions = [];
        if (permissions[list] is not JsonArray held) permissions[list] = held = [];
        return held;
    }

    private static string? Rule(JsonNode? each) => each?.GetValueKind() switch
    {
        JsonValueKind.String => each.GetValue<string>(),
        JsonValueKind.Object => each["rule"]?.GetValueKind() == JsonValueKind.String ? each["rule"]!.GetValue<string>() : null,
        _ => null,
    };
}
