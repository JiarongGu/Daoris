using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The censuses' ids and words are read off the registrations, every word declared by the provider that owns it, and a stand-in owning none.
/// </summary>
public sealed class ProviderVocabularyTests
{
    [Fact]
    public void Every_id_an_agent_or_a_door_goes_by_is_its_registrations_with_the_provider_that_owns_it()
    {
        Assert.Equal(
            [
                ("claude-code", "claude-code"), ("claude", "claude-code"), ("claude-code-acp", "claude-code"),
                ("codex", "codex"), ("codex-acp", "codex"),
                ("dsh", "dsh"),
                ("stub", "stub"), ("acp-stub", "stub"),
                ("alt-stub", "alt-stub"), ("alt-acp-stub", "alt-stub"),
            ],
            ProviderVocabulary.Ids.Select(row => (row.Id, row.Owner)));
    }

    [Fact]
    public void Every_word_a_provider_names_is_in_its_vocabulary_and_a_stand_in_owns_none()
    {
        var words = ProviderVocabulary.Words.ToDictionary(row => row.Word, row => (row.Owner, row.Kind), StringComparer.Ordinal);

        foreach (var variable in (string[])["CLAUDE_CONFIG_DIR", "ANTHROPIC_API_KEY", "ANTHROPIC_MODEL", "CLAUDE_CODE_EFFORT_LEVEL",
            "CLAUDE_CODE_EXECUTABLE", "DISABLE_UPDATES", "ENABLE_TOOL_SEARCH"])
        {
            Assert.Equal(("claude-code", "variable"), words[variable]);
        }

        Assert.Equal(("claude-code", "file"), words[".claude.json"]);
        Assert.Equal(("claude-code", "file"), words[".mcp.json"]);
        Assert.Equal(("claude-code", "wire"), words["_claude/"]);
        Assert.Equal(("claude-code", "wire"), words["claudeCode."]);
        Assert.Equal(("codex", "variable"), words["CODEX_HOME"]);
        Assert.Equal(("codex", "wire"), words["thread_active_writer"]);
        Assert.Equal(("codex", "wire"), words["x-codex-turn-metadata"]);
        Assert.Equal(("dsh", "variable"), words["DSH_HOME"]);
        Assert.Equal(15, words.Count);
        Assert.DoesNotContain(ProviderVocabulary.Words, row => row.Owner is "stub" or "alt-stub");
    }

    /// <summary>A variable a provider's vocabulary names is its program's, so its own spawn keeps it and every other provider's drops it (s-vi).</summary>
    [Fact]
    public void A_variable_in_a_vocabulary_is_declared_by_its_provider_and_by_no_other()
    {
        var claude = Providers.Built().Single(provider => provider.Id.Value == "claude-code");
        var codex = Providers.Built().Single(provider => provider.Id.Value == "codex");

        Assert.Contains("ENABLE_TOOL_SEARCH", RegisteredVariables.Declared(claude, null));
        Assert.Contains("ENABLE_TOOL_SEARCH", RegisteredVariables.Others(codex));
        Assert.DoesNotContain("ENABLE_TOOL_SEARCH", RegisteredVariables.Others(claude));
    }
}
