using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The widest quest list as its change since a mark: every id in the list's order, and only the quests whose answer moved, each exactly
/// as the list answers it, so a reader that lays the change over what it holds holds the list.
/// </summary>
public sealed class QuestChangesHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task A_change_since_a_mark_is_the_quests_that_moved_and_the_list_s_order()
    {
        var now = DateTimeOffset.UtcNow;
        await host.Composed.Quests.PublishAsync("Asker", "Keeper", "The first change", "why", now);

        var whole = (await host.GetAsync("/api/quests/changes")).Json;
        Assert.True(whole.GetProperty("whole").GetBoolean());
        await AssertListAsync(whole, whole.GetProperty("changed"));

        var again = (await host.GetAsync($"/api/quests/changes?since={Uri.EscapeDataString(Mark(whole))}")).Json;
        Assert.False(again.GetProperty("whole").GetBoolean());
        Assert.Empty(again.GetProperty("changed").EnumerateArray());
        Assert.Equal(Mark(whole), Mark(again));

        var published = await host.Composed.Quests.PublishAsync("Asker", "Keeper", "The second change", "why", now.AddSeconds(1));
        var moved = (await host.GetAsync($"/api/quests/changes?since={Uri.EscapeDataString(Mark(again))}")).Json;
        Assert.False(moved.GetProperty("whole").GetBoolean());
        Assert.Equal([published.Id], moved.GetProperty("changed").EnumerateArray().Select(row => row.GetProperty("id").GetString()));
        await AssertListAsync(moved, moved.GetProperty("changed"));

        var unknown = (await host.GetAsync("/api/quests/changes?since=elsewhere.1")).Json;
        Assert.True(unknown.GetProperty("whole").GetBoolean());
        await AssertListAsync(unknown, unknown.GetProperty("changed"));
    }

    [Fact]
    public async Task The_list_asked_again_at_a_mark_that_holds_is_the_answer_kept_and_one_moved_is_made_again()
    {
        await host.Composed.Quests.PublishAsync("Asker", "Keeper", "A kept answer", "why", DateTimeOffset.UtcNow);
        var first = (await host.GetAsync("/api/quests?includeClosed=true")).Json.GetRawText();
        Assert.Equal(first, (await host.GetAsync("/api/quests?includeClosed=true")).Json.GetRawText());

        var published = await host.Composed.Quests.PublishAsync("Asker", "Keeper", "A later one", "why", DateTimeOffset.UtcNow);

        var listed = (await host.GetAsync("/api/quests?includeClosed=true")).Json.EnumerateArray().Select(row => row.GetProperty("id").GetString());
        Assert.Contains(published.Id, listed);
    }

    private static string Mark(JsonElement answer) => answer.GetProperty("mark").GetString()!;

    // The order is the list's, and each changed quest is the list's own answer of it.
    private async Task AssertListAsync(JsonElement answer, JsonElement changed)
    {
        var listed = (await host.GetAsync("/api/quests?includeClosed=true")).Json.EnumerateArray().ToList();
        Assert.Equal(listed.Select(row => row.GetProperty("id").GetString()), answer.GetProperty("order").EnumerateArray().Select(id => id.GetString()));
        var byId = listed.ToDictionary(row => row.GetProperty("id").GetString()!, row => row.GetRawText());
        foreach (var row in changed.EnumerateArray()) Assert.Equal(byId[row.GetProperty("id").GetString()!], row.GetRawText());
    }
}
