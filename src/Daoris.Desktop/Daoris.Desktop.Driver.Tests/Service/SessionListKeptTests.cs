using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The list kept by its change: what moved laid over what is held, in the order the service names, a record gone dropped, and an
/// order naming a record not held read again whole.
/// </summary>
public sealed class SessionListKeptTests
{
    private sealed class Host(params string[] answers) : IServiceDoors
    {
        private int _at;
        public List<string> Asked { get; } = [];
        public string BaseUrl => "http://stand-in";
        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add(path);
            return Task.FromResult(new ServiceReply(200, answers[_at++]));
        }
        public void Dispose() { }
    }

    private static string Change(string mark, bool whole, string[] order, params string[] changed) =>
        new JsonObject
        {
            ["mark"] = mark, ["whole"] = whole, ["order"] = new JsonArray([.. order.Select(id => (JsonNode)id)]),
            ["changed"] = new JsonArray([.. changed.Select(id => (JsonNode)new JsonObject { ["id"] = id, ["state"] = $"{id}@{mark}" })]),
        }.ToJsonString();

    private static string[] Ids(IReadOnlyList<JsonObject> list) => [.. list.Select(record => record["id"]!.GetValue<string>())];

    [Fact]
    public async Task A_change_is_laid_over_the_held_list_and_a_gap_is_read_whole()
    {
        using var host = new Host(
            Change("m1", true, ["a", "b"], "a", "b"),
            Change("m2", false, ["c", "a"], "c"),
            Change("m3", false, ["d", "c"]),
            Change("m4", true, ["d", "c"], "d", "c"),
            Change("m4", false, ["d", "c"]));
        using var client = new ServiceClient(host);
        var kept = client.SessionList.Kept;

        var first = await kept.ReadAsync();
        Assert.Equal(["a", "b"], Ids(first));
        var second = await kept.ReadAsync();
        Assert.Equal(["c", "a"], Ids(second));
        Assert.Same(first[0], second[1]);
        Assert.Equal(["a", "b"], Ids(first));
        var third = await kept.ReadAsync();
        Assert.Equal(["d", "c"], Ids(third));
        Assert.Equal("d@m4", third[0]["state"]!.GetValue<string>());
        Assert.Equal(
            ["/api/sessions/changes", "/api/sessions/changes?since=m1", "/api/sessions/changes?since=m2", "/api/sessions/changes"],
            host.Asked);
        Assert.Equal("""[{"id":"d","state":"d@m4"},{"id":"c","state":"c@m4"}]""", await kept.JsonAsync());
    }
}
