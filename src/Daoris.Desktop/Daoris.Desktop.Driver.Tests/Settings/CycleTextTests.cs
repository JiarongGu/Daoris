using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A cycle as <c>driver.json</c> keeps it (FOUNDATION1 mod-iii): the settings' own compact text, which the graph homes' <c>Compact</c>
/// answers through.
/// </summary>
public sealed class CycleTextTests
{
    [Fact]
    public void A_cycle_is_compact_in_the_order_written_and_unescaped_and_the_graph_homes_answer_alike()
    {
        const string spelled = "{ \"b\": 1,\n  \"a\": \"<&>é\" }";

        Assert.Equal("{\"b\":1,\"a\":\"<&>é\"}", CycleText.Compact(spelled));
        Assert.Equal(CycleText.Compact(spelled), NodeGraphHomes.Compact(spelled));
    }

    [Fact]
    public void Text_that_is_not_json_throws_as_it_did()
    {
        Assert.ThrowsAny<JsonException>(() => CycleText.Compact("{not json"));
        Assert.ThrowsAny<JsonException>(() => NodeGraphHomes.Compact("{not json"));
    }
}
