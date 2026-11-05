namespace Daoris.Driver;

/// <summary>
/// Claude Code's settings file as the provider's <c>Models</c> part (the agent-provider design §2.3, row n): the aliases its program takes and
/// the efforts its schema keeps in <c>effortLevel</c>, which <see cref="AgentSettings"/> reads and writes. <c>max</c> is for one session only
/// and is never a default.
/// </summary>
internal sealed class ClaudeSettingsFile : ISettingsFile
{
    public IReadOnlyList<string> Models => AgentSettings.Models;

    public IReadOnlyList<string> Efforts => AgentSettings.Efforts;

    public IReadOnlyList<string> SessionOnly => ["max"];
}
