namespace Daoris.Driver;

/// <summary>
/// The window's read of the roles: every role of the catalogue, each seeded one carrying its order as it runs. Build's and Chat's carry the
/// order their policy arranges too, where it differs, since the hand-over's offer draws its next choices from the order a start would walk.
/// </summary>
public static class RoleRowsRunning
{
    public static IReadOnlyList<RoleRow> Of(string home, RoleCatalog catalogue, DriverConfig config)
    {
        var ledger = new StaffLedger(home);
        return [.. RoleDoor.Rows(catalogue, config).Select(row =>
            ActorChoices.Ids.Contains(row.Id, StringComparer.Ordinal) ? row with { Staffing = Arranged(ledger, ActorView.Of(row.Id, config), config) } : row)];
    }

    private static ActorView Arranged(StaffLedger ledger, ActorView view, DriverConfig config)
    {
        if (view.Id is not ("build" or "chat") || view.State == "unreadable") return view;
        var arranged = RoleOrders.Arranged(ledger, view.Id, view.Id, config);
        return arranged.SequenceEqual(view.Choices) ? view : view with { Arranged = arranged };
    }
}
