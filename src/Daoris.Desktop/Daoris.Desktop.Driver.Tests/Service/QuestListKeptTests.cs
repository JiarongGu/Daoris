using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The quest list kept by its change: every reader of every quest reads what is held, asking the service only what moved since the
/// mark it last read, and a host before the change door is read whole.
/// </summary>
public sealed class QuestListKeptTests
{
    private sealed class Host(params (int Status, string Body)[] answers) : IServiceDoors
    {
        private int _at;
        public List<string> Asked { get; } = [];
        public string BaseUrl => "http://stand-in";
        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add(path);
            var (status, body) = answers[_at++];
            return Task.FromResult(new ServiceReply(status, body));
        }
        public void Dispose() { }
    }

    private static JsonObject Quest(string id, string title) =>
        new() { ["id"] = id, ["from"] = "Asker", ["to"] = "Keeper", ["title"] = title, ["body"] = "why", ["status"] = "Open" };

    private static (int, string) Change(string mark, bool whole, string[] order, params JsonObject[] changed) => (200,
        new JsonObject
        {
            ["mark"] = mark, ["whole"] = whole, ["order"] = new JsonArray([.. order.Select(id => (JsonNode)id)]),
            ["changed"] = new JsonArray([.. changed]),
        }.ToJsonString());

    [Fact]
    public async Task Each_read_asks_only_what_moved_since_the_mark_it_holds()
    {
        using var host = new Host(
            Change("m1", true, ["q1", "q2"], Quest("q1", "First"), Quest("q2", "Second")),
            Change("m1", false, ["q1", "q2"]),
            Change("m2", false, ["q1", "q2"], Quest("q2", "Second, moved")));
        using var client = new ServiceClient(host);

        Assert.Equal(["First", "Second"], (await client.EveryQuestAsync()).Select(quest => quest.Title));
        Assert.Equal(["First", "Second"], (await client.QuestList.ListedAsync()).Select(quest => quest.Title));
        Assert.Equal("Second, moved", (await client.FindQuestAsync("#Q2"))!.Title);

        Assert.Equal(["/api/quests/changes", "/api/quests/changes?since=m1", "/api/quests/changes?since=m1"], host.Asked);
    }

    [Fact]
    public async Task A_change_that_moved_nothing_keeps_the_typed_list_and_its_text()
    {
        using var host = new Host(
            Change("m1", true, ["q1"], Quest("q1", "First")),
            Change("m1", false, ["q1"]),
            Change("m1", false, ["q1"]),
            Change("m1", false, ["q1"]));
        using var client = new ServiceClient(host);

        var first = await client.QuestList.ListedAsync();
        Assert.Same(first, await client.QuestList.ListedAsync());
        Assert.Same(await client.QuestsJsonAsync(), await client.QuestsJsonAsync());
    }

    [Fact]
    public async Task A_host_from_before_the_change_door_is_read_as_the_whole_list_each_time()
    {
        var list = new JsonArray(Quest("q1", "First")).ToJsonString();
        using var host = new Host((404, "{}"), (200, list), (404, "{}"), (200, list));
        using var client = new ServiceClient(host);

        Assert.Equal("First", Assert.Single(await client.EveryQuestAsync()).Title);
        Assert.Null(await client.FindQuestAsync("q2"));

        Assert.Equal(["/api/quests/changes", "/api/quests?includeClosed=true", "/api/quests/changes", "/api/quests?includeClosed=true"], host.Asked);
    }
}
