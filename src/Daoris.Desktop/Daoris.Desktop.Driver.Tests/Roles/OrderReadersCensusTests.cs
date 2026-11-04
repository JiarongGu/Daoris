using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1d, the order readers' census (the staff policy design §3.3, §9 row d): every direct reader of a role's stored order
/// (<c>ActorChoices.Effective</c>) is named with why it may read the order as stored, so a reader added later that should read the arranged
/// one (<see cref="RoleOrders"/>, or the start plan) fails here until it joins, with its reason.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class OrderReadersCensusTests
{
    private static readonly (string File, string Why)[] Census =
    [
        ("Daoris.Desktop.Driver/Roles/ActorChoices.cs", "the reader itself, and the actors' view of the stored order"),
        ("Daoris.Desktop.Driver/Roles/RoleOrders.cs", "the arranged reader every line's order but a start's own goes through"),
        ("Daoris.Desktop.Driver/Roles/RoleCatalog.cs", "the catalogue's order of the role that takes a work, the start plan's input"),
        ("Daoris.Desktop.Driver/Roles/StartPlan.cs", "the start's own walk, arranged by the policy before any account is asked"),
        ("Daoris.Desktop.Driver/Roles/StaffPolicyDoor.cs", "the probe of a role's next new start, which arranges it itself"),
        ("Daoris.Desktop.Driver/Participants/RoleStructure.cs", "the structure the screen reads, which shows the stored choices"),
        ("Daoris.Desktop.Driver/Participants/Helpers/HelperChoice.cs", "the fence a helper's rule decides; the reviewer walk arranges its alternatives within one agent"),
        ("Daoris.Desktop.Driver/Loop/DriverBrainstorms.cs", "Brainstorm's voices are read once and frozen; the judge refuses a policy on it"),
        ("Daoris.Desktop.Driver/Loop/Chat/ChatStarts.cs", "tells whether a caller's order is the seeded Chat order, which an added role's own replaces"),
        ("Daoris.Desktop.Modules/Bridge/Routes/HelpRoutes.cs", "Ask Daoris's own order, handed to the placing walk, which arranges it"),
    ];

    [GeneratedRegex(@"ActorChoices\.Effective\(")]
    private static partial Regex Reads();

    private static IEnumerable<string> Reading()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        foreach (var project in new[] { "Daoris.Desktop.Driver", "Daoris.Desktop.Modules" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                if (File.ReadLines(file).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reads().IsMatch(line))) yield return relative;
            }
        }
    }

    [Fact]
    public void Every_direct_reader_of_a_roles_stored_order_is_in_the_census_and_every_census_file_still_reads_it()
    {
        Assert.Equal([.. Census.Select(each => each.File).Order(StringComparer.Ordinal)], [.. Reading().Order(StringComparer.Ordinal)]);
        Assert.All(Census, each => Assert.False(string.IsNullOrWhiteSpace(each.Why)));
    }
}
