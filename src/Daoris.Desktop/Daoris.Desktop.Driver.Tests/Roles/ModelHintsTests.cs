using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's model is judged as a word, so it is also read against what its agent's open listed, as a hint: a Claude alias on a Codex choice is
/// said, never refused (the agent's own refusal at the open decides, D205).
/// </summary>
public sealed class ModelHintsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "model-hints", Guid.NewGuid().ToString("N")[..8]);

    public ModelHintsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void A_model_the_agent_s_open_did_not_list_is_said_and_one_it_listed_is_not()
    {
        var registry = ProviderRegistry.Built();
        Assert.Null(AgentOffers.Offered(_home, "codex-acp", "2.6.0", "acct", ["gpt-6.1-sol", "gpt-6.1-mini"], ["low", "high"], DateTimeOffset.UtcNow));

        var said = ModelHints.Of(_home, registry, new ActorChoice("codex-acp", "sonnet"));

        Assert.Contains("`codex-acp` did not list `sonnet`", said, StringComparison.Ordinal);
        Assert.Contains("gpt-6.1-sol", said, StringComparison.Ordinal);
        Assert.Null(ModelHints.Of(_home, registry, new ActorChoice("codex-acp", "gpt-6.1-sol")));
        Assert.Null(ModelHints.Of(_home, registry, new ActorChoice("codex-acp")));
    }

    [Fact]
    public void Nothing_is_said_where_no_open_has_listed_or_only_the_stamped_aliases_stand()
    {
        var registry = ProviderRegistry.Built();

        Assert.Null(ModelHints.Of(_home, registry, new ActorChoice("codex-acp", "sonnet")));
        Assert.Null(ModelHints.Of(_home, registry, new ActorChoice("claude-code", "claude-opus-9-9")));
        Assert.Null(ModelHints.Of(_home, registry, new ActorChoice("no-such-agent", "sonnet")));
    }
}
