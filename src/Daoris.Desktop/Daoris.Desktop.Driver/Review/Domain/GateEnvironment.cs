using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The environment a gate is composed with (INTEGRATE1f part f1; the integration design §5.5; D215's note on INTEGRATE1a): a gate never
/// inherits a person's or the steward's shell, since Daoris starts it. Its environment is composed <b>from the one it is given</b>, never by
/// inheriting and then adding, because an inherited block cannot lose a name: the Daoris names (the home, the roles, the keys, the capacity),
/// git's repository-locating variables and the merge tool's switches are removed; the build servers of the gate's own are added; the names
/// an accepted declaration passes come back from the given environment (never a Daoris name); and the admission's hold marker, the one
/// Daoris name a gate gets on purpose, is set when the gate runs inside a hold.
/// </summary>
/// <remarks>
/// <b>A twin</b> of <c>gateEnvironment</c>, <c>GATE_REMOVED</c> and <c>BESIDE_ENVIRONMENT</c> in <c>tools/merge-branch.mjs</c>: no code is
/// shared, so both hold one table, the driver tests' <c>fixtures/gate-environment.json</c>, row for row (<c>GateEnvironmentTests</c>,
/// <c>integration-gates.test.ts</c>). The tool also removes its tool tests' two stand-in variables, which no gate of the driver's meets, and
/// gives no gate the hold marker until its own hold plumbing is built; a name folded by case is held once here, as a process environment on
/// Windows holds it. The runner's own inputs (§5.9) are set on the runner's job alone by its caller, never on a gate, and the census names them there.
/// </remarks>
internal static class GateEnvironment
{
    /// <summary>The names a gate never inherits, as the composition records them (<c>/source/flags</c>); the first removes every Daoris name, even the capacity and the supervisor key.</summary>
    internal static readonly IReadOnlyList<string> RemovedRule =
    [
        "/^DAORIS_/i",
        "/^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES|CONFIG.*)$/i",
        "/^MERGE_BRANCH_/i",
    ];

    /// <summary>The build servers of a gate's own: none reused after the run, no MSBuild server and no shared compiler server, so a gate beside the owner's work leaves nothing to serve it and borrows none.</summary>
    internal static readonly IReadOnlyList<KeyValuePair<string, string>> Beside =
    [
        KeyValuePair.Create("MSBUILDDISABLENODEREUSE", "1"),
        KeyValuePair.Create("DOTNET_CLI_USE_MSBUILD_SERVER", "0"),
        KeyValuePair.Create("UseSharedCompilation", "false"),
    ];

    /// <summary>The admission's hold marker: a heavy run a gate starts sees itself inside the gate's hold and asks no place of its own.</summary>
    internal const string HoldVariable = MachineAdmission.InsideVariable;

    private static readonly IReadOnlyList<Regex> Removed =
        [.. RemovedRule.Select(rule => new Regex(rule[1..^2], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))];

    /// <summary>Whether the rule removes the name from a gate's environment, in any spelling.</summary>
    internal static bool IsRemoved(string name) => Removed.Any(pattern => pattern.IsMatch(name));

    /// <summary>Whether the name is a Daoris one, which no declaration passes.</summary>
    internal static bool IsDaorisName(string name) => name.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase);

    /// <summary>The rule as a composition records it, never a value a machine holds: the patterns removed and the variables added.</summary>
    internal static JsonObject Rule() => new()
    {
        ["removed"] = new JsonArray([.. RemovedRule.Select(rule => (JsonNode?)JsonValue.Create(rule))]),
        ["added"] = new JsonObject([.. Beside.Select(each => KeyValuePair.Create<string, JsonNode?>(each.Key, each.Value))]),
    };

    /// <summary>
    /// A gate's environment, composed from <paramref name="start"/>: what it holds less the removed names, then the names
    /// <paramref name="pass"/> lists (matched in any case, a removed one included, never a Daoris one) from <paramref name="start"/>
    /// in their own spelling, then the build servers of the gate's own, then the hold marker where <paramref name="hold"/> names the hold.
    /// </summary>
    /// <param name="start">The environment composed from: the install's own, not a shell a person or the steward holds.</param>
    /// <param name="pass">The variables' names the accepted declaration's <c>env.pass</c> lists.</param>
    /// <param name="hold">The id of the admission hold the gate runs inside; null where it runs in none.</param>
    internal static IReadOnlyList<KeyValuePair<string, string>> Compose(IEnumerable<KeyValuePair<string, string>> start, IEnumerable<string>? pass = null, string? hold = null)
    {
        var given = start.ToList();
        var composed = given.Where(each => !IsRemoved(each.Key)).ToList();
        var wanted = new HashSet<string>(
            (pass ?? []).Where(name => !IsDaorisName(name)), StringComparer.OrdinalIgnoreCase);
        foreach (var each in given.Where(each => wanted.Contains(each.Key))) Put(composed, each.Key, each.Value);
        foreach (var each in Beside) Put(composed, each.Key, each.Value);
        if (!string.IsNullOrEmpty(hold)) Put(composed, HoldVariable, hold);
        return composed;
    }

    /// <summary>One name set, in the place it already holds if it holds one under any spelling.</summary>
    private static void Put(List<KeyValuePair<string, string>> list, string name, string value)
    {
        var at = list.FindIndex(each => string.Equals(each.Key, name, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) list[at] = KeyValuePair.Create(name, value);
        else list.Add(KeyValuePair.Create(name, value));
    }
}
