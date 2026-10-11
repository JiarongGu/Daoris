using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>NEW1a: the route's own read of <c>ownTree</c> is three-way, absent being the driver's choice and false the checkout.</summary>
public sealed class DriverModuleTreeChoiceTests
{
    [Theory]
    [InlineData("""{ "repository": "engine" }""", null)]
    [InlineData("""{ "ownTree": true }""", true)]
    [InlineData("""{ "ownTree": false }""", false)]
    [InlineData("""{ "ownTree": null }""", null)]
    [InlineData("""{ "ownTree": "yes" }""", null)]
    [InlineData("""{ "ownTree": 1 }""", null)]
    public void The_tree_choice_is_absent_true_or_false(string payload, bool? expected)
    {
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(expected, DriverModule.TreeChoice(Request(document.RootElement)));
    }

    [Fact]
    public void A_request_with_no_payload_leaves_it_to_the_driver()
    {
        Assert.Null(DriverModule.TreeChoice(Request(null)));
    }

    private static IpcRequest Request(JsonElement? payload) => new() { Id = "t1", Module = "DAORIS.DRIVER", Type = "START_CHAT", Payload = payload };
}
