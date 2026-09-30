namespace Daoris.Driver;

/// <summary>A shell a terminal can run (CONSOLE4, D96): its id, the file that starts it, and what it is started with.</summary>
/// <param name="Id">The page's name for it: <c>pwsh</c>, <c>powershell</c>, <c>cmd</c> or <c>bash</c>.</param>
public sealed record TerminalShell(string Id, string Path, IReadOnlyList<string> Arguments);

/// <summary>What a terminal starts: the shell, the folder it starts in, and what its environment adds to this process's own.</summary>
public sealed record TerminalLaunch(
    TerminalShell Shell, string Directory, IReadOnlyDictionary<string, string> Environment, int Columns = 80, int Rows = 24);

/// <summary>
/// A shell of the person's own, under a pseudo-console (CONSOLE4a, D96): what a terminal view drives.
/// Disposing it closes it, and everything the shell started ends with it.
/// </summary>
public interface ITerminal : IDisposable
{
    /// <summary>What the person typed, as the renderer encodes it — keys, escape sequences, pasted text.</summary>
    void Write(string data);

    /// <summary>The view's size in cells. Taken after the close as well, and ignored then.</summary>
    void Resize(int columns, int rows);
}

/// <summary>Where terminals come from: the machine's shells, and one started.</summary>
/// <remarks>
/// A seam so the page's module is tested without a real shell under it; the pseudo-console's own tests run
/// real ones (<c>PseudoConsoleTests</c>).
/// </remarks>
public interface ITerminalFactory
{
    /// <summary>The shells this machine has, the default first.</summary>
    IReadOnlyList<TerminalShell> Shells();

    /// <summary>Start one. The callbacks are handed in rather than subscribed after, so nothing it says is missed.</summary>
    /// <param name="output">What the shell wrote, decoded, as it arrives.</param>
    /// <param name="exited">How the shell ended, told once, after the last of its output.</param>
    ITerminal Start(TerminalLaunch launch, Action<string> output, Action<int> exited);
}

/// <summary>The machine's terminals: its shells found on PATH, each started under a Windows pseudo-console.</summary>
public sealed class PseudoConsoleTerminals : ITerminalFactory
{
    public IReadOnlyList<TerminalShell> Shells() => TerminalShells.Available();

    public ITerminal Start(TerminalLaunch launch, Action<string> output, Action<int> exited) =>
        PseudoConsole.Start(launch, output, exited);
}

/// <summary>
/// Which shells a terminal offers (D96): PowerShell 7 when it is installed, else Windows PowerShell, then
/// Command Prompt and Git Bash where the machine has them.
/// </summary>
/// <remarks>
/// <para><b>Found on PATH the way the plugin door finds a command</b> (<see cref="CommandPresence"/>), with
/// PATHEXT and only what Windows can start: one resolver for a bare name, not a third.</para>
///
/// <para><b>Git Bash is the <c>bash.exe</c> beside git</b>, never whatever <c>bash</c> PATH finds first: on
/// Windows that is usually WSL's launcher in the system folder, a different shell in a different
/// filesystem. Git keeps its <c>git.exe</c> in <c>cmd\</c>, <c>bin\</c> or <c>mingw64\bin\</c>, and its
/// bash in <c>bin\</c> under the same root.</para>
///
/// <para>None elsewhere: the pseudo-console is the Windows one, so a shell found on another platform
/// could not be started by it.</para>
/// </remarks>
public static class TerminalShells
{
    public const string Pwsh = "pwsh";
    public const string WindowsPowerShell = "powershell";
    public const string Cmd = "cmd";
    public const string GitBash = "bash";

    /// <summary>The shells on <paramref name="path"/> (this process's PATH by default), in the order offered.</summary>
    public static IReadOnlyList<TerminalShell> Available(string? path = null)
    {
        if (!OperatingSystem.IsWindows()) return [];

        var found = new List<TerminalShell>();
        // The banner is the one line a new terminal would otherwise open with, every time.
        if (CommandPresence.Resolve("pwsh", path, startable: true) is { } pwsh) found.Add(new(Pwsh, pwsh, ["-NoLogo"]));
        if (CommandPresence.Resolve("powershell", path, startable: true) is { } powershell)
        {
            found.Add(new(WindowsPowerShell, powershell, ["-NoLogo"]));
        }

        if (CommandPresence.Resolve("cmd", path, startable: true) is { } cmd) found.Add(new(Cmd, cmd, []));
        // A login shell, as Git Bash's own shortcut starts it, so the person's profile is read.
        if (GitBashBeside(CommandPresence.Resolve("git", path, startable: true)) is { } bash) found.Add(new(GitBash, bash, ["--login", "-i"]));
        return found;
    }

    /// <summary>The shell a terminal opens with when none is named: PowerShell 7, else Windows PowerShell, else the first there is.</summary>
    public static TerminalShell? Default(IReadOnlyList<TerminalShell> available) =>
        available.FirstOrDefault(shell => shell.Id == Pwsh)
        ?? available.FirstOrDefault(shell => shell.Id == WindowsPowerShell)
        ?? available.FirstOrDefault();

    private static string? GitBashBeside(string? git)
    {
        // Three folders up from git.exe covers every layout git ships (`cmd\`, `bin\`, `mingw64\bin\`);
        // further up is another program's folder.
        var folder = git is null ? null : Path.GetDirectoryName(git);
        for (var level = 0; level < 3 && folder is not null; level++, folder = Path.GetDirectoryName(folder))
        {
            var bash = Path.Combine(folder, "bin", "bash.exe");
            if (File.Exists(bash)) return bash;
        }

        return null;
    }
}
