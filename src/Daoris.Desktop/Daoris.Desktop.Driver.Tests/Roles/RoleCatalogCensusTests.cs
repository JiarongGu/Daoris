using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 w, the judge's census (the foundation design §2.7, Part B's row w): every writer of an actor's staffing is named, each by the
/// file that reaches the judge or the staffing's one write, so a writer added later without joining the census fails here; and the eight
/// always-on ids are one list.
/// </summary>
public sealed partial class RoleCatalogCensusTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-catalog-census", Guid.NewGuid().ToString("N")[..8]);

    public RoleCatalogCensusTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>
    /// Each writer and the files that reach the judge or the staffing's write for it. FOUNDATION1y-i: the role door, which `SET_ROLE`,
    /// `daoris driver role` and `role_propose` call.
    /// </summary>
    private static readonly (string Writer, string[] Files)[] Census =
    [
        ("SET_ACTOR", ["Daoris.Desktop.Modules/Bridge/DriverContext.cs"]),
        ("daoris driver actor", ["Daoris.Desktop.Driver/Roles/ActorVerbs.cs", "Daoris.Desktop.Driver/Roles/StaffingVerbs.cs"]),
        ("Ask Daoris's proposals", ["Daoris.Desktop.Driver/Help/Proposals/HelpSettingProposals.cs"]),
        ("the import and the check", ["Daoris.Desktop.Driver/Roles/RoleStaffing.cs"]),
        ("the judges themselves", ["Daoris.Desktop.Driver/Roles/ActorChoices.cs", "Daoris.Desktop.Driver/Roles/RoleCatalog.cs"]),
        ("the role door", ["Daoris.Desktop.Driver/Roles/RoleDoor.cs"]),
        ("SET_ROLE", ["Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs"]),
        ("daoris driver role", ["Daoris.Desktop.Driver/Roles/RoleVerbs.cs"]),
    ];

    [GeneratedRegex(@"ActorChoices\.(Judge|Judged|With)\(|RoleStaffing\.SetAt\(|\.WithIntake\(|\.WithHelper\(|RoleCatalog\.Write\(|RoleDoor\.Apply\(|public static DriverConfig With(Intake|Helper)\(")]
    private static partial Regex Reaches();

    private static IEnumerable<string> Reaching()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains(".Tests/") || relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
            if (File.ReadLines(file).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reaches().IsMatch(line))) yield return relative;
        }
    }

    [Fact]
    public void Every_writer_of_the_staffing_is_in_the_census_and_every_census_file_still_reaches_it()
    {
        var named = Census.SelectMany(each => each.Files).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(named, Reaching().Order(StringComparer.Ordinal).ToList());
        Assert.Equal(
            ["SET_ACTOR", "daoris driver actor", "Ask Daoris's proposals", "the import and the check", "the judges themselves", "the role door", "SET_ROLE", "daoris driver role"],
            Census.Select(each => each.Writer));
    }

    [Fact]
    public async Task The_eight_always_on_ids_are_one_list_the_catalogue_the_run_and_the_settings_read()
    {
        Assert.Same(ActorKey.Ids, ActorChoices.Ids);
        Assert.Equal(ActorChoices.Ids, RoleCatalog.Seed.Select(role => role.Id));
        Assert.Empty(ActorChoices.Ids.Intersect(RoleCatalog.Reserved));

        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        foreach (var id in ActorChoices.Ids)
        {
            await using var run = new ActorRun(events, "s1", id, null, _ => Task.CompletedTask);
        }

        Assert.Throws<ArgumentException>(() => new ActorRun(events, "s1", "supervisor", null, _ => Task.CompletedTask));
    }
}
