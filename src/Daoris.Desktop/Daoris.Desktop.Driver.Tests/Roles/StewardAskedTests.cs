using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The steward's verb says its act waits for the owner's Confirm once the instance tells it so, and how it ended.</summary>
public sealed class StewardAskedTests
{
    private static JsonElement Told(string type, string id) => JsonSerializer.SerializeToElement(new
    {
        category = "notification",
        payload = new[] { new { module = InstanceClient.PipeModule, type, payload = new { id } } },
    });

    [Fact]
    public void The_verb_says_its_act_waits_for_the_owner_once_and_only_for_its_own_request()
    {
        var output = new StringWriter();
        var asked = new StewardAsked("r1", output);

        asked.Told(Told(InstanceDoor.Confirming, "r2"));
        asked.Told(Told("STANDING", "r1"));
        Assert.False(asked.Asked);
        Assert.Equal("", output.ToString());

        asked.Told(Told(InstanceDoor.Confirming, "r1"));
        asked.Told(Told(InstanceDoor.Confirming, "r1"));
        Assert.True(asked.Asked);
        var line = Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("waits for the owner's Confirm in Daoris's window (request r1)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void An_asked_act_ends_as_the_owner_s_or_as_nothing_done()
    {
        var asked = new StewardAsked("r1", new StringWriter());

        Assert.Contains("recorded as the owner's", asked.Ended("hold engine", done: true), StringComparison.Ordinal);
        Assert.Contains("nothing was done", asked.Ended("hold engine", done: false), StringComparison.Ordinal);
    }

    [Fact]
    public void An_asked_act_its_route_refuses_before_any_card_says_the_route_s_words()
    {
        var asked = new StewardAsked("r1", new StringWriter());
        var error = JsonSerializer.SerializeToElement(new { code = "not-allowed", message = "no agent here is called codex." });

        var line = asked.Ended("handover s1 --agent codex", done: false, error);

        Assert.Contains("no agent here is called codex.", line, StringComparison.Ordinal);
        Assert.Contains("before the owner was asked", line, StringComparison.Ordinal);
        Assert.DoesNotContain("did not confirm", line, StringComparison.Ordinal);
        Assert.Contains("did not confirm", asked.Ended("handover s1 --agent codex", done: false,
            JsonSerializer.SerializeToElement(new { code = "refused", message = "The person refused this." })), StringComparison.Ordinal);
    }
}
