using System.Diagnostics;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The change stream's door (FABRIC-EVENT3b, the fabric events design §A4.2): a read every caller may make on a local host, the key's
/// host included, answering families and a cursor, woken by a write through the host's own doors; and no door at all on a shared
/// host, which serves no machine's waits.
/// </summary>
public sealed class ChangeStreamHostTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    private static readonly string[] Every = [ChangeFamilies.Sessions, ChangeFamilies.Quests, ChangeFamilies.Asks, ChangeFamilies.Channel, ChangeFamilies.Words];

    private static (string Since, string[] Moved) Read(Answer answer)
    {
        Assert.Equal(200, answer.Status);
        return (answer.Json.GetProperty("since").GetString()!,
            [.. answer.Json.GetProperty("moved").EnumerateArray().Select(family => family.GetString()!)]);
    }

    [Fact]
    public void The_door_is_a_read_and_a_local_host_s_alone()
    {
        Assert.Equal(DoorClass.Open, PersonDoors.Find("GET", ChangeStream.Route)!.Class);
        Assert.Contains(("GET", ChangeStream.Route), host.Routes());
    }

    /// <summary>A keyless bootstrap on a host that holds the person key: answered at once, every family, with the cursor.</summary>
    [Fact]
    public async Task A_bootstrap_is_answered_to_any_caller_with_every_family()
    {
        var (since, moved) = Read(await host.GetAsync(ChangeStream.Route + "?wait=10"));

        Assert.Equal(Every, moved);
        Assert.Matches(@"^[0-9a-f]+\.\d+$", since);
    }

    /// <summary>An agent's publish through the host's own door wakes a held wait, which names the quests family alone.</summary>
    [Fact]
    public async Task An_agent_s_publish_wakes_a_held_wait()
    {
        var (since, _) = Read(await host.GetAsync(ChangeStream.Route));
        var waiting = host.GetAsync($"{ChangeStream.Route}?since={since}&wait=30");
        await Task.Delay(200);
        Assert.False(waiting.IsCompleted, "a current cursor was answered before anything moved");

        var clock = Stopwatch.StartNew();
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "Wake the driver", body = "why" });
        Assert.Equal(200, published.Status);
        var (next, moved) = Read(await waiting.WaitAsync(Poll.Patience));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"{clock.Elapsed}");
        Assert.Equal([ChangeFamilies.Quests], moved);
        Assert.NotEqual(since, next);
    }

    /// <summary>A stale cursor, from another start of the host, is answered every family at once, never refused.</summary>
    [Fact]
    public async Task A_stale_cursor_is_answered_every_family_at_once()
    {
        var clock = Stopwatch.StartNew();
        var (_, moved) = Read(await host.GetAsync(ChangeStream.Route + "?since=0123abcd.4&wait=10"));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"{clock.Elapsed}");
        Assert.Equal(Every, moved);
    }

    /// <summary>A shared host maps no change stream: its circles keep their own cursors, and it holds no machine's waits.</summary>
    [Fact]
    public void A_shared_host_maps_no_change_stream()
    {
        using var shared = new SharedHost();

        Assert.DoesNotContain(shared.Routes(), route => route.Pattern == ChangeStream.Route);
    }
}
