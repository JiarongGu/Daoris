namespace Daoris.Driver;

/// <summary>
/// Where Claude Code keeps a conversation under an account's home (the agent-provider design §2.3, SWITCHCTX1's table): its project folders,
/// keyed by the working directory the session ran in. The layout is the same in every version it keeps one; another provider declares its own
/// or none, and a provider that declares none keeps no conversation a move could carry.
/// </summary>
internal sealed class ClaudeConversationHome : IConversationHome
{
    public const string Projects = "projects";

    public IReadOnlyList<string> Keeps(string? version) => [Projects];
}
