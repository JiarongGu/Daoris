using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// A local host handed its person key, as <see cref="KeyedHost"/>, on a clock that stands still (TESTCLOCK3): a change stream's
/// wait never ends by itself, so an answer came at once or was woken.
/// </summary>
public sealed class HeldChangesHost() : DaorisHost(ServiceMode.Local, seed: LocalHost.Seed, input: KeyedHost.Key + "\n", clock: new MovableClock());

/// <summary>
/// The change stream's door (FABRIC-EVENT3b, the fabric events design §A4.2): a read every caller may make on a local host, the key's
/// host included, answering families and a cursor, woken by a write through the host's own doors; and no door at all on a shared
/// host, which serves no machine's waits. The host's clock stands still, so "at once" and "woken" are each an answer at all.
/// </summary>
public sealed class ChangeStreamHostTests(HeldChangesHost host) : IClassFixture<HeldChangesHost>
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
        var (since, moved) = Read(await host.GetAsync(ChangeStream.Route + "?wait=10").WaitAsync(Poll.Patience));

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

        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "Wake the driver", body = "why" });
        Assert.Equal(200, published.Status);
        var (next, moved) = Read(await waiting.WaitAsync(Poll.Patience));

        Assert.Equal([ChangeFamilies.Quests], moved);
        Assert.NotEqual(since, next);
    }

    /// <summary>A stale cursor, from another start of the host, is answered every family at once, never refused.</summary>
    [Fact]
    public async Task A_stale_cursor_is_answered_every_family_at_once()
    {
        var (_, moved) = Read(await host.GetAsync(ChangeStream.Route + "?since=0123abcd.4&wait=10").WaitAsync(Poll.Patience));

        Assert.Equal(Every, moved);
    }

    /// <summary>
    /// FOUNDATION1 row k part 2 (§4.1): the family <c>journal</c> and the journal's place are answered to a reader that asks with
    /// <c>journal=1</c> and to none else, so a read as every client from before it makes names no more than it always did and carries no new
    /// member.
    /// </summary>
    [Fact]
    public async Task The_journal_is_named_and_its_place_answered_only_to_a_reader_that_asks()
    {
        var older = await host.GetAsync(ChangeStream.Route + "?wait=10").WaitAsync(Poll.Patience);
        var asked = await host.GetAsync(ChangeStream.Route + "?wait=10&journal=1").WaitAsync(Poll.Patience);

        Assert.Equal(Every, Read(older).Moved);
        Assert.Equal([.. Every, ChangeFamilies.Journal], Read(asked).Moved);
        Assert.Equal(["since", "moved"], older.Json.EnumerateObject().Select(member => member.Name).ToArray());
        var place = asked.Json.GetProperty("journal");
        Assert.Matches("^[0-9a-f]{32}$", place.GetProperty("store").GetString()!);
        Assert.True(place.GetProperty("floor").GetInt64() >= 1);
        Assert.True(place.GetProperty("newest").GetInt64() >= 0);
    }

    /// <summary>
    /// An older client's reader, which names no <c>journal</c>, is woken by nothing a journal-only append does (a word taken moves no family),
    /// while a reader that asks is woken by it, naming the journal alone (§4.1, F10). The older reader is shown alive by the write after, which
    /// wakes it with its family alone.
    /// </summary>
    [Fact]
    public async Task A_journal_only_append_wakes_the_reader_that_asks_and_an_older_clients_reader_by_nothing()
    {
        var sessions = host.Composed.Sessions;
        var held = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        await sessions.KeepSaidAsync(held.Id, new SaidWord("w1", "One.", DateTimeOffset.UtcNow, []));
        var (since, _) = Read(await host.GetAsync(ChangeStream.Route).WaitAsync(Poll.Patience));
        var older = host.GetAsync($"{ChangeStream.Route}?since={since}&wait=30");
        var asking = host.GetAsync($"{ChangeStream.Route}?since={since}&wait=30&journal=1");
        await Task.Delay(200);
        Assert.False(older.IsCompleted || asking.IsCompleted, "a current cursor was answered before anything moved");

        await sessions.TakeSaidAsync(held.Id, ["w1"]);
        var (_, named) = Read(await asking.WaitAsync(Poll.Patience));
        await Task.Delay(300);

        Assert.Equal([ChangeFamilies.Journal], named);
        Assert.False(older.IsCompleted, "the older client's reader was woken by a journal-only append");

        await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        Assert.Equal([ChangeFamilies.Sessions], Read(await older.WaitAsync(Poll.Patience)).Moved);
    }

    /// <summary>A shared host maps no change stream: its circles keep their own cursors, and it holds no machine's waits.</summary>
    [Fact]
    public void A_shared_host_maps_no_change_stream()
    {
        using var shared = new SharedHost();

        Assert.DoesNotContain(shared.Routes(), route => route.Pattern == ChangeStream.Route);
    }
}
