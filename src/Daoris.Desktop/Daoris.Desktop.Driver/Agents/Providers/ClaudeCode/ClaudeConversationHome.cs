using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What Claude Code keeps of a conversation in its own home (AGENTPROVIDER1 row e): a folder under <c>projects/</c> for each working
/// directory a session ran in, named by the directory with every character that is not a letter or a digit turned into <c>-</c>, so
/// a person's tool home is read for Daoris's leftovers through this layout alone (Codex files its sessions by date and declares none).
/// </summary>
internal sealed class ClaudeConversationHome : IConversationHome
{
    private static readonly Regex Other = new("[^a-zA-Z0-9]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ClaudeConversationHome Home { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Keeps(string? version) => ["projects"];

    /// <inheritdoc/>
    public ProjectFolders? Projects { get; } = new("CLAUDE_CONFIG_DIR", ".claude", "projects", path => Other.Replace(path, "-"));
}
