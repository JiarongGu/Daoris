namespace Daoris.Devkit.Tests;

/// <summary>
/// Starting a command the way a person would type it (REV3 tools F10). The doctrine gate's default
/// command is `daoris`, and on Windows npm installs that as `daoris.cmd`: started without a shell,
/// .NET looks for `daoris.exe` only, and the gate said the tool "could not be run" on every Windows
/// machine that had installed it.
/// </summary>
public sealed class ProcessTests : IDisposable
{
    private readonly Fixture _fx = new("process");

    public void Dispose() => _fx.Dispose();

    [Fact]
    public void A_command_installed_as_a_cmd_shim_is_found_and_run()
    {
        if (!OperatingSystem.IsWindows()) return;   // a `.cmd` is a Windows shape; elsewhere npm writes a script with a shebang
        _fx.Write("fake.cmd", "@echo off\r\necho checked %1\r\nexit /b 0\r\n");

        var ran = Process.Run(_fx.Absolute("fake"), ["check"], _fx.Path);

        Assert.Equal(0, ran.ExitCode);
        Assert.Contains("checked check", ran.Output);
    }

    [Fact]
    public void An_argument_a_cmd_shim_would_reinterpret_is_refused_rather_than_passed()
    {
        if (!OperatingSystem.IsWindows()) return;
        _fx.Write("fake.cmd", "@echo off\r\nexit /b 0\r\n");

        Assert.Throws<DevkitException>(() => Process.Run(_fx.Absolute("fake"), ["check & del *"], _fx.Path));
    }
}
