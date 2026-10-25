using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// FOUNDATION1 row p (D212; the foundation design §19.2, §19.12) over the wire: <c>GET /api/protocol</c> answers the host's run and its
/// <c>settled</c> mark, and a keyed say or hand that sends <c>settled: true</c> is answered, after the take, with a success naming the record
/// the word left and no <c>said</c>, while a request without it, an older client's, is answered by today's door.
/// </summary>
public sealed class SaidSettledHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<Session> EndedAsync(string repository)
    {
        var sessions = host.Composed.Sessions;
        var made = await sessions.CreateAsync(null, repository, "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        return (await sessions.SetStateAsync(made.Id, SessionState.Completed, null, null, null, DateTimeOffset.UtcNow))!;
    }

    /// <summary>The host's run is drawn at its start and stays through its life, and its marks name the settlement at version 1.</summary>
    [Fact]
    public async Task The_protocol_answers_the_hosts_run_and_its_settled_mark()
    {
        var first = await host.GetAsync("/api/protocol");
        var second = await host.GetAsync("/api/protocol");

        Assert.Equal(200, first.Status);
        var run = first.Json.GetProperty("run").GetString();
        Assert.Matches("^[0-9a-f]{32}$", run!);
        Assert.Equal(run, second.Json.GetProperty("run").GetString());
        Assert.Equal(1, first.Json.GetProperty("marks").GetProperty("settled").GetInt32());
        // Only the marks this host keeps: none of the later rows' (the open, the head, the journal).
        Assert.Equal(["settled"], first.Json.GetProperty("marks").EnumerateObject().Select(mark => mark.Name).ToArray());
    }

    /// <summary>
    /// A keyed say after the take, asking <c>settled</c>, is answered <c>200</c> with <c>settled</c> naming the record and no <c>said</c> and no
    /// session; a say without it keeps a second word under a new id, as today.
    /// </summary>
    [Fact]
    public async Task A_keyed_say_after_the_take_asks_for_settled_and_an_older_client_is_answered_by_todays_door()
    {
        var record = await EndedAsync("Settle");
        var first = await host.PostAsync($"/api/sessions/{record.Id}/say", new { text = "Press it.", key = "wire/1", from = "daoris", settled = true });
        Assert.Equal(200, first.Status);
        var word = first.Json.GetProperty("said").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{record.Id}/taken", new { said = new[] { word } })).Status);

        var retry = await host.PostAsync($"/api/sessions/{record.Id}/say", new { text = "Press it.", key = "wire/1", from = "daoris", settled = true });

        Assert.Equal(200, retry.Status);
        Assert.Equal(record.Id, retry.Json.GetProperty("settled").GetString());
        Assert.False(retry.Json.TryGetProperty("said", out _));
        Assert.False(retry.Json.TryGetProperty("session", out _));
        Assert.Contains(record.Id, retry.Json.GetProperty("message").GetString());
        Assert.Empty((await host.Composed.Sessions.FindAsync(record.Id))!.Said);

        var older = await host.PostAsync($"/api/sessions/{record.Id}/say", new { text = "Press it.", key = "wire/1", from = "daoris" });
        Assert.Equal(200, older.Status);
        Assert.False(older.Json.TryGetProperty("settled", out _));
        Assert.NotEqual(word, older.Json.GetProperty("said").GetProperty("id").GetString());
    }

    /// <summary>A helper's hand and a partner's, retried after the take, are answered <c>settled</c> to a request that asks, with no word or record.</summary>
    [Fact]
    public async Task A_helpers_and_a_partners_hand_after_the_take_are_answered_settled_to_a_request_that_asks()
    {
        var sessions = host.Composed.Sessions;
        var asker = await EndedAsync("Hands");
        var helper = await sessions.CreateAsync(null, "Hands", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: asker.Id);
        var handed = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", settled = true });
        Assert.Equal(200, handed.Status);
        await sessions.TakeSaidAsync(asker.Id, [handed.Json.GetProperty("said").GetProperty("id").GetString()!]);

        var retry = await host.PostAsync($"/api/helpers/{helper.Id}/hand", new { text = "Helper says five.", settled = true });
        Assert.Equal(200, retry.Status);
        Assert.Equal(asker.Id, retry.Json.GetProperty("settled").GetString());
        Assert.False(retry.Json.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.Object);
        Assert.Empty((await sessions.FindAsync(asker.Id))!.Said);

        var lead = await EndedAsync("Hands");
        var partner = await sessions.CreateAsync(null, "Hands", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id, piece: "piece9");
        var piece = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "The piece.", settled = true });
        Assert.Equal(200, piece.Status);
        await sessions.TakeSaidAsync(lead.Id, [piece.Json.GetProperty("said").GetProperty("id").GetString()!]);

        var again = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "The piece.", settled = true });
        Assert.Equal(lead.Id, again.Json.GetProperty("settled").GetString());
        Assert.Empty((await sessions.FindAsync(lead.Id))!.Said);

        var older = await host.PostAsync($"/api/partners/{partner.Id}/hand", new { text = "The piece." });
        Assert.False(older.Json.TryGetProperty("settled", out var settled) && settled.ValueKind == JsonValueKind.String);
        Assert.Equal(JsonValueKind.Object, older.Json.GetProperty("said").ValueKind);
    }
}
