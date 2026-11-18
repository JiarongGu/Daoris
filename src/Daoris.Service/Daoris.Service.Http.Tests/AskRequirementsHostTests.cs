namespace Daoris.Service.Http.Tests;

/// <summary>An ask's answer carries the requirements it keeps, in the words they were given; one with none answers an empty list.</summary>
public sealed class AskRequirementsHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    [Fact]
    public async Task An_ask_answers_the_requirements_it_keeps_and_an_ask_with_none_answers_an_empty_list()
    {
        var kept = await host.PostAsync("/api/asks", new
        {
            workspace = "default",
            sentence = "Fix the stall on load.",
            requirements = new[] { new { quote = "the stall on load", check = "a cold start reaches the menu" } },
        });
        var id = kept.Json.GetProperty("ask").GetProperty("id").GetString()!;

        var read = (await host.GetAsync($"/api/asks/{id}")).Json;
        var requirement = Assert.Single(read.GetProperty("requirements").EnumerateArray());
        Assert.Equal("the stall on load", requirement.GetProperty("quote").GetString());
        Assert.Equal("a cold start reaches the menu", requirement.GetProperty("check").GetString());

        var bare = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Tidy the docs, no requirement." });
        var bareId = bare.Json.GetProperty("ask").GetProperty("id").GetString()!;
        Assert.Equal(0, (await host.GetAsync($"/api/asks/{bareId}")).Json.GetProperty("requirements").GetArrayLength());
    }
}
