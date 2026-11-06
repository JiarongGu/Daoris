using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Whether a start on a door is pointed at the <c>claude</c> Claude Code's pin chose (ACP2; the agent-provider design §7 row 7): a door's
/// spawn says so of its own program, so Codex's and dsh's doors take none of it.
/// </summary>
internal static class AdapterClaudeSeam
{
    public static bool TakesClaudeSeam(this ISessionAdapter adapter) => LegacyDoor.Of(adapter).ClaudeSeam;

    /// <summary>The seam on <paramref name="info"/>, applied by the door that takes it; nothing where it takes none.</summary>
    public static void PointAtClaude(this ISessionAdapter adapter, ProcessStartInfo info, string managedClaude) =>
        LegacyDoor.Of(adapter).PointAtClaude(info, managedClaude);
}
