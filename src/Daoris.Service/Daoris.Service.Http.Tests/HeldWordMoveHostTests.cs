using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// LINESTOGETHER1b (the review-flow design §2) over the wire: <c>POST /api/sessions/{id}/move</c> moves a held word to the head and answers
/// <c>moved</c>, a retry answers <c>moved</c> again with nothing kept, a word gone is <c>taken</c>, and a running head refuses, naming itself.
/// </summary>
public sealed class HeldWordMoveHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private async Task<Session> RecordAsync(SessionState state = SessionState.Completed)
    {
        var made = await host.Composed.Sessions.CreateAsync(null, "Moves", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        return (await host.Composed.Sessions.SetStateAsync(made.Id, state, null, null, null, DateTimeOffset.UtcNow))!;
    }

    [Fact]
    public async Task The_move_door_answers_moved_once_then_moved_again_taken_and_a_running_heads_refusal()
    {
        var part = await RecordAsync();
        var word = (await host.PostAsync($"/api/sessions/{part.Id}/say", new { text = "Use the blue one." })).Json.GetProperty("said").GetProperty("id").GetString();
        var head = await RecordAsync();
        var working = await RecordAsync(SessionState.Working);

        var refused = await host.PostAsync($"/api/sessions/{part.Id}/move", new { word, to = working.Id });
        var moved = await host.PostAsync($"/api/sessions/{part.Id}/move", new { word, to = head.Id });
        var again = await host.PostAsync($"/api/sessions/{part.Id}/move", new { word, to = head.Id });
        var taken = await host.PostAsync($"/api/sessions/{part.Id}/move", new { word = "gone1234", to = head.Id });

        Assert.Equal((409, "running", working.Id), (refused.Status, refused.Json.GetProperty("refusal").GetString(), refused.Json.GetProperty("head").GetString()));
        Assert.Equal((200, true, head.Id), (moved.Status, moved.Json.GetProperty("moved").GetBoolean(), moved.Json.GetProperty("to").GetString()));
        Assert.Equal($"move:{part.Id}:{word}", moved.Json.GetProperty("said").GetProperty("key").GetString());
        Assert.Equal((200, true, true, head.Id), (again.Status, again.Json.GetProperty("moved").GetBoolean(), again.Json.GetProperty("again").GetBoolean(), again.Json.GetProperty("to").GetString()));
        Assert.False(again.Json.TryGetProperty("said", out _));
        Assert.Equal((200, false, "taken"), (taken.Status, taken.Json.GetProperty("moved").GetBoolean(), taken.Json.GetProperty("why").GetString()));
        Assert.Single((await host.Composed.Sessions.FindAsync(head.Id))!.Said);
    }
}
