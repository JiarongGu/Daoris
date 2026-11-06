using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row h: the commit trailer is told from the maker the registration states for a door (a plugin's own, any other's its
/// provider's), never from the old seam's toolchain, and is the table it was for every built-in door and for a plugin's door that
/// declares <c>OpenAI</c>, <c>Anthropic</c> or none.
/// </summary>
public sealed class CommitTrailersRegistryTests
{
    private static ProviderRegistry With(params DeclaredDoor[] doors) => ProviderRegistry.Built().WithPlugins(new Declared(doors));

    private static DeclaredDoor Door(string name, string? maker) =>
        new(name, "acme", ["acme-agent", "--acp"], Posture: "edits", ProfileVariable: null, AccountOf: null, Product: "Acme", Maker: maker);

    [Theory]
    [InlineData("claude-code", "Anthropic")]
    [InlineData("claude-code-acp", "Anthropic")]
    [InlineData("codex-acp", "OpenAI")]
    [InlineData("dsh", "DeepSeek")]
    [InlineData("stub", null)]
    [InlineData("acp-stub", null)]
    [InlineData("alt-stub", null)]
    [InlineData("alt-acp-stub", null)]
    public void A_built_in_door_s_maker_is_its_provider_s_and_the_same_one_its_toolchain_states(string door, string? maker)
    {
        var adapter = ProviderRegistry.Built().Resolve(door);

        Assert.Equal(maker, CommitTrailers.MakerOf(adapter));
        Assert.Equal(adapter.Toolchain?.Maker, CommitTrailers.MakerOf(adapter));
    }

    [Theory]
    [InlineData("claude-code", "made by Anthropic. Use the model actually running this session")]
    [InlineData("codex-acp", "Co-Authored-By: Codex (OpenAI, MODEL) <noreply@openai.com>")]
    [InlineData("dsh", "Co-Authored-By: DeepSeek (MODEL) <noreply@deepseek.com>")]
    [InlineData("stub", "Co-Authored-By: AGENT (MAKER, MODEL) <your maker's attribution address>")]
    [InlineData("alt-stub", "Co-Authored-By: AGENT (MAKER, MODEL) <your maker's attribution address>")]
    public void Each_built_in_door_is_told_the_trailer_its_maker_gets(string door, string expected) =>
        Assert.Contains(expected, CommitTrailers.For(ProviderRegistry.Built().Resolve(door)));

    [Fact]
    public void A_plugin_door_declaring_a_maker_is_told_that_maker_s_trailer_and_one_declaring_none_the_placeholder()
    {
        var adapters = With(Door("acme-openai", "OpenAI"), Door("acme-anthropic", "Anthropic"), Door("acme-none", null));

        Assert.Contains("Co-Authored-By: Codex (OpenAI, MODEL) <noreply@openai.com>", CommitTrailers.For(adapters.Resolve("acme-openai")));
        Assert.Contains("made by Anthropic. Use the model actually running this session", CommitTrailers.For(adapters.Resolve("acme-anthropic")));
        Assert.Contains("<your maker's attribution address>", CommitTrailers.For(adapters.Resolve("acme-none")));
        Assert.Equal("OpenAI", CommitTrailers.MakerOf(adapters.Resolve("acme-openai")));
        Assert.Null(CommitTrailers.MakerOf(adapters.Resolve("acme-none")));
    }

    private sealed class Declared(IEnumerable<DeclaredDoor> doors) : IAgentPlugins
    {
        public IEnumerable<DeclaredDoor> Doors { get; } = doors;

        public IEnumerable<string> DeclaredAgents { get; } = doors.Select(door => door.Name);

        public IReadOnlyList<SkillSource> Skills(string to) => [];
    }
}
