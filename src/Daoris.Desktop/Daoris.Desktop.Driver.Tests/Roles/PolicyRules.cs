using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A fixture policy's moves and bound as the rules the course reads them from: <c>transient after 2: weighted</c> is <c>tries at 2: weighted</c>,
/// <c>failed after 2: next</c> <c>failures-on-choice at 2: next</c>, <c>limit: wait upTo 60</c> <c>limits at 1: wait</c> up to 60,
/// <c>quiet: next</c> <c>quiet at 1: next</c>, and the bound <c>moves at n: ask</c>. A test states a line's course the way the fixtures' policy
/// rows say it, and the rules are what the driver reads; no stored policy holds a move.
/// </summary>
internal static class PolicyRules
{
    /// <summary>
    /// The policy <paramref name="text"/> holds with the fixture's <c>moves</c> and <c>most</c> laid over it as the course reads a line's rules
    /// (<see cref="StaffPolicy.Moves"/>): the record itself passes them over. Null where the record does not read.
    /// </summary>
    public static StaffPolicy? Parse(string text)
    {
        var (policy, _) = StaffPolicy.Parse(text);
        if (policy is null) return null;
        using var document = System.Text.Json.JsonDocument.Parse(text);
        var moves = new Dictionary<string, StaffMove>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("moves", out var set) && set.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var each in set.EnumerateObject())
            {
                if (each.Value.ValueKind == System.Text.Json.JsonValueKind.String) moves[each.Name] = new StaffMove(each.Value.GetString()!);
                else if (each.Value.ValueKind == System.Text.Json.JsonValueKind.Object && each.Value.TryGetProperty("do", out var action))
                {
                    moves[each.Name] = new StaffMove(
                        action.GetString()!,
                        each.Value.TryGetProperty("after", out var after) && after.TryGetInt32(out var tries) ? tries : null,
                        each.Value.TryGetProperty("upTo", out var upTo) && upTo.TryGetInt32(out var minutes) ? minutes : null);
                }
            }
        }

        int? most = document.RootElement.TryGetProperty("most", out var bound) && bound.TryGetInt32(out var number) ? number : null;
        return policy with { Moves = moves, Most = most };
    }

    public static IReadOnlyList<SupervisorRule> From(StaffPolicy policy, string role, string place)
    {
        var rules = new List<SupervisorRule>();
        SupervisorRule Rule(string count, int at, string does, int? upTo = null) =>
            new($"{role}-{count}", count, SupervisorRules.Windows[count], at, does, upTo, place, [role]);
        foreach (var (condition, move) in policy.Moves.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (move.Do == StaffActions.Retry) continue;
            switch (condition)
            {
                case StaffConditions.Transient: rules.Add(Rule(SupervisorRules.Tries, Math.Clamp(move.After ?? TransientRetry.Bound, 1, TransientRetry.Bound), move.Do)); break;
                case StaffConditions.Failed: rules.Add(Rule(SupervisorRules.FailuresOnChoice, move.After ?? 1, move.Do)); break;
                case StaffConditions.Quiet: rules.Add(Rule(SupervisorRules.Quiet, 1, move.Do)); break;
                case StaffConditions.Limit: rules.Add(Rule(SupervisorRules.Limits, 1, move.Do, move.Do == StaffActions.Wait ? move.UpTo ?? 60 : null)); break;
            }
        }

        if (policy.Bound is { } bound && rules.Count > 0) rules.Add(Rule(SupervisorRules.Moves, bound, SupervisorRules.Ask));
        return rules;
    }
}
