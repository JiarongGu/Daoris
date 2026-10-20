using System.Diagnostics;

namespace Daoris.Devkit.Tests;

/// <summary>
/// The devkit's git, started past Git for Windows' launcher (GITSTART3): which layout is the launcher's, the variables it
/// hands its child, and that every git the devkit runs goes the same way. The rows are the driver's
/// <c>GitForWindowsTests</c>, held alike because the devkit's <see cref="GitForWindows"/> is its twin; the variables are
/// the ones measured on git 2.53.0.windows.3 (FIX-LOG, GITSTART1).
/// </summary>
/// <remarks>An install is empty files and folders under the devkit's <c>_fixtures/</c>; only the last rows start a git.</remarks>
public sealed class GitStartTests : IDisposable
{
    private readonly Fixture _fx = new("git-start");

    private string Top => _fx.Absolute("Git");

    public void Dispose() => _fx.Dispose();

    /// <summary>An install laid out as Git for Windows lays one, less whatever the case leaves out; its launcher's path.</summary>
    private string Lay(string prefix = "mingw64", string launcherFolder = "cmd", bool usr = true, bool real = true, bool bashConfig = false)
    {
        _fx.Write($"Git/{launcherFolder}/git.exe", "");
        if (real) _fx.Write($"Git/{prefix}/bin/git.exe", "");
        if (usr) Directory.CreateDirectory(Path.Combine(Top, "usr", "bin"));
        if (bashConfig) _fx.Write("Git/etc/git-bash.config", "");
        return Path.Combine(Top, launcherFolder, "git.exe");
    }

    private GitBehind Behind(string prefix = "mingw64") => new(Top, prefix, Path.Combine(Top, prefix, "bin", "git.exe"));

    private static Func<string, string?> Variables(params (string Name, string Value)[] set)
    {
        var variables = set.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return name => variables.TryGetValue(name, out var value) ? value : null;
    }

    private string Folders(string prefix = "mingw64") =>
        $"{Path.Combine(Top, prefix, "bin")};{Path.Combine(Top, "usr", "bin")};";

    // ── The layout ──────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("mingw64")]
    [InlineData("clangarm64")]
    [InlineData("mingw32")]
    public void The_launcher_in_a_cmd_folder_beside_its_prefix_and_usr_is_recognised(string prefix)
    {
        if (!OperatingSystem.IsWindows()) return;
        var launcher = Lay(prefix);

        Assert.Equal(Behind(prefix), GitForWindows.Behind(launcher));
    }

    [Fact]
    public void The_launcher_is_recognised_in_the_case_PATHEXT_found_it_in()
    {
        if (!OperatingSystem.IsWindows()) return;
        Lay();

        Assert.Equal(Behind(), GitForWindows.Behind(Path.Combine(Top, "cmd", "git.EXE")));
    }

    [Theory]
    [InlineData("no usr folder")]
    [InlineData("no git under a prefix")]
    [InlineData("a git that is not in a cmd folder")]
    [InlineData("the real git itself")]
    [InlineData("a git-bash.config the launcher would read")]
    [InlineData("a bare name")]
    public void Any_other_layout_is_not_the_launcher(string layout)
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Null(GitForWindows.Behind(Laid(layout)));
    }

    private string Laid(string layout)
    {
        switch (layout)
        {
            case "no usr folder": return Lay(usr: false);
            case "no git under a prefix": return Lay(real: false);
            case "a git that is not in a cmd folder": return Lay(launcherFolder: "shims");
            case "the real git itself": Lay(); return Behind().Git;
            case "a git-bash.config the launcher would read": return Lay(bashConfig: true);
            default: return "git";
        }
    }

    [Fact]
    public void A_start_of_any_other_git_is_left_as_it_was()
    {
        if (!OperatingSystem.IsWindows()) return;
        var file = Lay(launcherFolder: "shims");
        var info = new ProcessStartInfo(file);
        var before = info.Environment.ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.False(GitForWindows.Direct(info));
        Assert.Equal(file, info.FileName);
        Assert.Equal(before, info.Environment.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    [Fact]
    public void A_start_of_the_launcher_starts_its_real_git_with_the_launchers_variables()
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = new ProcessStartInfo(Lay());
        var userHome = _fx.Absolute("user");
        info.Environment.Remove("MSYSTEM");
        info.Environment["HOME"] = userHome;
        info.Environment["PATH"] = @"C:\a;C:\b";

        Assert.True(GitForWindows.Direct(info));
        Assert.Equal(Behind().Git, info.FileName);
        Assert.Equal("MINGW64", info.Environment["MSYSTEM"]);
        Assert.Equal(userHome, info.Environment["HOME"]);
        Assert.Equal(Folders() + userHome + @"\bin;C:\a;C:\b", info.Environment["PATH"]);
    }

    [Fact]
    public void A_PATH_too_long_for_one_variable_once_the_launchers_folders_are_put_first_keeps_the_launcher()
    {
        if (!OperatingSystem.IsWindows()) return;
        var launcher = Lay();
        var userHome = _fx.Absolute("user");
        var added = (Folders() + userHome + @"\bin;").Length;
        ProcessStartInfo Start(int expanded)
        {
            var info = new ProcessStartInfo(launcher);
            info.Environment["HOME"] = userHome;
            info.Environment["PATH"] = @"C:\" + new string('p', expanded - added - 3);
            return info;
        }

        var fits = Start(GitForWindows.LongestVariable);
        var tooLong = Start(GitForWindows.LongestVariable + 1);
        var held = tooLong.Environment.ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.True(GitForWindows.Direct(fits));
        Assert.Equal(GitForWindows.LongestVariable, fits.Environment["PATH"]!.Length);
        Assert.False(GitForWindows.Direct(tooLong));
        Assert.Equal(launcher, tooLong.FileName);
        Assert.Equal(held, tooLong.Environment.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    // ── The launcher's variables (measured: FIX-LOG, GITSTART1) ─────────────────────────────────────

    [Theory]
    [InlineData("mingw64", "MINGW64")]
    [InlineData("clangarm64", "CLANGARM64")]
    [InlineData("mingw32", "MINGW32")]
    public void The_launcher_puts_its_folders_and_HOMEs_bin_first_and_names_its_MSYSTEM_over_any(string prefix, string msystem)
    {
        if (!OperatingSystem.IsWindows()) return;

        var child = GitForWindows.ChildEnvironment(Behind(prefix), Variables(("PATH", @"C:\a"), ("HOME", @"C:\h"), ("MSYSTEM", "PROBE")));

        Assert.Equal(Folders(prefix) + @"C:\h\bin;C:\a", child["PATH"]);
        Assert.Equal(msystem, child["MSYSTEM"]);
        Assert.False(child.ContainsKey("HOME"));
    }

    [Fact]
    public void An_unset_PLINK_PROTOCOL_is_ssh_and_a_set_one_is_kept_even_empty()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal("ssh", GitForWindows.ChildEnvironment(Behind(), Variables(("HOME", @"C:\h")))["PLINK_PROTOCOL"]);
        Assert.False(GitForWindows.ChildEnvironment(Behind(), Variables(("HOME", @"C:\h"), ("PLINK_PROTOCOL", ""))).ContainsKey("PLINK_PROTOCOL"));
        Assert.False(GitForWindows.ChildEnvironment(Behind(), Variables(("HOME", @"C:\h"), ("PLINK_PROTOCOL", "plink"))).ContainsKey("PLINK_PROTOCOL"));
    }

    [Fact]
    public void A_set_HOME_is_kept_even_empty_and_its_bin_is_named_from_it()
    {
        if (!OperatingSystem.IsWindows()) return;

        var child = GitForWindows.ChildEnvironment(Behind(), Variables(("PATH", @"C:\a"), ("HOME", ""), ("USERPROFILE", @"C:\u")));

        Assert.False(child.ContainsKey("HOME"));
        Assert.Equal(Folders() + @"\bin;C:\a", child["PATH"]);
    }

    [Fact]
    public void An_unset_HOME_is_HOMEDRIVE_and_HOMEPATH_where_that_is_a_folder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var userHome = _fx.Absolute("user");
        Directory.CreateDirectory(userHome);
        var drive = Path.GetPathRoot(userHome)!.TrimEnd('\\');

        var child = GitForWindows.ChildEnvironment(Behind(),
            Variables(("PATH", @"C:\a"), ("HOMEDRIVE", drive), ("HOMEPATH", userHome[drive.Length..]), ("USERPROFILE", @"C:\u")));

        Assert.Equal(userHome, child["HOME"]);
        Assert.Equal(Folders() + userHome + @"\bin;C:\a", child["PATH"]);
    }

    [Fact]
    public void An_unset_HOME_is_USERPROFILE_where_HOMEDRIVE_and_HOMEPATH_is_no_folder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var missing = _fx.Absolute("no-such-home");
        var drive = Path.GetPathRoot(missing)!.TrimEnd('\\');

        var child = GitForWindows.ChildEnvironment(Behind(),
            Variables(("PATH", @"C:\a"), ("HOMEDRIVE", drive), ("HOMEPATH", missing[drive.Length..]), ("USERPROFILE", @"C:\u")));

        Assert.Equal(@"C:\u", child["HOME"]);
        Assert.Equal(Folders() + @"C:\u\bin;C:\a", child["PATH"]);
    }

    [Fact]
    public void An_unset_HOME_is_never_the_system_folder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var system = Environment.SystemDirectory;
        var drive = Path.GetPathRoot(system)!.TrimEnd('\\');

        var child = GitForWindows.ChildEnvironment(Behind(),
            Variables(("HOMEDRIVE", drive), ("HOMEPATH", system[drive.Length..]), ("USERPROFILE", @"C:\u")));

        Assert.Equal(@"C:\u", child["HOME"]);
    }

    [Fact]
    public void A_HOME_too_long_for_the_launchers_buffer_puts_no_bin_on_PATH()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Measured: the launcher's buffer is 4,096 characters, so a HOME of 4,088 still puts its bin first and 4,089 does not.
        var fits = @"C:\" + new string('h', 4085);
        var tooLong = @"C:\" + new string('h', 4086);

        Assert.Equal(Folders() + fits + @"\bin;C:\a", GitForWindows.ChildEnvironment(Behind(), Variables(("PATH", @"C:\a"), ("HOME", fits)))["PATH"]);
        Assert.Equal(Folders() + @"C:\a", GitForWindows.ChildEnvironment(Behind(), Variables(("PATH", @"C:\a"), ("HOME", tooLong)))["PATH"]);
    }

    // ── The devkit's git (GitStart) ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_bare_git_is_the_one_PATH_finds_past_the_launcher()
    {
        var info = GitStart.Past(new ProcessStartInfo("git"));
        var found = Process.Locate("git");

        var expected = found is null || Process.IsCommandScript(found) ? "git" : GitForWindows.Behind(found)?.Git ?? found;
        Assert.Equal(expected, info.FileName);
    }

    [Fact]
    public void A_start_of_any_other_named_file_is_left_as_it_was()
    {
        var file = Lay(launcherFolder: "shims");
        var info = GitStart.Past(new ProcessStartInfo(file));

        Assert.Equal(file, info.FileName);
    }

    [Fact]
    public void The_devkits_git_runs_and_finds_sh_for_a_bang_alias()
    {
        // A `!` alias runs through sh: where the launcher's PATH were not handed on, it would go unfound.
        var ran = GitStart.Run(["-c", "alias.probe=!echo reached-sh", "probe"], _fx.Path);

        Assert.True(ran.ExitCode == 0, ran.Error);
        Assert.Contains("reached-sh", ran.Output);
    }

    [Fact]
    public void The_devkits_git_hands_its_own_variables_on()
    {
        var ran = GitStart.Run(["config", "--get", "probe.value"], _fx.Path, new Dictionary<string, string>
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "probe.value",
            ["GIT_CONFIG_VALUE_0"] = "handed-on",
        });

        Assert.True(ran.ExitCode == 0, ran.Error);
        Assert.Equal("handed-on", ran.Output.Trim());
    }
}
