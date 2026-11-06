using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row o (the agent-provider design §4.5): a provider takes the product's rules by its own language. Claude Code's two
/// doors are handed exactly the file they were, byte for byte, with the lineage hook Claude Code's language installs for the file Loop
/// hands; a provider that states no language (Codex, dsh, the stand-ins) is handed none, so none of the person's denies is presented
/// as reaching it.
/// </summary>
public sealed class RuleLanguageTests : IDisposable
{
    private readonly string _home = Path.GetFullPath(Path.Combine("_fixtures", "rulelanguage-" + Guid.NewGuid().ToString("N")[..8]));

    public RuleLanguageTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void The_trust_preflight_reads_the_doors_own_home_and_trust_file_whichever_class_presents_it()
    {
        var built = AdapterSet.Built();
        foreach (var door in new[] { "claude-code", "claude-code-acp" })
        {
            var home = built.Resolve(door).Home();
            Assert.Equal("CLAUDE_CONFIG_DIR", home.Variable);
            Assert.Equal(ClaudeTrust.FileName, home.TrustFile);
        }

        Assert.Null(built.Resolve("codex-acp").Home().TrustFile);
        Assert.Equal("CODEX_HOME", built.Resolve("codex-acp").Home().Variable);
        Assert.Null(built.Resolve("dsh").Home().TrustFile);
        Assert.Equal(new DoorHome(null, false, null), TestDoors.Door("stand-agent").Home());
    }

    [Fact]
    public void Each_built_door_takes_its_providers_language_and_a_provider_stating_none_takes_none()
    {
        var built = AdapterSet.Built();
        Assert.Same(ClaudeRules.Language, built.Resolve("claude-code").RuleLanguage());
        Assert.Same(ClaudeRules.Language, built.Resolve("claude-code-acp").RuleLanguage());
        foreach (var door in new[] { "codex-acp", "dsh", "stub", "acp-stub" })
        {
            Assert.Null(built.Resolve(door).RuleLanguage());
            Assert.False(built.Resolve(door).TakesSettings);
        }

        var registry = ProviderRegistry.Of(built);
        Assert.True(registry.Provider("claude-code").Rules.IsKnown);
        Assert.All(["codex", "dsh", "stub"], id => Assert.False(registry.Provider(id).Rules.IsKnown));
        Assert.All(["codex", "dsh", "stub"], id => Assert.False(registry.Provider(id).Skills.IsKnown));
        Assert.True(registry.Provider("claude-code").Skills.IsKnown);
    }

    [Fact]
    public void The_language_writes_the_file_the_settings_writer_wrote_byte_for_byte_with_its_own_lineage_hook()
    {
        var rules = new RuleLists(["Read", "Bash(git commit:*)"], ["Edit"], ["Bash(git push:*)"]);
        IReadOnlyList<string> hardDeny = ["Pushing to any remote in any form: this stays the person's."];
        var guard = TreeGuard.For(_home, _home);
        var lineage = ChildLineage.PathOf(_home, "s1");

        var handed = File.ReadAllBytes(ClaudeRules.Language.Write(_home, "s1", new HandedRules(rules, hardDeny, guard, lineage)));
        var rendered = File.ReadAllBytes(ClaudeRules.Render(_home, "s2", rules, hardDeny, guard, ClaudeLineageHook.For(_home, "s1", lineage)));
        Assert.Equal(rendered, handed);

        // No lineage file asked: no lineage hook, the guard alone.
        var bare = File.ReadAllText(ClaudeRules.Language.Write(_home, "s3", new HandedRules(rules, hardDeny, guard, null)));
        Assert.DoesNotContain("SubagentStart", bare, StringComparison.Ordinal);
        Assert.Contains(TreeGuardHook.Matcher, bare, StringComparison.Ordinal);
    }

}
