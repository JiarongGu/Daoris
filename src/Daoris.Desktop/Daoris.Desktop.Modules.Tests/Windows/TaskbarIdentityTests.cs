using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One taskbar button for a pinned Daoris (TASKBAR1, D108). The window belongs to
/// <c>app/Daoris.Desktop.exe</c> while a person runs <c>Daoris.exe</c> at the install's root (D93), and
/// Windows groups a button by its process's executable unless the window names an application id. So
/// an install's windows name one, with a relaunch command that starts the root launcher.
/// </summary>
/// <remarks>
/// What these hold is every judgement the window's property store is handed: whether this is an install,
/// the id, and the command, name and icon a pin is made from. Whether Windows then shows one button is
/// seen only by looking at a taskbar, on a machine, after a republish.
/// </remarks>
public sealed class TaskbarIdentityTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-taskbar-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>An install as the publish lays it out (D93): the marker and the launcher at the root, the application in `app/`.</summary>
    private string Install(string name = "install", bool marked = true, bool launcher = true)
    {
        var install = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(install, InstallHome.AppFolder));
        if (marked) File.WriteAllText(Path.Combine(install, InstallHome.Marker), "# Daoris — installed desktop\n");
        if (launcher) File.WriteAllText(Path.Combine(install, TaskbarIdentity.Launcher), "");
        return install;
    }

    private static string App(string install) => Path.Combine(install, InstallHome.AppFolder);

    /// <summary>
    /// A pin made from the running window starts the launcher at the root, which a republish never moves,
    /// never the application in `app/`, which it replaces.
    /// </summary>
    [Fact]
    public void An_installs_window_relaunches_the_launcher_at_its_root()
    {
        var install = Install();
        var launcher = Path.Combine(install, "Daoris.exe");

        var identity = TaskbarIdentity.For(App(install));

        Assert.NotNull(identity);
        Assert.Equal("Daoris.Desktop", identity.AppId);
        Assert.Equal($"\"{launcher}\"", identity.RelaunchCommand);
        Assert.Equal("Daoris", identity.RelaunchDisplayName);
        Assert.Equal($"{launcher},0", identity.RelaunchIcon);
        Assert.DoesNotContain(Path.Combine(install, InstallHome.AppFolder), identity.RelaunchCommand);
    }

    /// <summary>The command is quoted, so an install under a folder with a space in its name still starts.</summary>
    [Fact]
    public void An_install_under_a_folder_with_a_space_is_still_one_command()
    {
        var install = Install("Program Files Daoris");

        var identity = TaskbarIdentity.For(App(install));

        Assert.Equal($"\"{Path.Combine(install, "Daoris.exe")}\"", identity!.RelaunchCommand);
    }

    /// <summary>
    /// A workspace build is not an install, and wears nothing: the dev loop's window keeps Windows' own
    /// grouping by its executable, and never joins the person's pinned Daoris.
    /// </summary>
    [Fact]
    public void A_workspace_build_wears_no_identity()
    {
        Assert.Null(TaskbarIdentity.For(App(Install(marked: false))));
        Assert.Null(TaskbarIdentity.For(Path.Combine(_root, "bin", "Debug")));
    }

    /// <summary>With no launcher at the root there is nothing a pin could start that outlives a publish.</summary>
    [Fact]
    public void An_install_without_its_launcher_wears_no_identity()
    {
        Assert.Null(TaskbarIdentity.For(App(Install(launcher: false))));
    }

    /// <summary>
    /// An install whose application runs from its own root, beside the marker, is still that install:
    /// the root is the one <see cref="InstallHome.RootOf"/> answers, the same the home is found from.
    /// </summary>
    [Fact]
    public void The_root_is_the_one_the_home_is_found_from()
    {
        var install = Install();

        Assert.Equal(TaskbarIdentity.For(App(install)), TaskbarIdentity.For(install));
        Assert.Equal(TaskbarIdentity.For(App(install)), TaskbarIdentity.For(App(install) + Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// The id is Windows' form (no more than 128 characters, no spaces, `Company.Product`) and carries no
    /// version: a pin made before an upgrade must still be the button after it. It never changes, because
    /// every pin a person has made carries it.
    /// </summary>
    [Fact]
    public void The_id_is_windows_form_and_never_changes()
    {
        Assert.Equal("Daoris.Desktop", TaskbarIdentity.Id);
        Assert.True(TaskbarIdentity.Id.Length <= 128);
        Assert.DoesNotContain(' ', TaskbarIdentity.Id);
        Assert.Matches(@"^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)+$", TaskbarIdentity.Id);
    }

    /// <summary>
    /// The launcher and the application are two artefacts that share no code (D93), and they agree on two
    /// things: the launcher's file name, which the relaunch command names, and the id, which the launcher
    /// carries for its own process so a chained start is one application to Windows. Held on the
    /// launcher's own source and project, the way the publish script's twin test holds its names.
    /// </summary>
    [Fact]
    public void The_launcher_is_named_and_identified_as_the_window_says()
    {
        var launcher = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Launcher");

        var project = File.ReadAllText(Path.Combine(launcher, "Daoris.Desktop.Launcher.csproj"));
        var assembly = Regex.Match(project, "<AssemblyName>([^<]+)</AssemblyName>").Groups[1].Value;
        Assert.Equal(TaskbarIdentity.Launcher, assembly + ".exe");

        var program = File.ReadAllText(Path.Combine(launcher, "Program.cs"));
        Assert.Contains($"AppId = \"{TaskbarIdentity.Id}\"", program);

        // Before it starts anything: Windows reads a process's id when it first shows something.
        var main = program[program.IndexOf("static int Main(", StringComparison.Ordinal)..];
        var identified = main.IndexOf("SetCurrentProcessExplicitAppUserModelID(AppId)", StringComparison.Ordinal);
        Assert.True(identified > 0, "the launcher does not name its process's id");
        Assert.True(identified < main.IndexOf("Process.Start(", StringComparison.Ordinal),
            "the launcher starts the application before it names its id");
    }

    /// <summary>
    /// Every window the application shows wears the identity, the main one and each secondary one alike,
    /// or a second window would be a second button. Held on the application's own source, because nothing
    /// without a desktop session can hold it otherwise: applied as each handle is created (a recreated
    /// handle loses what the old one carried), and removed as it is destroyed, which Windows requires of
    /// a window's properties.
    /// </summary>
    [Fact]
    public void Every_window_wears_it_from_its_handle_and_gives_it_back()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");

        Assert.Contains("TaskbarWindow.Wear(this)", File.ReadAllText(Path.Combine(app, "MainForm.cs")));
        Assert.Contains("TaskbarWindow.Wear(this)", File.ReadAllText(Path.Combine(app, "SecondaryForm.cs")));

        var window = File.ReadAllText(Path.Combine(app, "TaskbarWindow.cs"));
        Assert.Contains("TaskbarIdentity.For(AppContext.BaseDirectory)", window);
        Assert.Contains("HandleCreated +=", window);
        Assert.Contains("HandleDestroyed +=", window);
    }

    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
