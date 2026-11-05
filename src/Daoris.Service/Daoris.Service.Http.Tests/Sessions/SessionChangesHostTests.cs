using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The widest list as its change since a mark: every id in the list's order, and only the records whose answer moved, each exactly
/// as the list answers it, so a reader that lays the change over what it holds holds the list.
/// </summary>
public sealed class SessionChangesHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task A_change_since_a_mark_is_the_records_that_moved_and_the_list_s_order()
    {
        var now = DateTimeOffset.UtcNow;
        var ended = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now, kind: SessionKind.Chat);
        await host.Composed.Sessions.SetStateAsync(ended.Id, SessionState.Completed, "done", null, null, now);

        var whole = (await host.GetAsync("/api/sessions/changes")).Json;
        Assert.True(whole.GetProperty("whole").GetBoolean());
        await AssertListAsync(whole, whole.GetProperty("changed"));

        var again = (await host.GetAsync($"/api/sessions/changes?since={Uri.EscapeDataString(Mark(whole))}")).Json;
        Assert.False(again.GetProperty("whole").GetBoolean());
        Assert.Empty(again.GetProperty("changed").EnumerateArray());
        Assert.Equal(Mark(whole), Mark(again));

        var opened = await host.Composed.Sessions.CreateAsync(null, "Keeper", "stub", now.AddSeconds(1), kind: SessionKind.Chat);
        var moved = (await host.GetAsync($"/api/sessions/changes?since={Uri.EscapeDataString(Mark(again))}")).Json;
        Assert.False(moved.GetProperty("whole").GetBoolean());
        Assert.Equal([opened.Id], moved.GetProperty("changed").EnumerateArray().Select(row => row.GetProperty("id").GetString()));
        await AssertListAsync(moved, moved.GetProperty("changed"));

        var unknown = (await host.GetAsync("/api/sessions/changes?since=elsewhere.1")).Json;
        Assert.True(unknown.GetProperty("whole").GetBoolean());
        await AssertListAsync(unknown, unknown.GetProperty("changed"));
    }

    private static string Mark(JsonElement answer) => answer.GetProperty("mark").GetString()!;

    // The order is the list's, and each changed record is the list's own answer of it.
    private async Task AssertListAsync(JsonElement answer, JsonElement changed)
    {
        var listed = (await host.GetAsync("/api/sessions?includeClosed=true")).Json.EnumerateArray().ToList();
        Assert.Equal(listed.Select(row => row.GetProperty("id").GetString()), answer.GetProperty("order").EnumerateArray().Select(id => id.GetString()));
        var byId = listed.ToDictionary(row => row.GetProperty("id").GetString()!, row => row.GetRawText());
        foreach (var row in changed.EnumerateArray()) Assert.Equal(byId[row.GetProperty("id").GetString()!], row.GetRawText());
    }
}
