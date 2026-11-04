using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>The seam that points the Agent SDK at a <c>claude</c> Daoris chose (ACP2, §1a).</summary>
public static class ClaudeAcp
{
    /// <summary>The environment variable the ACP adapter's SDK reads to find its CLI.</summary>
    public const string ExecutableVariable = "CLAUDE_CODE_EXECUTABLE";

    /// <summary>
    /// Point this spawn at a managed <c>claude</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Null leaves the variable unset</b>, so the SDK finds its CLI the way it always did.
    /// Setting it to an empty string — or to a path that is not there — would break a machine that
    /// works today, which is the additive rule every part of the toolchain holds (D48 §2a).
    /// </remarks>
    public static void PointAtClaude(ProcessStartInfo info, string? managedClaude)
    {
        if (managedClaude is not { Length: > 0 }) return;

        info.Environment[ExecutableVariable] = managedClaude;
        // 🔴 The SDK runs that `claude` with this process's environment, so the pin's own switch
        // travels here (AGT2) — the same one the pipe door's spawn of the same binary carries.
        foreach (var (name, value) in ClaudeCodeAdapter.StayPinned) info.Environment[name] = value;
    }
}
