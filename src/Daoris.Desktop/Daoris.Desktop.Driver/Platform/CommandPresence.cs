namespace Daoris.Driver;

/// <summary>Whether a command would start, without starting it.</summary>
/// <remarks>
/// The shared kernel's (Platform, STRUCT3a): it asks the machine's PATH and files for the agents' doors, the tools, the
/// terminals, the trees, Setup and a plugin's tool checks alike.
/// </remarks>
public static class CommandPresence
{
    /// <summary>The variable a child's <c>PATH</c> is. Windows reads it in any case, and a start's environment there is case-blind.</summary>
    public const string PathVariable = "PATH";

    public static bool Resolvable(string command) => Resolve(command) is not null;

    /// <summary>
    /// The file a command names — itself when it is a path, else the first match on <paramref name="path"/>
    /// (this process's own by default), with Windows's extensions tried — or null where there is none.
    /// </summary>
    /// <param name="startable">
    /// Only what Windows can START: npm puts an extensionless POSIX `npm` script beside `npm.cmd`, and
    /// the bare name found first is a file no Windows process can run.
    /// </param>
    public static string? Resolve(string command, string? path = null, bool startable = false)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        if (Path.IsPathRooted(command)
            || command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(command) ? command : null;
        }

        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> extensions = !OperatingSystem.IsWindows()
            ? [""]
            : startable ? pathExt : pathExt.Prepend("");
        var directories = (path ?? Environment.GetEnvironmentVariable(PathVariable) ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }
}
