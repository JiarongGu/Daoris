using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// A session's turns are answered on each record of its line, the line's first record leading them, to a caller on this machine only:
/// a turn names its account.
/// </summary>
public sealed class TurnsHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task A_move_s_record_answers_its_line_s_turns_on_this_machine_only()
    {
        var first = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", door = "stub", tree = host.RootOf("turns-first") });
        var id = first.Json.GetProperty("session").GetProperty("id").GetString()!;
        var moved = await host.PostAsync("/api/sessions/chat", new
        {
            repository = "Keeper", door = "codex-acp", tree = host.RootOf("turns-moved"), from = id, fromWhy = "limit", fromBy = "daoris",
        });
        var next = moved.Json.GetProperty("session").GetProperty("id").GetString()!;

        var turns = (await ListedAsync(next))!.Value.GetProperty("turns").EnumerateArray()
            .Select(turn => (turn.GetProperty("number").GetInt32(), turn.GetProperty("cause").GetString(), turn.GetProperty("part").GetString()))
            .ToList();
        Assert.Equal([(1, "start", id), (2, "limit", next)], turns);
        var remote = (await ListedAsync(next, DaorisHost.OffMachine))!.Value;
        Assert.True(!remote.TryGetProperty("turns", out var none) || none.ValueKind == JsonValueKind.Null);
    }

    private async Task<JsonElement?> ListedAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);
}
