namespace Daoris.Driver;

/// <summary>
/// The id of the provider whose console asks the person's questions (D184 point 4): the one value of the settings' <em>questions</em> pause,
/// kept below the providers so the settings name no provider, and which Claude Code's declaration takes as its own id.
/// </summary>
internal static class ConsoleProviderId
{
    public const string Value = "claude-code";
}
