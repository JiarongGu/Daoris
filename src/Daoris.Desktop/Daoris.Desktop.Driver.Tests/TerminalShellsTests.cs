using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONSOLE4 (D96): which shells a terminal offers. PowerShell 7 when it is installed, else Windows
/// PowerShell, then whatever else the machine has — found on PATH by the plugin door's resolver, never a
/// third one.
/// </summary>
public sealed class TerminalShellsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-shells-" + Guid.NewGuid().ToString("N")[..8]);

    public TerminalShellsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A folder holding empty files by these names — enough for a PATH lookup, which only asks what exists.</summary>
    private string Folder(string name, params string[] files)
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        foreach (var file in files)
        {
            var at = Path.Combine(folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(at)!);
            File.WriteAllText(at, "");
        }

        return folder;
    }

    private static string PathOf(params string[] folders) => string.Join(Path.PathSeparator, folders);

    [Fact]
    public void PowerShell_7_comes_first_when_it_is_installed_and_the_rest_follow_in_order()
    {
        if (!OperatingSystem.IsWindows()) return; // Found by PATHEXT, and started under the Windows pseudo-console.

        var system = Folder("system", "cmd.exe", "powershell.exe");
        var seven = Folder("seven", "pwsh.exe");

        var shells = TerminalShells.Available(PathOf(system, seven));

        Assert.Equal([TerminalShells.Pwsh, TerminalShells.WindowsPowerShell, TerminalShells.Cmd], shells.Select(shell => shell.Id));
        // Found as PATHEXT spells the extension, which is the file Windows starts either way.
        Assert.Equal(Path.Combine(seven, "pwsh.exe"), shells[0].Path, ignoreCase: true);
        Assert.Equal(TerminalShells.Pwsh, TerminalShells.Default(shells)?.Id);
    }

    [Fact]
    public void Windows_PowerShell_is_the_default_where_there_is_no_PowerShell_7()
    {
        if (!OperatingSystem.IsWindows()) return;

        var shells = TerminalShells.Available(Folder("system", "cmd.exe", "powershell.exe"));

        Assert.Equal(TerminalShells.WindowsPowerShell, TerminalShells.Default(shells)?.Id);
        Assert.Equal(["-NoLogo"], shells[0].Arguments);
    }

    [Fact]
    public void Git_Bash_is_the_bash_beside_git_and_never_another_bash_on_PATH()
    {
        if (!OperatingSystem.IsWindows()) return;

        // `bash.exe` in the system folder is WSL's launcher, not Git Bash: it is not offered as one.
        var system = Folder("system", "cmd.exe", "bash.exe");
        var git = Folder("Git", @"cmd\git.exe", @"bin\bash.exe");

        var shells = TerminalShells.Available(PathOf(system, Path.Combine(git, "cmd")));

        var bash = Assert.Single(shells, shell => shell.Id == TerminalShells.GitBash);
        Assert.Equal(Path.Combine(git, "bin", "bash.exe"), bash.Path);
        Assert.Equal(["--login", "-i"], bash.Arguments);
        Assert.DoesNotContain(TerminalShells.GitBash, TerminalShells.Available(system).Select(shell => shell.Id));
    }

    [Fact]
    public void Git_Bash_is_found_from_git_s_other_folders_too()
    {
        if (!OperatingSystem.IsWindows()) return;

        var git = Folder("Git", @"mingw64\bin\git.exe", @"bin\bash.exe");

        var shells = TerminalShells.Available(Path.Combine(git, "mingw64", "bin"));

        Assert.Equal(Path.Combine(git, "bin", "bash.exe"), Assert.Single(shells).Path);
    }

    [Fact]
    public void A_machine_with_none_offers_none_and_has_no_default()
    {
        var shells = TerminalShells.Available(Folder("empty"));

        Assert.Empty(shells);
        Assert.Null(TerminalShells.Default(shells));
    }
}
