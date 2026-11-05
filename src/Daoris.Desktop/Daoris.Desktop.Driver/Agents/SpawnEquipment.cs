using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What every session's process carries on its environment: its toolchain's account, binary and pin, then its tools at the start by the
/// adapter's own switch, so its connector's tools come with its first request. Applied here rather than inside each adapter's spawn so no
/// adapter can forget it, and in Agents so a start's assembly and a conversation's agent equip by the same call; the switch is set last, so
/// an account's own variables cannot defer the tools again.
/// </summary>
internal static class SpawnEquipment
{
    public static void Equip(ISessionAdapter adapter, ProcessStartInfo info, HarnessSelection selection)
    {
        if (adapter.Toolchain is { } toolchain)
        {
            HarnessProbe.Apply(
                info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                selection.Environment);
        }

        foreach (var (name, value) in adapter.ToolsUpFront) info.Environment[name] = value;
    }
}
