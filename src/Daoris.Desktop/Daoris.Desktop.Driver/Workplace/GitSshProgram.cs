namespace Daoris.Driver;

/// <summary>
/// The ssh program an identity's act runs, resolved one way for the ssh door and for the identity's key generation and test:
/// the command the person chose in <c>tools.json</c> (<c>git.core.sshCommand</c>), where it is OpenSSH; else the ssh in the
/// <c>usr/bin</c> of the Git for Windows the act's git belongs to, so its key formats and known hosts agree with that git's; else
/// the first <c>ssh</c> on <c>PATH</c>. Always a file's full path, never a bare name.
/// </summary>
/// <remarks>The door compiles this in: the base library alone.</remarks>
public static class GitSshProgram
{
    /// <summary>
    /// The ssh an act of <paramref name="home"/> runs, or why there is none. <paramref name="git"/> is the git the act uses; where
    /// it is not given, the door reads its own from the <c>GIT_EXEC_PATH</c> git hands its child, else the <c>git</c> on <c>PATH</c>.
    /// </summary>
    public static (string? File, string? Problem) Of(string home, Func<string, string?> variable, string? git = null)
    {
        if (ChosenSsh(home) is { } chosen)
        {
            var (first, _) = GitDoors.FirstWord(chosen);
            var name = Path.GetFileNameWithoutExtension(first.Replace('\\', '/').Split('/')[^1]).ToLowerInvariant();
            if (name is not "ssh")
                return (null, $"daoris-git-ssh: the ssh you chose ({name}) is not OpenSSH, so it cannot carry a git identity.");
            if (Path.IsPathFullyQualified(first) && File.Exists(first)) return (first, null);
            if (GitDoors.Find(first, variable("PATH")) is { } found) return (found, null);
            return (null, $"daoris-git-ssh: the ssh you chose is not there ({first}).");
        }

        if (Beside(git ?? variable("GIT_EXEC_PATH") ?? GitDoors.Find("git", variable("PATH"))) is { } beside) return (beside, null);
        return GitDoors.Find("ssh", variable("PATH")) is { } ssh ? (ssh, null) : (null, "daoris-git-ssh: finds no ssh on git's PATH.");
    }

    /// <summary>The ssh in the <c>usr/bin</c> of the install that <paramref name="start"/> (a git file or a folder of it) lies in; null where none.</summary>
    private static string? Beside(string? start)
    {
        if (string.IsNullOrEmpty(start) || !Path.IsPathFullyQualified(start)) return null;
        var name = OperatingSystem.IsWindows() ? "ssh.exe" : "ssh";
        var folder = File.Exists(start) ? Path.GetDirectoryName(start) : start;
        for (; folder is not null; folder = Path.GetDirectoryName(folder))
        {
            var ssh = Path.Combine(folder, "usr", "bin", name);
            if (File.Exists(ssh)) return ssh;
        }

        return null;
    }

    /// <summary>The ssh command the person chose in <c>tools.json</c> (<c>git.core.sshCommand</c>), or null.</summary>
    private static string? ChosenSsh(string home)
    {
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.Tools);
            if (!File.Exists(path)) return null;
            return System.Text.Json.Nodes.JsonNode.Parse(AtomicFile.ReadText(path)) is System.Text.Json.Nodes.JsonObject root
                && root["git"] is System.Text.Json.Nodes.JsonObject git
                && git["core.sshCommand"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or DriverException)
        {
            return null;
        }
    }
}
