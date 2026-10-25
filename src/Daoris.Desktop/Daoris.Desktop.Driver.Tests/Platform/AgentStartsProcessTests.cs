using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TERMTRUST1a part (i), the Process half of the terminal-trust design §8 case 2 (D201 point 4): a stand-in harness started through the
/// agents' route (<see cref="AgentStarts"/>) with the built launcher laid out beside its home, once per kind of start the doors make (an
/// agent's, a plugin's, a piece's). Its first instruction starts a child, and that child is in this Daoris's agents' job, and in a piece's
/// beneath it for a piece; the harness speaks over the stdio the launcher was handed; a kill of the launcher alone ends the harness and its
/// child; a start with no launcher laid out is made as it was and holds the open event until it ends. Each harness is the system's
/// <c>cmd</c> running a script of this test's, never an agent.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class AgentStartsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-agent-starts-process-" + Guid.NewGuid().ToString("N")[..8]);

    public AgentStartsProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    /// <summary>A process id no process here holds a job under: high, and its own per test.</summary>
    private static int StandInId() => 1_900_000_000 + Random.Shared.Next(100_000_000);

    /// <summary>A home in an install whose <c>app/bin/</c> holds the built launcher, as the install lays it out.</summary>
    private string Installed() => Installed(_root);

    /// <summary>The same under <paramref name="under"/>: <see cref="LaunchedSessionJobProcessTests"/> lays it out too.</summary>
    internal static string Installed(string under)
    {
        var root = Path.Combine(under, "install");
        var home = Path.Combine(root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var bin = Path.Combine([root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        var built = Path.GetDirectoryName(PieceLauncherProcessTests.Launcher())!;
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)));
        Assert.Equal(Path.Combine(bin, PieceLauncher.Program), PieceStarts.LauncherPath(home));
        return home;
    }

    /// <summary>
    /// A stand-in harness's start, built as a session's is (every stream redirected as UTF-8, no window): its first instruction starts a
    /// child, which says <c>child</c> with the word it was handed and waits on its standard input for a line.
    /// </summary>
    private ProcessStartInfo Harness()
    {
        var child = Path.Combine(_root, "child.cmd");
        var harness = Path.Combine(_root, "harness.cmd");
        File.WriteAllText(child, "@echo off\r\necho child %AGENT_WORD%\r\nset /p line=\r\n");
        File.WriteAllText(harness, $"@cmd /d /c \"{child}\"\r\n");
        var info = new ProcessStartInfo(Cmd)
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
        foreach (var word in new[] { "/d", "/c", harness }) info.ArgumentList.Add(word);
        info.Environment["AGENT_WORD"] = "handed down";
        return info;
    }

    /// <summary>The <c>agent.contained</c> lines the home's machine log holds.</summary>
    private static List<System.Text.Json.JsonElement> Said(string home)
    {
        var logs = DaorisHome.Entry(home, HomeEntry.Logs);
        if (!Directory.Exists(logs)) return [];
        return Directory.EnumerateFiles(logs, "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == "agent.contained")
            .Select(line => line.GetProperty("data"))
            .ToList();
    }

    /// <summary>Until <paramref name="holds"/> holds, bounded: a condition that never holds fails.</summary>
    private static async Task Until(Func<bool> holds, string what)
    {
        var bound = Stopwatch.StartNew();
        while (!holds())
        {
            Assert.True(bound.Elapsed < TimeSpan.FromSeconds(30), what);
            await Task.Delay(50);
        }
    }

    [Theory]
    [InlineData(ContainedKind.Agent)]
    [InlineData(ContainedKind.Plugin)]
    [InlineData(ContainedKind.Piece)]
    public async Task A_harness_child_started_in_its_first_instruction_is_born_in_the_agents_job_and_speaks_over_the_launchers_stdio(ContainedKind kind)
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Installed();
        using var jobs = new AgentJobs(StandInId());
        var piece = Guid.NewGuid().ToString("N")[..12];
        using var pieceJob = kind == ContainedKind.Piece ? PieceJobs.Create(piece) : null;

        Process process;
        using (AgentJobs.Within(jobs))
        {
            process = (await AgentStarts.StartAsync(Harness(), kind, StartClass.Background, home, log: null, CancellationToken.None,
                kind == ContainedKind.Piece ? PieceJobs.NameOf(piece) : null))!;
        }

        using (process)
        {
            // The doors read the launcher as the session's process: its stdout is the child's.
            Assert.Equal(PieceLauncher.Program, Path.GetFileName(process.StartInfo.FileName));
            Assert.Equal("child handed down", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));

            // The launcher, the harness and the child its first instruction started: every one in the agents' job, none outside it.
            Assert.Equal(3, ProcessJob.ActiveOf(jobs.Name));
            Assert.True(ProcessJob.NamedHolds(jobs.Name, process.Handle));
            if (kind == ContainedKind.Piece) Assert.Equal(3, pieceJob!.Active);
            Assert.Equal(0, jobs.Uncontained);

            await process.StandardInput.WriteLineAsync("a line for the child");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, process.ExitCode);
        }

        await Until(() => ProcessJob.ActiveOf(jobs.Name) == 0, "the agents' job emptied as the harness ended");
        var said = Assert.Single(Said(home));
        Assert.Equal(AgentStarts.KindName(kind), said.GetProperty("kind").GetString());
        Assert.True(said.GetProperty("contained").GetBoolean());
    }

    [Fact]
    public async Task A_kill_of_the_launcher_alone_ends_the_harness_and_its_child()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Installed();
        using var jobs = new AgentJobs(StandInId());

        Process process;
        using (AgentJobs.Within(jobs))
        {
            process = (await AgentStarts.StartAsync(Harness(), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None))!;
        }

        using (process)
        {
            Assert.Equal("child handed down", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(3, ProcessJob.ActiveOf(jobs.Name));

            // The launcher alone, as a plugin's end kills it: its own job ends what it started.
            process.Kill();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }

        await Until(() => ProcessJob.ActiveOf(jobs.Name) == 0, "the harness and its child ended with the launcher");
    }

    [Fact]
    public async Task With_no_launcher_laid_out_the_harness_is_started_as_it_was_and_the_open_event_held_until_it_ends()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);
        Assert.Null(PieceStarts.LauncherPath(home));
        using var jobs = new AgentJobs(StandInId());

        Process process;
        using (AgentJobs.Within(jobs))
        {
            process = (await AgentStarts.StartAsync(Harness(), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None))!;
        }

        using (process)
        {
            Assert.Equal(Cmd, process.StartInfo.FileName);
            Assert.Equal("child handed down", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(1, jobs.Uncontained);
            Assert.True(EventWaitHandle.TryOpenExisting(jobs.OpenName, out var open), "the open event is held while it lives");
            open!.Dispose();
            Assert.False(ProcessJob.Exists(jobs.Name));

            await process.StandardInput.WriteLineAsync("a line for the child");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }

        await Until(() => jobs.Uncontained == 0, "the open event let go as the harness ended");
        Assert.False(EventWaitHandle.TryOpenExisting(jobs.OpenName, out _));
        var said = Assert.Single(Said(home));
        Assert.Equal((false, ContainedWhy.NoLauncher), (said.GetProperty("contained").GetBoolean(), said.GetProperty("why").GetString()));
    }
}
