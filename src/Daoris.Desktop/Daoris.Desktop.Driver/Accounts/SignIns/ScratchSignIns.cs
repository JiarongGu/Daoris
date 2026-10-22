namespace Daoris.Driver;

/// <summary>
/// 🔴 No sign-in of a real agent under a scratch home (LIVELOGIN1): a test, a probe or a transcription of today's words that ran
/// <c>codex login</c> or <c>claude auth login</c> under a home in <c>_fixtures/</c> opened the person's browser on their real account,
/// four times in one build and once in a driven session. Every sign-in action asks here before anything is made or started: the
/// window's (<see cref="HarnessActions.PrepareLogin"/> and the bridge's press) and the terminal's (<c>daoris agent login</c>).
/// </summary>
/// <remarks>
/// <para>A <b>scratch home</b> is one under a <c>_fixtures</c> folder, or under the OS's temporary folder, where the driver's tests
/// make theirs. A person's home is neither.</para>
/// <para>The <b>real agent binary</b> is the agent's own (<see cref="HarnessToolchain.Binary"/>'s first word, its extension
/// aside) found on <c>PATH</c>: what a start runs where no command is named and nothing is pinned, or a command named by that bare
/// word. A stub at a path, as <c>AgentSignInProcessTests</c> lays Claude Code's managed binary under its scratch home, is no real
/// one; nor is a command of another name (<c>node</c> and a script), nor an agent with no binary of its own (<c>stub</c>,
/// <c>acp-stub</c>).</para>
/// </remarks>
public static class ScratchSignIns
{
    /// <summary>The refusal for a sign-in of <paramref name="run"/> into <paramref name="profileHome"/>, or null where it may run.</summary>
    public static string? Refusal(HarnessToolchain toolchain, HarnessCommand run, string profileHome)
    {
        if (!IsScratch(profileHome) || !IsReal(toolchain, run.Run)) return null;
        return $"a sign-in under a scratch home runs no real agent, and `{run.Harness}`'s would have run the `{run.Run[0]}` on PATH, so nothing was "
            + "started — a test, a probe or a transcription signs in with the stub adapters (`stub`, `acp-stub`), or a stub binary "
            + "laid under its scratch home (LIVELOGIN1).";
    }

    /// <summary>Whether <paramref name="path"/> lies under a <c>_fixtures</c> folder or the OS's temporary folder.</summary>
    public static bool IsScratch(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("_fixtures", Comparer)) return true;
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        return full.Length > temp.Length
            && full.StartsWith(temp, Comparison)
            && (full[temp.Length] == Path.DirectorySeparatorChar || full[temp.Length] == Path.AltDirectorySeparatorChar);
    }

    /// <summary>Whether <paramref name="command"/> starts the agent's own binary from PATH: its bare name, its extension aside.</summary>
    public static bool IsReal(HarnessToolchain toolchain, IReadOnlyList<string> command)
    {
        if (toolchain.Binary is not { Count: > 0 } binary || command is not { Count: > 0 }) return false;
        var word = command[0];
        return Path.GetFileName(word) == word
            && string.Equals(Path.GetFileNameWithoutExtension(word), Path.GetFileNameWithoutExtension(binary[0]), Comparison);
    }

    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
