using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199's note on the strict start; the co-agent design §8.3): the built launcher, started as the driver starts a partner,
/// joins the piece's job before its harness runs, so the harness is born in it and the job's count holds both; the harness speaks over
/// the stdio the launcher was handed; and where no job answers to the name the harness is never started. The harness is the system's
/// <c>cmd</c>, never an agent.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PieceLauncherProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-piece-launcher-" + Guid.NewGuid().ToString("N")[..8]);

    public PieceLauncherProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    /// <summary>The launcher's build beside this test's own.</summary>
    private static string Launcher()
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.PieceStart", "bin", build.Parent.Name, build.Name, PieceLauncher.Program);
        Assert.True(File.Exists(built), "the launcher is built before these tests: " + built);
        return built;
    }

    /// <summary>The launcher started as the driver starts a harness: every stream redirected as UTF-8, and no window.</summary>
    private Process Start(string job, string harness, params string[] arguments)
    {
        var info = new ProcessStartInfo(Launcher())
        {
            UseShellExecute = false,
            WorkingDirectory = _root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var word in PieceLauncher.Arguments(job, harness, arguments)) info.ArgumentList.Add(word);
        return Process.Start(info)!;
    }

    [Fact]
    public async Task The_harness_is_born_in_the_pieces_job_and_speaks_over_the_launchers_stdio()
    {
        if (!OperatingSystem.IsWindows()) return;
        var piece = Guid.NewGuid().ToString("N")[..12];
        using var job = PieceJobs.Create(piece)!;
        Assert.NotNull(job);

        // Says it is there, then waits on its standard input for a line.
        using var launcher = Start(PieceJobs.NameOf(piece), Cmd, "/d", "/c", "echo ready& set /p line=");
        var said = await launcher.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal("ready", said);
        // The launcher and its harness, both in the job the driver made: nothing of the harness's ran outside it.
        Assert.Equal(2, job.Active);
        Assert.Equal(PartnerJob.Running, PieceJobs.Observe(PieceJobs.NameOf(piece), job));

        await launcher.StandardInput.WriteLineAsync("a line for the partner");
        await launcher.StandardInput.FlushAsync();
        await launcher.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, launcher.ExitCode);
        await job.WaitEmptyAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, job.Active);
    }

    [Fact]
    public async Task Where_no_job_answers_to_the_name_the_harness_is_never_started()
    {
        var marker = Path.Combine(_root, "started");
        var piece = Guid.NewGuid().ToString("N")[..12];

        using var launcher = Start(PieceJobs.NameOf(piece), Cmd, "/d", "/c", "type nul > started");
        launcher.StandardInput.Close();
        var error = await launcher.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(30));
        await launcher.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(PieceLauncher.Refused, launcher.ExitCode);
        Assert.Contains("could not join the piece's job", error);
        Assert.False(File.Exists(marker), "the harness ran outside any job");
    }

    [Fact]
    public async Task Ending_the_job_ends_the_harness_the_launcher_started()
    {
        if (!OperatingSystem.IsWindows()) return;
        var piece = Guid.NewGuid().ToString("N")[..12];
        using var job = PieceJobs.Create(piece)!;

        using var launcher = Start(PieceJobs.NameOf(piece), Cmd, "/d", "/c", "echo ready& set /p line=");
        Assert.Equal("ready", await launcher.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));

        job.Terminate();
        await job.WaitEmptyAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(launcher.WaitForExit(30_000));
        Assert.Equal(PartnerJob.Empty, PieceJobs.Observe(PieceJobs.NameOf(piece), job));
    }
}
