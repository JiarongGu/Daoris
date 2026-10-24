using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The holder's terminal (SUPERVISOR1a-ii, D208 point 2; the supervisor design §2.2, amended by the owner's direction of 2026-10-24): the
/// one start Daoris builds that adds the holder key, made by the window itself at the owner's <i>Appoint</i>, so the key never passes
/// through the page, the clipboard or a shell's history. **It is a tab of Daoris's own terminal panel**, a shell under the app's
/// pseudo-console (<see cref="TerminalModule"/>), never a separate console window of the system's: the key is in that panel process's
/// environment block alone.
/// </summary>
/// <remarks>
/// <para><b>The owner's shell</b>: <c>pwsh</c>, or Windows PowerShell where it is absent, with <c>-NoProfile</c>, since a profile is a
/// file an agent can write; outside every agent job, since the window's process is in none.</para>
/// <para><b>In a folder of its own outside every repository</b>, the home's <see cref="HomeEntry.RoleRooms"/> for the role, emptied at
/// each start and holding nothing but what this writes; the holder's harness, started there with the line the tab prints, loads no
/// repository's configuration.</para>
/// <para><b>Its first lines say what it is</b> and the line that starts the supervisor's session, written to the tab by the panel before
/// the shell's own output; no line holds the key.</para>
/// </remarks>
public static class HolderTerminal
{
    /// <summary>
    /// The line the tab prints for starting the holder's session (§2.2): Claude Code with no project or local settings, no MCP server but
    /// an empty configuration, and only read tools and the answer verb allowed. 1a-iii's look records the line it ran.
    /// </summary>
    public const string SessionLine =
        "claude --setting-sources user --strict-mcp-config --mcp-config '{\"mcpServers\":{}}' "
        + "--allowedTools 'Read' 'Grep' 'Glob' 'Bash(daoris-driver sessions supervise:*)'";

    /// <summary>The tab's title.</summary>
    public const string Title = "Daoris: the supervisor's terminal";

    /// <summary>The shell's arguments: no logo, and no profile, which an agent could have written.</summary>
    public static IReadOnlyList<string> Arguments { get; } = ["-NoLogo", "-NoProfile"];

    /// <summary>The owner's shell among the panel's: <c>pwsh</c>, else Windows PowerShell, with <see cref="Arguments"/>; null where neither is here.</summary>
    public static TerminalShell? ShellOf(IReadOnlyList<TerminalShell> available) =>
        (available.FirstOrDefault(shell => shell.Id == TerminalShells.Pwsh)
            ?? available.FirstOrDefault(shell => shell.Id == TerminalShells.WindowsPowerShell)) is { } shell
            ? shell with { Arguments = Arguments }
            : null;

    /// <summary>The role's own folder under the home, outside every repository.</summary>
    public static string Folder(string home, string role) => DaorisHome.Entry(home, HomeEntry.RoleRooms, role);

    /// <summary>The role's folder, emptied: nothing an agent wrote there earlier is read as configuration.</summary>
    public static string Emptied(string home, string role)
    {
        var folder = Folder(home, role);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>What the panel's process adds to its environment: the home, and the key, the one start that adds it.</summary>
    public static IReadOnlyDictionary<string, string> Environment(string home, string key) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [DaorisHome.Variable] = home, [HolderKey.Variable] = key };

    /// <summary>What the tab says first, before the shell's own output: what it is and how to start the session, never the key.</summary>
    public static string Intro(DateTimeOffset at)
    {
        var appointed = at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string[] lines =
        [
            $"{Title}.",
            $"This is the supervisor's terminal for Daoris, appointed at {appointed}: start only the supervisor's session here.",
            "Every program this terminal starts holds the supervisor's key: never build, test, merge or run a branch's code here,",
            "never print the environment, and never copy the key out of it. Daoris forgets the key when it restarts.",
            "Start the supervisor's session with:",
            "  " + SessionLine,
        ];
        return string.Join("\r\n", lines) + "\r\n\r\n";
    }
}
