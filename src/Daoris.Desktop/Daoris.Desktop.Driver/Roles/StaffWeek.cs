namespace Daoris.Driver;

/// <summary>
/// One role's last seven days: its new starts, by choice, and its moves to another choice,
/// by condition, as Ask Daoris's room says them so <i>why did this run on Opus?</i> is answered from records.
/// </summary>
public sealed record StaffWeekRole(int Starts, IReadOnlyList<(StaffKey Choice, int Count)> ByChoice, IReadOnlyList<(string Condition, int Count)> Moved)
{
    /// <summary>The week in a sentence: the starts with the choice each took, then the moves.</summary>
    public string Said
    {
        get
        {
            if (Starts == 0 && Moved.Count == 0) return "last 7 days: no new start";
            var starts = Starts == 0
                ? "no new start"
                : $"{Starts} {(Starts == 1 ? "start" : "starts")} ({string.Join(", ", ByChoice.Select(each => $"{Named(each.Choice)} ×{each.Count}"))})";
            return $"last 7 days: {starts}" + (Moved.Count > 0 ? $", moved: {string.Join(", ", Moved.Select(each => $"{each.Condition} ×{each.Count}"))}" : "");
        }
    }

    /// <summary>A choice by its agent, model and effort, leaving out what it does not name.</summary>
    internal static string Named(StaffKey choice) => string.Join(" ", new[] { choice.Agent, choice.Model, choice.Effort }.OfType<string>());
}

/// <summary>The seven days every role of a home has behind it, read from <c>role_starts</c> and <c>role_moves</c>.</summary>
public static class StaffWeek
{
    /// <summary>How far back the room looks.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>Whether the home's store holds any role's policy, set or stale; false for no store or one before the table.</summary>
    public static bool AnyPolicy(string home)
    {
        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
        return store is not null && store.Version >= StaffPolicies.Step
            && store.Query("SELECT COUNT(*) FROM role_policy", row => row.GetInt32(0)).FirstOrDefault() > 0;
    }

    /// <summary>
    /// Each role's week, by role id; a role with neither a start nor a move is left out. A home with no store, or one before the tables,
    /// has none, and the store is never created by the read.
    /// </summary>
    public static IReadOnlyDictionary<string, StaffWeekRole> Read(string home, TimeProvider? clock = null)
    {
        var weeks = new Dictionary<string, StaffWeekRole>(StringComparer.Ordinal);
        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
        if (store is null || store.Version < StaffPolicies.Step) return weeks;
        var since = StaffJson.Moment((clock ?? TimeProvider.System).GetUtcNow() - Window);
        var starts = store.Query(
            "SELECT role, choice, COUNT(*) FROM role_starts WHERE at >= $since GROUP BY role, choice ORDER BY role, MIN(at)",
            row => (Role: row.GetString(0), Choice: StaffKey.Read(row.GetString(1)), Count: row.GetInt32(2)), ("$since", since));
        var moves = store.Version < StaffMoves.Step
            ? []
            : store.Query(
                "SELECT role, COALESCE(condition, ''), COUNT(*) FROM role_moves WHERE kind = $kind AND at >= $since GROUP BY role, condition ORDER BY role, MIN(id)",
                row => (Role: row.GetString(0), Condition: row.GetString(1), Count: row.GetInt32(2)), ("$kind", StaffMoves.Moved), ("$since", since));

        foreach (var role in starts.Select(each => each.Role).Concat(moves.Select(each => each.Role)).Distinct(StringComparer.Ordinal))
        {
            var taken = starts.Where(each => each.Role == role && each.Choice is not null).Select(each => (each.Choice!.Value, each.Count))
                .OrderByDescending(each => each.Count).ToList();
            weeks[role] = new StaffWeekRole(
                starts.Where(each => each.Role == role).Sum(each => each.Count), taken,
                [.. moves.Where(each => each.Role == role && each.Condition.Length > 0).Select(each => (each.Condition, each.Count))]);
        }

        return weeks;
    }
}
