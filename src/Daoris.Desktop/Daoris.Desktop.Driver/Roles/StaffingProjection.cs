namespace Daoris.Driver;

/// <summary>
/// The staffing as a loaded record holds it and as the store's rows keep it (FOUNDATION1w-s parts 4 and 5, §19.15): the store's staffing read
/// into the five fields of a record, and a record's staffing written back as the rows.
/// </summary>
internal static class StaffingProjection
{
    /// <summary>The store's staffing as the five fields of a loaded record hold it.</summary>
    public static DriverConfig Config(StaffingRead read) => DriverConfig.Empty with
    {
        Actors = read.Actors,
        ActorErrors = read.Errors,
    };

    /// <summary>The staffing's rows rewritten as <paramref name="staffing"/> holds them, as the import writes them.</summary>
    public static void Rows(DesktopWrite write, DriverConfig staffing, IReadOnlyDictionary<string, string> asFound, string at)
    {
        // An added role's row is the catalogue's staffing (FOUNDATION1w) and stays; only the seeded eight are rewritten.
        write.Execute($"DELETE FROM role_staffing WHERE role IN ({string.Join(", ", ActorChoices.Ids.Select(id => $"'{id}'"))})");
        foreach (var id in ActorChoices.Ids)
        {
            (string State, string? Choices, string? Why)? row =
                staffing.ActorErrors.TryGetValue(id, out var why) ? ("unreadable", asFound.GetValueOrDefault(id) ?? "null", why)
                : staffing.Actors.TryGetValue(id, out var choices) ? ("set", RoleStaffing.Write(choices), null)
                : null;
            if (row is not { } each) continue;
            write.Execute(
                "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ($role, $state, $choices, $why, NULL, $at)",
                ("$role", id), ("$state", each.State), ("$choices", each.Choices), ("$why", each.Why), ("$at", at));
        }
    }
}
