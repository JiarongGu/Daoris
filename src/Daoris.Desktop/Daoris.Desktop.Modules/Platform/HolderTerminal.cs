using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The holder's terminal (SUPERVISOR1a-ii, D208 point 2; the supervisor design §2.2): the one start Daoris builds that adds the holder
/// key, made by the window itself at the owner's <i>Appoint</i>, so the key never passes through the page, the clipboard or a shell's
/// history.
/// </summary>
/// <remarks>
/// <para><b>A console of the owner's shell</b>: <c>pwsh</c>, or Windows PowerShell where it is absent, started directly with a console
/// of its own (the window is no console program, so its console child makes one) and the environment block built here, never through
/// <c>wt.exe</c> or a terminal server that keeps an environment of its own; <c>-NoProfile</c>, since a profile is a file an agent can
/// write; outside every agent job, since the window's process is in none.</para>
/// <para><b>In a folder of its own outside every repository</b>, the home's <see cref="HomeEntry.RoleRooms"/> for the role, emptied at
/// each start and holding nothing but what this writes; the holder's harness, started there with the line the terminal prints, loads
/// no project's configuration.</para>
/// <para><b>Its title and first lines say what it is</b> and the line that starts the supervisor's session; no line holds the key.</para>
/// </remarks>
public static class HolderTerminal
{
    /// <summary>
    /// The line the terminal prints for starting the holder's session (§2.2): Claude Code with no project or local settings, no MCP server
    /// but an empty configuration, and only read tools and the answer verb allowed. 1a-iii's look records the line it ran.
    /// </summary>
    public const string SessionLine =
        "claude --setting-sources user --strict-mcp-config --mcp-config '{\"mcpServers\":{}}' "
        + "--allowedTools 'Read' 'Grep' 'Glob' 'Bash(daoris-driver sessions supervise:*)'";

    /// <summary>The terminal's title.</summary>
    public const string Title = "Daoris: the supervisor's terminal";

    /// <summary>
    /// The start the window makes for <paramref name="role"/>'s holder: its folder emptied first, the shell found, the key the one variable
    /// added to the stripped environment. Null where no shell is found.
    /// </summary>
    public static ProcessStartInfo? Build(string home, string role, string key, DateTimeOffset at, Func<string, string?>? find = null)
    {
        if ((find ?? Shell)(role) is not { } shell) return null;
        var folder = Folder(home, role);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        var info = new ProcessStartInfo(shell)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = folder,
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NoExit");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add(Script(at));
        // Daoris's own environment is stripped at its entry; every start drops the key (GitWiring.Removed), and this one adds it back.
        GitWiring.Hand(info);
        info.Environment[HolderKey.Variable] = key;
        return info;
    }

    /// <summary>Start <paramref name="info"/>; true where a process started. A test hands its own starter.</summary>
    public static bool Start(ProcessStartInfo? info, Func<ProcessStartInfo, bool>? starter = null)
    {
        if (info is null) return false;
        if (starter is not null) return starter(info);
        using var started = Process.Start(info);
        return started is not null;
    }

    /// <summary>The role's own folder under the home, outside every repository.</summary>
    public static string Folder(string home, string role) => DaorisHome.Entry(home, HomeEntry.RoleRooms, role);

    /// <summary>What the terminal says first, as a PowerShell command: its title and its lines, none holding the key.</summary>
    public static string Script(DateTimeOffset at)
    {
        var appointed = at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string[] lines =
        [
            $"This is the supervisor's terminal for Daoris, appointed at {appointed}: start only the supervisor's session here.",
            "Every program this terminal starts holds the supervisor's key: never build, test, merge or run a branch's code here,",
            "never print the environment, and never copy the key out of it. Daoris forgets the key when it restarts.",
            "Start the supervisor's session with:",
            "  " + SessionLine,
        ];
        return $"$Host.UI.RawUI.WindowTitle = '{Quoted(Title)}'; "
            + string.Join("; ", lines.Select(line => $"Write-Host '{Quoted(line)}'"));

        static string Quoted(string text) => text.Replace("'", "''", StringComparison.Ordinal);
    }

    /// <summary>The owner's shell: <c>pwsh</c> on the PATH, else Windows PowerShell; null where neither is found.</summary>
    private static string? Shell(string _)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var pwsh = Path.Combine(folder.Trim(), "pwsh.exe");
            if (File.Exists(pwsh)) return pwsh;
        }

        var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(windows) ? windows : null;
    }
}
