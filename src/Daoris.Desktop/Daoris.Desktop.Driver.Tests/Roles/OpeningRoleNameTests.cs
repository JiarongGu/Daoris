using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The opening note names the role that took the work: a seeded role by its own words, an added role by its name
/// as written in the catalogue, and a role the catalogue cannot name by the actor's words.
/// </summary>
public sealed class OpeningRoleNameTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "opening-role-name", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private void AddRole(string id, string name)
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "acp-stub", "actors": { "build": [ { "agent": "acp-stub", "model": "sonnet", "effort": "low" } ] } }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.True(staffing.ImportFile().How is StaffingImported.Done or StaffingImported.Already);
        var added = RoleCatalog.Seed[0] with { Id = id, Name = AuthoredText.Of(name), Scope = new RoleScope(RoleScope.Repository, "engine") };
        RoleDoor.Apply(staffing, RoleChange.Set(added), [new ActorChoice("acp-stub", "sonnet", "low")]);
    }

    [Fact]
    public void An_added_role_is_named_as_written_and_the_seeded_one_by_the_actors_words()
    {
        AddRole("engine-build", "Engine builder");

        Assert.Equal("Engine builder", RotatedOpening.NameOf(_home, "engine-build", "build"));
        Assert.Equal(ActorWords.Name("build"), RotatedOpening.NameOf(_home, "build", "build"));
        Assert.NotEqual("Engine builder", RotatedOpening.NameOf(_home, "build", "build"));
    }

    [Fact]
    public void A_role_the_catalogue_does_not_hold_is_named_by_its_id()
    {
        AddRole("engine-build", "Engine builder");

        Assert.Equal(ActorWords.Name("no-such-role"), RotatedOpening.NameOf(_home, "no-such-role", "build"));
    }
}
