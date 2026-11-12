using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// An ask's close and delete and a quest's delete are any caller's (D284 point 2): a keyless call, as Ask Daoris's connector or a
/// terminal makes it, is answered and the reason it gave is kept; the key only attributes.
/// </summary>
public sealed class OpenEndDoorTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    private async Task<(string Ask, string Quest)> AskedAsync(string sentence)
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence, to = "Keeper" }, person: KeyedHost.Key);
        Assert.Equal(200, asked.Status);
        return (asked.Json.GetProperty("ask").GetProperty("id").GetString()!, asked.Json.GetProperty("quest").GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task A_keyless_call_closes_an_ask_and_the_reason_is_kept()
    {
        var (ask, _) = await AskedAsync("Close this one for the open-door test.");

        var closed = await host.PostAsync($"/api/asks/{ask}/close", new { reason = "Done at the terminal." });

        Assert.Equal(200, closed.Status);
        Assert.Contains("Done at the terminal.", closed.Body);
    }

    [Fact]
    public async Task A_keyless_call_deletes_an_ask_and_a_quest()
    {
        var (ask, _) = await AskedAsync("Delete this ask for the open-door test.");
        Assert.Equal(200, (await host.DeleteAsync($"/api/asks/{ask}")).Status);

        var (_, quest) = await AskedAsync("Delete this ask's quest for the open-door test.");
        Assert.Equal(200, (await host.DeleteAsync($"/api/quests/{quest}")).Status);
    }

    [Fact]
    public void These_doors_are_agent_doors_which_keep_their_marks()
    {
        foreach (var (method, pattern) in new[]
        {
            ("POST", "/api/asks/{id}/close"), ("DELETE", "/api/asks/{id}"), ("DELETE", "/api/quests/{id}"), ("DELETE", "/api/sessions/{id}"),
            ("POST", "/api/quests/{id}/conflicts/dismiss"), ("POST", "/api/quests/{id}/conflicts/forward"),
        })
        {
            var door = PersonDoors.Find(method, pattern)!;
            Assert.Equal(DoorClass.Agent, door.Class);
            Assert.NotNull(door.Marks);
        }
    }
}
