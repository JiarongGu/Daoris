using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a person's permissions answer for one of Daoris's acts in a tree: made, not made, or held until an allow.</summary>
internal enum FileVerdict
{
    Made,
    Denied,
    Held,
}

/// <summary>Daoris's three acts in a tree as a permission names them: <c>run(&lt;command&gt;)</c>, <c>copy(&lt;path&gt;)</c>, <c>share(&lt;path&gt;)</c>.</summary>
internal static class FileActs
{
    internal static string Run(string command) => $"run({command})";

    internal static string Copy(string path) => $"copy({path})";

    internal static string Share(string path) => $"share({path})";

    internal const string Person = "@person";

    internal static bool Names(string pattern) => Regex.IsMatch(pattern, @"^(run|copy|share)\(.+\)(@person)?$", RegexOptions.CultureInvariant);
}

/// <summary>
/// The person's restrictions on Daoris's acts, shaped as an agent's permission settings are: <c>deny</c>, <c>ask</c> and
/// <c>allow</c> lists of patterns, <c>*</c> matching anything, each empty by default so everything runs. Read deny first, then
/// allow, then ask, since an allow is what an ask waits for.
/// </summary>
internal sealed record FilePermissions(IReadOnlyList<string> Deny, IReadOnlyList<string> Ask, IReadOnlyList<string> Allow)
{
    internal static readonly FilePermissions None = new([], [], []);

    /// <summary>Who gave each allow, by its pattern, where the file names one.</summary>
    internal IReadOnlyDictionary<string, string> Makers { get; init; } = new Dictionary<string, string>();

    internal IReadOnlyDictionary<string, string> Decisions { get; init; } = new Dictionary<string, string>();

    internal FilePermissions With(FilePermissions other) => new([.. Deny, .. other.Deny], [.. Ask, .. other.Ask], [.. Allow, .. other.Allow])
    {
        Makers = Makers.Concat(other.Makers).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value),
        Decisions = Decisions.Concat(other.Decisions).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value),
    };

    internal FileVerdict Judge(string act) =>
        Deny.Any(pattern => Matches(pattern, act)) ? FileVerdict.Denied
        : Allow.Any(pattern => Matches(pattern, act)) ? FileVerdict.Made
        : Ask.Any(pattern => Matches(pattern, act)) ? FileVerdict.Held
        : FileVerdict.Made;

    internal bool AskNamesPerson(string act) => Ask.Any(pattern => pattern.EndsWith(FileActs.Person, StringComparison.Ordinal) && Matches(pattern, act));

    private static bool Matches(string pattern, string act) =>
        Regex.IsMatch(act, "^" + string.Join(".*", (pattern.EndsWith(FileActs.Person, StringComparison.Ordinal) ? pattern[..^FileActs.Person.Length] : pattern)
            .Split('*').Select(Regex.Escape)) + "$", RegexOptions.CultureInvariant);

    /// <summary>
    /// <paramref name="scope"/>'s <c>permissions</c>: each list's patterns, an allow also an object naming its <c>rule</c> and who gave it;
    /// a pattern naming no act is refused alone, said.
    /// </summary>
    internal static FilePermissions Read(JsonElement scope, string where, List<string> problems)
    {
        if (scope.ValueKind != JsonValueKind.Object || !scope.TryGetProperty("permissions", out var lists)) return None;
        if (lists.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"{where}permissions: is not an object");
            return None;
        }

        var makers = new Dictionary<string, string>();
        var decisions = new Dictionary<string, string>();

        List<string> Patterns(string name)
        {
            if (!lists.TryGetProperty(name, out var listed)) return [];
            if (listed.ValueKind != JsonValueKind.Array)
            {
                problems.Add($"{where}permissions.{name}: is not a list");
                return [];
            }

            var patterns = new List<string>();
            foreach (var each in listed.EnumerateArray())
            {
                var pattern = each.ValueKind == JsonValueKind.String ? each.GetString()
                    : each.ValueKind == JsonValueKind.Object ? Json.Text(each, "rule")
                    : null;
                if (pattern is not null && FileActs.Names(pattern))
                {
                    patterns.Add(pattern);
                    if (name == "allow" && each.ValueKind == JsonValueKind.Object && Json.Text(each, "by") is { Length: > 0 } maker) makers[pattern] = maker;
                    if (name == "allow" && each.ValueKind == JsonValueKind.Object && Json.Text(each, "decision") is { Length: > 0 } decision) decisions[pattern] = decision;
                }
                else problems.Add($"{where}permissions.{name}: '{pattern ?? each.GetRawText()}' names no run(…), copy(…) or share(…)");
            }

            return patterns;
        }

        var deny = Patterns("deny");
        var ask = Patterns("ask");
        var allow = Patterns("allow");
        return new(deny, ask, allow) { Makers = makers, Decisions = decisions };
    }
}
