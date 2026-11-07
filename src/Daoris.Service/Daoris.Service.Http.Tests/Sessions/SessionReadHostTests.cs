using System.Net;
using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// One record by its id answers exactly what the list answers of it, to a caller on this machine and off it: its entry key, whether
/// the ledger would delete it, and the part its line ends at, so a reader of one session never reads them all.
/// </summary>
public sealed class SessionReadHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task One_record_is_the_list_s_own_answer_of_it_on_and_off_the_machine()
    {
        var now = DateTimeOffset.UtcNow;
        var left = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat, tree: host.RootOf("read-one-left"));
        await host.Composed.Sessions.SetStateAsync(left.Id, SessionState.Completed, "done", null, null, now);
        var next = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", door = "stub", tree = host.RootOf("read-one-next"), from = left.Id });
        Assert.Equal(200, next.Status);
        var nextId = next.Json.GetProperty("session").GetProperty("id").GetString()!;

        foreach (var from in new IPAddress?[] { null, DaorisHost.OffMachine })
        {
            foreach (var id in new[] { left.Id, nextId })
            {
                var one = await host.GetAsync($"/api/sessions/{id}", from);
                Assert.Equal(200, one.Status);
                Assert.Equal(await ListedAsync(id, from), one.Json.GetRawText());
            }
        }

        Assert.Equal(nextId, (await host.GetAsync($"/api/sessions/{left.Id}")).Json.GetProperty("head").GetString());
    }

    [Fact]
    public async Task An_unknown_record_is_not_found()
    {
        var answer = await host.GetAsync("/api/sessions/no-such-session");
        Assert.Equal(404, answer.Status);
    }

    private async Task<string> ListedAsync(string id, IPAddress? from) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id).GetRawText();
}
