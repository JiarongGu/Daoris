using System.Net;
using System.Text.Json;
using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's words an ask carries, as they leave the driver: <c>ask --to … --require "…" "…" --through "…"</c> reaches the ask's
/// door as <c>requirements</c> of <c>{ quote, check }</c> and <c>through</c>, in the names the service binds, and the running
/// Daoris's press carries them in the same names. A service stood in: the fast half.
/// </summary>
public sealed class AskRequireWireTests
{
    private static readonly QuestRequirementWire Required = new("merge all changes to remote too", "it reaches its pull request");

    [Fact]
    public async Task A_new_ask_posts_its_requirements_and_through_words_in_the_doors_names()
    {
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) is ("POST", "/api/asks")
            ? (HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"quest":{"id":"q1"},"message":"Asked."}""")
            : null);

        var carried = new AskCarried([Required], "please do without my accept");
        Assert.True((await service.AskAsync("default", "ship it", [], [], "reports", carried: carried)).Ok);
        await service.AskAsync("default", "ship it", [], [], "reports");

        using var sent = JsonDocument.Parse(heard[0].Body);
        var requirement = Assert.Single(sent.RootElement.GetProperty("requirements").EnumerateArray());
        Assert.Equal(Required.Quote, requirement.GetProperty("quote").GetString());
        Assert.Equal(Required.Check, requirement.GetProperty("check").GetString());
        Assert.Equal("please do without my accept", sent.RootElement.GetProperty("through").GetString());
        using var bare = JsonDocument.Parse(heard[1].Body);
        Assert.False(bare.RootElement.TryGetProperty("requirements", out var none) && none.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public void The_running_daorises_press_carries_them_in_the_same_names()
    {
        var payload = AskNewCommand.Payload(new AskNew("default", "ship it", "reports", Requirements: [Required], Through: " please "));
        var requirement = Assert.Single((List<Dictionary<string, string?>>)payload["requirements"]!);
        Assert.Equal((Required.Quote, Required.Check), (requirement["quote"], requirement["check"]));
        Assert.Equal("please", payload["through"]);
    }
}
