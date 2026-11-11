using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <i>Second check</i>'s first choices (<see cref="SecondCheck.Choices"/>, each with its day's start budget) written as its staffing row and
/// policy in one transaction, so turning it on names who reads. Nothing is written where the role has a row or a policy of its own, where
/// its record is the person's (changed, turned off or removed), or where a supervisor of the person's decides the kind.
/// </summary>
/// <remarks>The seed reads no choices, so a home is given them here once; the role stays held by the person's rules, which read nothing.</remarks>
public sealed class SecondCheckStep : IHomeMigrationStep
{
    public string Name => "second-check";

    public void Run(HomeMigrationRun run)
    {
        // The migration runs before the start is confirmed, and its copies are the undo, so it writes as a settled start does.
        var staffing = new RoleStaffing(run.Home, () => StoreSettled.Yes);
        var imported = staffing.ImportFile();
        if (imported.How is StaffingImported.Unread or StaffingImported.Held) throw new DriverException($"the roles' staffing was not imported: {imported.Said}");
        if (!Unstaffed(staffing.Location)) return;

        run.Keep(staffing.Location);
        var written = false;
        staffing.Publish([], write =>
        {
            if (!Unstaffed(write)) return;
            RoleStaffing.AddedRows(write, [(SecondCheck.Role, SecondCheck.Choices)]);
            StaffPolicies.Save(write, SecondCheck.Role, SecondCheck.Policy(SecondCheck.Choices), SecondCheck.Choices, 0, Moments.Write(DateTimeOffset.UtcNow));
            write.Moved(StaffPolicies.Family);
            written = true;
        });
        if (written) run.Said(staffing.Location);
    }

    private const string Own = "SELECT role FROM role_staffing WHERE role = $role UNION ALL SELECT role FROM role_policy WHERE role = $role";

    private const string Records = "SELECT id, COALESCE(record, '') FROM roles";

    private static bool Unstaffed(string location)
    {
        using var read = DesktopStoreReader.Open(location);
        return read is not null && read.Version >= StaffPolicies.Step
            && read.Query(Own, row => row.GetString(0), ("$role", SecondCheck.Role)).Count == 0
            && Unclaimed(read.Query(Records, row => (row.GetString(0), row.GetString(1))));
    }

    private static bool Unstaffed(DesktopWrite write) =>
        write.Query(Own, row => row.GetString(0), ("$role", SecondCheck.Role)).Count == 0
        && Unclaimed(write.Query(Records, row => (row.GetString(0), row.GetString(1))));

    private static bool Unclaimed(IReadOnlyList<(string Id, string Record)> roles) =>
        !roles.Any(role => role.Id == SecondCheck.Role || DecidesTheKind(role.Record));

    private static bool DecidesTheKind(string record)
    {
        try
        {
            return JsonNode.Parse(record) is JsonObject root && root["decides"] is JsonArray decides
                && decides.Any(kind => HomeMigrationRun.Text(kind) == SupervisorKinds.AskDaoris);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
