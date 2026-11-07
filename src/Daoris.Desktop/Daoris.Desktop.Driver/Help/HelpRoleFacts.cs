namespace Daoris.Driver;

/// <summary>
/// The roles' facts for Ask Daoris's room: read from the home as the room is written, beside the access, because
/// the room's section (<see cref="HelpRoomRoles"/>) sits below the roles and names none of their types.
/// </summary>
internal static class HelpRoleFacts
{
    /// <summary>
    /// Every role of <paramref name="home"/> that takes work, as the room says it. A home with no policy and no start or move in the week
    /// has nothing to add to the order, and says none, so a room is not longer for roles nobody has spent; one that does not read says
    /// none, since the room is written at a start and holds no start for it.
    /// </summary>
    internal static IReadOnlyList<HelpRole> Read(string home)
    {
        try
        {
            var weeks = StaffWeek.Read(home);
            if (weeks.Count == 0 && !StaffWeek.AnyPolicy(home)) return [];

            var catalogue = RoleCatalog.ReadHome(home);
            var config = DriverConfig.Load(DaorisHome.Entry(home, HomeEntry.DriverConfig));
            var ledger = new StaffLedger(home);
            var next = RolePolicyRow.Of(ledger, catalogue, config).ToDictionary(row => row.Role, StringComparer.Ordinal);
            return
            [
                .. catalogue.Slots.Where(slot => slot.Role is { Takes.Count: > 0 }).Select(slot =>
                {
                    var role = slot.Role!;
                    var own = config.Actors.TryGetValue(role.Id, out var choices) ? choices : [];
                    var read = ledger.Policy(role.Id);
                    return new HelpRole(role.Id, role.Name.Texts.FirstOrDefault() ?? role.Id, role.Takes, role.Scope.Kind, role.Scope.Id, slot.Revision)
                    {
                        Choices = [.. own.Select(choice => StaffWeekRole.Named(StaffKey.Of(choice)))],
                        ChoiceState = ActorChoices.Ids.Contains(role.Id, StringComparer.Ordinal) ? ActorView.Of(role.Id, config).State : HelpRole.Before,
                        PolicyState = read.State,
                        PolicyRevision = read.Revision,
                        Policy = StaffPolicyFlags.Fold(read.Applied, own),
                        Why = read.Why,
                        Next = next.GetValueOrDefault(role.Id)?.Next?.Said,
                        Week = (weeks.GetValueOrDefault(role.Id) ?? new StaffWeekRole(0, [], [])).Said,
                    };
                }),
            ];
        }
        catch (Exception error) when (error is DriverException or IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
