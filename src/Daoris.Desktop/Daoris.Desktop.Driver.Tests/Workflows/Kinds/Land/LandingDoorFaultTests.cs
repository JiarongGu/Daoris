using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A landing whose delivery store failed is said and tried again at a later pass, as every other failure the landing's door names is.</summary>
public sealed class LandingDoorFaultTests
{
    [Fact]
    public async Task A_delivery_store_failure_is_said_and_left_for_a_later_pass()
    {
        var said = new List<string>();
        var door = new LandingDoor(() => throw new SqliteException("database is locked", 5), said.Add);
        var start = new LandStart("unit", "run", "land", "branch", ["s1"], "engine", "aurora", "tip", "graph", null) { Authority = StepAuthority.OfDoor("run", "land", 1) };

        Assert.True(door.Start(start));
        await door.IdleAsync();

        Assert.Contains(said, line => line.Contains("could not be tried") && line.Contains("database is locked"));
        Assert.True(door.Start(start));
        await door.IdleAsync();
    }
}
