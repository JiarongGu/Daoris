using System.Collections.Immutable;
using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The run-time half for git, by the scope (the design's §46, §62): over the starts a flow makes, each with whose ask it ran inside, a
/// start outside the store and the door is red where its arguments or questions name a judged declaration's path or a judged object
/// id; a door's start is red where it names one by path other than in its exclusion, never by id, since the store's own reads name
/// declarations' ids; a start under the store's scope is the store reading. The judged names are those the banned list holds, so the
/// half narrows with each row that bans one; the ids are the declarations' entries at each commit the run named, listed by the store,
/// seeded with every accepted and frozen id.
/// </summary>
internal static class DeclarationGitJudge
{
    /// <summary>The repository declarations' names the banned list holds: each <c>DeclarationNames</c> member it bans, by its value.</summary>
    internal static ImmutableArray<string> Judged(IEnumerable<string> banned) =>
        [.. banned.Where(id => id.StartsWith("F:Daoris.Driver.DeclarationNames.", StringComparison.Ordinal))
            .Select(id => typeof(DeclarationNames).GetField(id["F:Daoris.Driver.DeclarationNames.".Length..], BindingFlags.Public | BindingFlags.Static)?.GetValue(null))
            .OfType<string>().Where(name => DeclarationNames.Repository.Contains(name))];

    /// <summary>Each start of <paramref name="starts"/> that reads a judged declaration other than through the store, said with its command.</summary>
    internal static List<string> Problems(
        IEnumerable<(IReadOnlyList<string> Command, DeclarationAsker? Scope)> starts, IReadOnlyCollection<string> names, IReadOnlyCollection<string> ids)
    {
        var problems = new List<string>();
        foreach (var (command, scope) in starts)
        {
            if (scope == DeclarationAsker.Store) continue;
            var asked = command.SelectMany(each => each.Split('\n')).ToList();
            var said = string.Join(' ', command).Replace('\n', ' ');
            var who = scope is null ? "a start outside the store" : "a door's start";
            if (asked.Where(each => !(scope == DeclarationAsker.Door && each.StartsWith(":(exclude", StringComparison.Ordinal))).Select(each => Named(each, names)).FirstOrDefault(name => name is not null) is { } path)
                problems.Add($"{who} names {path} by path: git {said}");
            else if (scope is null && ids.FirstOrDefault(id => asked.Any(each => each.Contains(id, StringComparison.OrdinalIgnoreCase))) is { } id)
                problems.Add($"{who} names the declaration {id} by id: git {said}");
        }

        return problems;
    }

    // A pathspec's element, a <rev>:<path>'s path or a listed path names a declaration where its tail is one.
    private static string? Named(string argument, IReadOnlyCollection<string> names)
    {
        var path = argument.Replace('\\', '/');
        var tail = path[(path.LastIndexOfAny(['/', ':', ')']) + 1)..];
        return names.FirstOrDefault(name => string.Equals(name, tail, StringComparison.OrdinalIgnoreCase));
    }
}
