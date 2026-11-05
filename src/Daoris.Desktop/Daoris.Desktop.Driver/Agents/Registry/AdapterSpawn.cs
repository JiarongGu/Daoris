using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A real door's spawn as its adapter class prepares it (AGENTPROVIDER1 row d): the start, the conversation, the native resume and the
/// carriers, each the class's own, so a door registered on the contract spawns exactly as the class did. Row h moves each door's spawn
/// into its provider's folder as a <c>ProcessRecipe</c>, and row t takes this bridge away with the classes.
/// </summary>
internal sealed class AdapterSpawn(ISessionAdapter adapter) : ISpawn
{
    public ProcessStartInfo Start(SessionTarget target, IReadOnlyList<string>? command) => adapter.Prepare(target, command);

    public ProcessStartInfo Chat(ChatTarget target, IReadOnlyList<string>? command) => adapter.PrepareChat(target, command);

    public ProcessStartInfo? Resume(SessionTarget target, IReadOnlyList<string>? command, string conversation) =>
        adapter.PrepareResume(target, command, conversation);

    public IReadOnlyDictionary<string, string> ToolsUpFront => adapter.ToolsUpFront;

    public void HandServers(ProcessStartInfo info, string configFile) => adapter.HandServers(info, configFile);

    public void HandSettings(ProcessStartInfo info, string settingsFile) => adapter.HandSettings(info, settingsFile);

    public void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins) => adapter.HandPlugins(info, plugins);
}
