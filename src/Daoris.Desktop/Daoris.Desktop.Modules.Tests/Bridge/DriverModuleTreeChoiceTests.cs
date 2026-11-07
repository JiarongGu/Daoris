using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The route's own read of <c>ownTree</c> is three-way, absent being the driver's choice and false the checkout.</summary>
public sealed class DriverModuleTreeChoiceTests
{
    [Theory]
    [InlineData("""{ "repository": "engine" }""", null)]
    [InlineData("""{ "repository": "engine", "ownTree": true }""", true)]
    [InlineData("""{ "repository": "engine", "ownTree": false }""", false)]
    [InlineData("""{ "repository": "engine", "ownTree": null }""", null)]
    [InlineData("""{ "repository": "engine", "ownTree": "yes" }""", null)]
    [InlineData("""{ "repository": "engine", "ownTree": 1 }""", null)]
    public void The_tree_choice_is_absent_true_or_false(string payload, bool? expected)
    {
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(expected, RoutePayload.Reader<StartChatPress>()(Request(document.RootElement)).OwnTree);
    }

    private static IpcRequest Request(JsonElement? payload) => new() { Id = "t1", Module = "DAORIS.DRIVER", Type = "START_CHAT", Payload = payload };
}
