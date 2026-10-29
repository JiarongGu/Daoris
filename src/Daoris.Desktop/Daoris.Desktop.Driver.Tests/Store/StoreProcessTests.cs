using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i (D212; the foundation design §13.4's <c>store_runs</c>, §19.1): the process's run of the desktop store, drawn once and owned
/// below every family's store, so the bindings and the move record write under one run per process and a move's claim names the run its
/// executor's process is. Its kind is a verb until a composition root names the process a shell or a host.
/// </summary>
/// <remarks>Nothing in this test process names its kind, so it reads the default; the roots' own naming is read off their sources.</remarks>
public sealed class StoreProcessTests
{
    [Fact]
    public void The_process_draws_one_run_and_writes_as_a_verb_until_a_root_names_it()
    {
        var run = StoreProcess.Run;

        Assert.Matches("^[0-9a-f]{32}$", run);
        Assert.Same(run, StoreProcess.Run);
        Assert.Equal(StoreProcess.Verb, StoreProcess.Kind);
        Assert.Equal(("shell", "host", "verb"), (StoreProcess.Shell, StoreProcess.Host, StoreProcess.Verb));
    }

    [Fact]
    public void The_options_a_family_opens_the_store_with_name_the_process_s_run_and_kind_and_the_reading_handed()
    {
        Func<StoreSettled> settled = () => StoreSettled.Yes;

        var options = StoreProcess.Options(settled);

        Assert.Equal((StoreProcess.Run, StoreProcess.Kind), (options.Run, options.Kind));
        Assert.Same(settled, options.Settled);
    }

    [Fact]
    public void The_composition_roots_name_their_kind_and_a_verb_names_none()
    {
        var desktop = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        var host = File.ReadAllText(Path.Combine(desktop, "Daoris.Desktop.Driver.Host", "Program.cs"));
        var shell = File.ReadAllText(Path.Combine(desktop, "Daoris.Desktop.App", "Program.cs"));

        // The headless loop names itself once its verb is read, before its lease and its first look; every other verb stays a verb.
        var loop = host.IndexOf("DriverCommand.Read(args, out var problem)", StringComparison.Ordinal);
        var named = host.IndexOf("StoreProcess.Kind = StoreProcess.Host;", StringComparison.Ordinal);
        Assert.True(loop > 0, "the host reads its loop's verb no longer where this case looks");
        Assert.True(named > loop, "the headless loop does not name its kind after its verb is read");
        Assert.True(named < host.IndexOf("HomeLease.TryAcquire(home, LeaseKind.Headless", StringComparison.Ordinal), "the loop names its kind after its lease");
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(host, @"StoreProcess\.Kind\s*="));

        // The shell names itself before its home is established, and so before anything of it opens the store.
        var main = shell[shell.IndexOf("private static void Main(", StringComparison.Ordinal)..];
        var shellNamed = main.IndexOf("StoreProcess.Kind = Daoris.Driver.StoreProcess.Shell;", StringComparison.Ordinal);
        Assert.True(shellNamed > 0, "the shell does not name its kind");
        Assert.True(shellNamed < main.IndexOf("InstallHome.Establish(", StringComparison.Ordinal), "the shell names its kind after its home is established");
    }
}
