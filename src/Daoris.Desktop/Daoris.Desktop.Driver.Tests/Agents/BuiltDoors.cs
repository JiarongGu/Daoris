using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A door of this build's registration list, by its id: what a test holds where it needs a stand-in's or Claude Code's door as it ships.</summary>
internal static class BuiltDoors
{
    public static ISessionAdapter Of(string id) => AdapterSet.Built().Resolve(id);

    public static ISessionAdapter Stub => Of("stub");

    public static ISessionAdapter AcpStub => Of("acp-stub");

    public static ISessionAdapter Claude => Of("claude-code");
}
