using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A door of this build's registration list, by its id: what a test holds where it needs a stand-in's or Claude Code's door as it ships.</summary>
internal static class BuiltDoors
{
    public static AgentDoor Of(string id) => ProviderRegistry.Built().Door(id);

    public static AgentDoor Stub => Of("stub");

    public static AgentDoor AcpStub => Of("acp-stub");

    public static AgentDoor Claude => Of("claude-code");
}
