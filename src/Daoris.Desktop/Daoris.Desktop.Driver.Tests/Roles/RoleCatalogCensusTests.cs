using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The judge's census: every writer of an actor's staffing is named, each by the
/// file that reaches the judge or the staffing's one write, so a writer added later without joining the census fails here; and the eight
/// always-on ids are one list.
/// </summary>
[Trait(Category.Name, Category.Census)]
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
    /// Each writer and the files that reach the judge or the staffing's write for it. The role door, which `SET_ROLE`,
    /// and `daoris role` call, is the one door; the release's migration writes an older home's five orders once.
    /// </summary>
    private static readonly (string Writer, string[] Files)[] Census =
    [
        ("the migration's staffing step", ["Daoris.Desktop.Driver/Transitional/Migration/StaffingStep.cs"]),
        ("the import and the check", ["Daoris.Desktop.Driver/Roles/RoleStaffing.cs"]),
        ("the judges themselves", ["Daoris.Desktop.Driver/Roles/RoleCatalog.cs"]),
        ("the role door", ["Daoris.Desktop.Driver/Roles/RoleDoor.cs"]),
        ("SET_ROLE", ["Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs"]),
        ("daoris role", ["Daoris.Desktop.Driver/Roles/RoleVerbs.cs"]),
    ];

    [GeneratedRegex(@"ActorChoices\.(Judge|Judged|With)\(|RoleStaffing\.SetAt\(|staffing\.Replace\(|\.WithIntake\(|\.WithHelper\(|RoleCatalog\.Write(In)?\(|RoleDoor\.Apply\(|public static DriverConfig With(Intake|Helper)\(")]
    private static partial Regex Reaches();

    private static IEnumerable<string> Reaching()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var modules = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        foreach (var file in Directory.EnumerateFiles(driver, "*.cs", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)))
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
            ["the migration's staffing step", "the import and the check", "the judges themselves", "the role door", "SET_ROLE", "daoris role"],
            Census.Select(each => each.Writer));
    }

    [Fact]
    public async Task The_eight_always_on_ids_are_one_list_the_catalogue_the_run_and_the_settings_read()
    {
        Assert.Same(ActorKey.Ids, ActorChoices.Ids);
        Assert.Equal(ActorChoices.Ids, RoleCatalog.Seed.Where(role => role.Hold is null).Select(role => role.Id));
        Assert.Empty(ActorChoices.Ids.Intersect(AppointedRoles.Kinds));

        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        foreach (var id in ActorChoices.Ids)
        {
            await using var run = new ActorRun(events, "s1", id, null, _ => Task.CompletedTask);
        }

        Assert.Throws<ArgumentException>(() => new ActorRun(events, "s1", "supervisor", null, _ => Task.CompletedTask));
    }
}
