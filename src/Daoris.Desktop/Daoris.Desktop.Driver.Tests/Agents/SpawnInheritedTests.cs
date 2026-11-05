using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a spawn inherits of the driver's own environment (the agent-provider design §4.6, §7 row 9): a conversation clears an inherited
/// target and sets none (C3-1.1, row h), and each built-in door's spawn drops every other provider's registered credential and home while
/// keeping its own (s-vi). It sets the process's environment, so it runs alone, after the rest.
/// </summary>
[Collection(Alone.Name)]
public sealed class SpawnInheritedTests
{
    private static SessionTarget Target() => new(
        QuestId: "abc123", Title: "Fix the flaky gate", Body: "It fails one run in five.", Asker: "Platform",
        Repository: "Game", Root: "D:/fam/Game", ServiceUrl: "http://localhost:5177");

    private static SpawnRequest Chat() => SpawnRequests.Chat("Game", "D:/fam/Game", "http://localhost:5177", null);

    private static readonly string[] Planted =
        ["CLAUDE_CONFIG_DIR", "ANTHROPIC_API_KEY", "DISABLE_UPDATES", "CODEX_HOME", "DSH_HOME", StubAdapter.ConfigVariable];

    [Theory]
    [InlineData("claude-code", null)]
    [InlineData("claude-code-acp", null)]
    [InlineData("codex-acp", null)]
    [InlineData("stub", "node agent.mjs")]
    [InlineData("acp-stub", "node acp-agent.mjs")]
    public void A_conversation_clears_an_inherited_target_and_sets_none(string door, string? command) =>
        With(["DAORIS_TARGET", "DAORIS_QUEST_ID"], () =>
        {
            var info = AdapterSet.Built().Resolve(door).PrepareChat(Chat(), command?.Split(' '));
            Assert.False(info.Environment.ContainsKey("DAORIS_TARGET"));
            Assert.False(info.Environment.ContainsKey("DAORIS_QUEST_ID"));
        });

    /// <summary>Door, configured command, the planted variables it keeps as its provider's own; every other planted one is stripped.</summary>
    [Theory]
    [InlineData("claude-code", null, "CLAUDE_CONFIG_DIR ANTHROPIC_API_KEY DISABLE_UPDATES")]
    [InlineData("claude-code-acp", null, "CLAUDE_CONFIG_DIR ANTHROPIC_API_KEY DISABLE_UPDATES")]
    [InlineData("codex-acp", null, "CODEX_HOME")]
    [InlineData("dsh", null, "DSH_HOME")]
    [InlineData("stub", "node agent.mjs", "DAORIS_STUB_CONFIG_DIR")]
    [InlineData("acp-stub", "node acp-agent.mjs", "DAORIS_STUB_CONFIG_DIR")]
    public void A_door_s_spawn_keeps_its_own_provider_s_variables_and_drops_every_other_s(string door, string? command, string kept)
    {
        var own = kept.Split(' ');
        With(Planted, () =>
        {
            var info = AdapterSet.Built().Resolve(door).Prepare(Target(), command?.Split(' '));
            foreach (var name in Planted)
            {
                if (own.Contains(name)) Assert.Equal(Sentinel(name), info.Environment[name]);
                else Assert.False(info.Environment.ContainsKey(name), $"{door}'s spawn inherits {name}, another provider's");
            }
        });
    }

    /// <summary>A plugin's door, whose program is unmeasured, is stripped of nothing (O4-F3).</summary>
    [Fact]
    public void An_unmeasured_door_is_stripped_of_nothing() =>
        With(Planted, () =>
        {
            var info = Shell.Start(new ProcessRecipe("plugin-agent", []) { Input = true }, SpawnRequests.Of(Target(), null));
            Assert.All(Planted, name => Assert.Equal(Sentinel(name), info.Environment[name]));
        });

    private static string Sentinel(string name) => "inherited-" + name.ToLowerInvariant();

    private static void With(IReadOnlyList<string> names, Action body)
    {
        var was = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, Sentinel(name));
            body();
        }
        finally
        {
            foreach (var (name, value) in was) Environment.SetEnvironmentVariable(name, value);
        }
    }
}
