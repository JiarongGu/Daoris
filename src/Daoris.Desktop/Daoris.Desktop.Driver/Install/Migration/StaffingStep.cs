namespace Daoris.Driver;

/// <summary>
/// The roles' staffing into the store alone (D232; ONEWAY1b's conversion, run once): the file's staffing imported where the store holds none,
/// each of the five roles with no order of its own written the order it ran by its single-agent key, at account default, and Intake or Ask
/// Daoris with no key written off; an entry that does not read is kept as it is. Then <c>driver.json</c>'s four staffing keys taken out.
/// Second opinion, Partner and Helper stay by their caller's rule, so nothing is written for them.
/// </summary>
public sealed class StaffingStep : IHomeMigrationStep
{
    private static readonly string[] Keys = ["actors", "adapter", "intakeAdapter", "helperAdapter"];

    public string Name => "staffing";

    public void Run(HomeMigrationRun run)
    {
        // The migration runs before the start is confirmed, and its copies are the undo, so it writes as a settled start does.
        var staffing = new RoleStaffing(run.Home, () => StoreSettled.Yes);
        run.Keep(staffing.Location);
        var imported = staffing.ImportFile();
        if (imported.How is StaffingImported.Unread or StaffingImported.Held) throw new DriverException($"the roles' staffing was not imported: {imported.Said}");
        if (staffing.Read() is not { } read) return;

        var rows = staffing.Rows();
        var writes = new List<(string Role, StaffingRow? Row)>();
        foreach (var role in ActorChoices.Ordered)
        {
            if (rows.Any(row => row.Role == role && row.State is "set" or "unreadable")) continue;
            var key = role switch
            {
                "build" or "chat" or "brainstorm" => read.Adapter,
                "intake" => read.IntakeAdapter,
                "ask-daoris" => read.HelperAdapter,
                _ => null,
            };
            IReadOnlyList<ActorChoice> order = key is { Length: > 0 } agent ? [new ActorChoice(agent)] : ActorChoices.Seed(role) ?? [];
            var first = role is "build" or "intake" or "ask-daoris" ? order.FirstOrDefault()?.Agent : null;
            writes.Add((role, new StaffingRow(role, "set", RoleStaffing.Write(order), null, first)));
        }

        if (writes.Count > 0)
        {
            using var held = staffing.Lock();
            staffing.Replace(writes);
            run.Said(staffing.Location);
        }

        run.ChangeJson(staffing.FileOfHome, root => Keys.Count(root.Remove) > 0);
    }
}
