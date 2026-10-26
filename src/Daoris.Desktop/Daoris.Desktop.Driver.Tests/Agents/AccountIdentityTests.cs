using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class AccountIdentityTests
{
    [Theory]
    [InlineData(null, null, null, null, false)]
    [InlineData(null, "", "claude-code-acp", "CLAUDE-CODE-ACP", false)]
    [InlineData("", null, "codex-acp", "CODEX-ACP", false)]
    [InlineData("work", "WORK", "codex-acp", "CODEX-ACP", false)]
    [InlineData("work", "other", "codex-acp", "codex-acp", true)]
    [InlineData(null, "work", "codex-acp", "codex-acp", true)]
    [InlineData("work", null, "codex-acp", "codex-acp", true)]
    [InlineData("work", "work", "claude-code-acp", "codex-acp", true)]
    [InlineData(null, null, "claude-code-acp", "codex-acp", true)]
    [InlineData("work", "work", "codex-acp", null, false)]
    [InlineData("work", "work", "codex-acp", "", false)]
    [InlineData("work", "work", null, "codex-acp", false)]
    [InlineData("work", "work", "", "codex-acp", false)]
    public void Account_identity_keeps_own_home_case_and_the_agent_that_owns_the_account(
        string? ran, string? runs, string? priorAgent, string? agent, bool another)
    {
        var prior = new PriorSession("s1", null, "failed") { Profile = ran, Adapter = priorAgent };

        Assert.Equal(another, AccountIdentity.OnAnotherAccount(prior, runs, agent));
    }
}
