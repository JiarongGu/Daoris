using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Git for Windows' launcher, started past (GITSTART1): which layout is the launcher's, the variables it hands its child,
/// and that a git the person named is started as named. The variables are the ones measured on git 2.53.0.windows.3
/// (FIX-LOG, GITSTART1); <see cref="GitForWindowsProcessTests"/> holds them to a real install's launcher.
/// </summary>
/// <remarks>Nothing here starts a program: an install is empty files and folders in a temporary directory.</remarks>
public sealed class GitForWindowsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-for-windows-" + Guid.NewGuid().ToString("N")[..8]);

    private string Top => Path.Combine(_root, "Git");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Touch(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "");
    }

    /// <summary>An install laid out as Git for Windows lays one, less whatever the case leaves out; its launcher's path.</summary>
    private string Lay(string prefix = "mingw64", string launcherFolder = "cmd", bool usr = true, bool real = true, bool bashConfig = false)
    {
        var launcher = Path.Combine(Top, launcherFolder, "git.exe");
        Touch(launcher);
        if (real) Touch(Path.Combine(Top, prefix, "bin", "git.exe"));
        if (usr) Directory.CreateDirectory(Path.Combine(Top, "usr", "bin"));
        if (bashConfig) Touch(Path.Combine(Top, "etc", "git-bash.config"));
        return launcher;
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
        // The person's HOME, not a Daoris home: its bin is the launcher's folder, so it is spelled whole.
        var userHome = Path.Combine(_root, "user");
        info.Environment.Remove("MSYSTEM");
        info.Environment["HOME"] = userHome;
        info.Environment["PATH"] = @"C:\a;C:\b";

        Assert.True(GitForWindows.Direct(info));
        Assert.Equal(Behind().Git, info.FileName);
        Assert.Equal("MINGW64", info.Environment["MSYSTEM"]);
        Assert.Equal(userHome, info.Environment["HOME"]);
        Assert.Equal(Folders() + userHome + @"\bin;C:\a;C:\b", info.Environment["PATH"]);
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
        var userHome = Path.Combine(_root, "user");
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
        var missing = Path.Combine(_root, "no-such-home");
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
        var fits = @"C:\" + new string('h', 249);
        var tooLong = @"C:\" + new string('h', 250);

        Assert.Equal(Folders() + fits + @"\bin;C:\a", GitForWindows.ChildEnvironment(Behind(), Variables(("PATH", @"C:\a"), ("HOME", fits)))["PATH"]);
        Assert.Equal(Folders() + @"C:\a", GitForWindows.ChildEnvironment(Behind(), Variables(("PATH", @"C:\a"), ("HOME", tooLong)))["PATH"]);
    }

    // ── A fixture's git (TestGit) ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_fixtures_bare_git_is_the_one_PATH_finds_past_the_launcher()
    {
        var info = TestGit.Past(new ProcessStartInfo("git"));
        var found = CommandPresence.Resolve("git", startable: true);

        Assert.Equal(found is null ? "git" : GitForWindows.Behind(found)?.Git ?? found, info.FileName);
    }

    [Fact]
    public void A_fixtures_start_of_a_named_file_is_left_as_it_was()
    {
        var file = Lay();
        var info = TestGit.Past(new ProcessStartInfo(file));

        Assert.Equal(file, info.FileName);
    }

    // ── The person's git (TOOLS5) ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Daoris_own_git_named_as_a_file_starts_as_named_even_where_it_is_the_launcher()
    {
        if (!OperatingSystem.IsWindows()) return;
        var launcher = Lay();
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Tools), """{"tools":{"git":{"use":"file","file":""" + JsonSerializer.Serialize(launcher) + "}}}");

        var (info, refusal) = WorkingTree.GitStart(_root, ["status"], home);

        Assert.Null(refusal);
        Assert.Equal(launcher, info!.FileName);
        Assert.Equal(Environment.GetEnvironmentVariable("MSYSTEM"), info.Environment.TryGetValue("MSYSTEM", out var msystem) ? msystem : null);
    }
}
